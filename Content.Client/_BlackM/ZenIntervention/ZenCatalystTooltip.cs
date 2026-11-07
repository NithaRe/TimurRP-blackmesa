using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.ZenIntervention;

public sealed class ZenCatalystTooltip : PanelContainer
{
    private const string Dim = "#8D90AD";
    private const string Light = "#E6E8F2";
    private const string ActiveColor = ZenInterventionPalette.CalmHex;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ZenCatalystIndicatorControl _indicator;
    private readonly Label _title;
    private readonly RichTextLabel _status;
    private readonly RichTextLabel _waves;
    private readonly RichTextLabel _footer;
    private float _refreshAccumulator;
    private string _lastWaves = string.Empty;
    private bool? _lastActive;

    public ZenCatalystTooltip(ZenCatalystIndicatorControl indicator)
    {
        IoCManager.InjectDependencies(this);
        _indicator = indicator;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#2B303A").WithAlpha(0.97f),
            BorderColor = Color.FromHex("#7B7E9E"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
        };

        _title = new Label { Text = Loc.GetString("zen-catalyst-ui-title") };
        _title.StyleClasses.Add("LabelHeading");
        _status = new RichTextLabel();
        var description = new RichTextLabel();
        description.SetMessage(Loc.GetString("zen-catalyst-ui-description"), defaultColor: Color.FromHex(Dim));
        _waves = new RichTextLabel();
        _footer = new RichTextLabel();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 320,
            MaxWidth = 320,
        };
        root.AddChild(_title);
        root.AddChild(_status);
        root.AddChild(description);
        root.AddChild(Separator());
        root.AddChild(_waves);
        root.AddChild(Separator());
        root.AddChild(_footer);
        AddChild(root);
        Refresh();
    }

    private static Control Separator()
    {
        return new PanelContainer
        {
            MinHeight = 1,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#4A5163") },
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _refreshAccumulator += args.DeltaSeconds;
        if (_refreshAccumulator < 0.2f)
            return;
        _refreshAccumulator = 0;
        Refresh();
    }

    private void Refresh()
    {
        var active = _indicator.Active;
        if (_lastActive != active)
        {
            _lastActive = active;
            var color = Color.FromHex(active ? ActiveColor : Dim);
            _title.FontColorOverride = color;
            _status.SetMessage(Loc.GetString(active ? "zen-catalyst-ui-active" : "zen-catalyst-ui-inactive"), defaultColor: color);
            _footer.SetMessage(Loc.GetString(active ? "zen-catalyst-ui-stop" : "zen-catalyst-ui-idle"), defaultColor: Color.FromHex(Dim));
        }

        var text = new StringBuilder();
        var catalysts = _indicator.Catalysts;
        if (catalysts.Count > 1)
            text.AppendLine(Loc.GetString("zen-catalyst-ui-count", ("count", catalysts.Count)));
        for (var i = 0; i < catalysts.Count; i++)
        {
            if (i > 0)
                text.AppendLine();
            if (catalysts.Count > 1)
                text.AppendLine(Loc.GetString("zen-catalyst-ui-number", ("number", i + 1)));
            var catalyst = catalysts[i];
            var interval = catalyst.SpawnInterval.TotalMinutes.ToString("0.##");
            text.AppendLine(Loc.GetString("zen-catalyst-ui-wave", ("minutes", interval), ("count", catalyst.SpawnAmount)));
            var seconds = Math.Max(0, (int) Math.Ceiling((catalyst.NextSpawnTime - _timing.CurTime).TotalSeconds));
            var time = $"{seconds / 60:00}:{seconds % 60:00}";
            text.Append(Loc.GetString("zen-catalyst-ui-next-wave", ("time", time)));
            text.AppendLine();
        }
        var markup = $"[color={Light}]{text.ToString().TrimEnd()}[/color]";
        if (markup == _lastWaves)
            return;
        _lastWaves = markup;
        _waves.Visible = active;
        _waves.SetMessage(FormattedMessage.FromMarkupPermissive(markup));
    }
}
