using System.Numerics;
using Content.Shared._BlackM.NameTag;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.NameTag;

public sealed class NameTagOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;
    private readonly Font _font;
    private readonly Texture _frame;
    private readonly Texture _arrow;
    private readonly Texture _crest;
    private readonly Texture _sparkle;

    private readonly Dictionary<EntityUid, (string Text, double Start)> _appear = new();
    private readonly List<EntityUid> _toRemove = new();
    private readonly HashSet<EntityUid> _seen = new();

    private const float FrameSrc = 32f;
    private const float FrameDst = 24f;
    private const float AppearTime = 0.45f;
    private const float HeadOffset = 0.7f;
    private const float Decor = 0.75f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public NameTagOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entMan.System<SharedTransformSystem>();
        _font = new VectorFont(
            _cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 22);

        _frame = LoadTex("frame");
        _arrow = LoadTex("arrow");
        _crest = LoadTex("crest");
        _sparkle = LoadTex("sparkle");
    }

    private Texture LoadTex(string name)
    {
        return _cache.GetResource<TextureResource>($"/Textures/_BlackM/NameTag/{name}.png").Texture;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        var uiScale = _ui.RootControl.UIScale;
        var t = _timing.RealTime.TotalSeconds;

        _seen.Clear();

        var query = _entMan.EntityQueryEnumerator<NameTagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var tag, out var xform))
        {
            if (xform.MapID != args.MapId || string.IsNullOrEmpty(tag.Text))
                continue;

            _seen.Add(uid);

            if (!_appear.TryGetValue(uid, out var info) || info.Text != tag.Text)
            {
                info = (tag.Text, t);
                _appear[uid] = info;
            }

            var p = (float) Math.Clamp((t - info.Start) / AppearTime, 0, 1);
            var k = EaseOutBack(p);          
            var alpha = Math.Min(1f, p * 2f);
            var s = uiScale * k;
            if (s <= 0.01f)
                continue;

            var bob = (float) Math.Sin(t * 2.5) * 3f * uiScale;         
            var arrowBob = (float) Math.Sin(t * 5.0) * 2.5f * uiScale; 
            var pulse = 0.78f + 0.22f * (float) Math.Sin(t * 4.0);      
            var shimmer = 0.5f + 0.5f * (float) Math.Sin(t * 3.0);      

            var worldPos = _transform.GetWorldPosition(xform);

            var basePos = args.ViewportControl.WorldToScreen(worldPos);
            var onePlus = args.ViewportControl.WorldToScreen(worldPos + new Vector2(0f, 1f));

            var pixelsPerMeter = Vector2.Distance(basePos, onePlus);

            var anchor = basePos + new Vector2(0f, -pixelsPerMeter * HeadOffset + bob);

            var textSize = handle.GetDimensions(_font, tag.Text, s);
            var padX = 24f * s;
            var padY = 12f * s;
            var frameW = textSize.X + padX * 2;
            var frameH = textSize.Y + padY * 2;

            var arrowW = 36f * Decor * s;
            var arrowH = 26f * Decor * s;
            var arrowTop = anchor.Y - arrowH + arrowBob;

            var frameX = anchor.X - frameW / 2f;
            var frameY = arrowTop - frameH + 5f * s;

            var frameColor = tag.Color.WithAlpha(alpha * pulse);
            DrawNineSlice(handle, _frame, frameX, frameY, frameW, frameH, s, frameColor);

            var arrowRect = UIBox2.FromDimensions(anchor.X - arrowW / 2f, arrowTop, arrowW, arrowH);
            handle.DrawTextureRect(_arrow, arrowRect, tag.Color.WithAlpha(alpha));

            var crestW = 80f * Decor * s;
            var crestH = 34f * Decor * s;
            var crestRect = UIBox2.FromDimensions(
                anchor.X - crestW / 2f,
                frameY - crestH * 0.62f + 2f * s,
                crestW, crestH);
            handle.DrawTextureRect(_crest, crestRect, tag.Color.WithAlpha(alpha));

            var textPos = new Vector2(frameX + padX, frameY + padY);

            if (tag.Rainbow)
            {
                DrawRainbowString(handle, textPos, tag.Text, s, alpha, t);
            }
            else
            {
                var textColor = Color.InterpolateBetween(tag.Color, Color.White, 0.25f * shimmer);
                handle.DrawString(_font, textPos + new Vector2(2, 2) * s, tag.Text, s,
                    Color.Black.WithAlpha(0.8f * alpha));
                handle.DrawString(_font, textPos, tag.Text, s, textColor.WithAlpha(alpha));
            }

            var gem = 9.5f * (FrameDst / FrameSrc) * s;
            DrawSparkle(handle, new Vector2(frameX + gem, frameY + gem), s, alpha, t, 0f);
            DrawSparkle(handle, new Vector2(frameX + frameW - gem, frameY + gem), s, alpha, t, 1.6f);
            DrawSparkle(handle, new Vector2(frameX + gem, frameY + frameH - gem), s, alpha, t, 3.2f);
            DrawSparkle(handle, new Vector2(frameX + frameW - gem, frameY + frameH - gem), s, alpha, t, 4.8f);
            DrawSparkle(handle, new Vector2(anchor.X, frameY + 0.5f * s), s, alpha, t, 2.4f);
        }

        _toRemove.Clear();
        foreach (var key in _appear.Keys)
        {
            if (!_seen.Contains(key))
                _toRemove.Add(key);
        }

        foreach (var key in _toRemove)
            _appear.Remove(key);
    }

    private void DrawRainbowString(DrawingHandleScreen handle, Vector2 pos, string text, float s, float alpha, double t)
    {
        handle.DrawString(_font, pos + new Vector2(2, 2) * s, text, s, Color.Black.WithAlpha(0.8f * alpha));

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == ' ')
                continue;

            var offsetX = i == 0 ? 0f : handle.GetDimensions(_font, text.Substring(0, i), s).X;
            var wave = (float) Math.Sin(t * 4.0 + i * 0.5) * 2f * s;

            var hue = (float) ((t * 0.35 + i * 0.07) % 1.0);
            var color = Color.FromHsv(new Vector4(hue, 0.85f, 1f, alpha));

            handle.DrawString(_font, pos + new Vector2(offsetX, wave), ch.ToString(), s, color);
        }
    }

    private void DrawSparkle(DrawingHandleScreen handle, Vector2 center, float s, float alpha, double t, float phase)
    {
        var v = Math.Max(0f, (float) Math.Sin(t * 3.0 + phase));
        if (v <= 0.02f)
            return;

        var size = 26f * s * (0.25f + 0.75f * v);
        var rect = UIBox2.FromDimensions(center.X - size / 2f, center.Y - size / 2f, size, size);
        handle.DrawTextureRect(_sparkle, rect, Color.White.WithAlpha(alpha * v));
    }

    private static void DrawNineSlice(
        DrawingHandleScreen handle, Texture tex,
        float x, float y, float w, float h, float s, Color color)
    {
        var c = FrameDst * s;
        var src = FrameSrc;
        float tw = tex.Width;
        float th = tex.Height;

        var x0 = x;
        var x1 = x + c;
        var x2 = x + w - c;
        var x3 = x + w;
        var y0 = y;
        var y1 = y + c;
        var y2 = y + h - c;
        var y3 = y + h;

        var u1 = src;
        var u2 = tw - src;
        var v1 = src;
        var v2 = th - src;

        // верхний ряд
        Part(handle, tex, x0, y0, x1, y1, 0f, 0f, u1, v1, color);
        Part(handle, tex, x1, y0, x2, y1, u1, 0f, u2, v1, color);
        Part(handle, tex, x2, y0, x3, y1, u2, 0f, tw, v1, color);

        // средний ряд
        Part(handle, tex, x0, y1, x1, y2, 0f, v1, u1, v2, color);
        Part(handle, tex, x1, y1, x2, y2, u1, v1, u2, v2, color);
        Part(handle, tex, x2, y1, x3, y2, u2, v1, tw, v2, color);

        // нижний ряд
        Part(handle, tex, x0, y2, x1, y3, 0f, v2, u1, th, color);
        Part(handle, tex, x1, y2, x2, y3, u1, v2, u2, th, color);
        Part(handle, tex, x2, y2, x3, y3, u2, v2, tw, th, color);
    }

    private static void Part(
        DrawingHandleScreen handle, Texture tex,
        float dx0, float dy0, float dx1, float dy1,
        float sx0, float sy0, float sx1, float sy1,
        Color color)
    {
        handle.DrawTextureRectRegion(
            tex,
            new UIBox2(dx0, dy0, dx1, dy1),
            new UIBox2(sx0, sy0, sx1, sy1),
            color);
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        var p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}