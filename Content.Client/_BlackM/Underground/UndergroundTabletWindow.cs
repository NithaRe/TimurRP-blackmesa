using System.Numerics;
using Content.Shared._BlackM.Underground;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Underground;

public sealed class UndergroundTabletWindow : DefaultWindow
{
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    public event Action? ReturnRequested;
    private readonly PanelContainer _targetPanel;
    private readonly TextureRect _targetIcon;
    private readonly Texture? _headcrab;
    private readonly Texture? _houndeye;
    private readonly Texture? _disk;
    private readonly Texture? _multitool;
    private readonly Texture? _scannerFrame;
    private readonly Texture? _pulseRifle;
    private readonly Label _status;
    private readonly Label _time;
    private readonly RichTextLabel _mission;
    private readonly RichTextLabel _description;
    private readonly Label _progress;
    private readonly RichTextLabel _locator;
    private readonly ContainerButton _return;
    private readonly Label _returnText;

    public UndergroundTabletWindow()
    {
        IoCManager.InjectDependencies(this);
        _headcrab = LoadIcon("_BlackM/Mobs/headcrab.rsi", "idle");
        _houndeye = LoadIcon("_BlackM/Mobs/houndeye.rsi", "walking");
        _disk = LoadIcon("Objects/Specific/Research/researchdisk.rsi", "icon");
        _multitool = LoadIcon("_BlackM/Objects/Specific/Underground/lost_items.rsi", "multitool");
        _scannerFrame = LoadIcon("_BlackM/Objects/Specific/Underground/lost_items.rsi", "scanner_frame");
        _pulseRifle = LoadIcon("_BlackM/Objects/Specific/Underground/lost_items.rsi", "pulse_rifle");
        Title = Loc.GetString("underground-tablet-title");
        MinSize = new Vector2(340, 320);
        MaxSize = new Vector2(520, 620);
        SetSize = new Vector2(460, 480);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
            Stylesheet = UndergroundConsoleStylesheet.Create(_resources),
        };
        Contents.AddChild(root);

