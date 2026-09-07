using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._BlackM.Ghost.Customization;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._BlackM.Ghost.Customization;

public sealed class GhostCustomizationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IServerDbManager _dbManager = default!;

    private readonly Dictionary<NetUserId, bool> _whitelistCache = new();

    private readonly HashSet<EntityUid> _updating = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, ComponentInit>(OnGhostInit);
        SubscribeLocalEvent<GhostComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<GhostComponent, GhostCustomizationActionEvent>(OnCustomizationAction);
        SubscribeLocalEvent<GhostComponent, GhostSpriteSelectedMessage>(OnSpriteSelected);

        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            _whitelistCache.Remove(e.Session.UserId);
    }

    private void OnGhostInit(EntityUid uid, GhostComponent component, ComponentInit args)
    {
        EnsureComp<GhostCustomizationComponent>(uid);
    }

    private void OnUiOpened(EntityUid uid, GhostComponent ghost, BoundUIOpenedEvent args)
    {
        _ = UpdateUiStateAsync(uid);
    }

    private void OnCustomizationAction(EntityUid uid, GhostComponent ghost, GhostCustomizationActionEvent args)
    {
        if (!_ui.TryToggleUi(uid, GhostCustomizationUiKey.Key, args.Performer))
            return;

        _ = UpdateUiStateAsync(uid);
        args.Handled = true;
    }

    private async Task<bool> GetWhitelistCachedAsync(NetUserId userId)
    {
        if (_whitelistCache.TryGetValue(userId, out var cached))
            return cached;

        var result = await _dbManager.GetWhitelistStatusAsync(userId);
        _whitelistCache[userId] = result;
        return result;
    }

    private async Task UpdateUiStateAsync(EntityUid uid)
    {
        if (!_updating.Add(uid))
            return;

        try
        {
            var session = _playerManager.TryGetSessionByEntity(uid, out var playerSession) ? playerSession : null;
            var isWhitelisted = session is not null && await GetWhitelistCachedAsync(session.UserId);

            if (!EntityManager.EntityExists(uid))
                return;

            var options = _proto.EnumeratePrototypes<GhostSpritePrototype>()
                .OrderBy(p => p.Locked)
                .ThenBy(p => p.Name)
                .ThenBy(p => p.ID)
                .Select(p => new GhostCustomizationOptionState(
                    p.ID,
                    p.Name,
                    p.Locked && !isWhitelisted,
                    p.Locked && !isWhitelisted ? Loc.GetString("ghost-customization-status-whitelist") : null))
                .ToList();

            var selected = CompOrNull<GhostCustomizationComponent>(uid)?.SelectedSprite ?? string.Empty;

            if (!string.IsNullOrEmpty(selected) && options.All(option => option.Id != selected || option.Locked))
            {
                selected = string.Empty;
                var component = EnsureComp<GhostCustomizationComponent>(uid);
                component.SelectedSprite = null;
                Dirty(uid, component);
            }

            _ui.SetUiState(uid, GhostCustomizationUiKey.Key,
                new GhostCustomizationBoundUserInterfaceState(options, selected));
        }
        finally
        {
            _updating.Remove(uid);
        }
    }

    private void OnSpriteSelected(EntityUid uid, GhostComponent ghost, GhostSpriteSelectedMessage args)
    {
        if (!_proto.TryIndex<GhostSpritePrototype>(args.SpriteId, out var proto))
            return;

        _ = HandleSpriteSelectedAsync(uid, proto);
    }

    private async Task HandleSpriteSelectedAsync(EntityUid uid, GhostSpritePrototype proto)
    {
        var session = _playerManager.TryGetSessionByEntity(uid, out var playerSession) ? playerSession : null;
        if (session is null)
            return;

        var isWhitelisted = await GetWhitelistCachedAsync(session.UserId);
        if (proto.Locked && !isWhitelisted)
            return;

        if (!EntityManager.EntityExists(uid))
            return;

        var component = EnsureComp<GhostCustomizationComponent>(uid);
        component.SelectedSprite = proto.ID;
        Dirty(uid, component);

        await UpdateUiStateAsync(uid);
    }
}
