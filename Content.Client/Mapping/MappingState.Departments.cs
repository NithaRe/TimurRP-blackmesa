using System.Linq;
using System.Numerics;
using Content.Shared._BlackM.Map;
using Content.Shared.Administration;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Map.Components;

namespace Content.Client.Mapping;

public sealed partial class MappingState
{
    private readonly HashSet<Vector2i> _departmentTiles = new();
    private EntityUid? _departmentGrid;
    private int _departmentTool;
    private bool _departmentDragging;
    private bool _departmentPending;
    private Vector2i? _departmentLast;
    private Vector2i? _departmentRectangleStart;
    private Vector2i? _departmentHover;
    private TimeSpan _departmentTimeout;
    private DepartmentCopyClientSystem _departmentCopy = default!;

    private void SetupDepartmentSelection()
    {
        _departmentCopy = _entityManager.System<DepartmentCopyClientSystem>();
        _departmentCopy.Result += OnDepartmentCopied;
        Screen.DepartmentBrush.Texture.TexturePath = "/Textures/Interface/pencil.png";
        Screen.DepartmentEraser.Texture.TexturePath = "/Textures/Interface/eraser.svg.png";
        Screen.DepartmentRectangle.Texture.TexturePath = "/Textures/Interface/Nano/square.png";
        Screen.DepartmentClear.Texture.TexturePath = "/Textures/Interface/Nano/cross.svg.png";
        Screen.DepartmentBrushSize.IsValid = value => value is >= 1 and <= 15;
        Screen.DepartmentBrushSize.Value = 1;
        Screen.DepartmentBrush.OnPressed += args => SetDepartmentTool(args.Button.Pressed ? 1 : 0);
        Screen.DepartmentEraser.OnPressed += args => SetDepartmentTool(args.Button.Pressed ? 2 : 0);
        Screen.DepartmentRectangle.OnPressed += args => SetDepartmentTool(args.Button.Pressed ? 3 : 0);
        Screen.DepartmentClear.OnPressed += _ => ClearDepartmentSelection();
        Screen.DepartmentCopy.OnPressed += _ => CopyDepartment();
        RefreshDepartmentControls();
    }

    private void ShutdownDepartmentSelection()
    {
        _departmentCopy.Result -= OnDepartmentCopied;
        _departmentTiles.Clear();
        _departmentGrid = null;
        _departmentTool = 0;
        _departmentDragging = false;
        _departmentPending = false;
    }

    private void SetDepartmentTool(int tool)
    {
        _departmentTool = tool;
        _departmentDragging = false;
        _departmentLast = null;
        _departmentRectangleStart = null;
        Screen.DepartmentBrush.Pressed = tool == 1;
        Screen.DepartmentEraser.Pressed = tool == 2;
        Screen.DepartmentRectangle.Pressed = tool == 3;
        if (tool == 0)
            return;

        Deselect();
        _placement.Clear();
        _decal.SetActive(false);
        Screen.Pick.Pressed = false;
        Screen.Delete.Pressed = false;
        Screen.EraseDecalButton.Pressed = false;
        _updateEraseDecal = false;
        State = CursorState.None;
    }

    private void ClearDepartmentSelection()
    {
        _departmentTiles.Clear();
        _departmentGrid = null;
        _departmentLast = null;
        _departmentRectangleStart = null;
        _departmentDragging = false;
        RefreshDepartmentControls();
    }

    private void RefreshDepartmentControls()
    {
        Screen.DepartmentCount.Text = Loc.GetString("department-selection-count", ("count", _departmentTiles.Count));
        Screen.DepartmentCopy.Disabled = _departmentTiles.Count == 0 || _departmentPending || !_admin.HasFlag(AdminFlags.Mapping);
    }

    private void CopyDepartment()
    {
        if (_departmentPending || _departmentGrid is not { } grid || _departmentTiles.Count == 0)
            return;
        SetDepartmentTool(0);
        _departmentPending = true;
        _departmentTimeout = _timing.RealTime + TimeSpan.FromSeconds(60);
        Screen.DepartmentStatus.Text = Loc.GetString("department-copy-working");
        RefreshDepartmentControls();
        _departmentCopy.Copy(grid, _departmentTiles.ToArray());
    }

    private void OnDepartmentCopied(DepartmentCopyResult result)
    {
        _departmentPending = false;
        if (result.Success)
            ClearDepartmentSelection();
        Screen.DepartmentStatus.Text = result.Message;
        Screen.DepartmentStatus.ToolTip = result.Message;
        RefreshDepartmentControls();
    }

    private bool HandleDepartmentInput(ViewportBoundKeyEventArgs args)
    {
        if (_departmentTool == 0)
            return false;
        var key = args.KeyEventArgs;
        if (key.Function == EngineKeyFunctions.EditorCancelPlace ||
            key.Function == ContentKeyFunctions.MappingUnselect ||
            key.Function == ContentKeyFunctions.MappingOpenContextMenu)
        {
            SetDepartmentTool(0);
            key.Handle();
            return true;
        }

        if (key.Function != EngineKeyFunctions.EditorPlaceObject && key.Function != EngineKeyFunctions.Use &&
            key.Function != ContentKeyFunctions.MappingPick && key.Function != ContentKeyFunctions.MappingRemoveDecal)
            return false;

        if (key.State == BoundKeyState.Down)
        {
            if (UserInterfaceManager.CurrentlyHovered is IViewportControl && !_departmentDragging)
            {
                _departmentDragging = true;
                _departmentLast = null;
                UpdateDepartmentSelection();
                _departmentRectangleStart = _departmentHover;
            }
        }
        else
        {
            if (_departmentDragging && _departmentTool == 3 &&
                _departmentRectangleStart is { } start && _departmentHover is { } end)
                PaintDepartmentRectangle(start, end);
            _departmentDragging = false;
            _departmentLast = null;
            _departmentRectangleStart = null;
        }

        key.Handle();
        return true;
    }

