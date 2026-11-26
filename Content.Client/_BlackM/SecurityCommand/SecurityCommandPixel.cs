using System;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.SecurityCommand;

public sealed class SecurityCommandPixel
{
    public static readonly ResPath TrailRsi = new("/Textures/_BlackM/Interface/SecurityCommand/trail.rsi");
    public static readonly ResPath MarkersRsi = new("/Textures/_BlackM/Interface/SecurityCommand/markers.rsi");

    public const float TrailSize = 16f;

    public const float MarkerSize = 32f;

    public const float WorldTexel = 1f / 32f;

    public const float MoveDelay = 0.15f;
    public const float DangerDelay = 0.12f;
    public const float ReticleDelay = 0.35f;

    public readonly Texture Line;
    public readonly Texture Dash;
    public readonly Texture Dots;
    public readonly Texture Chevron;
    public readonly Texture Arrowhead;
    public readonly Texture Node;
    public readonly Texture NodeStart;
    public readonly Texture Corner;
    public readonly Texture CursorDot;

    public readonly Texture[] Move;
    public readonly Texture Post;
    public readonly Texture[] Danger;
    public readonly Texture DangerFill;
    public readonly Texture Pointer;
    public readonly Texture Badge;
    public readonly Texture[] Reticle;
    public readonly Texture[] Arrival;

    public SecurityCommandPixel(SpriteSystem sprite)
    {
        Texture One(ResPath rsi, string state)
        {
            return sprite.Frame0(new SpriteSpecifier.Rsi(rsi, state));
        }

        Texture[] Frames(ResPath rsi, string state)
        {
            var rsiState = sprite.RsiStateLike(new SpriteSpecifier.Rsi(rsi, state));
            var count = Math.Max(1, rsiState.AnimationFrameCount);
            var frames = new Texture[count];
            for (var i = 0; i < count; i++)
            {
                frames[i] = rsiState.GetFrame(default, i);
            }

            return frames;
        }

        Line = One(TrailRsi, "line");
        Dash = One(TrailRsi, "dash");
        Dots = One(TrailRsi, "dots");
        Chevron = One(TrailRsi, "chevron");
        Arrowhead = One(TrailRsi, "arrowhead");
        Node = One(TrailRsi, "node");
        NodeStart = One(TrailRsi, "node_start");
        Corner = One(TrailRsi, "corner");
        CursorDot = One(TrailRsi, "cursor_dot");

        Move = Frames(MarkersRsi, "move");
        Post = One(MarkersRsi, "post");
        Danger = Frames(MarkersRsi, "danger");
        DangerFill = One(MarkersRsi, "danger_fill");
        Pointer = One(MarkersRsi, "pointer");
        Badge = One(MarkersRsi, "badge");
        Reticle = Frames(MarkersRsi, "reticle");
        Arrival = Frames(MarkersRsi, "arrival");
    }

    public static Texture Loop(Texture[] frames, float time, float delay)
    {
        if (frames.Length == 1 || delay <= 0f)
            return frames[0];

        var index = (int) MathF.Floor(time / delay) % frames.Length;
        if (index < 0)
            index += frames.Length;

        return frames[index];
    }

    public static Texture Once(Texture[] frames, float t)
    {
        var index = Math.Clamp((int) (t * frames.Length), 0, frames.Length - 1);
        return frames[index];
    }

    public static void Sprite(DrawingHandleBase handle, Texture texture, Vector2 center, float size, Color color)
    {
        switch (handle)
        {
            case DrawingHandleWorld world:
                world.DrawTextureRect(texture, Box2.CenteredAround(center, new Vector2(size, size)), color);
                break;
            case DrawingHandleScreen screen:
                var leftTop = new Vector2(MathF.Round(center.X - size * 0.5f), MathF.Round(center.Y - size * 0.5f));
                screen.DrawTextureRect(texture, new UIBox2(leftTop, leftTop + new Vector2(size, size)), color);
                break;
        }
    }

