using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Corvax.Interfaces.Server;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.DiscordAuth;
using Robust.Server.Player;
using Robust.Server.ServerStatus;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.DiscordAuth;

public sealed class DiscordAuthManager : IServerDiscordAuthManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IStatusHost _statusHost = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;

    public event EventHandler<ICommonSession>? PlayerVerified;

    private readonly HttpClient _http = new();
    private ISawmill _sawmill = default!;
    private DiscordAuthStorage _storage = default!;

    private sealed class PendingState
    {
        public NetUserId UserId;
        public string Ckey = string.Empty;
        public DateTime Expires;
        public bool Used;
        public bool Success;
        public string LinkCode = string.Empty;
    }

    private readonly Dictionary<string, PendingState> _pendingStates = new();
    private readonly object _pendingLock = new();

    private const string TextContentType = "text/plain; charset=utf-8";
    private const string HtmlContentType = "text/html; charset=utf-8";
    private const string DiscordServerInviteUrl = "https://discord.gg/8bvmPnkTPM";

    private const string LinkPath = "/discord/link";
    private const string LinkCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private bool _enabled;
    private bool _isOptional;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("blackm.discord_auth");
        _storage = new DiscordAuthStorage(Path.Combine(_resourceManager.UserData.RootDir?.ToString() ?? "data", "discord_auth"));

        _netManager.RegisterNetMessage<MsgDiscordAuthCheck>(OnAuthCheck);
        _netManager.RegisterNetMessage<MsgDiscordAuthByPass>(OnAuthByPass);
        _netManager.RegisterNetMessage<MsgDiscordAuthRequired>();
        _netManager.RegisterNetMessage<MsgDiscordAuthVerified>();

        _cfg.OnValueChanged(BlackMCVars.DiscordAuthEnabled, v => _enabled = v, true);
        _cfg.OnValueChanged(BlackMCVars.DiscordAuthIsOptional, v => _isOptional = v, true);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        _statusHost.AddHandler(OnHttpCallback);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (!_enabled || e.NewStatus != SessionStatus.Connected)
            return;

        SendAuthRequiredIfNeeded(e.Session);
    }

    private async void SendAuthRequiredIfNeeded(ICommonSession session)
    {
        try
        {
            if (await IsVerified(session.UserId, default))
            {
                _netManager.ServerSendMessage(new MsgDiscordAuthVerified(), session.Channel);
                return;
            }

            var (state, code) = GetOrCreatePending(session.UserId);
            _netManager.ServerSendMessage(new MsgDiscordAuthRequired
            {
                AuthUrl = BuildAuthUrl(state),
                LinkCode = FormatLinkCode(code),
                QrCode = Array.Empty<byte>(),
            }, session.Channel);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to send discord auth requirement to {session.UserId}: {ex}");
        }
    }

    private void OnAuthCheck(MsgDiscordAuthCheck message)
    {
        if (!_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        SendAuthRequiredIfNeeded(session);
    }

    private void OnAuthByPass(MsgDiscordAuthByPass message)
    {
        if (!_isOptional)
            return;

        var channel = message.MsgChannel;
        var session = _playerManager.GetSessionByChannel(channel);
        _netManager.ServerSendMessage(new MsgDiscordAuthVerified(), channel);
        PlayerVerified?.Invoke(this, session);
    }

    public Task<bool> IsVerified(NetUserId userId, CancellationToken cancel)
    {
        if (!_enabled)
            return Task.FromResult(true);

        return Task.FromResult(_storage.IsVerified(userId.UserId.ToString()));
    }

    public Task<string> GenerateAuthLink(NetUserId userId, CancellationToken cancel)
    {
        var (state, _) = GetOrCreatePending(userId);
        return Task.FromResult(BuildAuthUrl(state));
    }

    private (string State, string Code) GetOrCreatePending(NetUserId userId)
    {
        var ckey = _playerManager.TryGetSessionById(userId, out var s) ? s.Name : userId.UserId.ToString();

        lock (_pendingLock)
        {
            var now = DateTime.UtcNow;

            foreach (var key in _pendingStates.Where(kv => kv.Value.Expires < now).Select(kv => kv.Key).ToList())
                _pendingStates.Remove(key);

            var existing = _pendingStates.FirstOrDefault(kv =>
                kv.Value.UserId == userId
                && !kv.Value.Used
                && kv.Value.Expires > now.AddMinutes(5));

            if (existing.Key != null)
                return (existing.Key, existing.Value.LinkCode);

            var state = Guid.NewGuid().ToString("N");
            var code = GenerateLinkCode();
            _pendingStates[state] = new PendingState
            {
                UserId = userId,
                Ckey = ckey,
                Expires = now.AddMinutes(10),
                LinkCode = code,
            };

            return (state, code);
        }
    }

    private string GenerateLinkCode()
    {
        while (true)
        {
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = LinkCodeAlphabet[RandomNumberGenerator.GetInt32(LinkCodeAlphabet.Length)];

            var code = new string(chars);
            if (_pendingStates.Values.All(p => p.LinkCode != code))
                return code;
        }
    }

    private static string FormatLinkCode(string code) => code.Length == 8 ? $"{code[..4]}-{code[4..]}" : code;

    private string BuildAuthUrl(string state)
    {
        var clientId = _cfg.GetCVar(BlackMCVars.DiscordAuthClientId);
        var redirectUri = _cfg.GetCVar(BlackMCVars.DiscordAuthRedirectUri);

        return "https://discord.com/api/oauth2/authorize" +
               $"?client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               "&response_type=code" +
               "&scope=identify+guilds" +
               $"&state={state}";
    }

    private async Task<bool> OnLinkRequest(IStatusHandlerContext context)
    {
        var secret = _cfg.GetCVar(BlackMCVars.DiscordAuthWebhookSecret);
        if (string.IsNullOrEmpty(secret))
            return false;

        var token = string.Empty;
        foreach (var (name, value) in context.RequestHeaders)
        {
            if (name.Equals("X-Auth-Token", StringComparison.OrdinalIgnoreCase))
            {
                token = value.ToString();
                break;
            }
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret)))
        {
            await context.RespondAsync("{\"error\":\"unauthorized\"}", HttpStatusCode.Unauthorized, "application/json");
            return true;
        }

        DiscordLinkRequest? request;
        try
        {
            using var reader = new StreamReader(context.RequestBody, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            request = body.Length is > 0 and < 2048 ? JsonSerializer.Deserialize<DiscordLinkRequest>(body) : null;
        }
        catch
        {
            request = null;
        }

        var code = (request?.Code ?? string.Empty)
            .Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();

        if (request == null || code.Length != 8 || !ulong.TryParse(request.DiscordId, out _))
        {
            await context.RespondAsync("{\"error\":\"bad_request\"}", HttpStatusCode.BadRequest, "application/json");
            return true;
        }

        var discordId = request.DiscordId;

        PendingState? entry = null;
        lock (_pendingLock)
        {
            var now = DateTime.UtcNow;
            foreach (var p in _pendingStates.Values)
            {
                if (p.LinkCode == code && !p.Used && p.Expires > now)
                {
                    entry = p;
                    p.Used = true;
                    break;
                }
            }
        }

        if (entry == null)
        {
            await context.RespondAsync("{\"error\":\"code_not_found\"}", HttpStatusCode.NotFound, "application/json");
            return true;
        }

        try
        {
            var thisUserId = entry.UserId.UserId.ToString();

            if (_storage.TryGetUserIdByDiscordId(discordId, out var existingUserId) && existingUserId != thisUserId)
            {
                lock (_pendingLock)
                    entry.Used = false;

                await context.RespondAsync("{\"error\":\"discord_already_linked\"}", HttpStatusCode.Conflict, "application/json");
                return true;
            }

            _storage.SetVerified(thisUserId, entry.Ckey, discordId, true);

            lock (_pendingLock)
            {
                entry.Success = true;
                entry.Expires = DateTime.UtcNow.AddMinutes(2);
            }

            if (_playerManager.TryGetSessionById(entry.UserId, out var session))
            {
                _netManager.ServerSendMessage(new MsgDiscordAuthVerified(), session.Channel);
                PlayerVerified?.Invoke(this, session);
            }

            _ = NotifyBotAsync(discordId, entry.Ckey, thisUserId);

            var ok = JsonSerializer.Serialize(new { ok = true, ckey = entry.Ckey });
            await context.RespondAsync(ok, HttpStatusCode.OK, "application/json");
        }
        catch (Exception ex)
        {
            lock (_pendingLock)
                entry.Used = false;

            _sawmill.Error($"Discord manual link failed: {ex}");
            await context.RespondAsync("{\"error\":\"internal\"}", HttpStatusCode.InternalServerError, "application/json");
        }

        return true;
    }

    private async Task<bool> OnHttpCallback(IStatusHandlerContext context)
    {
        if (context.RequestMethod == HttpMethod.Post && context.Url.AbsolutePath == LinkPath)
            return await OnLinkRequest(context);

        var callbackPath = _cfg.GetCVar(BlackMCVars.DiscordAuthCallbackPath);
        if (context.RequestMethod != HttpMethod.Get || context.Url.AbsolutePath != callbackPath)
            return false;

        var query = System.Web.HttpUtility.ParseQueryString(context.Url.Query);
        var code = query["code"];
        var state = query["state"];

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            await context.RespondAsync("Отсутствует code/state.", HttpStatusCode.BadRequest, TextContentType);
            return true;
        }

        PendingState entry;
        lock (_pendingLock)
        {
            if (!_pendingStates.TryGetValue(state, out entry!) || entry.Expires < DateTime.UtcNow)
            {
                _pendingStates.Remove(state);
                context.RespondAsync(
                    "Ссылка устарела, зайдите на сервер заново.",
                    HttpStatusCode.BadRequest, TextContentType);
                return true;
            }

            if (entry.Used)
            {
                var msg = entry.Success
                    ? "Вы уже подтвердили аккаунт. Можно закрыть эту вкладку и вернуться в игру."
                    : "Эта ссылка уже была использована и подтверждение не удалось. Зайдите на сервер заново.";
                context.RespondAsync(
                    $"<html><body style='font-family:sans-serif;text-align:center;margin-top:15%'><h2>{msg}</h2></body></html>",
                    entry.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest, HtmlContentType);
                return true;
            }

            entry.Used = true;
        }

        try
        {
            var (success, discordId, inGuild) = await CompleteVerification(code);

            lock (_pendingLock)
            {
                entry.Success = success && inGuild;
                entry.Expires = DateTime.UtcNow.AddMinutes(2);
            }

            if (!success || string.IsNullOrEmpty(discordId))
            {
                await context.RespondAsync(
                    "Верификация не пройдена - убедитесь, что вы авторизовались через Discord.",
                    HttpStatusCode.Forbidden, TextContentType);
                return true;
            }

            if (!inGuild)
            {
                await context.RespondAsync(
                    "<html><body style='font-family:sans-serif;text-align:center;margin-top:15%'>" +
                    "<h2>Вы не состоите на нашем Discord-сервере.</h2>" +
                    $"<p>Вступите по ссылке <a href='{DiscordServerInviteUrl}'>{DiscordServerInviteUrl}</a> " +
                    "и повторите авторизацию.</p></body></html>",
                    HttpStatusCode.Forbidden, HtmlContentType);
                return true;
            }

            var thisUserId = entry.UserId.UserId.ToString();

            if (_storage.TryGetUserIdByDiscordId(discordId, out var existingUserId)
                && existingUserId != thisUserId)
            {
                lock (_pendingLock)
                {
                    entry.Success = false;
                }

                await context.RespondAsync(
                    "<html><body style='font-family:sans-serif;text-align:center;margin-top:15%'>" +
                    "<h2>Этот Discord-аккаунт уже привязан к другому аккаунту SS14.</h2>" +
                    "<p>Если это ошибка - обратитесь к администрации.</p></body></html>",
                    HttpStatusCode.Conflict, HtmlContentType);
                return true;
            }

            _storage.SetVerified(thisUserId, entry.Ckey, discordId, true);

            if (_playerManager.TryGetSessionById(entry.UserId, out var session))
            {
                _netManager.ServerSendMessage(new MsgDiscordAuthVerified(), session.Channel);
                PlayerVerified?.Invoke(this, session);
            }

            _ = NotifyBotAsync(discordId, entry.Ckey, thisUserId);

            await context.RespondAsync(
                "<html><body style='font-family:sans-serif;text-align:center;margin-top:15%'>" +
                "<h2>Готово!</h2><p>Можете закрыть эту вкладку и вернуться в игру.</p></body></html>",
                HttpStatusCode.OK, HtmlContentType);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Discord auth callback failed: {ex}");
            await context.RespondAsync("Внутренняя ошибка.", HttpStatusCode.InternalServerError, TextContentType);
        }

        return true;
    }

    private async Task<(bool Success, string? DiscordId, bool InGuild)> CompleteVerification(string code)
    {
        var clientId = _cfg.GetCVar(BlackMCVars.DiscordAuthClientId);
        var clientSecret = _cfg.GetCVar(BlackMCVars.DiscordAuthClientSecret);
        var redirectUri = _cfg.GetCVar(BlackMCVars.DiscordAuthRedirectUri);
        var requiredGuildId = _cfg.GetCVar(BlackMCVars.DiscordAuthGuildId);

        var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        });

        using var tokenResp = await _http.PostAsync("https://discord.com/api/oauth2/token", tokenReq);
        if (!tokenResp.IsSuccessStatusCode)
            return (false, null, false);

        var token = JsonSerializer.Deserialize<DiscordTokenResponse>(await tokenResp.Content.ReadAsStringAsync());
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
            return (false, null, false);

        using var userReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var userResp = await _http.SendAsync(userReq);
        if (!userResp.IsSuccessStatusCode)
            return (false, null, false);

        var user = JsonSerializer.Deserialize<DiscordUserResponse>(await userResp.Content.ReadAsStringAsync());
        if (user == null || string.IsNullOrEmpty(user.Id))
            return (false, null, false);

        if (string.IsNullOrEmpty(requiredGuildId))
            return (true, user.Id, true);

        using var guildsReq = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me/guilds");
        guildsReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var guildsResp = await _http.SendAsync(guildsReq);
        if (!guildsResp.IsSuccessStatusCode)
            return (true, user.Id, false);

        var guilds = JsonSerializer.Deserialize<List<DiscordGuildResponse>>(await guildsResp.Content.ReadAsStringAsync())
                     ?? new List<DiscordGuildResponse>();

        var inGuild = guilds.Any(g => g.Id == requiredGuildId);
        return (true, user.Id, inGuild);
    }

    private async Task NotifyBotAsync(string discordId, string ckey, string userId)
    {
        var webhookUrl = _cfg.GetCVar(BlackMCVars.DiscordAuthWebhookUrl);
        if (string.IsNullOrEmpty(webhookUrl))
            return;

        try
        {
            var payload = JsonSerializer.Serialize(new { discordId, ckey, userId });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            content.Headers.Add("X-Auth-Token", _cfg.GetCVar(BlackMCVars.DiscordAuthWebhookSecret));

            await _http.PostAsync(webhookUrl, content);
        }
        catch
        {
            // ignore
        }
    }
}
