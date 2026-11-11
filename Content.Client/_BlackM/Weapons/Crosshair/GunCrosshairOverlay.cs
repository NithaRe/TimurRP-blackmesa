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
    private const int ArcSegments = 48;
    private const float MinGap = 3f;
    private const float MaxGap = 110f;
    private const float KickGap = 4f;
    private const float TickLength = 5f;
    private const float Thickness = 2f;

    private static readonly Color OutlineColor = Color.Black.WithAlpha(0.35f);
    private static readonly Color ArcBackColor = Color.White.WithAlpha(0.12f);
    private static readonly Color ShadowColor = Color.Black.WithAlpha(0.5f);
    private static readonly Color TextColor = Color.White.WithAlpha(0.75f);
    private static readonly Color LowAmmoColor = new(1f, 0.72f, 0.3f, 0.9f);
    private static readonly Color NoAmmoColor = new(1f, 0.38f, 0.34f, 0.9f);

    private readonly IEyeManager _eye;
    private readonly IInputManager _input;
    private readonly GunCrosshairSystem _sys;
    private readonly Font _font;
    private readonly Vector2[] _arc = new Vector2[ArcSegments + 1];
    private readonly List<UIBox2> _boxes = new();

    private int _cachedAmmo = -1;
    private string _ammoText = string.Empty;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public GunCrosshairOverlay(IEyeManager eye, IInputManager input, IResourceCache cache, GunCrosshairSystem sys)
    {
        _eye = eye;
        _input = input;
        _sys = sys;
        _font = cache.GetFont("/Fonts/_BlackM/Tektur-Regular.ttf", 9);
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
        var scale = Math.Clamp(uiScale, 1f, 1.5f);
        var alpha = _sys.Alpha;

        // Whole-pixel center keeps the 2px lines crisp.
        var center = new Vector2(MathF.Round(mouse.Position.X), MathF.Round(mouse.Position.Y));
        var distance = (center - _eye.WorldToScreen(_sys.Origin)).Length();

        var gap = MathF.Round(Math.Clamp(distance * MathF.Tan(_sys.Spread), MinGap * scale, MaxGap * scale) + _sys.Kick * KickGap * scale);
        var length = MathF.Round(TickLength * scale);
        var half = Thickness * 0.5f;

        _boxes.Clear();
        _boxes.Add(new UIBox2(center.X - half, center.Y - gap - length, center.X + half, center.Y - gap));
        _boxes.Add(new UIBox2(center.X - half, center.Y + gap, center.X + half, center.Y + gap + length));
        _boxes.Add(new UIBox2(center.X - gap - length, center.Y - half, center.X - gap, center.Y + half));
        _boxes.Add(new UIBox2(center.X + gap, center.Y - half, center.X + gap + length, center.Y + half));
        _boxes.Add(new UIBox2(center.X - half, center.Y - half, center.X + half, center.Y + half));

        var outline = Fade(OutlineColor, alpha);
        foreach (var box in _boxes)
        {
            handle.DrawRect(new UIBox2(box.Left - 1f, box.Top - 1f, box.Right + 1f, box.Bottom + 1f), outline);
        }

        var color = Fade(_sys.CurrentColor, alpha);
        foreach (var box in _boxes)
        {
            handle.DrawRect(box, color);
        }

        var outer = gap + length;

        if (_sys.ShowCooldown)
        {
            var radius = outer + 4f * scale;
            DrawArc(handle, center, radius, 1f, Fade(ArcBackColor, alpha));
            DrawArc(handle, center, radius, _sys.CooldownProgress, color);
            outer = radius;
        }

        DrawAmmo(handle, center + new Vector2(outer + 4f * scale, 2f * scale), alpha);
    }

    private void DrawArc(DrawingHandleScreen handle, Vector2 center, float radius, float progress, Color color)
    {
        var count = Math.Max(2, (int) MathF.Ceiling(ArcSegments * progress) + 1);
        var sweep = MathF.Tau * progress;

        for (var i = 0; i < count; i++)
        {
            var angle = -MathF.PI / 2f + sweep * i / (count - 1);
            _arc[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _arc.AsSpan(0, count), color);
    }

    private void DrawAmmo(DrawingHandleScreen handle, Vector2 pos, float alpha)
    {
        var capacity = _sys.Capacity;
        if (capacity <= 0)
            return;

        var ammo = _sys.Ammo;
        if (ammo != _cachedAmmo)
        {
            _cachedAmmo = ammo;
            _ammoText = ammo.ToString();
        }

        var color = ammo <= 0 ? NoAmmoColor
            : ammo <= Math.Max(1, capacity / 4) ? LowAmmoColor
            : TextColor;

        handle.DrawString(_font, pos + Vector2.One, _ammoText, 1f, Fade(ShadowColor, alpha));
        handle.DrawString(_font, pos, _ammoText, 1f, Fade(color, alpha));
    }

    private static Color Fade(Color color, float alpha) => color.WithAlpha(color.A * alpha);
}
