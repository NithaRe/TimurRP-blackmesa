using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenCrystalChamberWindow : DefaultWindow
{
    public event Action<ZenCrystalCommandMessage>? Command;

    private readonly Label _sample = new() { ClipText = true };
    private readonly Label _readings = new() { ClipText = true };
    private readonly RichTextLabel _status = new();
    private readonly RichTextLabel _journal = new();
    private readonly ProgressBar _progress = new() { MinValue = 0, MaxValue = 1, MinHeight = 14 };
    private readonly OptionButton _exposure = new() { HorizontalExpand = true };
    private readonly Slider _intensity = new() { MinValue = 1, MaxValue = 3, Value = 1, Rounded = true, MinHeight = 22, HorizontalExpand = true };
    private readonly Slider _temperature = new() { MinValue = -4, MaxValue = 16, Value = 4, Rounded = true, MinHeight = 22, HorizontalExpand = true };
    private readonly Label _intensityValue = new() { MinWidth = 24 };
    private readonly Label _temperatureValue = new() { MinWidth = 65 };
    private readonly BoxContainer _experiments;
    private readonly BoxContainer _generation;
    private readonly Button _scan;
    private readonly Button _expose;
    private readonly Button _stabilize;
    private readonly Button _growth;
    private readonly Button _service;
    private readonly Button _finalize;
    private readonly Button _normal;
    private readonly Button _forced;
    private readonly Button _stop;
    private bool _updating;
    private string _lastJournal = string.Empty;

    public ZenCrystalChamberWindow()
    {
        MinSize = new Vector2(480, 460);
        SetSize = new Vector2(570, 620);
        var root = Column();
        var header = new BoxContainer { SeparationOverride = 10 };
        var preview = new EntityPrototypeView { MinSize = new Vector2(48, 48), SetSize = new Vector2(48, 48), Scale = new Vector2(1.5f) };
        preview.SetPrototype("ZenCrystal");
        header.AddChild(preview);
        var summary = Column();
        summary.AddChild(_sample);
        summary.AddChild(_readings);
        header.AddChild(summary);
        root.AddChild(header);
        root.AddChild(_progress);
        root.AddChild(_status);

        _experiments = Column();
        var exposureRow = new BoxContainer { SeparationOverride = 8 };
        foreach (var exposure in Enum.GetValues<ZenCrystalExposure>())
            _exposure.AddItem(Loc.GetString("zen-crystal-exposure-" + exposure.ToString().ToLowerInvariant()), (int) exposure);
        _exposure.SelectId(0);
        _exposure.OnItemSelected += args => _exposure.SelectId(args.Id);
        exposureRow.AddChild(_exposure);
        exposureRow.AddChild(_intensity);
        exposureRow.AddChild(_intensityValue);
        _intensity.OnValueChanged += _ => _intensityValue.Text = ((int) _intensity.Value).ToString();
        _intensityValue.Text = "1";
        _intensity.ToolTip = Loc.GetString("zen-crystal-ui-intensity");
        _experiments.AddChild(exposureRow);
        var actions = new BoxContainer { SeparationOverride = 6 };
        _scan = AddButton(actions, "zen-crystal-ui-scan", () => Send(ZenCrystalCommand.Scan));
        _expose = AddButton(actions, "zen-crystal-ui-expose", () => Command?.Invoke(new ZenCrystalCommandMessage(
            ZenCrystalCommand.Expose, (int) _intensity.Value, (ZenCrystalExposure) _exposure.SelectedId)));
        _stabilize = AddButton(actions, "zen-crystal-ui-stabilize", () => Send(ZenCrystalCommand.Stabilize));
        _experiments.AddChild(actions);
        var temperatureRow = new BoxContainer { SeparationOverride = 8 };
        temperatureRow.AddChild(new Label { Text = Loc.GetString("zen-crystal-ui-temperature") });
        temperatureRow.AddChild(_temperature);
        temperatureRow.AddChild(_temperatureValue);
        _temperature.OnValueChanged += _ =>
        {
            if (!_updating)
                Command?.Invoke(new ZenCrystalCommandMessage(ZenCrystalCommand.SetTemperature, (int) _temperature.Value * 5));
            _temperatureValue.Text = $"{(int) _temperature.Value * 5} °C";
        };
        _experiments.AddChild(temperatureRow);
        var growthRow = new BoxContainer { SeparationOverride = 6 };
        _growth = AddButton(growthRow, "zen-crystal-ui-growth", () => Send(ZenCrystalCommand.StartGrowth));
        _service = AddButton(growthRow, "zen-crystal-ui-service", () => Send(ZenCrystalCommand.ServiceGrowth));
        _finalize = AddButton(growthRow, "zen-crystal-ui-finalize", () => Send(ZenCrystalCommand.Finalize));
        _experiments.AddChild(growthRow);
        root.AddChild(_experiments);

        _generation = new BoxContainer { SeparationOverride = 6 };
        _normal = AddButton(_generation, "zen-crystal-ui-normal", () => Command?.Invoke(new ZenCrystalCommandMessage(ZenCrystalCommand.Generate, 0)));
        _forced = AddButton(_generation, "zen-crystal-ui-forced", () => Command?.Invoke(new ZenCrystalCommandMessage(ZenCrystalCommand.Generate, 1)));
        _normal.ToolTip = Loc.GetString("zen-crystal-ui-normal-tooltip");
        _forced.ToolTip = Loc.GetString("zen-crystal-ui-forced-tooltip");
        root.AddChild(_generation);
        _stop = AddButton(root, "zen-crystal-ui-stop", () => Send(ZenCrystalCommand.Stop));
        root.AddChild(new Label { Text = Loc.GetString("zen-crystal-ui-journal") });
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, MinHeight = 120 };
        scroll.AddChild(_journal);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    private static BoxContainer Column() => new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        HorizontalExpand = true,
        SeparationOverride = 8,
    };

    private Button AddButton(Control parent, string key, Action pressed)
    {
        var button = new Button { Text = Loc.GetString(key), HorizontalExpand = true, MinHeight = 28 };
        button.OnPressed += _ => pressed();
        parent.AddChild(button);
        return button;
    }

    private void Send(ZenCrystalCommand command) => Command?.Invoke(new ZenCrystalCommandMessage(command));

    public void UpdateState(ZenCrystalChamberState state)
    {
        _updating = true;
        Title = Loc.GetString(state.Resonator ? "zen-crystal-ui-resonator-title" : "zen-crystal-ui-chamber-title");
        _sample.Text = state.HasCrystal ? state.Name + " / " + Loc.GetString("zen-crystal-stage-" + state.Stage.ToString().ToLowerInvariant())
            : Loc.GetString("zen-crystal-ui-no-crystal");
        _readings.Text = state.Resonator
            ? Loc.GetString("zen-crystal-ui-energy", ("energy", (int) state.Energy), ("capacity", (int) state.EnergyCapacity))
            : Loc.GetString("zen-crystal-ui-readings", ("step", state.Step), ("instability", (int) state.Instability),
                ("quality", (int) Math.Round(state.Quality * 100f)), ("mineral", state.MineralCharges), ("organic", state.OrganicCharges));
        _progress.Value = state.Resonator ? (state.EnergyCapacity > 0 ? state.Energy / state.EnergyCapacity : 0f) :
            state.Stage == ZenCrystalStage.Structure ? state.Step / 3f : state.Growth;
        var status = !state.Powered ? Loc.GetString("zen-crystal-ui-no-power") : state.Status;
        if (state.Operation != null)
            status += " " + Loc.GetString("zen-crystal-ui-seconds", ("seconds", state.RemainingSeconds));
        _status.SetMessage(status);
        _temperature.SetValueWithoutEvent(state.Temperature / 5f);
        _temperatureValue.Text = $"{state.Temperature} °C";
        _experiments.Visible = !state.Resonator;
        _generation.Visible = state.Resonator;
        var ready = state.Powered && state.HasCrystal && state.Operation == null;
        _exposure.Disabled = !ready || state.Stage != ZenCrystalStage.Structure;
        _intensity.Disabled = _exposure.Disabled;
        _temperature.Disabled = !ready;
        _scan.Disabled = !ready || state.Stage is not (ZenCrystalStage.Structure or ZenCrystalStage.Growth);
        _expose.Disabled = !ready || state.Stage != ZenCrystalStage.Structure || !state.Scanned;
        _stabilize.Disabled = !ready || state.Stage != ZenCrystalStage.Structure || state.Instability <= 0f;
        _growth.Disabled = !ready || state.Stage != ZenCrystalStage.Growth || state.Running || state.NeedsService;
        _service.Disabled = !ready || !state.NeedsService;
        _finalize.Disabled = !ready || state.Stage != ZenCrystalStage.Mature || !state.Connected;
        _normal.Disabled = !ready || !state.Connected || state.Stage != ZenCrystalStage.Researched || (state.Running && !state.Forced);
        _forced.Disabled = !ready || !state.Connected || state.Stage != ZenCrystalStage.Researched || (state.Running && state.Forced);
        _stop.Disabled = !state.Powered || (!state.Running && state.Operation == null);
        var journal = string.Join("\n\n", state.Journal);
        if (journal != _lastJournal)
        {
            _journal.SetMessage(journal);
            _lastJournal = journal;
        }
        _updating = false;
    }
}
