using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Pinpointer.UI;
using Content.Client.Resources;
using Content.Shared._BlackM.SecurityCommand;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.SecurityCommand;

public sealed class SecurityTacticalMapControl : NavMapControl
{
    private const string FontPath = "/Fonts/_BlackM/Tektur-Regular.ttf";
    private const float DangerRadius = 1.6f;
    private const float HoverDistance = 14f;

    private static readonly Color Accent = Color.FromHex("#9B5DE5");

    private readonly SecurityCommandClientSystem _system;
    private readonly SharedTransformSystem _xformSystem;
    private readonly IInputManager _input;
    private readonly IPlayerManager _player;
    private readonly Font _font;
    private readonly Font _fontSmall;

    private SecurityOrderData? _hoverOrder;
    private SecurityOfficerData? _hoverOfficer;
    private Vector2? _hoverAnchor;

    private PanelContainer? _menu;

    public event Action<SecurityOfficerData>? OfficerClicked;

    public event Action<SecurityMapContext>? ContextRequested;

    public NetEntity? SelectedOfficer { get; set; }

    public int? SelectedSquad { get; set; }

    public bool MenuOpen => _menu != null;

    public SecurityTacticalMapControl(SecurityCommandClientSystem system) : base(useAvailableSize: true)
    {
        _system = system;
        _xformSystem = EntManager.System<SharedTransformSystem>();
        _input = IoCManager.Resolve<IInputManager>();
        _player = IoCManager.Resolve<IPlayerManager>();

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 10);
        _fontSmall = cache.GetFont(FontPath, 8);

