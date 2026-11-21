using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;

namespace Content.Client._BlackM.Weapons.Crosshair;

public sealed class GunCrosshairOverlay : Overlay
{
    private const float MinGap = 3f;
    private const float MaxGapFraction = 0.4f;
    private const float TickLength = 5f;
    private const float KickPush = 3f;
    private const float ArcGap = 4f;
    private const float TextGap = 5f;

    private const int MinSegments = 24;
    private const int MaxSegments = 96;

    private const float BlockedAlpha = 0.45f;

    private static readonly Color OutlineColor = Color.Black.WithAlpha(0.4f);
    private static readonly Color ArcBackColor = Color.White.WithAlpha(0.1f);
    private static readonly Color LowAmmoColor = new(1f, 0.76f, 0.36f, 0.9f);
    private static readonly Color NoAmmoColor = new(1f, 0.42f, 0.38f, 0.9f);

    private readonly IEyeManager _eye;
    private readonly IInputManager _input;
    private readonly GunCrosshairSystem _sys;
    private readonly Font _font;

    private readonly UIBox2[] _boxes = new UIBox2[5];
    private readonly Vector2[] _verts = new Vector2[MaxSegments * 6];

    private int _cachedAmmo = int.MinValue;
    private string _ammoText = string.Empty;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public GunCrosshairOverlay(IEyeManager eye, IInputManager input, IResourceCache cache, GunCrosshairSystem sys)
    {
        _eye = eye;
        _input = input;
        _sys = sys;
        _font = cache.GetFont("/Fonts/_BlackM/Tektur-Regular.ttf", 8);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _sys.CrosshairVisible && base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_sys.Map != args.MapId)
            return;

        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return;

        var handle = args.ScreenHandle;
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;
        var scale = Math.Clamp(uiScale, 1f, 2f);
        var px = MathF.Max(1f, MathF.Round(scale));

        var alpha = _sys.Alpha;
        if (_sys.Blocked && !_sys.Empty)
            alpha *= BlockedAlpha;

        var center = new Vector2(MathF.Round(mouse.Position.X), MathF.Round(mouse.Position.Y));

        var distance = (center - _eye.WorldToScreen(_sys.Origin)).Length();
        var spread = MathF.Min(_sys.Spread, MathF.PI / 2f - 0.05f);
        var viewport = args.ViewportBounds.Size;
        var maxGap = MathF.Max(MinGap * scale, MathF.Min(viewport.X, viewport.Y) * MaxGapFraction);

        var gap = MathF.Round(Math.Clamp(distance * MathF.Tan(spread), MinGap * scale, maxGap) + _sys.Kick * KickPush * scale);
        var length = MathF.Round(TickLength * scale);
        var half = MathF.Floor(px / 2f);

        _boxes[0] = UIBox2.FromDimensions(center.X - half, center.Y - gap - length, px, length);
        _boxes[1] = UIBox2.FromDimensions(center.X - half, center.Y + gap, px, length);
        _boxes[2] = UIBox2.FromDimensions(center.X - gap - length, center.Y - half, length, px);
        _boxes[3] = UIBox2.FromDimensions(center.X + gap, center.Y - half, length, px);
        _boxes[4] = UIBox2.FromDimensions(center.X - half, center.Y - half, px, px);

        var outline = Fade(OutlineColor, alpha);
        var color = Fade(_sys.CurrentColor, alpha);

        foreach (var box in _boxes)
            handle.DrawRect(new UIBox2(box.Left - 1f, box.Top - 1f, box.Right + 1f, box.Bottom + 1f), outline);
        foreach (var box in _boxes)
            handle.DrawRect(box, color);

        var outer = gap + length;

        if (_sys.ShowCooldown)
        {
            var radius = outer + ArcGap * scale;
            DrawArc(handle, center, radius, px, 1f, Fade(ArcBackColor, alpha));
            DrawArc(handle, center, radius, px, _sys.CooldownProgress, color);
            outer = radius;
        }

        DrawAmmo(handle, center, MathF.Min(outer, 32f * scale) + TextGap * scale, alpha);
    }

    private void DrawArc(DrawingHandleScreen handle, Vector2 center, float radius, float thickness, float progress, Color color)
    {
        if (progress <= 0f)
            return;

        var total = Math.Clamp((int) (MathF.Tau * radius / 4f), MinSegments, MaxSegments);
        var segments = Math.Max(1, (int) MathF.Ceiling(total * progress));
        var sweep = MathF.Tau * progress;
        var start = -MathF.PI / 2f;
        var inner = radius - thickness / 2f;
        var outerR = radius + thickness / 2f;
        var n = 0;

        for (var i = 0; i < segments; i++)
        {
            var a0 = start + sweep * i / segments;
            var a1 = start + sweep * (i + 1) / segments;
            var i0 = Point(center, inner, a0);
            var o0 = Point(center, outerR, a0);
            var i1 = Point(center, inner, a1);
            var o1 = Point(center, outerR, a1);

            _verts[n++] = i0;
            _verts[n++] = o0;
            _verts[n++] = o1;
            _verts[n++] = i0;
            _verts[n++] = o1;
            _verts[n++] = i1;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _verts.AsSpan(0, n), color);
    }

    private void DrawAmmo(DrawingHandleScreen handle, Vector2 center, float offset, float alpha)
    {
        var capacity = _sys.Capacity;
        var ammo = _sys.Ammo;
        if (capacity <= 0 || ammo > Math.Max(1, capacity / 4))
            return;

        if (ammo != _cachedAmmo)
        {
            _cachedAmmo = ammo;
            _ammoText = ammo.ToString();
        }

        var color = ammo <= 0 ? NoAmmoColor : LowAmmoColor;
        var pos = new Vector2(MathF.Round(center.X + offset), MathF.Round(center.Y - _font.GetLineHeight(1f) / 2f));

        handle.DrawString(_font, pos + Vector2.One, _ammoText, 1f, Fade(OutlineColor, alpha));
        handle.DrawString(_font, pos, _ammoText, 1f, Fade(color, alpha));
    }

    private static Vector2 Point(Vector2 center, float radius, float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return center + new Vector2(cos, sin) * radius;
    }

    private static Color Fade(Color color, float alpha) => color.WithAlpha(color.A * alpha);
}