        var header = Panel("#292929", "#595959", 10);
        root.AddChild(header);
        var headerContents = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        header.AddChild(headerContents);
        var heading = Text(Loc.GetString("underground-tablet-heading"), UndergroundConsoleStylesheet.Amber);
        heading.StyleClasses.Add("UndergroundHeading");
        headerContents.AddChild(heading);
        var statusRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 12,
        };
        headerContents.AddChild(statusRow);
        _status = Text("", UndergroundConsoleStylesheet.Blue);
        _status.HorizontalExpand = true;
        statusRow.AddChild(_status);
        _time = Text("", UndergroundConsoleStylesheet.Ink);
        statusRow.AddChild(_time);

        // The scroll container constrains text width and keeps the footer reachable
        // on small screens instead of making the window grow with its contents.
        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        root.AddChild(scroll);
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };
        scroll.AddChild(column);
        var details = Panel("#222222", "#494949", 12);
        column.AddChild(details);
        var detailColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };
        details.AddChild(detailColumn);
        detailColumn.AddChild(Text(Loc.GetString("underground-terminal-selected"), UndergroundConsoleStylesheet.Muted));
        var targetRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 10,
        };
        detailColumn.AddChild(targetRow);
        _targetPanel = Panel("#171717", "#424242", 5);
        _targetPanel.Visible = false;
        targetRow.AddChild(_targetPanel);
        _targetIcon = new TextureRect
        {
            SetSize = new Vector2(64, 64),
            CanShrink = true,
            Stretch = TextureRect.StretchMode.Scale,
            VerticalAlignment = VAlignment.Center,
        };
        _targetPanel.AddChild(_targetIcon);
        _mission = Wrapped();
        _mission.StyleClasses.Add("UndergroundHeading");
        _mission.VerticalAlignment = VAlignment.Center;
        targetRow.AddChild(_mission);
        _description = Wrapped();
        detailColumn.AddChild(_description);
        _progress = Text("", UndergroundConsoleStylesheet.Ink);
        detailColumn.AddChild(_progress);
        _locator = Wrapped();
        _locator.SetMessage(Loc.GetString("underground-tablet-locator"), defaultColor: UndergroundConsoleStylesheet.Muted);
        detailColumn.AddChild(_locator);
        var hint = Wrapped();
        hint.SetMessage(Loc.GetString("underground-tablet-warning"), defaultColor: UndergroundConsoleStylesheet.Muted);
        column.AddChild(hint);

        _return = new ContainerButton { HorizontalExpand = true, MinHeight = 42 };
        _return.StyleClasses.Add("PrimaryAction");
        _returnText = Text(Loc.GetString("underground-tablet-return"), UndergroundConsoleStylesheet.Ink);
        _returnText.HorizontalAlignment = HAlignment.Center;
        _returnText.VerticalAlignment = VAlignment.Center;
        _return.AddChild(_returnText);
        _return.OnPressed += _ => ReturnRequested?.Invoke();
        root.AddChild(_return);

        OnOpen += FitToScreen;
        FitToScreen();
    }

    private static PanelContainer Panel(string background, string border, int padding) => new()
    {
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex(background),
            BorderColor = Color.FromHex(border),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = padding,
            ContentMarginRightOverride = padding,
            ContentMarginTopOverride = padding,
            ContentMarginBottomOverride = padding,
        },
    };

    private static Label Text(string value, Color color) => new() { Text = value, FontColorOverride = color };
    private static RichTextLabel Wrapped() => new() { HorizontalExpand = true, MaxWidth = 450 };

    private Texture? LoadIcon(string path, string state)
    {
        try { return _entities.System<SpriteSystem>().Frame0(new SpriteSpecifier.Rsi(new ResPath(path), state)); }
        catch { return null; }
    }

    private void FitToScreen()
    {
        var available = UserInterfaceManager.WindowRoot.Size - new Vector2(24, 24);
        if (available.X <= 0 || available.Y <= 0)
            return;
        MinSize = Vector2.Min(new Vector2(340, 320), available);
        MaxSize = Vector2.Min(new Vector2(520, 620), available);
        SetSize = Vector2.Min(new Vector2(460, 480), available);
        Measure(available);
    }

    public void UpdateState(UndergroundConsoleState state)
    {
        _status.Text = Loc.GetString(UndergroundConsoleWindow.GetStatusKey(state.Phase));
        var seconds = Math.Max(0, state.RemainingSeconds);
        _time.Text = state.Phase == UndergroundConsolePhase.Exploring
            ? Loc.GetString("underground-console-time", ("time", $"{seconds / 60:00}:{seconds % 60:00}"))
            : Loc.GetString("underground-console-time-none");
        if (state.Phase == UndergroundConsolePhase.Offline)
        {
            _targetIcon.Texture = null;
            _targetPanel.Visible = false;
            _mission.SetMessage(Loc.GetString("underground-tablet-no-contract"), defaultColor: UndergroundConsoleStylesheet.Muted);
            _description.SetMessage("");
            _progress.Text = "";
            _locator.Visible = false;
            _return.Disabled = true;
            _returnText.FontColorOverride = UndergroundConsoleStylesheet.Muted;
            return;
        }
        _targetIcon.Texture = state.Mission switch
        {
            UndergroundMission.LostResearchDisk => _disk,
            UndergroundMission.LostMultitool => _multitool,
            UndergroundMission.LostScannerFrame => _scannerFrame,
            UndergroundMission.LostPulseRifle => _pulseRifle,
            _ => UndergroundMissions.IsHoundeye(state.Mission) ? _houndeye : _headcrab,
        };
        _targetPanel.Visible = _targetIcon.Texture != null;
        _mission.SetMessage(Loc.GetString(UndergroundConsoleWindow.NameKey(state.Mission)), defaultColor: UndergroundConsoleStylesheet.Amber);
        _description.SetMessage(Loc.GetString(UndergroundConsoleWindow.DescriptionKey(state.Mission)), defaultColor: UndergroundConsoleStylesheet.Ink);
        _progress.Text = Loc.GetString("underground-mission-progress",
            ("current", state.Progress), ("goal", UndergroundMissions.Goal(state.Mission)));
        _locator.Visible = UndergroundMissions.IsDelivery(state.Mission) && state.Phase == UndergroundConsolePhase.Exploring;
        _return.Disabled = !state.CanReturn;
        _returnText.FontColorOverride = _return.Disabled ? UndergroundConsoleStylesheet.Muted : UndergroundConsoleStylesheet.Ink;
    }
}
