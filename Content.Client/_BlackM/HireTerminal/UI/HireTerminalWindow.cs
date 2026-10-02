using System.Numerics;
using Content.Shared._BlackM.HireTerminal;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.HireTerminal.UI;

public sealed class HireTerminalWindow : DefaultWindow
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private static readonly Color BgDark     = Color.FromHex("#1B1B1E");
    private static readonly Color BgPanel    = Color.FromHex("#242427");
    private static readonly Color BgCard     = Color.FromHex("#2C2C30");
    private static readonly Color Border     = Color.FromHex("#3E3E44");
    private static readonly Color Accent     = Color.FromHex("#E2E2E6");
    private static readonly Color TextMain   = Color.FromHex("#D6D6DA");
    private static readonly Color TextMuted  = Color.FromHex("#8C8C94");
    private static readonly Color Good       = Color.FromHex("#6FAF7A");
    private static readonly Color GoodBg     = Color.FromHex("#27342B");
    private static readonly Color Warn       = Color.FromHex("#C9A646");
    private static readonly Color WarnBg     = Color.FromHex("#37321F");
    private static readonly Color Bad        = Color.FromHex("#C46A5E");
    private static readonly Color BadBg      = Color.FromHex("#382826");
    private static readonly Color BtnNormal  = Color.FromHex("#34343A");

    public event Action<string, string>? OnSubmit;
    public event Action? OnClaim;
    public event Action? OnEject;
    public event Action<int>? OnApprove;
    public event Action<int>? OnReject;

    private readonly PanelContainer _modeBanner = new();
    private readonly Label _modeLabel = new();

    private readonly Label _holderName = new();
    private readonly Label _holderJob = new();
    private readonly Label _holderNumber = new();
    private readonly Label _holderExtra = new();
    private readonly RichTextLabel _noPassportHint = new();
    private readonly BoxContainer _holderInfo = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly Button _ejectButton = new();

    private readonly BoxContainer _formBox = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, SeparationOverride = 6 };
    private readonly BoxContainer _positionsBox = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6 };
    private readonly RichTextLabel _selectedLabel = new() { HorizontalExpand = true };
    private readonly LineEdit _comment = new();
    private readonly Button _submitButton = new();

    private readonly BoxContainer _statusBox = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true, SeparationOverride = 8 };
    private readonly PanelContainer _statusPanel = new();
    private readonly Label _statusTitle = new();
    private readonly RichTextLabel _statusText = new() { HorizontalExpand = true };
    private readonly Button _claimButton = new();

    private readonly BoxContainer _queueBox = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _queueTitle = new();
    private readonly BoxContainer _queueList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };

    private readonly BoxContainer _logList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };

    private readonly Dictionary<string, Button> _positionButtons = new();
    private readonly Dictionary<string, Color> _positionColors = new();
    private string? _selectedId;
    private string _positionsSig = string.Empty;
    private string _queueSig = string.Empty;
    private string _logSig = string.Empty;
    private HireTerminalMode _mode;

    public HireTerminalWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("hire-terminal-title");
        MinSize = new Vector2(940, 660);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8, Margin = new Thickness(4) };
        Contents.AddChild(root);

        root.AddChild(BuildLogColumn());
        root.AddChild(BuildMainColumn());
    }


    private Control BuildLogColumn()
    {
        var panel = new PanelContainer
        {
            PanelOverride = Box(BgDark, Border, new Thickness(1)),
            MinWidth = 250,
            MaxWidth = 250,
            VerticalExpand = true,
        };

        var col = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 6 };
        panel.AddChild(col);

        col.AddChild(Text(Loc.GetString("hire-ui-log-title"), Accent, "LabelHeading"));
        col.AddChild(Wrap(Loc.GetString("hire-ui-log-subtitle"), TextMuted));
        col.AddChild(new PanelContainer { PanelOverride = Box(Border), MinHeight = 1 });

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_logList);
        col.AddChild(scroll);

        return panel;
    }

    private Control BuildMainColumn()
    {
        var col = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 8 };

        _modeBanner.AddChild(_modeLabel);
        _modeLabel.Margin = new Thickness(10, 6);
        col.AddChild(_modeBanner);

        var holderPanel = new PanelContainer { PanelOverride = Box(BgPanel, Border, new Thickness(1)) };
        var holderRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(10, 8), SeparationOverride = 10 };
        holderPanel.AddChild(holderRow);

        _holderName.FontColorOverride = TextMain;
        _holderName.StyleClasses.Add("LabelHeading");
        _holderJob.FontColorOverride = TextMuted;
        _holderNumber.FontColorOverride = TextMuted;
        _holderNumber.StyleClasses.Add("LabelSubText");
        _holderExtra.FontColorOverride = Warn;

        _holderInfo.AddChild(Text(Loc.GetString("hire-ui-holder-caption"), TextMuted, "LabelSubText"));
        _holderInfo.AddChild(_holderName);
        _holderInfo.AddChild(_holderJob);
        _holderInfo.AddChild(_holderExtra);
        _holderInfo.AddChild(_holderNumber);

        SetRich(_noPassportHint, Loc.GetString("hire-ui-no-passport-hint"), TextMuted);
        _noPassportHint.HorizontalExpand = true;
        _noPassportHint.VerticalAlignment = Control.VAlignment.Center;

        _ejectButton.Text = Loc.GetString("hire-ui-eject");
        _ejectButton.MinWidth = 140;
        _ejectButton.VerticalAlignment = Control.VAlignment.Center;
        _ejectButton.OnPressed += _ => OnEject?.Invoke();

        holderRow.AddChild(_holderInfo);
        holderRow.AddChild(_noPassportHint);
        holderRow.AddChild(_ejectButton);
        col.AddChild(holderPanel);

        _formBox.AddChild(Text(Loc.GetString("hire-ui-step-position"), Accent, "LabelHeading"));

        var posScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, MinHeight = 150 };
        var posPanel = new PanelContainer { PanelOverride = Box(BgDark, Border, new Thickness(1)), VerticalExpand = true };
        _positionsBox.Margin = new Thickness(8);
        posScroll.AddChild(_positionsBox);
        posPanel.AddChild(posScroll);
        _formBox.AddChild(posPanel);

        _formBox.AddChild(_selectedLabel);

        _formBox.AddChild(Text(Loc.GetString("hire-ui-step-comment"), Accent, "LabelHeading"));
        _comment.PlaceHolder = Loc.GetString("hire-ui-comment-placeholder");
        _comment.HorizontalExpand = true;
        _comment.IsValid = s => s.Length <= 120;
        _formBox.AddChild(_comment);

        _submitButton.MinHeight = 34;
        _submitButton.HorizontalExpand = true;
        _submitButton.OnPressed += _ =>
        {
            if (_selectedId != null)
                OnSubmit?.Invoke(_selectedId, _comment.Text);
        };
        _formBox.AddChild(_submitButton);
        col.AddChild(_formBox);

        _statusPanel.PanelOverride = Box(BgCard, Border, new Thickness(1));
        var statusCol = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(12), SeparationOverride = 6 };
        _statusPanel.AddChild(statusCol);
        _statusTitle.StyleClasses.Add("LabelHeading");
        statusCol.AddChild(_statusTitle);
        statusCol.AddChild(_statusText);

        _claimButton.Text = Loc.GetString("hire-ui-claim");
        _claimButton.MinHeight = 38;
        _claimButton.StyleBoxOverride = Box(GoodBg, Good, new Thickness(1), 8, 4);
        _claimButton.Label.FontColorOverride = Color.White;
        _claimButton.OnPressed += _ => OnClaim?.Invoke();
        statusCol.AddChild(_claimButton);

        _statusBox.AddChild(_statusPanel);
        col.AddChild(_statusBox);

        _queueTitle.StyleClasses.Add("LabelHeading");
        _queueTitle.FontColorOverride = Warn;
        _queueBox.AddChild(_queueTitle);

        var queueScroll = new ScrollContainer { MinHeight = 130, MaxHeight = 130, HScrollEnabled = false };
        var queuePanel = new PanelContainer { PanelOverride = Box(BgDark, Border, new Thickness(1)) };
        _queueList.Margin = new Thickness(6);
        queueScroll.AddChild(_queueList);
        queuePanel.AddChild(queueScroll);
        _queueBox.AddChild(queuePanel);
        col.AddChild(_queueBox);

        return col;
    }


    public void UpdateState(HireTerminalBoundUserInterfaceState state)
    {
        _mode = state.Mode;

        UpdateBanner(state);
        UpdateHolder(state);
        RebuildPositionsIfChanged(state);
        UpdateActionArea(state);
        UpdateQueue(state);
        UpdateLog(state);
    }

    private void UpdateBanner(HireTerminalBoundUserInterfaceState state)
    {
        if (state.Mode == HireTerminalMode.SelfService)
        {
            _modeBanner.PanelOverride = Box(GoodBg, Good, new Thickness(1));
            _modeLabel.FontColorOverride = Color.White;
            _modeLabel.Text = Loc.GetString("hire-ui-mode-self");
        }
        else
        {
            _modeBanner.PanelOverride = Box(WarnBg, Warn, new Thickness(1));
            _modeLabel.FontColorOverride = Color.White;
            _modeLabel.Text = Loc.GetString("hire-ui-mode-review");
        }
    }

    private void UpdateHolder(HireTerminalBoundUserInterfaceState state)
    {
        var has = state.Status != HirePassportStatus.NoPassport;

        _holderInfo.Visible = has;
        _noPassportHint.Visible = !has;
        _ejectButton.Disabled = !has;

        _holderName.Text = state.HolderName;
        _holderJob.Text = Loc.GetString("hire-ui-holder-job", ("job", state.HolderJob));
        _holderNumber.Text = Loc.GetString("hire-ui-holder-number", ("number", state.PassportNumber));

        var hasExtra = !string.IsNullOrWhiteSpace(state.AdditionalJob);
        _holderExtra.Visible = hasExtra;
        _holderExtra.Text = hasExtra ? Loc.GetString("hire-ui-holder-extra", ("job", state.AdditionalJob)) : string.Empty;
    }

    private void UpdateActionArea(HireTerminalBoundUserInterfaceState state)
    {
        var status = state.Status;
        var showForm = status is HirePassportStatus.CanApply or HirePassportStatus.Rejected;

        _formBox.Visible = showForm;
        _statusBox.Visible = !showForm;

        RefreshSelectedLabel();

        var self = state.Mode == HireTerminalMode.SelfService;
        _submitButton.Text = Loc.GetString(self ? "hire-ui-submit-self" : "hire-ui-submit-review");
        _submitButton.Disabled = _selectedId == null || !showForm;
        _submitButton.StyleBoxOverride = _submitButton.Disabled
            ? null
            : Box(self ? GoodBg : WarnBg, self ? Good : Warn, new Thickness(1), 8, 4);
        _submitButton.Label.FontColorOverride = Color.White;

        if (status == HirePassportStatus.Rejected)
        {
            SetRich(_selectedLabel, Loc.GetString("hire-ui-rejected-hint",
                ("position", state.ApplicationPosition), ("note", state.StatusNote)), Bad);
        }

        _claimButton.Visible = false;
        switch (status)
        {
            case HirePassportStatus.NoPassport:
                SetStatus(BgCard, TextMuted, Loc.GetString("hire-ui-status-none-title"),
                    Loc.GetString("hire-ui-status-none-text"));
                break;

            case HirePassportStatus.Invalid:
                SetStatus(BadBg, Bad, Loc.GetString("hire-ui-status-invalid-title"), state.StatusNote);
                break;

            case HirePassportStatus.Pending:
                SetStatus(WarnBg, Warn, Loc.GetString("hire-ui-status-pending-title"),
                    Loc.GetString("hire-ui-status-pending-text", ("position", state.ApplicationPosition)));
                break;

            case HirePassportStatus.ReadyToClaim:
                SetStatus(GoodBg, Good, Loc.GetString("hire-ui-status-approved-title"),
                    Loc.GetString("hire-ui-status-approved-text",
                        ("position", state.ApplicationPosition), ("note", state.StatusNote)));
                _claimButton.Visible = true;
                break;

            case HirePassportStatus.AlreadyHas:
                SetStatus(BgCard, Warn, Loc.GetString("hire-ui-status-has-title"),
                    Loc.GetString("hire-ui-status-has-text", ("job", state.AdditionalJob)));
                break;
        }
    }

    private void RefreshSelectedLabel()
    {
        if (_selectedId == null || !_positionButtons.TryGetValue(_selectedId, out var btn))
            SetRich(_selectedLabel, Loc.GetString("hire-ui-selected-none"), TextMuted);
        else
            SetRich(_selectedLabel, Loc.GetString("hire-ui-selected", ("position", btn.Text ?? string.Empty)), Accent);
    }

    private void SetStatus(Color bg, Color titleColor, string title, string text)
    {
        _statusPanel.PanelOverride = Box(bg, titleColor, new Thickness(1));
        _statusTitle.Text = title;
        _statusTitle.FontColorOverride = titleColor;
        SetRich(_statusText, text, TextMain);
    }


    private void RebuildPositionsIfChanged(HireTerminalBoundUserInterfaceState state)
    {
        var sig = string.Join("|", state.Positions.ConvertAll(p => p.Id));
        if (sig == _positionsSig)
        {
            RefreshPositionStyles();
            return;
        }

        _positionsSig = sig;
        _positionsBox.RemoveAllChildren();
        _positionButtons.Clear();
        _positionColors.Clear();

        if (_selectedId != null && !state.Positions.Exists(p => p.Id == _selectedId))
            _selectedId = null;

        string? lastDept = null;
        GridContainer? grid = null;

        foreach (var pos in state.Positions)
        {
            if (pos.Department != lastDept)
            {
                lastDept = pos.Department;

                var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
                header.AddChild(new PanelContainer { PanelOverride = Box(pos.Color), MinSize = new Vector2(4, 14), VerticalAlignment = Control.VAlignment.Center });
                header.AddChild(Text(pos.Department, pos.Color));
                _positionsBox.AddChild(header);

                grid = new GridContainer { Columns = 3, HorizontalExpand = true, HSeparationOverride = 6, VSeparationOverride = 6 };
                _positionsBox.AddChild(grid);
            }

            var btn = new Button
            {
                Text = pos.Name,
                HorizontalExpand = true,
                ClipText = true,
                ToolTip = pos.Description,
            };

            var id = pos.Id;
            btn.OnPressed += _ =>
            {
                _selectedId = id;
                RefreshPositionStyles();
                RefreshSubmitState();
            };

            _positionButtons[pos.Id] = btn;
            _positionColors[pos.Id] = pos.Color;
            grid!.AddChild(btn);
        }

        RefreshPositionStyles();
    }

    private void RefreshPositionStyles()
    {
        foreach (var (id, btn) in _positionButtons)
        {
            var color = _positionColors[id];
            if (id == _selectedId)
            {
                btn.StyleBoxOverride = Box(color.WithAlpha(0.35f), color, new Thickness(2), 6, 3);
                btn.Label.FontColorOverride = Color.White;
            }
            else
            {
                btn.StyleBoxOverride = Box(BtnNormal, Border, new Thickness(1), 6, 3);
                btn.Label.FontColorOverride = TextMain;
            }
        }
    }

    private void RefreshSubmitState()
    {
        var self = _mode == HireTerminalMode.SelfService;
        _submitButton.Disabled = _selectedId == null;
        RefreshSelectedLabel();
        _submitButton.StyleBoxOverride = _submitButton.Disabled
            ? null
            : Box(self ? GoodBg : WarnBg, self ? Good : Warn, new Thickness(1), 8, 4);
    }


    private void UpdateQueue(HireTerminalBoundUserInterfaceState state)
    {
        var show = state.Mode == HireTerminalMode.NeedsReview;
        _queueBox.Visible = show;
        if (!show)
            return;

        var isReviewer = false;
        if (_player.LocalEntity is { } local)
            isReviewer = state.Reviewers.Contains(_entMan.GetNetEntity(local));

        _queueTitle.Text = Loc.GetString(
            isReviewer ? "hire-ui-queue-title-reviewer" : "hire-ui-queue-title",
            ("count", state.Pending.Count));

        var sig = isReviewer + ":" + string.Join("|", state.Pending.ConvertAll(p => p.Id));
        if (sig == _queueSig)
            return;

        _queueSig = sig;
        _queueList.RemoveAllChildren();

        if (state.Pending.Count == 0)
        {
            _queueList.AddChild(Text(Loc.GetString("hire-ui-queue-empty"), TextMuted));
            return;
        }

        foreach (var app in state.Pending)
        {
            var card = new PanelContainer { PanelOverride = Box(BgCard, Border, new Thickness(3, 0, 0, 0), 8, 5) };
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            card.AddChild(row);

            var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            info.AddChild(Text(Loc.GetString("hire-ui-queue-applicant", ("name", app.ApplicantName), ("time", app.Time)), TextMain));
            info.AddChild(Wrap(Loc.GetString("hire-ui-queue-line", ("job", app.CurrentJob), ("position", app.PositionName)), Accent));
            if (!string.IsNullOrWhiteSpace(app.Comment))
                info.AddChild(Wrap($"«{app.Comment}»", TextMuted));
            row.AddChild(info);

            if (isReviewer)
            {
                var id = app.Id;

                var approve = new Button { Text = Loc.GetString("hire-ui-approve"), MinWidth = 96, VerticalAlignment = Control.VAlignment.Center };
                approve.StyleBoxOverride = Box(GoodBg, Good, new Thickness(1), 8, 4);
                approve.Label.FontColorOverride = Color.White;
                approve.OnPressed += _ => OnApprove?.Invoke(id);

                var reject = new Button { Text = Loc.GetString("hire-ui-reject"), MinWidth = 96, VerticalAlignment = Control.VAlignment.Center };
                reject.StyleBoxOverride = Box(BadBg, Bad, new Thickness(1), 8, 4);
                reject.Label.FontColorOverride = Color.White;
                reject.OnPressed += _ => OnReject?.Invoke(id);

                row.AddChild(approve);
                row.AddChild(reject);
            }
            else
            {
                var wait = Text(Loc.GetString("hire-ui-queue-waiting"), Warn, "LabelSubText");
                wait.VerticalAlignment = Control.VAlignment.Center;
                row.AddChild(wait);
            }

            _queueList.AddChild(card);
        }
    }


    private void UpdateLog(HireTerminalBoundUserInterfaceState state)
    {
        var sig = string.Join("|", state.Log.ConvertAll(l => l.Time + l.Name + l.PositionName));
        if (sig == _logSig)
            return;

        _logSig = sig;
        _logList.RemoveAllChildren();

        if (state.Log.Count == 0)
        {
            _logList.AddChild(Wrap(Loc.GetString("hire-ui-log-empty"), TextMuted));
            return;
        }

        foreach (var entry in state.Log)
        {
            var card = new PanelContainer { PanelOverride = Box(BgPanel, entry.Color, new Thickness(3, 0, 0, 0), 8, 5) };
            var col = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            card.AddChild(col);

            col.AddChild(Text(entry.Name, TextMain));
            col.AddChild(Wrap(Loc.GetString("hire-ui-log-position", ("position", entry.PositionName), ("department", entry.Department)), entry.Color));
            col.AddChild(Wrap(Loc.GetString("hire-ui-log-meta", ("time", entry.Time), ("by", entry.ApprovedBy)), TextMuted));

            _logList.AddChild(card);
        }
    }


    private static RichTextLabel Wrap(string text, Color color)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        SetRich(label, text, color);
        return label;
    }

    private static void SetRich(RichTextLabel label, string text, Color color)
    {
        var msg = new FormattedMessage();
        msg.PushColor(color);
        msg.AddText(text);
        msg.Pop();
        label.SetMessage(msg);
    }

    private static Label Text(string text, Color color, string? styleClass = null)
    {
        var label = new Label { Text = text, FontColorOverride = color };
        if (styleClass != null)
            label.StyleClasses.Add(styleClass);
        return label;
    }

    private static StyleBoxFlat Box(Color bg, Color? border = null, Thickness? borderThickness = null,
        float marginH = 0, float marginV = 0)
    {
        var box = new StyleBoxFlat { BackgroundColor = bg };

        if (border != null)
        {
            box.BorderColor = border.Value;
            box.BorderThickness = borderThickness ?? new Thickness(1);
        }

        if (marginH > 0 || marginV > 0)
        {
            box.ContentMarginLeftOverride = marginH;
            box.ContentMarginRightOverride = marginH;
            box.ContentMarginTopOverride = marginV;
            box.ContentMarginBottomOverride = marginV;
        }

        return box;
    }
}