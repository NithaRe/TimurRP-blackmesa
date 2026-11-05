using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._BlackM.Ams;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Ams;

[RegisterComponent]
public sealed partial class AmsShakeComponent : Component
{
    public Vector2 Offset;
}

public sealed class AmsEffectsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    public sealed class Blast
    {
        public MapId Map;
        public Vector2 Epicenter;
        public float Speed;
        public float MaxRadius;
        public TimeSpan Start;
        public bool PassedLocal;
    }

    public readonly List<Blast> Blasts = new();

    public float CascadeTint;
    public float WhiteFlash;
    public float GreenFlash;

    private AmsEffectsOverlay? _world;
    private AmsScreenOverlay? _screen;

    private float _time;
    private float _jolt;
    private float _rumble;
    private int _seed;
    private EntityUid? _shaken;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AmsBlastEvent>(OnBlast);
        SubscribeLocalEvent<AmsShakeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);

        _world = new AmsEffectsOverlay(EntityManager, _timing, this);
        _screen = new AmsScreenOverlay(this);
        _overlays.AddOverlay(_world);
        _overlays.AddOverlay(_screen);

        _seed = _random.Next(1000);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_world != null)
            _overlays.RemoveOverlay(_world);
        if (_screen != null)
            _overlays.RemoveOverlay(_screen);

        Blasts.Clear();
        StopShake();
    }

    private void OnBlast(AmsBlastEvent ev)
    {
        Blasts.Add(new Blast
        {
            Map = new MapId(ev.Map),
            Epicenter = ev.Epicenter,
            Speed = MathF.Max(1f, ev.Speed),
            MaxRadius = ev.MaxRadius,
            Start = ev.Start,
        });

        WhiteFlash = 1f;
        _jolt = MathF.Max(_jolt, 1f);
    }

    private void OnGetEyeOffset(Entity<AmsShakeComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.Offset;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _time += frameTime;
        var now = _timing.CurTime;

        WhiteFlash = MathF.Max(0f, WhiteFlash - frameTime / 1.8f);
        GreenFlash = MathF.Max(0f, GreenFlash - frameTime / 1.2f);
        _jolt = MathF.Max(0f, _jolt - frameTime * 0.9f);

        var local = _player.LocalEntity;
        var rumbleTarget = 0f;
        var tintTarget = 0f;

        if (local is { } me && TryComp(me, out TransformComponent? myXform))
        {
            var myPos = _xform.GetWorldPosition(myXform);
            var myMap = myXform.MapID;

            var query = EntityQueryEnumerator<AmsComponent, TransformComponent>();
            while (query.MoveNext(out _, out var ams, out var xform))
            {
                if (xform.MapID != myMap)
                    continue;

                var dist = (_xform.GetWorldPosition(xform) - myPos).Length();

                switch (ams.Status)
                {
                    case AmsStatus.Synchronizing:
                        var charge = AmsHelpers.GetCharge(ams.Status, ams.SyncStartTime, ams.ChargeTime, now);
                        rumbleTarget = MathF.Max(rumbleTarget, 0.15f * charge * Near(dist, 10f));
                        break;

                    case AmsStatus.Ready:
                    case AmsStatus.Active:
                        rumbleTarget = MathF.Max(rumbleTarget, 0.2f * Near(dist, 10f));
                        break;

                    case AmsStatus.Detonating:
                        var p = AmsHelpers.GetDetonationProgress(ams, now);
                        rumbleTarget = MathF.Max(rumbleTarget, (0.15f + 0.85f * p) * (0.25f + 0.75f * Near(dist, 30f)));
                        tintTarget = MathF.Max(tintTarget, (0.3f + 0.7f * p) * Near(dist, 25f));
                        break;
                }
            }

            for (var i = Blasts.Count - 1; i >= 0; i--)
            {
                var blast = Blasts[i];
                var radius = blast.Speed * (float) (now - blast.Start).TotalSeconds;

                if (radius > blast.MaxRadius + 30f)
                {
                    Blasts.RemoveAt(i);
                    continue;
                }

                if (blast.Map != myMap)
                    continue;

                var d = (myPos - blast.Epicenter).Length();
                if (!blast.PassedLocal && radius >= d)
                {
                    blast.PassedLocal = true;
                    GreenFlash = 1f;
                    _jolt = MathF.Max(_jolt, 0.8f);
                }

                if (radius <= blast.MaxRadius)
                    rumbleTarget = MathF.Max(rumbleTarget, 0.35f + 0.65f * Near(MathF.Abs(d - radius), 20f));
            }
        }
        else
        {
            Blasts.RemoveAll(b => b.Speed * (float) (now - b.Start).TotalSeconds > b.MaxRadius + 30f);
        }

        _rumble += (rumbleTarget - _rumble) * MathF.Min(1f, frameTime * 3f);
        CascadeTint += (tintTarget - CascadeTint) * MathF.Min(1f, frameTime * 2f);

        UpdateShake(local);
    }

    private static float Near(float dist, float range) => Math.Clamp(1f - dist / range, 0f, 1f);

    #region shake

    private const float RumbleAmp = 0.06f;
    private const float JoltAmp = 0.35f;

    private void UpdateShake(EntityUid? local)
    {
        var active = _rumble > 0.002f || _jolt > 0.002f;
        if (!active || local == null || TerminatingOrDeleted(local.Value))
        {
            StopShake();
            return;
        }

        if (_shaken != local)
        {
            StopShake();
            _shaken = local;
        }

        var comp = EnsureComp<AmsShakeComponent>(local.Value);
        var scale = _cfg.GetCVar(CCVars.ScreenShakeIntensity);

        var amp = _rumble * RumbleAmp + _jolt * _jolt * JoltAmp;
        var freq = 14f + 10f * _jolt;
        comp.Offset = new Vector2(Noise(_time * freq, _seed), Noise(_time * freq, _seed + 7)) * amp * scale;

        UpdateEye(local.Value);
    }

    private void StopShake()
    {
        if (_shaken is not { } uid)
            return;

        _shaken = null;

        if (TerminatingOrDeleted(uid))
            return;

        RemComp<AmsShakeComponent>(uid);
        UpdateEye(uid);
    }

    private void UpdateEye(EntityUid uid)
    {
        if (TryComp(uid, out EyeComponent? eye))
            _eye.UpdateEyeOffset((uid, eye));
    }

    private static float Noise(float x, int seed)
    {
        var i = (int) MathF.Floor(x);
        var f = x - i;
        var u = f * f * (3f - 2f * f);
        return Hash(i, seed) + (Hash(i + 1, seed) - Hash(i, seed)) * u;
    }

    private static float Hash(int i, int seed)
    {
        unchecked
        {
            var h = (uint) (i * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 8388607.5f - 1f;
        }
    }

    #endregion
}
