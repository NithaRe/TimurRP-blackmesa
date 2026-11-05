using System.Numerics;
using Content.Goobstation.Common.MartialArts;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.PullHand;

public sealed class PullHandOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;
    private readonly InventorySystem _inventory;

    private readonly Texture _hand;
    private readonly Texture _grip;

    private const float PixelsPerTile = 32f;

    private const float HandAlpha = 0.55f;

    private const float PopTime = 0.18f;
    private static readonly Vector2 ItemEdge = new(0.22f, 0.18f);
    private static readonly Vector2 MobEdge = new(0.24f, 0.14f);
    private static readonly Vector2 ChokeEdge = new(0.12f, 0.1f);

    private static readonly Vector2 TorsoOffset = new(0f, -0.04f);
    private static readonly Vector2 NeckOffset = new(0f, 0.08f);

    private const float TwoHandSpread = 0.15f;
    private const float TwoHandSplay = 0.2f; // radians

    private static readonly Color GloveColor = Color.FromHex("#4A4A52");
    private static readonly Color DefaultColor = Color.FromHex("#B8BCC4");

    private readonly Dictionary<EntityUid, (EntityUid Puller, TimeSpan Start)> _grabs = new();
    private readonly List<EntityUid> _stale = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public PullHandOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _inventory = _entMan.System<InventorySystem>();

        var rsi = _cache.GetResource<RSIResource>("/Textures/_BlackM/Effects/PullHand/pull_hand.rsi").RSI;
        _hand = rsi.TryGetState("hand", out var hand) ? hand.Frame0 : Texture.Transparent;
        _grip = rsi.TryGetState("grip", out var grip) ? grip.Frame0 : Texture.Transparent;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.RealTime;
        var view = args.WorldAABB.Enlarged(2f);

        Cleanup();

        var query = _entMan.EntityQueryEnumerator<PullableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pullable, out var xform))
        {
            if (pullable.Puller is not { } puller || xform.MapID != args.MapId)
                continue;

            if (!_entMan.TryGetComponent<TransformComponent>(puller, out var pullerXform)
                || pullerXform.MapID != args.MapId
                || !_entMan.HasComponent<HandsComponent>(puller))
                continue;

            if (_entMan.TryGetComponent<SpriteComponent>(uid, out var sprite) && !sprite.Visible)
                continue;

            var pulledPos = _transform.GetWorldPosition(xform);
            if (!view.Contains(pulledPos))
                continue;

            var delta = _transform.GetWorldPosition(pullerXform) - pulledPos;
            var dist = delta.Length();
            if (dist < 0.05f)
                continue;

            var dir = delta / dist;

            // pop-in when the grab starts
            if (!_grabs.TryGetValue(uid, out var grab) || grab.Puller != puller)
            {
                grab = (puller, now);
                _grabs[uid] = grab;
            }

            var t = (float) Math.Clamp((now - grab.Start).TotalSeconds / PopTime, 0.0, 1.0);
            var pop = 1f + 0.45f * (1f - t) * (1f - t);
            var alpha = MathF.Min(1f, t * 2.5f) * HandAlpha;

            var color = HandColor(puller).WithAlpha(alpha);
            var isMob = _entMan.HasComponent<MobStateComponent>(uid);

            var anchor = pulledPos + (isMob ? TorsoOffset : Vector2.Zero);
            var edge = EdgePoint(dir, isMob ? MobEdge : ItemEdge, dist);

            switch (pullable.GrabStage)
            {
                case GrabStage.Hard:
                    DrawTwoHands(handle, anchor + edge, dir, pop, color);
                    break;

                case GrabStage.Suffocate:
                    // both hands on the neck, trembling
                    var time = (float) now.TotalSeconds;
                    var shake = new Vector2(MathF.Sin(time * 41f + uid.Id), MathF.Cos(time * 37f + uid.Id)) * 0.012f;
                    DrawTwoHands(handle, pulledPos + NeckOffset + EdgePoint(dir, ChokeEdge, dist) + shake, dir, pop, color);
                    break;

                case GrabStage.Soft:
                    DrawHand(handle, _grip, anchor + edge, dir, pop, color);
                    break;

                default:
                    DrawHand(handle, _hand, anchor + edge, dir, pop, color);
                    break;
            }
        }
    }

    private static Vector2 EdgePoint(Vector2 dir, Vector2 radii, float dist)
    {
        var point = new Vector2(dir.X * radii.X, dir.Y * radii.Y);
        var max = dist * 0.5f;
        return point.LengthSquared() > max * max ? Vector2.Normalize(point) * max : point;
    }

    private void DrawTwoHands(DrawingHandleWorld handle, Vector2 center, Vector2 dir, float pop, Color color)
    {
        var side = new Vector2(-dir.Y, dir.X);

        DrawHand(handle, _grip, center + side * TwoHandSpread, Rotate(dir, -TwoHandSplay), pop, color);
        DrawHand(handle, _grip, center - side * TwoHandSpread, Rotate(dir, TwoHandSplay), pop, color);
    }

    private static void DrawHand(DrawingHandleWorld handle, Texture texture, Vector2 center, Vector2 dir,
        float scale, Color color)
    {
        var size = new Vector2(texture.Width, texture.Height) / PixelsPerTile * scale;
        var box = Box2.CenteredAround(center, size);

        var angle = new Angle(MathF.Atan2(-dir.Y, -dir.X) - MathF.PI / 2f);
        handle.DrawTextureRect(texture, new Box2Rotated(box, angle, center), color);
    }

    private Color HandColor(EntityUid puller)
    {
        if (_inventory.TryGetSlotEntity(puller, "gloves", out _))
            return GloveColor;

        if (_entMan.TryGetComponent<HumanoidAppearanceComponent>(puller, out var humanoid))
            return humanoid.SkinColor;

        return DefaultColor;
    }

    private void Cleanup()
    {
        _stale.Clear();
        foreach (var (uid, grab) in _grabs)
        {
            if (!_entMan.TryGetComponent<PullableComponent>(uid, out var pullable) || pullable.Puller != grab.Puller)
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _grabs.Remove(uid);
        }
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var c = MathF.Cos(radians);
        var s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
