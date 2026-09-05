// SPDX-FileCopyrightText: 2023 EmoGarbage404 <retron404@gmail.com>
// SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 coolmankid12345 <55817627+coolmankid12345@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 coolmankid12345 <coolmankid12345@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Killerqu00 <47712032+Killerqu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 Wrexbe (Josh) <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 nikthechampiongr <32041239+nikthechampiongr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusIcon.Components;
using Content.Shared.Zombies;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Zombies;

public sealed class ZombieSystem : SharedZombieSystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZombieComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZombieComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZombieComponent, GetStatusIconsEvent>(GetZombieIcon);
        SubscribeLocalEvent<InitialInfectedComponent, GetStatusIconsEvent>(GetInitialInfectedIcon);
    }

    private void GetZombieIcon(Entity<ZombieComponent> ent, ref GetStatusIconsEvent args)
    {
        var iconPrototype = _prototype.Index(ent.Comp.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }

    private void GetInitialInfectedIcon(Entity<InitialInfectedComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<ZombieComponent>(ent))
            return;

        var iconPrototype = _prototype.Index(ent.Comp.StatusIcon);
        args.StatusIcons.Add(iconPrototype);
    }

    private void OnStartup(EntityUid uid, ZombieComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        SetLayer(uid, sprite, component.BodyOverrideLayer, component.BodyOverride);
        SetLayer(uid, sprite, component.InfectionOverlayLayer, component.InfectionOverlay);

        if (SetLayer(uid,
                sprite,
                component.InfectionAnimatedOverlayLayer,
                component.InfectionAnimatedOverlay) is { } animatedLayer)
        {
            sprite.LayerSetShader(animatedLayer, "unshaded");
        }
    }

    private void OnShutdown(Entity<ZombieComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        HideLayer(ent, sprite, ent.Comp.BodyOverrideLayer);
        HideLayer(ent, sprite, ent.Comp.InfectionOverlayLayer);
        HideLayer(ent, sprite, ent.Comp.InfectionAnimatedOverlayLayer);
    }

    private int? SetLayer(EntityUid uid, SpriteComponent sprite, string layerKey, SpriteSpecifier? layerSprite)
    {
        if (layerSprite is null)
            return null;

        var layer = _sprite.LayerMapReserve((uid, sprite), layerKey);
        _sprite.LayerSetSprite((uid, sprite), layer, layerSprite);
        _sprite.LayerSetVisible((uid, sprite), layer, true);
        return layer;
    }

    private void HideLayer(EntityUid uid, SpriteComponent sprite, string layerKey)
    {
        if (!sprite.LayerMapTryGet(layerKey, out var layer))
            return;

        _sprite.LayerSetVisible((uid, sprite), layer, false);
    }
}
