using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._BlackM.Weapons.Crosshair;

public sealed class GunAimOverlay : Overlay
{
    private const float StartOffset = 0.5f;
    private const float EndOffset = 0.35f;
    private const int LineSegments = 14;
    private const int ConeSegments = 8;
    private const float LineAlpha = 0.22f;
    private const float ConeAlpha = 0.12f;
    private const float MinConeSpread = 0.009f;

    private readonly IEyeManager _eye;
    private readonly GunCrosshairSystem _sys;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public GunAimOverlay(IEyeManager eye, GunCrosshairSystem sys)
    {
        _eye = eye;
        _sys = sys;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _sys.AimLineVisible &&
               args.MapId == _sys.Map &&
               args.Viewport.Eye == _eye.CurrentEye &&
               base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var from = _sys.Origin;
        var delta = _sys.Target - from;
        var distance = delta.Length();

        if (distance <= StartOffset + EndOffset)
            return;

        var dir = delta / distance;
        var alpha = _sys.Alpha;
        var color = _sys.CurrentColor;

        // Brightest mid-way, fades out at the shooter and at the crosshair.
        var start = from + dir * StartOffset;
        var end = _sys.Target - dir * EndOffset;
        for (var i = 0; i < LineSegments; i++)
        {
            var t0 = (float) i / LineSegments;
            var t1 = (float) (i + 1) / LineSegments;
            var a = MathF.Sin(MathF.PI * (t0 + t1) * 0.5f) * LineAlpha * alpha;
            handle.DrawLine(Vector2.Lerp(start, end, t0), Vector2.Lerp(start, end, t1), color.WithAlpha(a));
        }

        var spread = MathF.Min(_sys.Spread, MathF.PI / 2f - 0.05f);
        if (spread < MinConeSpread)
            return;

        DrawConeEdge(handle, from, Rotate(dir, spread), distance, color, alpha);
        DrawConeEdge(handle, from, Rotate(dir, -spread), distance, color, alpha);
    }

    private static void DrawConeEdge(DrawingHandleWorld handle, Vector2 from, Vector2 dir, float distance, Color color, float alpha)
    {
        var start = from + dir * StartOffset;
        var end = from + dir * distance;

        for (var i = 0; i < ConeSegments; i++)
        {
            var t0 = (float) i / ConeSegments;
            var t1 = (float) (i + 1) / ConeSegments;
            handle.DrawLine(Vector2.Lerp(start, end, t0), Vector2.Lerp(start, end, t1), color.WithAlpha(t1 * ConeAlpha * alpha));
        }
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