    private void UpdateDepartmentSelection()
    {
        if (_departmentPending && _timing.RealTime > _departmentTimeout)
            OnDepartmentCopied(new DepartmentCopyResult(Loc.GetString("department-copy-timeout")));

        _departmentHover = null;
        if (_departmentGrid is { } selected && !_entityManager.EntityExists(selected))
            ClearDepartmentSelection();

        if (_departmentTool == 0 || UserInterfaceManager.CurrentlyHovered is not IViewportControl viewport ||
            _input.MouseScreenPosition is not { IsValid: true } pointer)
        {
            _departmentLast = null;
            return;
        }

        var position = viewport.PixelToMap(pointer.Position);
        EntityUid grid;
        if ((_departmentTiles.Count > 0 || _departmentRectangleStart != null || _departmentLast != null) &&
            _departmentGrid is { } current && _entityManager.TryGetComponent(current, out TransformComponent? xform) &&
            xform.MapID == position.MapId)
        {
            grid = current;
        }
        else
        {
            if (_departmentTiles.Count > 0)
                ClearDepartmentSelection();
            if (!_mapMan.TryFindGridAt(position, out grid, out _))
                return;
            _departmentGrid = grid;
        }

        var cell = Vector2.Transform(position.Position, _transform.GetInvWorldMatrix(grid)).Floored();
        _departmentHover = cell;
        if (!_departmentDragging || _departmentTool == 3)
            return;

        var from = _departmentLast ?? cell;
        var steps = Math.Max(Math.Abs(cell.X - from.X), Math.Abs(cell.Y - from.Y));
        // Interpolate between frames so fast brush strokes do not leave gaps.
        if (steps <= 512)
        {
            for (var i = 0; i <= steps; i++)
            {
                var point = Vector2.Lerp(from, cell, steps == 0 ? 0 : (float) i / steps);
                PaintDepartmentBrush(grid, new Vector2i((int) MathF.Round(point.X), (int) MathF.Round(point.Y)));
            }
        }
        _departmentLast = cell;
        RefreshDepartmentControls();
    }

    private void PaintDepartmentBrush(EntityUid grid, Vector2i cell)
    {
        var size = Math.Clamp(Screen.DepartmentBrushSize.Value, 1, 15);
        for (var x = 0; x < size; x++)
        for (var y = 0; y < size; y++)
            SetDepartmentTile(grid, cell + new Vector2i(x - size / 2, y - size / 2), _departmentTool != 2);
    }

    private void PaintDepartmentRectangle(Vector2i start, Vector2i end)
    {
        if (_departmentGrid is not { } grid)
            return;
        var width = Math.Abs((long) end.X - start.X) + 1;
        var height = Math.Abs((long) end.Y - start.Y) + 1;
        if (width * height > DepartmentCopyRequest.MaxTiles)
        {
            Screen.DepartmentStatus.Text = Loc.GetString("department-selection-limit");
            return;
        }
        for (var x = Math.Min(start.X, end.X); x <= Math.Max(start.X, end.X); x++)
        for (var y = Math.Min(start.Y, end.Y); y <= Math.Max(start.Y, end.Y); y++)
            SetDepartmentTile(grid, new Vector2i(x, y), true);
        RefreshDepartmentControls();
    }

    private void SetDepartmentTile(EntityUid grid, Vector2i cell, bool add)
    {
        if (!add)
        {
            _departmentTiles.Remove(cell);
            return;
        }
        if (_departmentTiles.Count >= DepartmentCopyRequest.MaxTiles)
        {
            Screen.DepartmentStatus.Text = Loc.GetString("department-selection-limit");
            return;
        }
        if (_entityManager.TryGetComponent(grid, out MapGridComponent? comp) &&
            !_entityManager.System<SharedMapSystem>().GetTileRef(grid, comp, cell).Tile.IsEmpty)
            _departmentTiles.Add(cell);
    }

    public void DrawDepartmentSelection(in OverlayDrawArgs args)
    {
        if (_departmentGrid is not { } grid || !_entityManager.TryGetComponent(grid, out TransformComponent? xform) ||
            xform.MapID != args.MapId)
            return;
        var handle = args.WorldHandle;
        handle.SetTransform(_transform.GetWorldMatrix(grid));
        var visible = _transform.GetInvWorldMatrix(grid).TransformBox(args.WorldBounds);
        foreach (var cell in _departmentTiles)
        {
            var tile = new Box2(cell, cell + Vector2i.One);
            if (visible.Intersects(tile))
                handle.DrawRect(tile, Color.Cyan.WithAlpha(0.25f));
        }
        if (_departmentTool != 0 && _departmentHover is { } hover)
        {
            var size = Math.Clamp(Screen.DepartmentBrushSize.Value, 1, 15);
            var minimum = hover - new Vector2i(size / 2, size / 2);
            var box = new Box2(minimum, minimum + new Vector2i(size, size));
            if (_departmentTool == 3)
            {
                var start = _departmentRectangleStart ?? hover;
                box = new Box2(new Vector2(Math.Min(start.X, hover.X), Math.Min(start.Y, hover.Y)),
                    new Vector2(Math.Max(start.X, hover.X) + 1, Math.Max(start.Y, hover.Y) + 1));
            }
            handle.DrawRect(box, _departmentTool == 2 ? Color.Red : Color.White, filled: false);
        }
        handle.SetTransform(Matrix3x2.Identity);
    }
}
