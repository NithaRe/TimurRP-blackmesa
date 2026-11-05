using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.AltActionEffect;

public sealed class AltActionEffectOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly AltActionEffectSystem _system;
    private readonly SharedTransformSystem _transform;

    private readonly Texture[] _rings;

    private const string RingState = "ring";

    private const float PixelsPerTile = 32f;

    public const float Duration = 0.3f;

    private const float RingTime = 0.3f;

    private const float RingAlpha = 0.85f;

    private readonly List<ActiveAltEffect> _stale = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public AltActionEffectOverlay(AltActionEffectSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _transform = _entMan.System<SharedTransformSystem>();

        var rsi = _cache.GetResource<RSIResource>("/Textures/_BlackM/Effects/AltAction/alt_action.rsi").RSI;
        _rings = rsi.TryGetState(RingState, out var ring)
            ? ring.GetFrames(RsiDirection.South)
            : Array.Empty<Texture>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.Effects.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var now = _timing.RealTime;
        var view = args.WorldAABB.Enlarged(2f);

        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var upright = -eyeRot;

        _stale.Clear();

        foreach (var effect in _system.Effects)
        {
            var elapsed = (float) (now - effect.Start).TotalSeconds;

            if (elapsed >= Duration
                || !_entMan.TryGetComponent<TransformComponent>(effect.Target, out var xform)
                || _entMan.Deleted(effect.Target))
            {
                _stale.Add(effect);
                continue;
            }

            if (xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            if (!view.Contains(pos))
                continue;

            DrawRing(handle, pos, elapsed, upright);
        }

        foreach (var effect in _stale)
        {
            _system.Effects.Remove(effect);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawRing(DrawingHandleWorld handle, Vector2 pos, float elapsed, Angle upright)
    {
        if (elapsed >= RingTime || _rings.Length == 0)
            return;

        var u = elapsed / RingTime;
        var frame = Math.Clamp((int) (u * _rings.Length), 0, _rings.Length - 1);
        var alpha = RingAlpha * (1f - u * u);

        DrawUpright(handle, _rings[frame], pos, 1f, upright, Color.White.WithAlpha(alpha));
    }

    private static void DrawUpright(DrawingHandleWorld handle, Texture texture, Vector2 center, float scale, Angle upright, Color color)
    {
        var size = new Vector2(texture.Width, texture.Height) / PixelsPerTile * scale;
        DrawRotated(handle, texture, center, size, upright, color);
    }

    private static void DrawRotated(DrawingHandleWorld handle, Texture texture, Vector2 center, Vector2 size, Angle angle, Color color)
    {
        var box = Box2.CenteredAround(center, size);
        handle.DrawTextureRect(texture, new Box2Rotated(box, angle, center), color);
    }
}
