using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.Decals;
using Content.Shared._BlackM.CritCrawl;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Decals;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._BlackM.CritCrawl;

public sealed class CritCrawlTrailSystem : EntitySystem
{
    [Dependency] private readonly CritCrawlSystem _crawl = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly string[] LightTrail =
        { "BlackMCritTrailLight0", "BlackMCritTrailLight1", "BlackMCritTrailLight2", "BlackMCritTrailLight3" };

    private static readonly string[] HeavyTrail =
        { "BlackMCritTrailHeavy0", "BlackMCritTrailHeavy1", "BlackMCritTrailHeavy2", "BlackMCritTrailHeavy3" };

    private const string HandLeft = "BlackMCritHandLeft";
    private const string HandRight = "BlackMCritHandRight";

    private static readonly string[] Drops =
        { "BlackMCritDrops0", "BlackMCritDrops1", "BlackMCritDrops2", "BlackMCritDrops3", "BlackMCritDrops4", "BlackMCritDrops5" };

    private static readonly string[] Splats = { "BlackMBloodSplat0", "BlackMBloodSplat1", "BlackMBloodSplat2" };

    private const int MaxDecalsAround = 6;

    private static readonly Vector2 DecalCenter = new(0.5f, 0.5f);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CritCrawlComponent, MoveEvent>(OnMove);
    }

    private void OnMove(EntityUid uid, CritCrawlComponent comp, ref MoveEvent args)
    {
        if (args.OldPosition.EntityId != args.NewPosition.EntityId)
            return;

        if (!_crawl.CanCrawl(uid, comp))
        {
            ResetTrail(comp);
            return;
        }

        var delta = args.NewPosition.Position - args.OldPosition.Position;
        var dist = delta.Length();
        if (dist > 2f) // teleport, not crawling
        {
            ResetTrail(comp);
            return;
        }

        if (dist < 0.0001f)
            return;

        var dir = delta / dist;
        comp.TrailDirection = comp.TrailDirection == Vector2.Zero
            ? dir
            : Vector2.Normalize(comp.TrailDirection * 0.6f + dir * 0.4f);

        comp.TrailAccumulator += dist;
        comp.HandAccumulator += dist;
        comp.SoundAccumulator += dist;
        comp.PuddleAccumulator += dist;

        TryGetBlood(uid, out var blood, out var bleed);
        var heavy = bleed >= comp.HeavyBleedThreshold;

        if (comp.SoundAccumulator >= comp.CrawlSoundDistance)
        {
            comp.SoundAccumulator = 0f;
            var sound = heavy ? comp.WetCrawlSound : comp.CrawlSound;
            if (sound != null)
                _audio.PlayPvs(sound, uid);
        }

        if (comp.TrailAccumulator < comp.TrailDistance)
            return;

        comp.TrailAccumulator = 0f;

        if (blood == null)
            return;

        var grid = Transform(uid).GridUid;
        if (grid == null || args.NewPosition.EntityId != grid.Value)
            return;

        var amount = MathF.Min(comp.TrailBaseAmount + bleed * comp.TrailBleedFactor, comp.TrailMaxAmount);
        if (amount <= 0f)
            return;

        var drop = _solutions.SplitSolution(blood.BloodSolution!.Value, FixedPoint2.New(amount));
        if (drop.Volume <= FixedPoint2.Zero)
            return;

        var color = drop.GetColor(_proto);
        var trailDir = comp.TrailDirection;
        var angle = new Angle(MathF.Atan2(trailDir.Y, trailDir.X));
        var side = new Vector2(-trailDir.Y, trailDir.X);
        var bleedFactor = Math.Clamp(bleed / MathF.Max(comp.HeavyBleedThreshold, 0.01f), 0f, 1f);

        var center = args.NewPosition.Position - trailDir * 0.25f + side * _random.NextFloat(-0.04f, 0.04f);
        var smearAlpha = Math.Clamp(0.55f + 0.4f * bleedFactor + _random.NextFloat(-0.08f, 0.08f), 0.35f, 0.95f);
        var smearAngle = angle + Angle.FromDegrees(_random.NextFloat(-4f, 4f));
        TryAddBlood(_random.Pick(heavy ? HeavyTrail : LightTrail), grid.Value, center, color.WithAlpha(smearAlpha), smearAngle);

        if (comp.HandAccumulator >= comp.HandprintDistance)
        {
            comp.HandAccumulator = 0f;
            comp.NextHandLeft = !comp.NextHandLeft;
            var sign = comp.NextHandLeft ? 1f : -1f;
            var handPos = args.NewPosition.Position + trailDir * 0.2f + side * sign * 0.22f;
            var handAngle = angle + Angle.FromDegrees(sign * _random.NextFloat(5f, 20f));
            var handAlpha = Math.Clamp(0.6f + 0.35f * bleedFactor, 0.5f, 0.95f);
            TryAddBlood(comp.NextHandLeft ? HandLeft : HandRight, grid.Value, handPos, color.WithAlpha(handAlpha), handAngle);
        }

        if (_random.Prob(Math.Clamp(comp.DropsChance * (1f + bleedFactor * 2f), 0f, 1f)))
        {
            var dripPos = center + side * _random.NextFloat(-0.3f, 0.3f) + trailDir * _random.NextFloat(-0.2f, 0.2f);
            TryAddBlood(_random.Pick(Drops), grid.Value, dripPos, color.WithAlpha(_random.NextFloat(0.7f, 1f)), _random.NextAngle());
        }

        if (!heavy || comp.PuddleAccumulator < comp.PuddleDistance)
            return;

        comp.PuddleAccumulator = 0f;
        var splatPos = center + side * _random.NextFloat(-0.1f, 0.1f);
        TryAddBlood(_random.Pick(Splats), grid.Value, splatPos, color.WithAlpha(_random.NextFloat(0.8f, 0.95f)), _random.NextAngle());
    }

    private bool TryGetBlood(EntityUid uid, out BloodstreamComponent? blood, out float bleed)
    {
        bleed = 0f;
        blood = null;

        if (!TryComp<BloodstreamComponent>(uid, out var stream)
            || !_solutions.ResolveSolution(uid, stream.BloodSolutionName, ref stream.BloodSolution, out var bloodSol)
            || bloodSol.Volume <= FixedPoint2.Zero)
            return false;

        blood = stream;
        bleed = stream.BleedAmount;
        return true;
    }

    private bool TryAddBlood(string id, EntityUid grid, Vector2 center, Color color, Angle rotation)
    {
        var around = _decals.GetDecalsInRange(grid, center - DecalCenter, 0.45f, IsTrailDecal);
        if (around.Count >= MaxDecalsAround)
            return false;

        return _decals.TryAddDecal(id, new EntityCoordinates(grid, center - DecalCenter), out _, color, rotation, zIndex: 0, cleanable: true);
    }

    private static bool IsTrailDecal(Decal decal)
    {
        return decal.Id.StartsWith("BlackMCrit", StringComparison.Ordinal)
               || decal.Id.StartsWith("BlackMBlood", StringComparison.Ordinal);
    }

    private static void ResetTrail(CritCrawlComponent comp)
    {
        comp.TrailAccumulator = 0f;
        comp.HandAccumulator = 0f;
        comp.SoundAccumulator = 0f;
        comp.PuddleAccumulator = 0f;
        comp.TrailDirection = Vector2.Zero;
    }
}
