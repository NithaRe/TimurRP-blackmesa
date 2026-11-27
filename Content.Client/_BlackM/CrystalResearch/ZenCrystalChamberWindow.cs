using System.Numerics;
using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenCrystalChamberWindow : DefaultWindow
{
    public event Action<ZenCrystalCommandMessage>? Command;
    private readonly RichTextLabel _sample = ZenResearchUi.Text();
    private readonly RichTextLabel _readings = ZenResearchUi.Text();
    private readonly RichTextLabel _status = ZenResearchUi.Text();
    private readonly RichTextLabel _reaction = ZenResearchUi.Text();
    private readonly RichTextLabel _medium = ZenResearchUi.Text();
    private readonly RichTextLabel _output = ZenResearchUi.Text();
    private readonly RichTextLabel _hazard = ZenResearchUi.Text();
    private readonly RichTextLabel _risk = ZenResearchUi.Text();
    private readonly ProgressBar _progress = ZenResearchUi.Gauge(ZenResearchUi.Green);
    private readonly ProgressBar _instability = ZenResearchUi.Gauge(ZenResearchUi.Amber);
    private readonly BoxContainer _actions = ZenResearchUi.Column(6);
    private readonly Button _growth;
    private readonly Button _finalize;
    private readonly Button _normal;
    private readonly Button _forced;
    private readonly Button _stop;
    private readonly Button _accelerate;
    private readonly Button _quench;
    private bool _confirmQuench;
    private bool _canQuench;
    private NetEntity? _sampleEntity;
    private bool _configured;

    public ZenCrystalChamberWindow()
    {
        MinSize = new Vector2(450, 480);
        SetSize = new Vector2(480, 550);
        var root = ZenResearchUi.Column(10);
        root.AddChild(_sample);
        root.AddChild(_readings);
        root.AddChild(_progress);
        root.AddChild(_instability);
        root.AddChild(_hazard);
        root.AddChild(ZenResearchUi.Rule());
        var conditions = ZenResearchUi.Column(10);
        conditions.AddChild(_status);
        conditions.AddChild(_reaction);
        conditions.AddChild(_medium);
        conditions.AddChild(_output);
        conditions.AddChild(_risk);
        root.AddChild(ZenResearchUi.Scroll(conditions));
        _growth = Action("zen-crystal-ui-growth", ZenCrystalCommand.StartGrowth);
        _accelerate = Action("zen-lab-accelerate", ZenCrystalCommand.AccelerateGrowth);
        _accelerate.ToolTip = Loc.GetString("zen-lab-accelerate-tooltip");
        _finalize = Action("zen-crystal-ui-finalize", ZenCrystalCommand.Finalize);
        var modes = new BoxContainer { SeparationOverride = 6 };
        _normal = Action("zen-crystal-ui-normal", ZenCrystalCommand.Generate, 0, modes);
        _forced = Action("zen-crystal-ui-forced", ZenCrystalCommand.Generate, 1, modes);
        _normal.ToggleMode = true;
        _forced.ToggleMode = true;
        _normal.ToolTip = Loc.GetString("zen-crystal-ui-normal-tooltip");
        _forced.ToolTip = Loc.GetString("zen-crystal-ui-forced-tooltip");
        _actions.AddChild(modes);
        _stop = Action("zen-crystal-ui-stop", ZenCrystalCommand.Stop);
        _quench = new Button { Text = Loc.GetString("zen-lab-quench"), HorizontalExpand = true, MinHeight = 30 };
        _quench.ToolTip = Loc.GetString("zen-lab-quench-tooltip");
        _quench.OnPressed += _ =>
        {
            if (!_canQuench)
                return;
            if (!_confirmQuench)
            {
                _confirmQuench = true;
                _quench.Text = Loc.GetString("zen-lab-quench-confirm");
                return;
            }
            _confirmQuench = false;
            Command?.Invoke(new ZenCrystalCommandMessage(ZenCrystalCommand.Quench, sample: _sampleEntity));
        };
        _actions.AddChild(_quench);
        root.AddChild(_actions);
        Contents.AddChild(ZenResearchUi.Screen(root));
    }

    private Button Action(string key, ZenCrystalCommand command, int value = 0, BoxContainer? parent = null)
    {
        var button = new Button { Text = Loc.GetString(key), HorizontalExpand = true, MinHeight = 30 };
        button.OnPressed += _ => Command?.Invoke(new ZenCrystalCommandMessage(command, value));
        (parent ?? _actions).AddChild(button);
        return button;
    }

    public void UpdateState(ZenCrystalChamberState state)
    {
        if (_sampleEntity != state.Sample)
        {
            _sampleEntity = state.Sample;
            _confirmQuench = false;
        }
        if (!_configured)
        {
            if (state.Resonator)
            {
                MinSize = new Vector2(390, 340);
                SetSize = new Vector2(440, 360);
            }
            _configured = true;
        }
        Title = Loc.GetString(state.Resonator ? "zen-crystal-ui-resonator-title" : "zen-crystal-ui-chamber-title");
        _sample.SetMessage(state.HasCrystal
            ? state.Name + " / " + Loc.GetString("zen-crystal-stage-" + state.Stage.ToString().ToLowerInvariant())
            : Loc.GetString("zen-crystal-ui-no-crystal"));
        _readings.Visible = state.HasCrystal;
        _readings.SetMessage(state.Resonator
            ? Loc.GetString("zen-crystal-ui-energy", ("energy", (int) state.Energy), ("capacity", (int) state.EnergyCapacity))
            : Loc.GetString("zen-lab-ui-readings", ("step", state.Step), ("total", state.Tasks),
                ("quality", (int) Math.Round(state.Quality * 100f)), ("instability", (int) state.Instability)));
        _progress.Visible = state.HasCrystal;
        _progress.Value = state.Resonator
            ? Math.Clamp(state.Energy / Math.Max(1f, state.EnergyCapacity), 0f, 1f)
            : state.Growth;
        _instability.Visible = state.HasCrystal && !state.Resonator;
        _instability.Value = Math.Clamp(state.Instability / 100f, 0f, 1f);
        _hazard.Visible = state.Runaway;
        _hazard.SetMessage(state.Hazard);
        _risk.Visible = state.HasCrystal && !state.Resonator && state.Stage is not (ZenCrystalStage.Researched or ZenCrystalStage.Exhausted);
        _risk.SetMessage(Loc.GetString("zen-lab-growth-reward", ("progress", (int) Math.Round(state.BonusProgress * 100f))));
        _instability.ForegroundStyleBoxOverride =
            new StyleBoxFlat(state.Instability >= 75 ? ZenResearchUi.Red : ZenResearchUi.Amber);
        var status = !state.Powered ? Loc.GetString("zen-crystal-ui-no-power") : state.Status;
        if (state.Operation != null)
            status += " " + Loc.GetString("zen-crystal-ui-seconds", ("seconds", state.RemainingSeconds));
        if (state.Resonator && state.Running && state.Powered)
            status = Loc.GetString(!state.Connected ? "zen-crystal-no-server" :
                state.Forced ? "zen-crystal-generating-forced" : "zen-crystal-generating");
        _status.SetMessage(status);
        _reaction.Visible = state.HasCrystal && !state.Resonator;
        _reaction.SetMessage(Loc.GetString("zen-lab-temperature-live", ("temperature", state.Temperature.ToString("F1"))));
        _medium.Visible = !state.Resonator;
        _medium.SetMessage(Loc.GetString(state.LidOpen ? "zen-lab-lid-open" : "zen-lab-lid-closed") + "\n" +
            (state.MediumPresent
                ? Loc.GetString("zen-lab-cartridge-state", ("medium", state.MediumName),
                    ("state", Loc.GetString(state.MediumSpent ? "zen-lab-medium-spent" : "zen-lab-medium-fresh")))
                : Loc.GetString("zen-lab-medium-missing")));
        _output.Visible = state.Resonator && state.HasCrystal;
        _output.SetMessage(Loc.GetString("zen-lab-resonator-output",
            ("rate", state.Running && state.Powered && state.Connected ? state.Forced ? 900 : 480 : 0),
            ("minutes", (int) Math.Ceiling(state.Energy / (state.Forced ? 150f : 60f)))));
        _actions.Visible = state.HasCrystal;
        var ready = state.Powered && !state.Exposing && state.Operation == null && !state.Runaway;
        _growth.Visible = !state.Resonator && state.Stage == ZenCrystalStage.Growth;
        _growth.Disabled = !ready || state.LidOpen || (state.Running && !state.AcceleratedGrowth);
        _accelerate.Visible = _growth.Visible;
        _accelerate.Disabled = !ready || state.LidOpen || (state.Running && state.AcceleratedGrowth);
        _finalize.Visible = !state.Resonator && state.Stage == ZenCrystalStage.Mature;
        _finalize.Disabled = !ready || state.LidOpen || !state.Connected;
        _normal.Visible = state.Resonator && state.Stage == ZenCrystalStage.Researched;
        _forced.Visible = _normal.Visible;
        _normal.Pressed = state.Running && !state.Forced;
        _forced.Pressed = state.Running && state.Forced;
        _normal.Disabled = !ready || !state.Connected || (state.Running && !state.Forced);
        _forced.Disabled = !ready || !state.Connected || (state.Running && state.Forced);
        _stop.Visible = !state.Exposing && (state.Running || state.Operation != null);
        _stop.Disabled = !state.Powered;
        _canQuench = state.HasCrystal && !state.Resonator && (state.Runaway || state.Instability >= 65f) &&
            state.Stage is not (ZenCrystalStage.Researched or ZenCrystalStage.Exhausted);
        _quench.Visible = _canQuench;
        _quench.Disabled = !_canQuench;
        if (!_canQuench)
            _confirmQuench = false;
        _quench.Text = Loc.GetString(_confirmQuench ? "zen-lab-quench-confirm" : "zen-lab-quench");
    }
}