    public static void Quad(DrawingHandleBase handle, Texture texture, Vector2 center, Vector2 size, Vector2 dir, Color color)
    {
        var angle = MathF.Atan2(dir.Y, dir.X);

        switch (handle)
        {
            case DrawingHandleWorld world:
                var box = Box2.CenteredAround(center, size);
                world.DrawTextureRect(texture, new Box2Rotated(box, new Angle(angle), center), color);
                break;
            case DrawingHandleScreen screen:
                if (MathF.Abs(angle) < 0.0001f)
                {
                    screen.DrawTextureRect(texture, new UIBox2(center - size * 0.5f, center + size * 0.5f), color);
                    break;
                }

                var old = screen.GetTransform();
                screen.SetTransform(Matrix3x2.CreateRotation(angle) * Matrix3x2.CreateTranslation(center) * old);
                screen.DrawTextureRect(texture, new UIBox2(-size * 0.5f, size * 0.5f), color);
                screen.SetTransform(old);
                break;
        }
    }

    public static void TiledLine(DrawingHandleBase handle, Texture texture, Vector2 a, Vector2 b, float texel, Color color, int maxTiles = 256)
    {
        var delta = b - a;
        var len = delta.Length();
        if (len < texel)
            return;

        var dir = delta / len;
        var tile = TrailSize * texel;
        var count = Math.Clamp((int) MathF.Round(len / tile), 1, maxTiles);
        var piece = len / count;
        var size = new Vector2(piece, tile);

        for (var i = 0; i < count; i++)
        {
            Quad(handle, texture, a + dir * (piece * (i + 0.5f)), size, dir, color);
        }
    }

    public static void ChevronTrail(
        DrawingHandleBase handle,
        Texture texture,
        Vector2 a,
        Vector2 b,
        float spacing,
        float size,
        Color color,
        float phase,
        int maxCount = 48)
    {
        var delta = b - a;
        var len = delta.Length();
        if (len < size * 0.5f || spacing <= 0f)
            return;

        if (len / spacing > maxCount)
            spacing = len / maxCount;

        var dir = delta / len;
        var frac = phase - MathF.Floor(phase);
        var fadeLen = MathF.Max(0.0001f, MathF.Min(spacing * 1.5f, len * 0.3f));
        var quad = new Vector2(size, size);
        var half = size * 0.25f;

        for (var s = frac * spacing; s <= len - half; s += spacing)
        {
            if (s < half)
                continue;

            var fade = MathF.Min(1f, MathF.Min(s / fadeLen, (len - s) / fadeLen));
            if (fade <= 0.02f)
                continue;

            Quad(handle, texture, a + dir * s, quad, dir, color.WithAlpha(color.A * fade));
        }
    }

    public void PointerAt(DrawingHandleBase handle, Vector2 tip, Vector2 dir, float texel, Color color)
    {
        var size = MarkerSize * texel;
        Quad(handle, Pointer, tip - dir * (12f * texel), new Vector2(size, size), dir, color);
    }

    public void CheckpointBorder(DrawingHandleBase handle, Vector2[] corners, float texel, Color color, bool yDown)
    {
        if (corners.Length < 3)
            return;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, corners, color.WithAlpha(color.A * 0.12f));

        for (var i = 0; i < corners.Length; i++)
        {
            TiledLine(handle, Line, corners[i], corners[(i + 1) % corners.Length], texel, color);
        }

        ZoneOutline(handle, corners, texel, color, yDown);
    }

    public void ZoneOutline(DrawingHandleBase handle, Vector2[] corners, float texel, Color color, bool yDown)
    {
        var count = corners.Length;
        if (count < 2)
            return;

        const float cornerOffset = 6f;
        const float arm = 13f;
        var quad = new Vector2(TrailSize * texel, TrailSize * texel);

        for (var i = 0; i < count; i++)
        {
            var c = corners[i];
            var next = corners[(i + 1) % count] - c;
            var prev = corners[(i + count - 1) % count] - c;
            var nl = next.Length();
            var pl = prev.Length();
            if (nl < 0.0001f || pl < 0.0001f)
                continue;

            var n = next / nl;
            var p = prev / pl;

            var cross = n.X * p.Y - n.Y * p.X;
            var xAxis = (cross > 0f) == yDown ? n : p;
            var downAxis = xAxis == n ? p : n;

            Quad(handle, Corner, c + (xAxis + downAxis) * (cornerOffset * texel), quad, xAxis, color);

            var start = c + n * MathF.Min(arm * texel, nl * 0.5f);
            var end = c + next - n * MathF.Min(arm * texel, nl * 0.5f);
            if ((end - start).LengthSquared() > texel * texel * 4f)
                TiledLine(handle, Dash, start, end, texel, color.WithAlpha(color.A * 0.85f));
        }
    }
}
