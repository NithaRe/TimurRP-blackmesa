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

public sealed class UndergroundConsoleWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    public event Action? StartRequested;
    public event Action<UndergroundMission>? MissionSelected;
    public event Action? ClaimRequested;

    private readonly Label _status;
    private readonly Label _time;
    private readonly RichTextLabel _missionName;
    private readonly Label _missionType;
    private readonly RichTextLabel _missionDescription;
    private readonly RichTextLabel _instructions;
    private readonly Label _progress;
    private readonly Label _reward;
    private readonly TextureRect _targetIcon;
    private readonly ContainerButton _start;
    private readonly Label _startText;
    private readonly ContainerButton _claim;
    private readonly Label _claimText;
    private readonly Dictionary<UndergroundMission, (ContainerButton Button, Label Marker)> _missionCards = new();
    private readonly Texture? _headcrab;
    private readonly Texture? _houndeye;
    private readonly Texture? _disk;
    private readonly Texture? _multitool;
    private readonly Texture? _scannerFrame;
    private readonly Texture? _pulseRifle;

    public UndergroundConsoleWindow()
    {
        IoCManager.InjectDependencies(this);
        Title = Loc.GetString("underground-console-title");
        MinSize = new Vector2(660, 480);
        SetSize = new Vector2(850, 650);
        _headcrab = LoadIcon("_BlackM/Mobs/headcrab.rsi", "idle");
        _houndeye = LoadIcon("_BlackM/Mobs/houndeye.rsi", "walking");
        _disk = LoadIcon("Objects/Specific/Research/researchdisk.rsi", "icon");
        _multitool = LoadIcon("_BlackM/Objects/Specific/Underground/lost_items.rsi", "multitool");
        _scannerFrame = LoadIcon("_BlackM/Objects/Specific/Underground/lost_items.rsi", "scanner_frame");
        _pulseRifle = LoadIcon("_BlackM/Objects/Specific/Underground/lost_items.rsi", "pulse_rifle");

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(10),
            Stylesheet = UndergroundConsoleStylesheet.Create(_resources),
        };
        Contents.AddChild(root);

        var header = Panel("#292929", "#595959", 10);
        root.AddChild(header);
        var headerRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 12 };
        header.AddChild(headerRow);
        var headerText = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 3 };
        headerRow.AddChild(headerText);
        headerText.AddChild(Heading(Loc.GetString("underground-terminal-heading"), UndergroundConsoleStylesheet.Amber));
        headerText.AddChild(Text(Loc.GetString("underground-terminal-subheading"), UndergroundConsoleStylesheet.Muted));
        var headerStatus = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 3 };
        headerRow.AddChild(headerStatus);
        _status = Text("", UndergroundConsoleStylesheet.Blue); headerStatus.AddChild(_status);
        _time = Text("", UndergroundConsoleStylesheet.Ink); headerStatus.AddChild(_time);

        var main = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true, SeparationOverride = 10 };
        root.AddChild(main);
        var listColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SetWidth = 260, VerticalExpand = true, SeparationOverride = 6 };
        main.AddChild(listColumn);
        listColumn.AddChild(Wrapped(Loc.GetString("underground-terminal-contracts"), UndergroundConsoleStylesheet.Muted));
        var listScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        listColumn.AddChild(listScroll);
        var list = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6 };
        listScroll.AddChild(list);
        var contractIcon = LoadIcon("_BlackM/Interface/Misc/underground_contracts.rsi", "document");
        AddCard(list, UndergroundMission.HeadcrabHunt, contractIcon);
        AddCard(list, UndergroundMission.HoundeyeHunt, contractIcon);
        AddCard(list, UndergroundMission.HeadcrabSample, contractIcon);
        AddCard(list, UndergroundMission.HoundeyeSample, contractIcon);
        AddCard(list, UndergroundMission.FastHeadcrabHunt, contractIcon);
        AddCard(list, UndergroundMission.FastHeadcrabSample, contractIcon);
        AddCard(list, UndergroundMission.LostResearchDisk, contractIcon);
        AddCard(list, UndergroundMission.LostMultitool, contractIcon);
        AddCard(list, UndergroundMission.LostScannerFrame, contractIcon);
        AddCard(list, UndergroundMission.LostPulseRifle, contractIcon);

        var details = Panel("#222222", "#494949", 12);
        details.HorizontalExpand = true;
        details.VerticalExpand = true;
        main.AddChild(details);
        var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 8 };
        var detailScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        details.AddChild(detailScroll);
        detailScroll.AddChild(column);
        column.AddChild(Text(Loc.GetString("underground-terminal-selected"), UndergroundConsoleStylesheet.Muted));
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        column.AddChild(top);
        var iconPanel = Panel("#171717", "#424242", 6);
        top.AddChild(iconPanel);
        _targetIcon = Icon(null, 86);
        iconPanel.AddChild(_targetIcon);
        var topText = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 5 };
        top.AddChild(topText);
        _missionType = Text("", UndergroundConsoleStylesheet.Blue); topText.AddChild(_missionType);
        _missionName = Wrapped(); _missionName.StyleClasses.Add("UndergroundHeading"); topText.AddChild(_missionName);
        _missionDescription = Wrapped(); topText.AddChild(_missionDescription);
        column.AddChild(Rule());
        _progress = Text("", UndergroundConsoleStylesheet.Ink); column.AddChild(_progress);
        _instructions = Wrapped(); column.AddChild(_instructions);
        column.AddChild(Rule());

        var rewardRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        column.AddChild(rewardRow);
        rewardRow.AddChild(Icon(LoadIcon("Objects/Economy/cash.rsi", "cash_1000"), 44));
        var rewardText = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalAlignment = VAlignment.Center, SeparationOverride = 3 };
        rewardRow.AddChild(rewardText);
        rewardText.AddChild(Text(Loc.GetString("underground-terminal-team-reward"), UndergroundConsoleStylesheet.Muted));
        _reward = Text("", UndergroundConsoleStylesheet.Amber); rewardText.AddChild(_reward);

        var loot = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        column.AddChild(loot);
        loot.AddChild(Icon(LoadIcon("Objects/Specific/Chemistry/vial.rsi", "vial-1"), 44));
        var lootText = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalAlignment = VAlignment.Center, SeparationOverride = 3 };
        loot.AddChild(lootText);
        lootText.AddChild(Text(Loc.GetString("underground-terminal-loot-label"), UndergroundConsoleStylesheet.Muted));
        lootText.AddChild(Text(Loc.GetString("underground-terminal-loot-value"), UndergroundConsoleStylesheet.Ink));
        column.AddChild(Rule());
        column.AddChild(Text(Loc.GetString("underground-terminal-threats"), UndergroundConsoleStylesheet.Muted));
        var threats = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        column.AddChild(threats);
        threats.AddChild(Threat(_headcrab, "underground-mission-threat-headcrab"));
        threats.AddChild(Threat(_houndeye, "underground-mission-threat-houndeye"));
        threats.AddChild(Threat(LoadIcon("_BlackM/Mobs/vortigaunt.rsi", "walking"), "underground-mission-threat-vortigaunt"));

        var footer = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        root.AddChild(footer);
        _start = ActionButton("PrimaryAction", out var startText);
        _startText = startText;
        _start.OnPressed += _ => StartRequested?.Invoke();
        footer.AddChild(_start);
        _claim = ActionButton("SecondaryAction", out var claimText);
        _claimText = claimText;
        _claimText.Text = Loc.GetString("underground-mission-claim");
        _claim.OnPressed += _ => ClaimRequested?.Invoke();
        footer.AddChild(_claim);
        root.AddChild(Wrapped(Loc.GetString("underground-console-warning"), UndergroundConsoleStylesheet.Muted));

        OnOpen += FitToScreen;
        FitToScreen();
    }

    private void AddCard(BoxContainer list, UndergroundMission mission, Texture? texture)
    {
        var card = new ContainerButton { ToggleMode = true, MinHeight = 72, HorizontalExpand = true };
        card.StyleClasses.Add("ContractCard");
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
        card.AddChild(row);
        row.AddChild(Icon(texture, 46));
        var text = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalAlignment = VAlignment.Center, SeparationOverride = 2 };
        row.AddChild(text);
        text.AddChild(Wrapped(Loc.GetString(NameKey(mission))));
        text.AddChild(Wrapped(Loc.GetString(TypeKey(mission)), UndergroundConsoleStylesheet.Muted));
        var marker = Text("", UndergroundConsoleStylesheet.Amber);
        text.AddChild(marker);
        card.OnPressed += _ => MissionSelected?.Invoke(mission);
        _missionCards.Add(mission, (card, marker));
        list.AddChild(card);
    }

    private static ContainerButton ActionButton(string style, out Label text)
    {
        var button = new ContainerButton { HorizontalExpand = true, MinHeight = 42 };
        button.StyleClasses.Add(style);
        text = Text("", UndergroundConsoleStylesheet.Ink);
        text.HorizontalAlignment = HAlignment.Center;
        text.VerticalAlignment = VAlignment.Center;
        button.AddChild(text);
        return button;
    }

    private static PanelContainer Panel(string background, string border, int padding) => new()
    {
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex(background), BorderColor = Color.FromHex(border),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = padding, ContentMarginRightOverride = padding,
            ContentMarginTopOverride = padding, ContentMarginBottomOverride = padding,
        },
    };

    private static PanelContainer Rule() => new() { SetHeight = 1, PanelOverride = new StyleBoxFlat(Color.FromHex("#454545")) };
    private static Label Text(string text, Color color) => new() { Text = text, FontColorOverride = color };
    private static Label Heading(string text, Color color)
    {
        var label = Text(text, color);
        label.StyleClasses.Add("UndergroundHeading");
        return label;
    }
    private static RichTextLabel Wrapped(string text = "", Color? color = null)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(text, defaultColor: color ?? UndergroundConsoleStylesheet.Ink);
        return label;
    }
    private static TextureRect Icon(Texture? texture, int size) => new()
    {
        Texture = texture, SetSize = new Vector2(size, size), CanShrink = true,
        Stretch = TextureRect.StretchMode.Scale, VerticalAlignment = VAlignment.Center,
    };
    private static Control Threat(Texture? texture, string tooltip)
    {
        var icon = Icon(texture, 40);
        icon.ToolTip = Robust.Shared.Localization.Loc.GetString(tooltip);
        return icon;
    }
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
        MinSize = Vector2.Min(new Vector2(660, 480), available);
        SetSize = Vector2.Min(new Vector2(950, 700), available);
        Measure(available);
    }

    public void UpdateState(UndergroundConsoleState state)
    {
        _status.Text = Loc.GetString(GetStatusKey(state.Phase));
        var seconds = Math.Max(0, state.RemainingSeconds);
        _time.Text = state.Phase is UndergroundConsolePhase.Exploring or UndergroundConsolePhase.Boarding
            ? Loc.GetString("underground-console-time", ("time", $"{seconds / 60:00}:{seconds % 60:00}"))
            : Loc.GetString("underground-terminal-duration", ("time", $"{state.DurationSeconds / 60:00}:{state.DurationSeconds % 60:00}"));
        _startText.Text = Loc.GetString(state.Phase == UndergroundConsolePhase.Ready ? "underground-console-depart" : "underground-console-start");
        _start.Disabled = !state.CanStart || state.CanClaim;
        _claim.Disabled = !state.CanClaim;
        _startText.FontColorOverride = _start.Disabled ? UndergroundConsoleStylesheet.Muted : UndergroundConsoleStylesheet.Ink;
        _claimText.FontColorOverride = _claim.Disabled ? UndergroundConsoleStylesheet.Muted : UndergroundConsoleStylesheet.Ink;
        foreach (var (mission, entry) in _missionCards)
        {
            entry.Button.Disabled = !state.CanSelect;
            entry.Button.Pressed = mission == state.Mission;
            entry.Marker.Text = mission == state.Mission ? Loc.GetString("underground-terminal-active") : "";
        }
        _missionType.Text = Loc.GetString(TypeKey(state.Mission));
        _missionName.SetMessage(Loc.GetString(NameKey(state.Mission)), defaultColor: UndergroundConsoleStylesheet.Amber);
        _missionDescription.SetMessage(Loc.GetString(DescriptionKey(state.Mission)), defaultColor: UndergroundConsoleStylesheet.Ink);
        _instructions.SetMessage(Loc.GetString(UndergroundMissions.IsItem(state.Mission) ? "underground-terminal-item-instructions" : UndergroundMissions.IsSample(state.Mission) ? "underground-terminal-sample-instructions" : "underground-terminal-hunt-instructions"), defaultColor: UndergroundConsoleStylesheet.Muted);
        _progress.Text = Loc.GetString("underground-mission-progress", ("current", state.Progress), ("goal", UndergroundMissions.Goal(state.Mission)));
        _reward.Text = Loc.GetString("underground-terminal-amount", ("amount", UndergroundMissions.RewardAmount(state.Mission)));
        _targetIcon.Texture = state.Mission switch
        {
            UndergroundMission.LostResearchDisk => _disk,
            UndergroundMission.LostMultitool => _multitool,
            UndergroundMission.LostScannerFrame => _scannerFrame,
            UndergroundMission.LostPulseRifle => _pulseRifle,
            _ => UndergroundMissions.IsHoundeye(state.Mission) ? _houndeye : _headcrab,
        };
    }

    private static string TypeKey(UndergroundMission mission) => UndergroundMissions.IsItem(mission) ? "underground-terminal-type-item" : UndergroundMissions.IsSample(mission) ? "underground-terminal-type-sample" : "underground-terminal-type-hunt";

    private static string NameKey(UndergroundMission mission) => mission switch
    {
        UndergroundMission.FastHeadcrabHunt => "underground-mission-fast-hunt",
        UndergroundMission.FastHeadcrabSample => "underground-mission-fast-sample",
        UndergroundMission.LostResearchDisk => "underground-mission-lost-disk",
        UndergroundMission.LostMultitool => "underground-mission-lost-multitool",
        UndergroundMission.LostScannerFrame => "underground-mission-lost-scanner",
        UndergroundMission.LostPulseRifle => "underground-mission-lost-rifle",
        UndergroundMission.HoundeyeHunt => "underground-mission-houndeye-hunt",
        UndergroundMission.HeadcrabSample => "underground-mission-headcrab-sample",
        UndergroundMission.HoundeyeSample => "underground-mission-houndeye-sample",
        _ => "underground-mission-headcrab-hunt",
    };
    private static string DescriptionKey(UndergroundMission mission) => mission switch
    {
        UndergroundMission.FastHeadcrabHunt => "underground-mission-fast-hunt-desc",
        UndergroundMission.FastHeadcrabSample => "underground-mission-fast-sample-desc",
        UndergroundMission.LostResearchDisk => "underground-mission-lost-disk-desc",
        UndergroundMission.LostMultitool => "underground-mission-lost-multitool-desc",
        UndergroundMission.LostScannerFrame => "underground-mission-lost-scanner-desc",
        UndergroundMission.LostPulseRifle => "underground-mission-lost-rifle-desc",
        UndergroundMission.HoundeyeHunt => "underground-mission-houndeye-hunt-desc",
        UndergroundMission.HeadcrabSample => "underground-mission-headcrab-sample-desc",
        UndergroundMission.HoundeyeSample => "underground-mission-houndeye-sample-desc",
        _ => "underground-mission-headcrab-hunt-desc",
    };
    private static string GetStatusKey(UndergroundConsolePhase phase) => phase switch
    {
        UndergroundConsolePhase.Idle => "underground-terminal-phase-idle",
        UndergroundConsolePhase.Boarding => "underground-terminal-phase-boarding",
        UndergroundConsolePhase.Generating => "underground-terminal-phase-generating",
        UndergroundConsolePhase.Ready => "underground-terminal-phase-ready",
        UndergroundConsolePhase.Outbound => "underground-terminal-phase-outbound",
        UndergroundConsolePhase.Exploring => "underground-terminal-phase-exploring",
        UndergroundConsolePhase.Returning => "underground-terminal-phase-returning",
        UndergroundConsolePhase.Cleanup => "underground-terminal-phase-cleanup",
        UndergroundConsolePhase.Faulted => "underground-terminal-phase-faulted",
        _ => "underground-terminal-phase-offline",
    };
}
