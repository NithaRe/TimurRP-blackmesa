// SPDX-FileCopyrightText: 2026 BlackM Project
// SPDX-License-Identifier: CC-BY-SA-3.0

using Content.Shared._BlackM.Kovak;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Kovak;

public sealed class KovakHelmetSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _lastSent = TimeSpan.Zero;
    private static readonly TimeSpan SendCooldown = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<KovakHelmetComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(EntityUid uid, KovakHelmetComponent comp, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var verb = new InteractionVerb
        {
            Text = comp.IsOpen ? "Опустить забрало" : "Поднять забрало",
            Icon = new SpriteSpecifier.Texture(new ResPath(comp.IsOpen
                ? "/Textures/Interface/VerbIcons/close.svg.192dpi.png"
                : "/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
            Act = () =>
            {
                // Защита от спама на клиенте
                var now = _timing.CurTime;
                if (now - _lastSent < SendCooldown)
                    return;
                _lastSent = now;

                RaiseNetworkEvent(new KovakHelmetToggleMessage(GetNetEntity(uid)));
            }
        };

        args.Verbs.Add(verb);
    }
}