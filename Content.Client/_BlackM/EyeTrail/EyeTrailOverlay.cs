using System.Numerics;
using Content.Shared._BlackM.EyeTrail;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.EyeTrail;

public sealed class EyeTrailOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly EyeTrailSystem _system;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    private readonly List<Vector2> _points = new();
    private readonly List<float> _ages = new();
    private readonly List<(Vector2i Cell, float Age)> _cells = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public EyeTrailOverlay(EyeTrailSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _transform = _entMan.System<SharedTransformSystem>();
        _shader = _proto.Index<ShaderPrototype>("unshaded").Instance();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.Trails.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var now = (float) _timing.RealTime.TotalSeconds;

        // Draw in a camera-aligned frame so pixels stay square on screen.
        handle.UseShader(_shader);
        handle.SetTransform(Vector2.Zero, -eyeRot);

        foreach (var (uid, state) in _system.Trails)
        {
            if (!_entMan.TryGetComponent<EyeTrailComponent>(uid, out var trail)
                || trail.Colors.Count == 0
                || !_entMan.TryGetComponent<TransformComponent>(state.Parent, out var parentXform)
                || parentXform.MapID != args.MapId)
                continue;

            var matrix = _transform.GetWorldMatrix(state.Parent);
            var pixel = MathF.Max(trail.PixelSize, 0.001f);

            TransformComponent? wearerXform = null;
            SpriteComponent? sprite = null;
            var live = state.HasWearer
                       && _entMan.TryGetComponent(state.Wearer, out wearerXform)
                       && _entMan.TryGetComponent(state.Wearer, out sprite);

            foreach (var strand in state.Strands)
            {
                _points.Clear();
                _ages.Clear();

                foreach (var p in strand.Points)
                {
                    _points.Add(eyeRot.RotateVec(Vector2.Transform(p.Local, matrix)) / pixel);
                    _ages.Add(Math.Clamp((now - p.Time) / MathF.Max(trail.Lifetime, 0.01f), 0f, 1f));
                }

                if (strand.Active && live)
                {
                    _points.Add(eyeRot.RotateVec(_system.EyeWorldPosition(wearerXform!, sprite!, strand.Pixel, eyeRot)) / pixel);
                    _ages.Add(0f);
                }

                if (_points.Count < 2)
                    continue;

                Rasterize();

                foreach (var (cell, age) in _cells)
                {
                    if (age >= 1f)
                        continue;

                    if (age >= trail.DitherFrom && ((cell.X + cell.Y) & 1) != 0)
                        continue;

                    var band = Math.Min(trail.Colors.Count - 1, (int) (age * trail.Colors.Count));
                    var box = new Box2(cell.X * pixel, cell.Y * pixel, (cell.X + 1) * pixel, (cell.Y + 1) * pixel);
                    handle.DrawRect(box, trail.Colors[band]);
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    // 8-connected 1 px line through the samples.
    private void Rasterize()
    {
        _cells.Clear();

        for (var i = 0; i < _points.Count - 1; i++)
        {
            var a = _points[i];
            var b = _points[i + 1];
            var steps = Math.Max(1, (int) MathF.Ceiling(MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y))));

            for (var s = 0; s <= steps; s++)
            {
                var t = (float) s / steps;
                var p = Vector2.Lerp(a, b, t);
                var cell = new Vector2i((int) MathF.Floor(p.X), (int) MathF.Floor(p.Y));
                var age = _ages[i] + (_ages[i + 1] - _ages[i]) * t;

                if (_cells.Count > 0 && _cells[^1].Cell == cell)
                {
                    _cells[^1] = (cell, MathF.Min(_cells[^1].Age, age));
                    continue;
                }

                _cells.Add((cell, age));
            }
        }
    }
}
