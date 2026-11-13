using System.Numerics;
using Content.Shared._BlackM.Monorail;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._BlackM.Monorail;

public sealed class MonorailInteriorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private readonly MonorailInteriorSystem _system;
    private readonly SharedTransformSystem _xform;

    private const int Steps = 48;

    private const int CornerSegments = 8;

    private const int ContourPoints = 4 * (CornerSegments + 1);

    private readonly Vector2[] _inner = new Vector2[ContourPoints];
    private readonly Vector2[] _outer = new Vector2[ContourPoints];
    private readonly Vector2[] _tris = new Vector2[ContourPoints * 6];

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public MonorailInteriorOverlay(MonorailInteriorSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _xform = _entManager.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.Fade > 0.001f && _system.Car != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Car is not { } uid ||
            !_entManager.TryGetComponent(uid, out MonorailCarComponent? car) ||
            !_entManager.TryGetComponent(uid, out TransformComponent? xform) ||
            xform.MapID != args.MapId)
            return;

        var fade = Smooth(_system.Fade);
        var darkness = Math.Clamp(car.InteriorDarkness, 0f, 1f) * fade;
        if (darkness <= 0.002f)
            return;

        var handle = args.WorldHandle;

        var center = _xform.GetWorldPosition(xform);
        var aabb = args.WorldAABB;
        var far = 4f + MathF.Max(
            MathF.Max(MathF.Abs(aabb.Left - center.X), MathF.Abs(aabb.Right - center.X)),
            MathF.Max(MathF.Abs(aabb.Bottom - center.Y), MathF.Abs(aabb.Top - center.Y))) * 1.5f;

        handle.SetTransform(_xform.GetWorldMatrix(uid));

        var he = car.BodyHalfExtents;
        var radius = Math.Clamp(car.InteriorCornerRadius, 0f, MathF.Min(he.X, he.Y));
        var tint = car.InteriorTint;
        var feather = MathF.Max(0.05f, car.InteriorFeather);

        BuildContour(_inner, he, radius, 0f);
        for (var i = 0; i < Steps; i++)
        {
            BuildContour(_outer, he, radius, feather * (i + 1) / Steps);
            var a = Smooth((i + 0.5f) / Steps);
            DrawBand(handle, tint.WithAlpha(darkness * a));
            Array.Copy(_outer, _inner, ContourPoints);
        }

        BuildContour(_outer, he, radius, far);
        DrawBand(handle, tint.WithAlpha(darkness));

        handle.SetTransform(Matrix3x2.Identity);
    }

    private static void BuildContour(Vector2[] into, Vector2 halfExtents, float radius, float offset)
    {
        var r = radius + offset;
        var cx = halfExtents.X - radius;
        var cy = halfExtents.Y - radius;
        var n = 0;

        for (var corner = 0; corner < 4; corner++)
        {
            var sx = corner is 0 or 3 ? 1f : -1f;
            var sy = corner is 0 or 1 ? 1f : -1f;
            var start = corner * MathF.PI / 2f;

            for (var s = 0; s <= CornerSegments; s++)
            {
                var ang = start + (MathF.PI / 2f) * s / CornerSegments;
                into[n++] = new Vector2(sx * cx + MathF.Cos(ang) * r, sy * cy + MathF.Sin(ang) * r);
            }
        }
    }

    private void DrawBand(DrawingHandleWorld handle, Color color)
    {
        if (color.A <= 0.002f)
            return;

        var v = 0;
        for (var i = 0; i < ContourPoints; i++)
        {
            var j = (i + 1) % ContourPoints;
            _tris[v++] = _inner[i];
            _tris[v++] = _outer[i];
            _tris[v++] = _outer[j];
            _tris[v++] = _inner[i];
            _tris[v++] = _outer[j];
            _tris[v++] = _inner[j];
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris.AsSpan(0, v), color);
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
