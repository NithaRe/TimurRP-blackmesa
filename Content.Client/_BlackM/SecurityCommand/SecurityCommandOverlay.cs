using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._BlackM.Overlays;
using Content.Client.Resources;
using Content.Shared._BlackM.SecurityCommand;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.SecurityCommand;

public sealed class SecurityCommandOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    private const string FontPath = "/Fonts/_BlackM/Tektur-Regular.ttf";
    private const float EdgeMargin = 46f;
    private const float BadgeRadius = 15f;
    private const float HudWidth = 290f;
    private const int HudMaxLines = 6;
    private const int HudMaxTextLength = 30;
    private const float DangerRadius = 1.6f;

    private readonly SecurityCommandClientSystem _system;
    private readonly SharedTransformSystem _transform;
    private readonly Font _font;
    private readonly Font _fontSmall;
    private readonly List<ResolvedOrder> _resolved = new();
    private readonly List<GuidanceTarget> _guidance = new();
    private readonly NavigationArrow _navigationArrow;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public SecurityCommandOverlay(SecurityCommandClientSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _transform = _entMan.System<SharedTransformSystem>();
        _navigationArrow = new NavigationArrow(_entMan.System<SpriteSystem>());
        _font = _cache.GetFont(FontPath, 11);
        _fontSmall = _cache.GetFont(FontPath, 9);
        ZIndex = 5;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _system.State != null || _system.PlacingType != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.EntityExists(local))
            return;

        var localMap = _transform.GetMapCoordinates(local);
        ResolveOrders();

        switch (args.Space)
        {
            case OverlaySpace.WorldSpace:
                DrawWorld(args, localMap);
                break;
            case OverlaySpace.ScreenSpace:
                DrawScreen(args, localMap);
                break;
        }
    }

    private sealed class ResolvedOrder
    {
        public SecurityOrderData Data = default!;
        public MapId Map;
        public Vector2[] Points = Array.Empty<Vector2>();
        public Vector2 Anchor;
        public bool Relevant;
        public float Fade = 1f;
    }

    private static bool IsArea(SecurityOrderType type)
    {
        return type is SecurityOrderType.PatrolZone or SecurityOrderType.Post;
    }

    private static bool InsideArea(ResolvedOrder order, Vector2 position)
    {
        var min = order.Points[0];
        var max = order.Points[0];
        foreach (var p in order.Points)
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }

        return new Box2(min, max).Contains(position);
    }

    private void ResolveOrders()
    {
        _resolved.Clear();

        if (_system.State is not { } state)
            return;

        foreach (var order in state.Orders)
        {
            Vector2[] points;
            MapId map;

            if (IsArea(order.Type))
            {
                if (!TryResolveZone(order, out map, out points))
                    continue;
            }
            else if (!TryResolvePoints(order, out map, out points))
            {
                continue;
            }

            var anchor = points[0];
            if (IsArea(order.Type))
            {
                var sum = Vector2.Zero;
                foreach (var p in points)
                {
                    sum += p;
                }

                anchor = sum / points.Length;
            }

            var relevant = _system.IsRelevantToLocal(order);
            var fade = relevant ? _system.GetMarkerFade(order) : 1f;

            if (fade <= 0f)
                continue;

            _resolved.Add(new ResolvedOrder
            {
                Data = order,
                Map = map,
                Points = points,
                Anchor = anchor,
                Relevant = relevant,
                Fade = fade,
            });
        }
    }

    private bool TryResolve(NetCoordinates net, out EntityCoordinates coords, out MapCoordinates map)
    {
        coords = default;
        map = MapCoordinates.Nullspace;

        if (!_entMan.TryGetEntity(net.NetEntity, out var ent) || !_entMan.EntityExists(ent))
            return false;

        coords = new EntityCoordinates(ent.Value, net.Position);
        map = _transform.ToMapCoordinates(coords);
        return map.MapId != MapId.Nullspace;
    }

    private bool TryResolvePoints(SecurityOrderData order, out MapId map, out Vector2[] points)
    {
        map = MapId.Nullspace;
        points = new Vector2[order.Points.Count];

        if (points.Length == 0)
            return false;

        for (var i = 0; i < order.Points.Count; i++)
        {
            if (!TryResolve(order.Points[i], out _, out var mapCoords))
                return false;

            if (map == MapId.Nullspace)
                map = mapCoords.MapId;
            else if (map != mapCoords.MapId)
                return false;

            points[i] = mapCoords.Position;
        }

        return true;
    }

    private bool TryResolveZone(SecurityOrderData order, out MapId map, out Vector2[] points)
    {
        map = MapId.Nullspace;
        points = Array.Empty<Vector2>();

        if (order.Points.Count != 2)
            return false;

        if (!TryResolve(order.Points[0], out var a, out var mapA) ||
            !TryResolve(order.Points[1], out var b, out var mapB) ||
            mapA.MapId != mapB.MapId)
            return false;

        map = mapA.MapId;

        if (a.EntityId == b.EntityId)
        {
            var min = Vector2.Min(a.Position, b.Position);
            var max = Vector2.Max(a.Position, b.Position);
            points = new[]
            {
                _transform.ToMapCoordinates(new EntityCoordinates(a.EntityId, min)).Position,
                _transform.ToMapCoordinates(new EntityCoordinates(a.EntityId, new Vector2(max.X, min.Y))).Position,
                _transform.ToMapCoordinates(new EntityCoordinates(a.EntityId, max)).Position,
                _transform.ToMapCoordinates(new EntityCoordinates(a.EntityId, new Vector2(min.X, max.Y))).Position,
            };
            return true;
        }

        var wMin = Vector2.Min(mapA.Position, mapB.Position);
        var wMax = Vector2.Max(mapA.Position, mapB.Position);
        points = new[] { wMin, new Vector2(wMax.X, wMin.Y), wMax, new Vector2(wMin.X, wMax.Y) };
        return true;
    }

    private void DrawWorld(in OverlayDrawArgs args, MapCoordinates localMap)
    {
    }

    private void DrawRoute(DrawingHandleWorld handle, Vector2[] points, Color color, bool loopHint)
    {
        var px = _system.Pixel;
        var time = (float) _timing.RealTime.TotalSeconds;
        const float t = SecurityCommandPixel.WorldTexel;

        for (var i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            SecurityCommandPixel.TiledLine(handle, px.Line, a, b, t, color.WithAlpha(color.A * 0.55f));

            SecurityCommandPixel.ChevronTrail(handle, px.Chevron, a, b, 1.6f,
                SecurityCommandPixel.TrailSize * t, color, time * 0.5f);
        }

        if (loopHint && points.Length > 2)
            SecurityCommandPixel.TiledLine(handle, px.Dots, points[^1], points[0], t, color.WithAlpha(color.A * 0.6f));

        for (var i = 0; i < points.Length; i++)
        {
            SecurityCommandPixel.Sprite(handle, i == 0 ? px.NodeStart : px.Node, points[i],
                SecurityCommandPixel.TrailSize * t * (i == 0 ? 1.5f : 1f), color);
        }
    }

    private void DrawPlacementPreview(DrawingHandleWorld handle, MapId viewMap)
    {
        if (_system.PlacingType is not { } type)
            return;

        var px = _system.Pixel;
        const float t = SecurityCommandPixel.WorldTexel;
        var time = (float) _timing.RealTime.TotalSeconds;
        var color = _system.Draft.Color;
        var points = new List<Vector2>();
        foreach (var coords in _system.PendingPoints)
        {
            var map = _transform.ToMapCoordinates(coords);
            if (map.MapId == viewMap)
                points.Add(map.Position);
        }

        var mouse = _system.GetMouseMapPosition();
        var hasMouse = mouse.MapId == viewMap;

        switch (type)
        {
            case SecurityOrderType.PatrolRoute:
                return;

            case SecurityOrderType.PatrolZone:
            case SecurityOrderType.Post:
                if (hasMouse && points.Count == 1)
                {
                    var min = Vector2.Min(points[0], mouse.Position);
                    var max = Vector2.Max(points[0], mouse.Position);
                    px.ZoneOutline(handle, new[]
                    {
                        min, new Vector2(max.X, min.Y), max, new Vector2(min.X, max.Y),
                    }, t, color.WithAlpha(0.9f), false);
                }
                break;
        }

        if (hasMouse)
        {
            SecurityCommandPixel.Sprite(handle,
                SecurityCommandPixel.Loop(px.Reticle, time, SecurityCommandPixel.ReticleDelay), mouse.Position, 1f,
                color.WithAlpha(0.9f));
        }
    }

    private readonly record struct GuidanceTarget(Vector2 ScreenPosition, float DistanceSquared, Color Color, string? Caption);

    private void DrawScreen(in OverlayDrawArgs args, MapCoordinates localMap)
    {
        if (args.ViewportControl is not { } viewport || args.MapId != localMap.MapId)
            return;

        var handle = args.ScreenHandle;
        var bounds = new UIBox2(args.ViewportBounds.Left, args.ViewportBounds.Top,
            args.ViewportBounds.Right, args.ViewportBounds.Bottom);
        var inner = new UIBox2(bounds.Left + EdgeMargin, bounds.Top + EdgeMargin,
            bounds.Right - EdgeMargin, bounds.Bottom - EdgeMargin);
        var playerScreen = viewport.WorldToScreen(localMap.Position);
        var playerVisible = bounds.Contains(playerScreen);
        const float minDistanceSquared = SecurityCommandConstants.ArrivalDistance * SecurityCommandConstants.ArrivalDistance;

        _guidance.Clear();
        foreach (var order in _resolved)
        {
            if (order.Map != localMap.MapId)
                continue;

            var target = GetGuidancePosition(order, localMap.Position);
            var screen = viewport.WorldToScreen(target);
            var visible = inner.Contains(screen);
            var distanceSquared = Vector2.DistanceSquared(target, localMap.Position);
            var guide = playerVisible && order.Relevant && (!visible || distanceSquared >= minDistanceSquared);
            if (!visible && !guide)
                continue;

            var color = order.Data.Color.WithAlpha((order.Relevant ? 1f : 0.55f) * order.Fade);
            var label = Truncate(SecurityCommandClientSystem.GetDisplayText(order.Data), 24);
            var dist = order.Relevant ? GetDistanceText(order, localMap.Position, MathF.Sqrt(distanceSquared)) : null;
            var text = dist != null ? $"{label} · {dist}" : label;
            if (_system.GetPostDutyText(order.Data) is { } timer)
                text = $"{text} · {timer}";

            if (visible)
            {
                DrawArrow(handle, screen - new Vector2(0f, 4f), new Vector2(0f, 1f), 6f, color);
                DrawCentered(handle, _fontSmall, new Vector2(screen.X, screen.Y - 26f), text, color);
            }

            if (guide)
                _guidance.Add(new GuidanceTarget(screen, distanceSquared, color, visible ? null : text));
        }

        _guidance.Sort(static (a, b) => a.DistanceSquared.CompareTo(b.DistanceSquared));
        var uiScale = (viewport as Control)?.UIScale ?? 1f;
        var time = (float) _timing.RealTime.TotalSeconds;
        for (var i = Math.Min(NavigationArrow.MaxArrows, _guidance.Count) - 1; i >= 0; i--)
        {
            var target = _guidance[i];
            var center = _navigationArrow.Draw(handle, playerScreen, target.ScreenPosition, uiScale, time,
                i == 0, target.Color, target.Color.A);
            if (center is { } position && target.Caption is { } caption)
                DrawGuidanceCaption(handle, bounds, position + new Vector2(0f, 21f * uiScale), target.Color, caption);
        }

        DrawCursorHint(handle);
    }

    private static Vector2 GetGuidancePosition(ResolvedOrder order, Vector2 position)
    {
        if (order.Data.Type != SecurityOrderType.PatrolRoute)
            return order.Anchor;

        var closest = order.Points[0];
        var closestDistance = Vector2.DistanceSquared(closest, position);
        foreach (var point in order.Points.AsSpan(1))
        {
            var distance = Vector2.DistanceSquared(point, position);
            if (distance >= closestDistance)
                continue;

            closest = point;
            closestDistance = distance;
        }

        return closest;
    }

    private static string? GetDistanceText(ResolvedOrder order, Vector2 position, float distance)
    {
        switch (order.Data.Type)
        {
            case SecurityOrderType.Label:
                return null;

            case SecurityOrderType.PatrolZone:
            case SecurityOrderType.Post:
                if (InsideArea(order, position))
                    return Loc.GetString(order.Data.Type == SecurityOrderType.Post
                        ? "security-command-dist-on-post"
                        : "security-command-dist-in-zone");

                break;

            case SecurityOrderType.PatrolRoute when distance <= SecurityCommandConstants.ArrivalDistance:
                return Loc.GetString("security-command-dist-on-route");

            case SecurityOrderType.Danger when distance <= DangerRadius:
                return Loc.GetString("security-command-dist-in-danger");

            case SecurityOrderType.Move when distance <= SecurityCommandConstants.ArrivalDistance:
                return Loc.GetString("security-command-dist-arrived");
        }

        return Loc.GetString("security-command-dist", ("meters", (int) MathF.Round(distance)));
    }

    private void DrawGuidanceCaption(DrawingHandleScreen handle, UIBox2 bounds, Vector2 anchor, Color color, string text)
    {
        var dims = handle.GetDimensions(_fontSmall, text, 1f);
        var inset = new Vector2(4f);
        var min = bounds.TopLeft + inset;
        var max = Vector2.Max(min, bounds.BottomRight - dims - inset);
        var position = Vector2.Clamp(anchor - new Vector2(dims.X / 2f, 0f), min, max);
        DrawText(handle, _fontSmall, position, text, color);
    }

    private static readonly Vector2[] ArrowVerts = new Vector2[3];

    private static void DrawArrow(DrawingHandleScreen handle, Vector2 tip, Vector2 dir, float size, Color color)
    {
        var perp = new Vector2(-dir.Y, dir.X) * size * 0.6f;
        var back = tip - dir * size;
        var shadow = new Vector2(1f, 1f);

        var verts = ArrowVerts;
        verts[0] = tip + shadow;
        verts[1] = back + perp + shadow;
        verts[2] = back - perp + shadow;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, verts, Color.Black.WithAlpha(0.6f * color.A));

        verts[0] = tip;
        verts[1] = back + perp;
        verts[2] = back - perp;
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, verts, color);
    }

    private static void DrawIcon(DrawingHandleScreen handle, Texture texture, Vector2 center, float size, Color color)
    {
        var half = size / 2f;
        handle.DrawTextureRect(texture, new UIBox2(center.X - half + 1f, center.Y - half + 1f, center.X + half + 1f, center.Y + half + 1f), Color.Black.WithAlpha(0.45f * color.A));
        handle.DrawTextureRect(texture, new UIBox2(center.X - half, center.Y - half, center.X + half, center.Y + half), color);
    }

    private void DrawCursorHint(DrawingHandleScreen handle)
    {
        if (_system.PlacingType is not { } type)
            return;

        var hint = type switch
        {
            SecurityOrderType.PatrolRoute => Loc.GetString("security-command-cursor-route",
                ("count", _system.PendingPoints.Count), ("max", SecurityCommandConstants.MaxRoutePoints)),
            SecurityOrderType.Post => Loc.GetString(_system.PendingPoints.Count == 0
                ? "security-command-cursor-post-first"
                : "security-command-cursor-post-second"),
            SecurityOrderType.PatrolZone => Loc.GetString(_system.PendingPoints.Count == 0
                ? "security-command-cursor-zone-first"
                : "security-command-cursor-zone-second"),
            _ => Loc.GetString("security-command-cursor-point", ("type", SecurityCommandClientSystem.GetTypeName(type))),
        };

        var pos = _input.MouseScreenPosition.Position + new Vector2(18f, 18f);
        DrawText(handle, _fontSmall, pos, hint, _system.Draft.Color);
    }

    private static void DrawText(DrawingHandleScreen handle, Font font, Vector2 pos, string text, Color color)
    {
        handle.DrawString(font, pos + new Vector2(1f, 1f), text, Color.Black.WithAlpha(0.85f));
        handle.DrawString(font, pos, text, color);
    }

    private static void DrawCentered(DrawingHandleScreen handle, Font font, Vector2 pos, string text, Color color)
    {
        var dims = handle.GetDimensions(font, text, 1f);
        DrawText(handle, font, new Vector2(pos.X - dims.X / 2f, pos.Y), text, color);
    }

    private static string Truncate(string text, int max)
    {
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
