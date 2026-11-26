using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Shared._BlackM.CCVar;
using Robust.Server.ServerStatus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Server._BlackM.Shop;

public sealed class ShopEntitlementsManager
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IStatusHost _statusHost = default!;
    [Dependency] private IResourceManager _resourceManager = default!;
    [Dependency] private ITaskManager _taskManager = default!;

    private const string Path_ = "/shop/entitlements";
    private const int MaxBodyBytes = 1024 * 1024;
    private const int MaxEntries = 20000;
    private static readonly TimeSpan SignatureWindow = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public sealed class Entry
    {
        public string? OocColor { get; set; }
        public long OocColorExpiresAt { get; set; }
        public Dictionary<string, long> Phrase { get; set; } = new();
    }

    private sealed class RequestDto
    {
        [JsonPropertyName("entries")] public List<EntryDto>? Entries { get; set; }
    }

    private sealed class EntryDto
    {
        [JsonPropertyName("userId")] public string? UserId { get; set; }
        [JsonPropertyName("oocColor")] public string? OocColor { get; set; }
        [JsonPropertyName("oocColorExpiresAt")] public long OocColorExpiresAt { get; set; }
        [JsonPropertyName("phrase")] public List<PhraseDto>? Phrase { get; set; }
    }

    private sealed class PhraseDto
    {
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("expiresAt")] public long ExpiresAt { get; set; }
    }

    private ISawmill _sawmill = default!;
    private string _secret = string.Empty;
    private HashSet<IPAddress> _allowedIps = new();
    private string _file = string.Empty;
    private Dictionary<NetUserId, Entry> _entries = new();

    public event Action? Changed;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("blackm.shop");
        var dir = Path.Combine(_resourceManager.UserData.RootDir?.ToString() ?? "data", "discord_auth");
        Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "shop_entitlements.json");

        _cfg.OnValueChanged(BlackMCVars.DiscordAuthLinkSecret, v => _secret = v, true);
        _cfg.OnValueChanged(BlackMCVars.DiscordAuthLinkAllowedIps, OnAllowedIpsChanged, true);

        Load();
        _statusHost.AddHandler(OnHttpRequest);
    }

    public bool TryGetOocColor(NetUserId user, out string color)
    {
        color = string.Empty;
        if (!_entries.TryGetValue(user, out var e) || e.OocColor == null || e.OocColorExpiresAt <= Now())
            return false;

        color = e.OocColor;
        return true;
    }

    public IEnumerable<string> GetPhraseCategories(NetUserId user)
    {
        if (!_entries.TryGetValue(user, out var e))
            yield break;

        var now = Now();
        foreach (var (category, expires) in e.Phrase)
        {
            if (expires > now)
                yield return category;
        }
    }

    public bool PurgeExpired()
    {
        var now = Now();
        var changed = false;
        var empty = new List<NetUserId>();
        foreach (var (user, e) in _entries)
        {
            if (e.OocColor != null && e.OocColorExpiresAt <= now)
            {
                e.OocColor = null;
                changed = true;
            }

            var expired = new List<string>();
            foreach (var (category, expires) in e.Phrase)
            {
                if (expires <= now)
                    expired.Add(category);
            }

            foreach (var category in expired)
                e.Phrase.Remove(category);

            changed |= expired.Count > 0;
            if (e.OocColor == null && e.Phrase.Count == 0)
                empty.Add(user);
        }

        foreach (var user in empty)
            _entries.Remove(user);

        if (changed)
            Changed?.Invoke();

        return changed;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void OnAllowedIpsChanged(string value)
    {
        var set = new HashSet<IPAddress>();
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(part, out var ip))
                set.Add(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip);
        }

        _allowedIps = set;
    }

    private async Task<bool> OnHttpRequest(IStatusHandlerContext context)
    {
        if (context.RequestMethod != HttpMethod.Post || context.Url.AbsolutePath != Path_)
            return false;

        if (string.IsNullOrEmpty(_secret))
            return false;

        if (_allowedIps.Count > 0)
        {
            var ip = context.RemoteEndPoint.Address;
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            if (!_allowedIps.Contains(ip))
                return false;
        }

        try
        {
            await Handle(context);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Shop entitlements request failed: {ex}");
            await Respond(context, HttpStatusCode.InternalServerError, "{\"error\":\"internal\"}");
        }

        return true;
    }

    private async Task Handle(IStatusHandlerContext context)
    {
        var body = await ReadBodyAsync(context.RequestBody);
        if (body == null)
        {
            await Respond(context, HttpStatusCode.RequestEntityTooLarge, "{\"error\":\"too_large\"}");
            return;
        }

        var timestamp = GetHeader(context, "X-Timestamp");
        var signature = GetHeader(context, "X-Signature");
        if (!long.TryParse(timestamp, out var ts)
            || Math.Abs(Now() - ts) > SignatureWindow.TotalSeconds
            || !VerifySignature(timestamp, body, signature))
        {
            await Respond(context, HttpStatusCode.Unauthorized, "{\"error\":\"unauthorized\"}");
            return;
        }

        RequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<RequestDto>(body, JsonOptions);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request?.Entries == null || request.Entries.Count > MaxEntries)
        {
            await Respond(context, HttpStatusCode.BadRequest, "{\"error\":\"bad_request\"}");
            return;
        }

        var snapshot = Parse(request.Entries);
        var tcs = new TaskCompletionSource();
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                _entries = snapshot;
                Save();
                Changed?.Invoke();
            }
            finally
            {
                tcs.TrySetResult();
            }
        });
        await tcs.Task;

        await Respond(context, HttpStatusCode.OK, $"{{\"ok\":true,\"count\":{snapshot.Count}}}");
    }

    private static Dictionary<NetUserId, Entry> Parse(IEnumerable<EntryDto> entries)
    {
        var now = Now();
        var result = new Dictionary<NetUserId, Entry>();
        foreach (var dto in entries)
        {
            if (!Guid.TryParse(dto.UserId, out var guid))
                continue;

            var entry = new Entry();
            if (dto.OocColor != null && dto.OocColorExpiresAt > now && Color.TryFromHex(dto.OocColor) is { } color)
            {
                entry.OocColor = color.ToHexNoAlpha();
                entry.OocColorExpiresAt = dto.OocColorExpiresAt;
            }

            foreach (var phrase in dto.Phrase ?? new List<PhraseDto>())
            {
                if (string.IsNullOrWhiteSpace(phrase.Category) || phrase.Category.Length > 64 || phrase.ExpiresAt <= now)
                    continue;

                entry.Phrase[phrase.Category] = Math.Max(phrase.ExpiresAt, entry.Phrase.GetValueOrDefault(phrase.Category));
            }

            if (entry.OocColor != null || entry.Phrase.Count > 0)
                result[new NetUserId(guid)] = entry;
        }

        return result;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file))
                return;

            var dict = JsonSerializer.Deserialize<Dictionary<Guid, Entry>>(File.ReadAllText(_file), JsonOptions);
            if (dict == null)
                return;

            foreach (var (guid, entry) in dict)
                _entries[new NetUserId(guid)] = entry;

            PurgeExpired();
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to load {_file}: {ex.Message}");
        }
    }

    private void Save()
    {
        try
        {
            var dict = new Dictionary<Guid, Entry>();
            foreach (var (user, entry) in _entries)
                dict[user.UserId] = entry;

            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(dict, JsonOptions));
            File.Move(tmp, _file, true);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to save {_file}: {ex.Message}");
        }
    }

    private bool VerifySignature(string timestamp, byte[] body, string signatureHex)
    {
        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signatureHex);
        }
        catch (FormatException)
        {
            return false;
        }

        var prefix = Encoding.UTF8.GetBytes(timestamp + ".");
        var message = new byte[prefix.Length + body.Length];
        Buffer.BlockCopy(prefix, 0, message, 0, prefix.Length);
        Buffer.BlockCopy(body, 0, message, prefix.Length, body.Length);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
        return CryptographicOperations.FixedTimeEquals(provided, hmac.ComputeHash(message));
    }

    private static string GetHeader(IStatusHandlerContext context, string name)
    {
        foreach (var (key, value) in context.RequestHeaders)
        {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
                return value.ToString();
        }

        return string.Empty;
    }

    private static async Task<byte[]?> ReadBodyAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            ms.Write(buffer, 0, read);
            if (ms.Length > MaxBodyBytes)
                return null;
        }

        return ms.ToArray();
    }

    private static Task Respond(IStatusHandlerContext context, HttpStatusCode status, string json)
        => context.RespondAsync(json, status, "application/json");
}
