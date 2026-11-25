// SPDX-FileCopyrightText: 2022 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2023 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Coolsurf6 <coolsurf24@yahoo.com.au>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Kayzel <43700376+KayzelW@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Trest <144359854+trest100@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 kurokoTurbo <92106367+kurokoTurbo@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 RichardBlonski <48651647+RichardBlonski@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._BlackM.CritCrawl; // BlackM
using Content.Shared.Mobs;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.DamageOverlays.Overlays;

public sealed partial class DamageOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _critShader;
    private readonly ShaderInstance _oxygenShader;
    private readonly ShaderInstance _bruteShader;

    public MobState State = MobState.Alive;

    /// <summary>
    /// Shitmed Change: Handles the red pulsing overlay
    /// </summary>
    public float PainLevel = 0f;

    private float _oldPainLevel = 0f;

    /// <summary>
    /// Handles the darkening overlay.
    /// </summary>
    public float OxygenLevel = 0f;

    private float _oldOxygenLevel = 0f;

    /// <summary>
    /// Handles the red heartbeat overlay when crit. BlackM edit
    /// </summary>
    public float CritLevel = 0f;

    private float _oldCritLevel = 0f;

    public float DeadLevel = 1f;

    // BlackM start - red heartbeat crit overlay
    private float _crawlBlend = 1f;

    private CritCrawlSystem? _critCrawl;
    // BlackM end

    public DamageOverlay()
    {
        // TODO: Replace
        IoCManager.InjectDependencies(this);
        _oxygenShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
        _critShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
        _bruteShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return;

        if (args.Viewport.Eye != eyeComp.Eye)
            return;

        /*
         * Here's the rundown:
         * 1. There's lerping for each level so the transitions are smooth.
         * 2. There's 3 overlays, 1 for brute damage, 1 for oxygen damage (that also doubles as a crit overlay),
         * and a white one during crit that closes in as you progress towards death. When you die it slowly disappears.
         * The crit overlay also occasionally reduces its alpha as a "blink"
         */

        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;

        var time = (float) _timing.RealTime.TotalSeconds;
        var lastFrameTime = (float) _timing.FrameTime.TotalSeconds;

        // If they just died then lerp out the white overlay.
        if (State != MobState.Dead)
        {
            DeadLevel = 1f;
        }
        else if (!MathHelper.CloseTo(0f, DeadLevel, 0.001f))
        {
            var diff = -DeadLevel;
            DeadLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            DeadLevel = 0f;
        }

        if (!MathHelper.CloseTo(_oldPainLevel, PainLevel, 0.001f))
        {
            var diff = PainLevel - _oldPainLevel;
            _oldPainLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        if (!MathHelper.CloseTo(_oldOxygenLevel, OxygenLevel, 0.001f))
        {
            var diff = OxygenLevel - _oldOxygenLevel;
            _oldOxygenLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldOxygenLevel = OxygenLevel;
        }

        if (!MathHelper.CloseTo(_oldCritLevel, CritLevel, 0.001f))
        {
            var diff = CritLevel - _oldCritLevel;
            _oldCritLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldCritLevel = CritLevel;
        }

        /*
         * darknessAlphaOuter is the maximum alpha for anything outside of the larger circle
         * darknessAlphaInner (on the shader) is the alpha for anything inside the smallest circle
         *
         * outerCircleRadius is what we end at for max level for the outer circle
         * outerCircleMaxRadius is what we start at for 0 level for the outer circle
         *
         * innerCircleRadius is what we end at for max level for the inner circle
         * innerCircleMaxRadius is what we start at for 0 level for the inner circle
         */

        // Only show pain overlay if there's actual pain and we're not in critical stat
        // Goobstation start
        float level = 0f;
        level = _oldPainLevel;
        if (_oldPainLevel > 0f && _oldCritLevel <= 0f)
        {
            var pulseRate = 3f;
            var adjustedTime = time * pulseRate;
            float outerMaxLevel = 2.0f * distance;
            float outerMinLevel = 0.8f * distance;
            float innerMaxLevel = 0.6f * distance;
            float innerMinLevel = 0.2f * distance;

            var outerRadius = outerMaxLevel - _oldPainLevel * (outerMaxLevel - outerMinLevel);
            var innerRadius = innerMaxLevel - _oldPainLevel * (innerMaxLevel - innerMinLevel);

            var pulse = MathF.Max(0f, MathF.Sin(adjustedTime));

            _bruteShader.SetParameter("time", pulse);
            _bruteShader.SetParameter("color", new Vector3(1f, 0f, 0f));
            _bruteShader.SetParameter("darknessAlphaOuter", 0.8f * _oldPainLevel); // Scale alpha with pain level

            _bruteShader.SetParameter("outerCircleRadius", outerRadius);
            _bruteShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            _bruteShader.SetParameter("innerCircleRadius", innerRadius);
            _bruteShader.SetParameter("innerCircleMaxRadius", innerRadius + 0.02f * distance);
            handle.UseShader(_bruteShader);
            handle.DrawRect(viewport, Color.White);
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        // BlackM edit start
        level = State == MobState.Alive ? _oldOxygenLevel : 0f;

        if (level > 0f)
        {
            float outerMaxLevel = 0.6f * distance;
            float outerMinLevel = 0.06f * distance;
            float innerMaxLevel = 0.02f * distance;
            float innerMinLevel = 0.02f * distance;

            var outerRadius = outerMaxLevel - level * (outerMaxLevel - outerMinLevel);
            var innerRadius = innerMaxLevel - level * (innerMaxLevel - innerMinLevel);
            var outerDarkness = MathF.Min(0.98f, 0.3f * MathF.Log(level) + 1f);

            _oxygenShader.SetParameter("time", 0.0f);
            _oxygenShader.SetParameter("color", new Vector3(0f, 0f, 0f));
            _oxygenShader.SetParameter("darknessAlphaOuter", outerDarkness);
            _oxygenShader.SetParameter("innerCircleRadius", innerRadius);
            _oxygenShader.SetParameter("innerCircleMaxRadius", innerRadius);
            _oxygenShader.SetParameter("outerCircleRadius", outerRadius);
            _oxygenShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            handle.UseShader(_oxygenShader);
            handle.DrawRect(viewport, Color.White);
        }

        level = State != MobState.Dead ? _oldCritLevel : DeadLevel;

        var crawlTarget = State == MobState.Critical && LocalCanCrawl() ? 1f : 0f;
        if (!MathHelper.CloseTo(_crawlBlend, crawlTarget, 0.001f))
            _crawlBlend += GetDiff(crawlTarget - _crawlBlend, lastFrameTime * 0.4f);
        else
            _crawlBlend = crawlTarget;

        if (level > 0f)
        {
            var blend = _crawlBlend;

            var period = MathHelper.Lerp(1.6f, 0.85f, blend);
            var beat = Heartbeat(time, period) * MathHelper.Lerp(0.6f, 1f, blend);

            var vignetteColor = Vector3.Lerp(new Vector3(0.22f, 0.0f, 0.01f), new Vector3(0.5f, 0.02f, 0.03f), blend);
            var outerMax = 1.4f * distance;
            var outerMin = MathHelper.Lerp(0.32f, 0.55f, blend) * distance;
            var innerMax = 0.5f * distance;
            var innerMin = MathHelper.Lerp(0.06f, 0.2f, blend) * distance;

            var outerRadius = outerMax - level * (outerMax - outerMin);
            var innerRadius = innerMax - level * (innerMax - innerMin);
            var vignetteAlpha = MathHelper.Lerp(0.93f, 0.78f, blend) * MathF.Min(1f, level * 1.5f);

            _oxygenShader.SetParameter("time", beat);
            _oxygenShader.SetParameter("color", vignetteColor);
            _oxygenShader.SetParameter("darknessAlphaOuter", vignetteAlpha);
            _oxygenShader.SetParameter("innerCircleRadius", innerRadius);
            _oxygenShader.SetParameter("innerCircleMaxRadius", innerRadius + 0.06f * distance);
            _oxygenShader.SetParameter("outerCircleRadius", outerRadius);
            _oxygenShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.12f * distance);
            handle.UseShader(_oxygenShader);
            handle.DrawRect(viewport, Color.White);

            if (beat > 0.01f)
            {
                var flashColor = Vector3.Lerp(new Vector3(0.55f, 0.0f, 0.02f), new Vector3(1f, 0.08f, 0.06f), blend);
                var flashOuter = MathHelper.Lerp(0.6f, 0.75f, blend) * distance;
                var flashInner = MathHelper.Lerp(0.25f, 0.4f, blend) * distance;

                _critShader.SetParameter("time", beat);
                _critShader.SetParameter("color", flashColor);
                _critShader.SetParameter("darknessAlphaOuter", MathHelper.Lerp(0.25f, 0.45f, blend) * beat * MathF.Min(1f, level * 1.5f));
                _critShader.SetParameter("innerCircleRadius", flashInner);
                _critShader.SetParameter("innerCircleMaxRadius", flashInner + 0.05f * distance);
                _critShader.SetParameter("outerCircleRadius", flashOuter);
                _critShader.SetParameter("outerCircleMaxRadius", flashOuter + 0.15f * distance);
                handle.UseShader(_critShader);
                handle.DrawRect(viewport, Color.White);
            }
        }
        // BlackM edit end

        handle.UseShader(null);
    }

    // BlackM start
    private bool LocalCanCrawl()
    {
        if (_playerManager.LocalEntity is not { } ent
            || !_entityManager.TryGetComponent(ent, out CritCrawlComponent? crawl))
            return false;

        _critCrawl ??= _entityManager.System<CritCrawlSystem>();
        return _critCrawl.CanCrawl(ent, crawl);
    }

    private static float Heartbeat(float time, float period)
    {
        var t = time % period / period;
        var lub = MathF.Exp(-MathF.Pow((t - 0.08f) / 0.055f, 2f));
        var dub = MathF.Exp(-MathF.Pow((t - 0.3f) / 0.06f, 2f)) * 0.6f;
        return MathF.Min(1f, lub + dub);
    }
    // BlackM end

    private float GetDiff(float value, float lastFrameTime)
    {
        var adjustment = value * 5f * lastFrameTime;

        if (value < 0f)
            adjustment = Math.Clamp(adjustment, value, -value);
        else
            adjustment = Math.Clamp(adjustment, -value, value);

        return adjustment;
    }
}
