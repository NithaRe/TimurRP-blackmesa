// SPDX-FileCopyrightText: 2025 AftrLite <61218133+AftrLite@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 OnsenCapy <101037138+OnsenCapy@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Religion;
using Content.Server._DV.CosmicCult.Components;
using Content.Server.Popups;
using Content.Shared._DV.CosmicCult.Components;
using Content.Shared._DV.CosmicCult;
using Content.Shared.Damage;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Server.Atmos.Rotting;
using Content.Server.Administration.Systems;

namespace Content.Server._DV.CosmicCult.Abilities;

public sealed partial class CosmicConversionSystem : EntitySystem
{
    [Dependency] private CosmicCultRuleSystem _cultRule = default!;
    [Dependency] private CosmicGlyphSystem _cosmicGlyph = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedCosmicCultSystem _cosmicCult = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private RottingSystem _rotting = default!;
    [Dependency] private RejuvenateSystem _rejuvenateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicGlyphConversionComponent, TryActivateGlyphEvent>(OnConversionGlyph);
    }

    private void OnConversionGlyph(Entity<CosmicGlyphConversionComponent> uid, ref TryActivateGlyphEvent args)
    {
        var possibleTargets = _cosmicGlyph.GetTargetsNearGlyph(uid,
            uid.Comp.ConversionRange,
            entity => _cosmicCult.EntityIsCultist(entity));

        if (possibleTargets.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-conditions-not-met"), uid, args.User);
            args.Cancel();
            return;
        }

        if (possibleTargets.Count > 1)
        {
            _popup.PopupEntity(Loc.GetString("cult-glyph-too-many-targets"), uid, args.User);
            args.Cancel();
            return;
        }

        foreach (var target in possibleTargets)
        {
            if (_rotting.IsRotten(target)) //Goobstation: Prevents using space corpses.
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-rotting"), uid, args.User);
                args.Cancel();
            }
            if (HasComp<BibleUserComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-chaplain"), uid, args.User);
                args.Cancel();
            }
            else if (uid.Comp.NegateProtection == false && HasComp<MindShieldComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("cult-glyph-target-mindshield"), uid, args.User);
                args.Cancel();
            }
            else
            {
                _stun.TryUpdateStunDuration(target, TimeSpan.FromSeconds(4f));
                _rejuvenateSystem.PerformRejuvenate(target); //Goobstation: No one likes being brought into the antag gang dead, now do we?
                _cultRule.CosmicConversion(uid, target);
                var finaleQuery = EntityQueryEnumerator<CosmicFinaleComponent>(); // Enumerator for The Monument's Finale
                while (finaleQuery.MoveNext(out var monument, out var comp)
                    && comp.CurrentState == FinaleState.ActiveBuffer)
                {
                    comp.BufferTimer -= TimeSpan.FromSeconds(45);
                    _popup.PopupCoordinates(Loc.GetString("cosmiccult-finale-speedup"), Transform(monument).Coordinates, PopupType.Large);
                }
            }
        }
    }
}