        WallColor = new Color(120, 205, 130);
        TileColor = new Color(20, 44, 30);
        BackgroundColor = Color.FromSrgb(TileColor.WithAlpha(BackgroundOpacity));
    }

    public void SetGrid(EntityUid? grid)
    {
        if (MapUid == grid)
            return;

        MapUid = grid;
        Offset = Vector2.Zero;
        TargetOffset = Vector2.Zero;
        ForceNavMapUpdate();
    }

    public void CenterOn(MapCoordinates position)
    {
        if (!TryGetGrid(out var grid, out var xform) || position.MapId != xform.MapID)
            return;

        var local = Vector2.Transform(position.Position, _xformSystem.GetInvWorldMatrix(xform));
        CenterToCoordinates(new EntityCoordinates(grid, local));
    }

    public void CenterOnLocal()
    {
        if (_player.LocalEntity is not { } local || !EntManager.EntityExists(local))
            return;

        CenterOn(_xformSystem.GetMapCoordinates(local));
    }

    public void CenterOnOrder(SecurityOrderData order)
    {
        if (order.Points.Count == 0)
            return;

        var sum = Vector2.Zero;
        var count = 0;
        var mapId = MapId.Nullspace;

        foreach (var net in order.Points)
        {
            var coords = EntManager.GetCoordinates(net);
            if (!coords.IsValid(EntManager))
                continue;

            var map = _xformSystem.ToMapCoordinates(coords);
            mapId = map.MapId;
            sum += map.Position;
            count++;
        }

        if (count > 0)
            CenterOn(new MapCoordinates(sum / count, mapId));
    }

    public void OpenMenu(Control content, Vector2 pixelPosition)
    {
        CloseMenu();

        _menu = new PanelContainer
        {
            MouseFilter = MouseFilterMode.Stop,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#0d1013").WithAlpha(0.96f),
                BorderColor = Accent,
                BorderThickness = new Thickness(1),
            },
        };
        _menu.AddChild(content);
        AddChild(_menu);

        _menu.Measure(Vector2Helpers.Infinity);
        var size = _menu.DesiredSize;
        var scale = UIScale > 0f ? UIScale : 1f;
        var pos = pixelPosition / scale + new Vector2(6f, 6f);
        var min = new Vector2(4f, 4f);
        var max = Vector2.Max(min, Size - size - min);
        LayoutContainer.SetPosition(_menu, Vector2.Clamp(pos, min, max));
    }

    public void CloseMenu()
    {
        _menu?.Orphan();
        _menu = null;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        var dragged = (StartDragPosition - args.PointerLocation.Position).Length() > MinDragDistance;

        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick && args.Function != EngineKeyFunctions.UIRightClick)
            return;

        if (_menu != null)
        {
            CloseMenu();
            if (args.Function == EngineKeyFunctions.UIClick || _system.PlacingType != null)
            {
                args.Handle();
                return;
            }
        }

        if (_system.PlacingType == null)
        {
            HandleFreeClick(args, dragged);
            return;
        }

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (dragged || !TryGetGrid(out var grid, out _))
                return;

            var local = args.PointerLocation.Position - GlobalPixelPosition;
            var gridLocal = ControlToGridLocal(local, GetOffset());
            _system.AddMapPoint(new EntityCoordinates(grid, gridLocal));
            args.Handle();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick)
        {
            _system.PlacementSecondary();
            args.Handle();
        }
    }

    private void HandleFreeClick(GUIBoundKeyEventArgs args, bool dragged)
    {
        var local = args.PointerLocation.Position - GlobalPixelPosition;

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (dragged)
                return;

            if (_hoverOfficer is { } officer)
            {
                OfficerClicked?.Invoke(officer);
                args.Handle();
            }
            else if (_hoverOrder is { } order)
            {
                ContextRequested?.Invoke(new SecurityMapContext(local, null, order, null));
                args.Handle();
            }

            return;
        }

        EntityCoordinates? coords = null;
        if (TryGetGrid(out var grid, out _))
            coords = new EntityCoordinates(grid, ControlToGridLocal(local, GetOffset()));

        ContextRequested?.Invoke(new SecurityMapContext(local, coords, _hoverOrder, _hoverOfficer));
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (!TryGetGrid(out _, out var gridXform))
        {
            var text = Loc.GetString("security-command-map-no-navmap");
            var dims = handle.GetDimensions(_font, text, 1f);
            DrawText(handle, _font, new Vector2(PixelSize.X, PixelSize.Y) / 2f - dims / 2f, text, Color.LightGray);
            return;
        }

        var mapId = gridXform.MapID;
        var inv = _xformSystem.GetInvWorldMatrix(gridXform);
        var offset = GetOffset();
        var scale = MinimapScale;
        var iconSize = Math.Clamp(scale * 1.25f, 15f, 25f);
        var time = (float) Timing.RealTime.TotalSeconds;
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 4f);
        var hasMouse = TryGetMouseLocal(out var mouse);

        string? hoverText = null;
        var hoverColor = Color.White;
        var hoverBest = HoverDistance;
        _hoverOrder = null;
        _hoverOfficer = null;
        _hoverAnchor = null;

        void TryHover(Vector2 pos, string text, Color color, SecurityOrderData? order, SecurityOfficerData? officer)
        {
            if (!hasMouse || _menu != null)
                return;

            var dist = (pos - mouse).Length();
            if (dist >= hoverBest)
                return;

            hoverBest = dist;
            hoverText = text;
            hoverColor = color;
            _hoverOrder = order;
            _hoverOfficer = officer;
            _hoverAnchor = pos;
        }

        Vector2 ToControl(Vector2 world)
        {
            return LocalToControl(Vector2.Transform(world, inv), offset);
        }

        if (_system.State is { } state)
        {
            var points = new List<Vector2>();
            var locals = new List<Vector2>();

            foreach (var order in state.Orders)
            {
                points.Clear();
                locals.Clear();

                foreach (var net in order.Points)
                {
                    var coords = EntManager.GetCoordinates(net);
                    if (!coords.IsValid(EntManager))
                        continue;

                    var map = _xformSystem.ToMapCoordinates(coords);
                    if (map.MapId != mapId)
                        continue;

                    var local = Vector2.Transform(map.Position, inv);
                    locals.Add(local);
                    points.Add(LocalToControl(local, offset));
                }

                if (points.Count == 0)
                    continue;

                var relevant = _system.IsRelevantToLocal(order);
                var color = order.Color.WithAlpha(relevant ? 1f : 0.85f);
                var anchor = DrawOrderShape(handle, order.Type, points, locals, offset, color, scale, pulse);

                var icon = _system.GetIcon(SecurityCommandSprites.Order(order.Type));
                var pin = order.Type == SecurityOrderType.Move;
                DrawIcon(handle, icon, anchor, iconSize, color, pin);

                var label = SecurityCommandClientSystem.GetDisplayText(order);
                if (scale >= 7f)
                    DrawCentered(handle, _fontSmall, anchor + new Vector2(0f, iconSize * 0.55f), Truncate(label, 22), color);

                var issuer = Loc.GetString("security-command-order-issuer", ("name", order.IssuerName));
                var reached = _system.IsReached(order.Id)
                    ? "\n" + Loc.GetString("security-command-map-reached")
                    : string.Empty;
                var timer = _system.GetPostDutyText(order) is { } duty ? "\n" + duty : string.Empty;
                TryHover(anchor, label + "\n" + _system.DescribeTarget(order) + "\n" + issuer + reached + timer, color, order, null);
            }

            var localNet = _player.LocalEntity is { } localEnt ? EntManager.GetNetEntity(localEnt) : NetEntity.Invalid;
            foreach (var officer in state.Officers)
            {
                if (!_system.TryGetOfficerPosition(officer.Entity, out var map) || map.MapId != mapId)
                    continue;

                var pos = ToControl(map.Position);
                var squad = _system.GetSquad(officer.SquadId);
                var color = officer.Status switch
                {
                    SecurityOfficerStatus.Dead => Color.FromHex("#e04a40"),
                    SecurityOfficerStatus.Critical => Color.FromHex("#f0903a"),
                    SecurityOfficerStatus.Offline => Color.Gray,
                    _ => squad?.Color ?? Color.FromHex("#d8dde3"),
                };

                var size = iconSize * 0.8f;
                if (officer.Entity == localNet)
                    handle.DrawCircle(pos, size * 0.75f + 2f * pulse, Accent.WithAlpha(0.8f), false);

                var selected = SelectedOfficer == officer.Entity ||
                               SelectedSquad != null && officer.SquadId == SelectedSquad;
                if (selected)
                {
                    var ring = size * 0.85f + 2f;
                    handle.DrawCircle(pos, ring, _system.Draft.Color.WithAlpha(0.25f));
                    handle.DrawCircle(pos, ring, _system.Draft.Color, false);
                    handle.DrawCircle(pos, ring + 1f, _system.Draft.Color, false);
                }

                var disc = size * 0.62f;
                handle.DrawCircle(pos, disc, Color.FromHex("#0d1013").WithAlpha(0.9f));
                handle.DrawCircle(pos, disc, color, false);
                handle.DrawCircle(pos, disc - 1f, color, false);
                var iconTint = officer.Status == SecurityOfficerStatus.Offline ? Color.White.WithAlpha(0.45f) : Color.White;
                DrawIcon(handle, _system.GetIcon(SecurityCommandSprites.ForOfficer(officer)), pos, size * 0.72f, iconTint, false);

                var name = squad != null ? $"[{squad.Name}] {officer.Name}" : officer.Name;
                if (scale >= 10f)
                    DrawCentered(handle, _fontSmall, pos + new Vector2(0f, size * 0.55f), Truncate(officer.Name, 18), Color.White);

                var officerHint = state.IsCommander ? "\n" + Loc.GetString("security-command-map-officer-hint") : string.Empty;
                TryHover(pos, $"{name}\n{officer.Job}" + officerHint, color, null, officer);
            }
        }

        DrawPlacement(handle, mapId, inv, offset, hasMouse, mouse, iconSize);

        if (_hoverAnchor is { } hoverPos && _system.PlacingType == null)
        {
            handle.DrawCircle(hoverPos, iconSize * 0.7f + 2f * pulse, hoverColor.WithAlpha(0.9f), false);
            handle.DrawCircle(hoverPos, iconSize * 0.7f + 3f + 2f * pulse, hoverColor.WithAlpha(0.4f), false);
        }

        if (hoverText != null)
            DrawTooltip(handle, mouse + new Vector2(14f, 14f), hoverText, hoverColor);
    }

    private Vector2 DrawOrderShape(
        DrawingHandleScreen handle,
        SecurityOrderType type,
        List<Vector2> points,
        List<Vector2> locals,
        Vector2 offset,
        Color color,
        float scale,
        float pulse)
    {
        var anchor = points[0];
        var px = _system.Pixel;
        var texel = MapTexel(scale);
        var time = (float) Timing.RealTime.TotalSeconds;

        switch (type)
        {
            case SecurityOrderType.Move:
                SecurityCommandPixel.Sprite(handle,
                    SecurityCommandPixel.Loop(px.Move, time, SecurityCommandPixel.MoveDelay), anchor,
                    SecurityCommandPixel.MarkerSize * texel, color);
                break;

            case SecurityOrderType.Post:
            {
                if (locals.Count < 2)
                    break;

                var box = BoxCorners(locals, offset);
                px.CheckpointBorder(handle, box, texel, color.WithAlpha(color.A * (0.85f + 0.15f * pulse)), true);
                anchor = (box[0] + box[2]) / 2f;
                break;
            }

            case SecurityOrderType.Danger:
            {
                var radius = MathF.Max(DangerRadius * scale, 10f);
                var size = radius * 2f * SecurityCommandPixel.MarkerSize / 25.2f;
                SecurityCommandPixel.Sprite(handle, px.DangerFill, anchor, size, color.WithAlpha(color.A * (0.2f + 0.12f * pulse)));
                SecurityCommandPixel.Sprite(handle,
                    SecurityCommandPixel.Loop(px.Danger, time, SecurityCommandPixel.DangerDelay), anchor, size, color);
                break;
            }

            case SecurityOrderType.Label:
                SecurityCommandPixel.Sprite(handle, px.Node, anchor, SecurityCommandPixel.TrailSize * texel, color);
                break;

            case SecurityOrderType.PatrolRoute:
                var phase = time * 0.6f;
                for (var i = 0; i < points.Count - 1; i++)
                {
                    SecurityCommandPixel.TiledLine(handle, px.Line, points[i], points[i + 1], texel, color.WithAlpha(color.A * 0.6f));

                    SecurityCommandPixel.ChevronTrail(handle, px.Chevron, points[i], points[i + 1], 26f,
                        SecurityCommandPixel.TrailSize * texel, color, phase, 24);
                }

                if (points.Count > 2)
                    SecurityCommandPixel.TiledLine(handle, px.Dots, points[^1], points[0], texel, color.WithAlpha(color.A * 0.6f));

                for (var i = 0; i < points.Count; i++)
                {
                    SecurityCommandPixel.Sprite(handle, i == 0 ? px.NodeStart : px.Node, points[i],
                        SecurityCommandPixel.TrailSize * texel, color);
                }

                break;

            case SecurityOrderType.PatrolZone:
            {
                var min = locals[0];
                var max = locals[0];
                foreach (var l in locals)
                {
                    min = Vector2.Min(min, l);
                    max = Vector2.Max(max, l);
                }

                var corners = new[]
                {
                    LocalToControl(new Vector2(min.X, min.Y), offset),
                    LocalToControl(new Vector2(max.X, min.Y), offset),
                    LocalToControl(new Vector2(max.X, max.Y), offset),
                    LocalToControl(new Vector2(min.X, max.Y), offset),
                };

                px.ZoneOutline(handle, corners, texel, color, true);

                anchor = (corners[0] + corners[2]) / 2f;
                break;
            }
        }

        return anchor;
    }

    private Vector2[] BoxCorners(List<Vector2> locals, Vector2 offset)
    {
        var min = locals[0];
        var max = locals[0];
        foreach (var l in locals)
        {
            min = Vector2.Min(min, l);
            max = Vector2.Max(max, l);
        }

        return new[]
        {
            LocalToControl(new Vector2(min.X, min.Y), offset),
            LocalToControl(new Vector2(max.X, min.Y), offset),
            LocalToControl(new Vector2(max.X, max.Y), offset),
            LocalToControl(new Vector2(min.X, max.Y), offset),
        };
    }

    private static float MapTexel(float scale)
    {
        return scale >= 12f ? 2f : 1f;
    }

    private void DrawPlacement(
        DrawingHandleScreen handle,
        MapId mapId,
        Matrix3x2 inv,
        Vector2 offset,
        bool hasMouse,
        Vector2 mouse,
        float iconSize)
    {
        if (_system.PlacingType is not { } type)
            return;

        var color = _system.Draft.Color;
        var points = new List<Vector2>();
        var locals = new List<Vector2>();

        foreach (var coords in _system.PendingPoints)
        {
            var map = _xformSystem.ToMapCoordinates(coords);
            if (map.MapId != mapId)
                continue;

            var local = Vector2.Transform(map.Position, inv);
            locals.Add(local);
            points.Add(LocalToControl(local, offset));
        }

        var px = _system.Pixel;
        var texel = MapTexel(MinimapScale);

        switch (type)
        {
            case SecurityOrderType.PatrolRoute:
                for (var i = 0; i < points.Count - 1; i++)
                {
                    SecurityCommandPixel.TiledLine(handle, px.Line, points[i], points[i + 1], texel, color);
                }

                for (var i = 0; i < points.Count; i++)
                {
                    SecurityCommandPixel.Sprite(handle, i == 0 ? px.NodeStart : px.Node, points[i],
                        SecurityCommandPixel.TrailSize * texel, color);
                }

                if (hasMouse && points.Count > 0)
                    SecurityCommandPixel.TiledLine(handle, px.Dash, points[^1], mouse, texel, color.WithAlpha(0.7f));
                break;

            case SecurityOrderType.PatrolZone:
            case SecurityOrderType.Post:
                if (hasMouse && locals.Count == 1)
                {
                    var mouseLocal = ControlToGridLocal(mouse, offset);
                    var min = Vector2.Min(locals[0], mouseLocal);
                    var max = Vector2.Max(locals[0], mouseLocal);
                    var a = LocalToControl(min, offset);
                    var b = LocalToControl(max, offset);
                    var lo = Vector2.Min(a, b);
                    var hi = Vector2.Max(a, b);
                    px.ZoneOutline(handle, new[]
                    {
                        lo, new Vector2(hi.X, lo.Y), hi, new Vector2(lo.X, hi.Y),
                    }, texel, color, true);
                }
                break;
        }

        if (!hasMouse)
            return;

        DrawIcon(handle, _system.GetIcon(SecurityCommandSprites.Order(type)), mouse, iconSize, color.WithAlpha(0.85f),
            type == SecurityOrderType.Move);
    }

    private void DrawLegendHint(DrawingHandleScreen handle)
    {
        if (_system.PlacingType is not { } type)
            return;

        var text = Loc.GetString("security-command-map-placing", ("type", SecurityCommandClientSystem.GetTypeName(type)));
        var dims = handle.GetDimensions(_font, text, 1f);
        var pos = new Vector2(PixelSize.X / 2f - dims.X / 2f, PixelSize.Y - dims.Y - 12f);
        var back = new UIBox2(pos - new Vector2(8f, 4f), pos + dims + new Vector2(8f, 4f));
        handle.DrawRect(back, Color.Black.WithAlpha(0.7f));
        handle.DrawRect(new UIBox2(back.Left, back.Top, back.Left + 3f, back.Bottom), _system.Draft.Color);
        DrawText(handle, _font, pos, text, Color.White);
    }

    private void DrawTooltip(DrawingHandleScreen handle, Vector2 pos, string text, Color accent)
    {
        var lines = text.Split('\n');
        var width = 0f;
        var lineHeight = 13f;
        foreach (var line in lines)
        {
            width = MathF.Max(width, handle.GetDimensions(_fontSmall, line, 1f).X);
        }

        var size = new Vector2(width + 16f, lines.Length * lineHeight + 8f);

        if (pos.X + size.X > PixelSize.X)
            pos.X = PixelSize.X - size.X - 2f;
        if (pos.Y + size.Y > PixelSize.Y)
            pos.Y = PixelSize.Y - size.Y - 2f;

        var box = new UIBox2(pos, pos + size);
        handle.DrawRect(box, Color.FromHex("#0d1013").WithAlpha(0.92f));
        handle.DrawRect(box, accent.WithAlpha(0.8f), false);
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Left + 3f, box.Bottom), accent);

        var y = pos.Y + 4f;
        for (var i = 0; i < lines.Length; i++)
        {
            DrawText(handle, _fontSmall, new Vector2(pos.X + 9f, y), lines[i], i == 0 ? Color.White : Color.LightGray);
            y += lineHeight;
        }
    }

    private bool TryGetGrid(out EntityUid grid, out TransformComponent xform)
    {
        grid = EntityUid.Invalid;
        xform = default!;

        if (MapUid is not { } uid || !EntManager.TryGetComponent(uid, out TransformComponent? x))
            return false;

        grid = uid;
        xform = x;
        return true;
    }

    private bool TryGetMouseLocal(out Vector2 local)
    {
        local = _input.MouseScreenPosition.Position - GlobalPixelPosition;
        return local.X >= 0 && local.Y >= 0 && local.X <= PixelSize.X && local.Y <= PixelSize.Y;
    }

    private Vector2 LocalToControl(Vector2 gridLocal, Vector2 offset)
    {
        var rel = gridLocal - offset;
        return ScalePosition(new Vector2(rel.X, -rel.Y));
    }

    private Vector2 ControlToGridLocal(Vector2 control, Vector2 offset)
    {
        var scale = MinimapScale;
        if (scale <= 0f)
            return offset;

        var unscaled = (control - MidPointVector) / scale;
        return new Vector2(unscaled.X, -unscaled.Y) + offset;
    }

    private static void DrawIcon(DrawingHandleScreen handle, Texture texture, Vector2 pos, float size, Color color, bool pin)
    {
        var half = size / 2f;
        var top = pos.Y - (pin ? size : half);
        var box = new UIBox2(pos.X - half, top, pos.X + half, top + size);
        var shadow = box.Translated(new Vector2(1f, 1f));
        handle.DrawTextureRect(texture, shadow, Color.Black.WithAlpha(0.55f * color.A));
        handle.DrawTextureRect(texture, box, color);
    }

    private static void DrawText(DrawingHandleScreen handle, Font font, Vector2 pos, string text, Color color)
    {
        handle.DrawString(font, pos + new Vector2(1f, 1f), text, Color.Black.WithAlpha(0.85f));
        handle.DrawString(font, pos, text, color);
    }

    private static void DrawCentered(DrawingHandleScreen handle, Font font, Vector2 pos, string text, Color color)
    {
        var size = handle.GetDimensions(font, text, 1f);
        DrawText(handle, font, pos - new Vector2(size.X / 2f, 0f), text, color);
    }

    private static string Truncate(string text, int max)
    {
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}

public sealed record SecurityMapContext(
    Vector2 PixelPosition,
    EntityCoordinates? Coordinates,
    SecurityOrderData? Order,
    SecurityOfficerData? Officer);
