using System.Numerics;
using Content.Shared._BlackM.Cargo.Delivery;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Cargo.Delivery;

public sealed class CargoDeliveryTerminalWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public event Action<NetEntity>? OnSend;
    public event Action? OnPrintCrate;
    public event Action<NetEntity>? OnRecall;

    private static readonly Color PanelColor = Color.FromHex("#1B1E24");
    private static readonly Color CardColor = Color.FromHex("#252A33");
    private static readonly Color CardSelected = Color.FromHex("#33405A");
    private static readonly Color Muted = Color.FromHex("#8C93A0");
    private static readonly Color Good = Color.FromHex("#6FCF6F");
    private static readonly Color Bad = Color.FromHex("#E86A5F");
    private static readonly Color Warn = Color.FromHex("#E8C25F");

    private readonly BoxContainer _pointList;
    private readonly BoxContainer _droneList;

    private readonly EntityPrototypeView _crateView;
    private readonly Label _padLabel;
    private readonly GridContainer _contentsGrid;
    private readonly Label _contentsEmpty;

    private readonly EntityPrototypeView _routeFrom;
    private readonly EntityPrototypeView _routeTo;
    private readonly Label _routeLabel;

    private readonly Button _sendButton;
    private readonly Button _printButton;

    private CargoDeliveryTerminalState? _state;
    private NetEntity? _selected;

    public CargoDeliveryTerminalWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cargo-delivery-terminal-title");
        MinSize = new Vector2(760, 680);
        SetSize = new Vector2(760, 700);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        Contents.AddChild(root);

        var middle = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(middle);

        var pointsPanel = Panel(PanelColor);
        pointsPanel.HorizontalExpand = true;
        pointsPanel.SizeFlagsStretchRatio = 1.1f;
        middle.AddChild(pointsPanel);

        var pointsBox = VBox(6);
        pointsPanel.AddChild(pointsBox);
        pointsBox.AddChild(Header("cargo-delivery-ui-points"));

        var pointScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _pointList = VBox(6);
        _pointList.HorizontalExpand = true;
        pointScroll.AddChild(_pointList);
        pointsBox.AddChild(pointScroll);

        var previewPanel = Panel(PanelColor);
        previewPanel.HorizontalExpand = true;
        middle.AddChild(previewPanel);

        var previewBox = VBox(6);
        previewPanel.AddChild(previewBox);

        var crateRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        _crateView = new EntityPrototypeView
        {
            SetSize = new Vector2(64, 64),
            Scale = new Vector2(2, 2),
            Stretch = SpriteView.StretchMode.Fill,
        };
        crateRow.AddChild(_crateView);
        _padLabel = new Label { VerticalAlignment = VAlignment.Center, HorizontalExpand = true, ClipText = true };
        crateRow.AddChild(_padLabel);
        previewBox.AddChild(crateRow);

        previewBox.AddChild(new Label { Text = Loc.GetString("cargo-delivery-ui-contents"), FontColorOverride = Muted });
        var contentsScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, MinSize = new Vector2(0, 110) };
        var contentsBox = VBox(2);
        _contentsGrid = new GridContainer { Columns = 6, HSeparationOverride = 4, VSeparationOverride = 4 };
        _contentsEmpty = new Label { Text = Loc.GetString("cargo-delivery-ui-contents-empty"), FontColorOverride = Muted };
        contentsBox.AddChild(_contentsGrid);
        contentsBox.AddChild(_contentsEmpty);
        contentsScroll.AddChild(contentsBox);
        previewBox.AddChild(contentsScroll);

        previewBox.AddChild(new Label { Text = Loc.GetString("cargo-delivery-ui-route"), FontColorOverride = Muted });
        var routeRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _routeFrom = new EntityPrototypeView { SetSize = new Vector2(32, 32), Stretch = SpriteView.StretchMode.Fill };
        _routeTo = new EntityPrototypeView { SetSize = new Vector2(32, 32), Stretch = SpriteView.StretchMode.Fill };
        routeRow.AddChild(_routeFrom);
        routeRow.AddChild(new Label { Text = "→", VerticalAlignment = VAlignment.Center, FontColorOverride = Muted });
        routeRow.AddChild(_routeTo);
        _routeLabel = new Label { VerticalAlignment = VAlignment.Center, HorizontalExpand = true, ClipText = true };
        routeRow.AddChild(_routeLabel);
        previewBox.AddChild(routeRow);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        _sendButton = new Button { Text = Loc.GetString("cargo-delivery-terminal-send"), HorizontalExpand = true, MinHeight = 32 };
        _sendButton.OnPressed += _ =>
        {
            if (_selected is { } point)
                OnSend?.Invoke(point);
        };
        _printButton = new Button { HorizontalExpand = true, MinHeight = 32 };
        _printButton.OnPressed += _ => OnPrintCrate?.Invoke();
        buttons.AddChild(_sendButton);
        buttons.AddChild(_printButton);
        previewBox.AddChild(buttons);

        var fleetPanel = Panel(PanelColor);
        fleetPanel.MinHeight = 190;
        root.AddChild(fleetPanel);
        var fleetBox = VBox(4);
        fleetPanel.AddChild(fleetBox);
        fleetBox.AddChild(Header("cargo-delivery-ui-fleet"));

        var fleetScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, MinSize = new Vector2(0, 150) };
        _droneList = VBox(4);
        _droneList.HorizontalExpand = true;
        fleetScroll.AddChild(_droneList);
        fleetBox.AddChild(fleetScroll);
    }

    public void UpdateState(CargoDeliveryTerminalState state)
    {
        _state = state;

        if (_selected is { } sel && state.Points.Find(p => p.Point == sel) == null)
            _selected = null;

        RebuildPoints();
        RebuildPreview();
        RebuildFleet();
    }

    #region Points

    private void RebuildPoints()
    {
        _pointList.RemoveAllChildren();
        if (_state == null)
            return;

        if (_state.Points.Count == 0)
        {
            _pointList.AddChild(new Label { Text = Loc.GetString("cargo-delivery-ui-no-points"), FontColorOverride = Muted });
            return;
        }

        foreach (var point in _state.Points)
            _pointList.AddChild(PointCard(point));
    }

    private Control PointCard(CargoDeliveryPointEntry point)
    {
        var selected = _selected == point.Point;
        var button = new ContainerButton { HorizontalExpand = true };
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = selected ? CardSelected : CardColor,
                BorderColor = point.Color.WithAlpha(selected ? 1f : 0.45f),
                BorderThickness = new Thickness(4, 1, 1, 1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
        };
        button.AddChild(panel);

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        panel.AddChild(row);

        var icon = new EntityPrototypeView
        {
            SetSize = new Vector2(48, 48),
            Scale = new Vector2(1.5f, 1.5f),
            Stretch = SpriteView.StretchMode.Fill,
        };
        if (_proto.HasIndex<EntityPrototype>(point.ProtoId))
            icon.SetPrototype(point.ProtoId);
        row.AddChild(icon);

        var text = VBox(0);
        text.HorizontalExpand = true;
        text.AddChild(new Label { Text = point.DepartmentName, FontColorOverride = point.Color });
        var location = new RichTextLabel { RectClipContent = true };
        location.SetMessage(FormattedMessage.FromMarkupPermissive(point.Location), defaultColor: Muted);
        text.AddChild(location);

        string status;
        Color statusColor;
        if (!point.Available)
        {
            status = Loc.GetString("cargo-delivery-ui-point-unavailable");
            statusColor = Bad;
        }
        else if (point.CooldownSeconds > 0)
        {
            status = Loc.GetString("cargo-delivery-ui-point-cooldown", ("seconds", point.CooldownSeconds));
            statusColor = Warn;
        }
        else
        {
            status = Loc.GetString("cargo-delivery-ui-point-ready", ("distance", point.Distance));
            statusColor = Good;
        }

        if (point.Incoming > 0)
            status += "  •  " + Loc.GetString("cargo-delivery-ui-point-incoming", ("count", point.Incoming));

        text.AddChild(new Label { Text = status, FontColorOverride = statusColor });
        row.AddChild(text);

        button.Disabled = !point.Available;
        button.OnPressed += _ =>
        {
            _selected = point.Point;
            RebuildPoints();
            RebuildPreview();
        };

        return button;
    }

    #endregion

    #region Preview

    private void RebuildPreview()
    {
        if (_state == null)
            return;

        var crateProto = _state.CrateProto;
        var hasCrate = crateProto != null && _proto.HasIndex<EntityPrototype>(crateProto);
        _crateView.Visible = hasCrate;
        _routeFrom.Visible = hasCrate;
        if (hasCrate)
        {
            _crateView.SetPrototype(crateProto);
            _routeFrom.SetPrototype(crateProto);
        }

        var ready = _state.PadStatus == CargoDeliveryPadStatus.Ready;
        _padLabel.Text = Loc.GetString(GetPadStatusLoc(_state.PadStatus));
        _padLabel.FontColorOverride = ready ? Good : Warn;

        _contentsGrid.RemoveAllChildren();
        foreach (var item in _state.Contents)
            _contentsGrid.AddChild(ItemSlot(item));
        _contentsEmpty.Visible = _state.Contents.Count == 0;

        var point = _selected is { } sel ? _state.Points.Find(p => p.Point == sel) : null;
        _routeTo.Visible = point != null && _proto.HasIndex<EntityPrototype>(point.ProtoId);
        if (point != null && _routeTo.Visible)
            _routeTo.SetPrototype(point.ProtoId);

        if (point == null)
        {
            _routeLabel.Text = Loc.GetString("cargo-delivery-ui-route-select");
            _routeLabel.FontColorOverride = Muted;
        }
        else
        {
            _routeLabel.Text = Loc.GetString("cargo-delivery-ui-route-value",
                ("point", point.DepartmentName), ("distance", point.Distance));
            _routeLabel.FontColorOverride = point.Color;
        }

        _sendButton.Disabled = point == null || !point.Available || point.CooldownSeconds > 0 || !ready || _state.IdleDrones == 0;

        _printButton.Text = _state.CratePrintCooldown > 0
            ? Loc.GetString("cargo-delivery-terminal-print-cooldown", ("seconds", _state.CratePrintCooldown))
            : Loc.GetString("cargo-delivery-terminal-print", ("price", _state.CratePrice));
        _printButton.Disabled = _state.CratePrintCooldown > 0 || _state.PadStatus != CargoDeliveryPadStatus.NoCrate;
    }

    private Control ItemSlot(CargoDeliveryContentEntry item)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CardColor },
            ToolTip = item.Count > 1 ? $"{item.Name} ×{item.Count}" : item.Name,
            MinSize = new Vector2(40, 40),
        };

        if (_proto.HasIndex<EntityPrototype>(item.ProtoId))
        {
            var view = new EntityPrototypeView { SetSize = new Vector2(32, 32), Stretch = SpriteView.StretchMode.Fill };
            view.SetPrototype(item.ProtoId);
            panel.AddChild(view);
        }

        if (item.Count > 1)
        {
            panel.AddChild(new Label
            {
                Text = item.Count.ToString(),
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Bottom,
                FontColorOverride = Color.White,
            });
        }

        return panel;
    }

    #endregion

    #region Fleet

    private void RebuildFleet()
    {
        _droneList.RemoveAllChildren();
        if (_state == null)
            return;

        if (_state.Drones.Count == 0)
        {
            _droneList.AddChild(new Label { Text = Loc.GetString("cargo-delivery-terminal-no-drones"), FontColorOverride = Muted });
            return;
        }

        foreach (var drone in _state.Drones)
            _droneList.AddChild(DroneRow(drone));
    }

    private Control DroneRow(CargoDeliveryDroneEntry drone)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CardColor,
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        panel.AddChild(row);

        if (_entMan.TryGetEntity(drone.Drone, out var uid) && _entMan.EntityExists(uid))
        {
            var view = new SpriteView { SetSize = new Vector2(40, 40), Stretch = SpriteView.StretchMode.Fill, OverrideDirection = Direction.South };
            view.SetEntity(uid);
            row.AddChild(view);
        }
        else if (_proto.HasIndex<EntityPrototype>(drone.ProtoId))
        {
            var view = new EntityPrototypeView { SetSize = new Vector2(40, 40), Stretch = SpriteView.StretchMode.Fill };
            view.SetPrototype(drone.ProtoId);
            row.AddChild(view);
        }

        var text = VBox(0);
        text.HorizontalExpand = true;
        text.AddChild(new Label { Text = drone.Name, ClipText = true });
        text.AddChild(new Label { Text = DroneStatus(drone), FontColorOverride = DroneColor(drone.State), ClipText = true });
        row.AddChild(text);

        var integrity = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = drone.Integrity,
            MinSize = new Vector2(90, 12),
            VerticalAlignment = VAlignment.Center,
            ToolTip = Loc.GetString("cargo-delivery-ui-integrity", ("value", drone.Integrity)),
        };
        integrity.ForegroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = drone.Integrity > 60 ? Good : drone.Integrity > 25 ? Warn : Bad,
        };
        row.AddChild(integrity);

        if (drone.Own && drone.State is CargoDeliveryDroneState.ToPad or CargoDeliveryDroneState.Loading or CargoDeliveryDroneState.Delivering)
        {
            var recall = new Button { Text = Loc.GetString("cargo-delivery-terminal-recall"), VerticalAlignment = VAlignment.Center };
            recall.OnPressed += _ => OnRecall?.Invoke(drone.Drone);
            row.AddChild(recall);
        }

        return panel;
    }

    private static string DroneStatus(CargoDeliveryDroneEntry drone)
    {
        var dist = drone.Distance >= 0 ? $" • {drone.Distance} м" : string.Empty;
        return drone.State switch
        {
            CargoDeliveryDroneState.Idle => Loc.GetString("cargo-delivery-drone-state-idle"),
            CargoDeliveryDroneState.ToPad => Loc.GetString("cargo-delivery-drone-state-to-pad", ("point", drone.Destination)) + dist,
            CargoDeliveryDroneState.Loading => Loc.GetString("cargo-delivery-drone-state-loading", ("point", drone.Destination)),
            CargoDeliveryDroneState.Delivering => Loc.GetString("cargo-delivery-drone-state-delivering", ("point", drone.Destination)) + dist,
            CargoDeliveryDroneState.Downed => Loc.GetString("cargo-delivery-drone-state-downed", ("seconds", drone.Seconds)),
            CargoDeliveryDroneState.Returning => Loc.GetString("cargo-delivery-drone-state-returning") + dist,
            _ => string.Empty,
        };
    }

    private static Color DroneColor(CargoDeliveryDroneState state)
    {
        return state switch
        {
            CargoDeliveryDroneState.Idle => Good,
            CargoDeliveryDroneState.Downed => Bad,
            CargoDeliveryDroneState.Returning => Muted,
            _ => Warn,
        };
    }

    #endregion

    #region Helpers

    private static BoxContainer VBox(int sep)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = sep };
    }

    private static PanelContainer Panel(Color color)
    {
        return new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = color,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            },
        };
    }

    private static Label Header(string loc)
    {
        return new Label { Text = Loc.GetString(loc), StyleClasses = { "LabelHeading" } };
    }

    private static string GetPadStatusLoc(CargoDeliveryPadStatus status)
    {
        return status switch
        {
            CargoDeliveryPadStatus.NoPad => "cargo-delivery-pad-status-no-pad",
            CargoDeliveryPadStatus.NoCrate => "cargo-delivery-pad-status-no-crate",
            CargoDeliveryPadStatus.CrateOpen => "cargo-delivery-pad-status-open",
            CargoDeliveryPadStatus.CrateEmpty => "cargo-delivery-pad-status-empty",
            CargoDeliveryPadStatus.CrateHasMob => "cargo-delivery-pad-status-mob",
            CargoDeliveryPadStatus.CrateHasForbidden => "cargo-delivery-pad-status-forbidden",
            CargoDeliveryPadStatus.CrateReserved => "cargo-delivery-pad-status-reserved",
            _ => "cargo-delivery-pad-status-ready",
        };
    }

    #endregion
}
