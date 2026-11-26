using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Overlays;

internal sealed class NavigationArrow
{
    public const int MaxArrows = 4;

    public static readonly ResPath NavigatorRsi = new("/Textures/_BlackM/Interface/EngineeringBreakdown/navigator.rsi");

    private const float Radius = 92f;
    private const float Size = 32f;

    private readonly Texture _texture;

    public NavigationArrow(SpriteSystem sprite)
    {
        _texture = sprite.Frame0(new SpriteSpecifier.Rsi(NavigatorRsi, "arrow"));
    }

    public Vector2? Draw(DrawingHandleScreen handle, Vector2 player, Vector2 target, float uiScale, float time,
        bool nearest, Color color, float opacity = 1f)
    {
        var delta = target - player;
        if (delta.LengthSquared() < 0.01f)
            return null;

        var direction = Vector2.Normalize(delta);
        var radius = nearest ? Radius + MathF.Sin(time * 4f) * 4f : Radius;
        var center = player + direction * (radius * uiScale);
        var angle = new Angle(MathF.Atan2(direction.X, -direction.Y));
        var size = new Vector2(Size * (nearest ? 1.1f : 0.8f) * uiScale);
        var alpha = nearest ? 0.9f : 0.5f;

        handle.SetTransform(Matrix3Helpers.CreateTransform(center, angle));
        handle.DrawTextureRect(_texture, UIBox2.FromDimensions(-size / 2f, size), color.WithAlpha(alpha * opacity));
        handle.SetTransform(Matrix3x2.Identity);
        return center;
    }
}
