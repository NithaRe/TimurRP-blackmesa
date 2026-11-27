using System.Numerics;
using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenCrystalEmitterWindow : DefaultWindow
{
    [Dependency] private IResourceCache _resources = default!;
    public event Action<ZenCrystalEmitterCommandMessage>? Command;
    private readonly RichTextLabel _target = ZenResearchUi.Text();
    private readonly RichTextLabel _status = ZenResearchUi.Text();
    private readonly RichTextLabel _dose = ZenResearchUi.Text();
    private readonly RichTextLabel _resource = ZenResearchUi.Text();
    private readonly Label _powerValue = new() { MinWidth = 55 };
    private readonly Label _temperatureValue = new() { MinWidth = 55 };
    private readonly Slider _power = new() { MinValue = 2, MaxValue = 12, Value = 5, HorizontalExpand = true, MinHeight = 22 };
    private readonly Slider _temperature = new() { MinValue = -20, MaxValue = 80, Value = 20, Rounded = true, HorizontalExpand = true, MinHeight = 22 };
    private readonly BoxContainer _thermostat = ZenResearchUi.Column(4);
    private readonly Button _toggle = new() { HorizontalExpand = true, MinHeight = 30 };
    private bool _updating;

    public ZenCrystalEmitterWindow()
    {
        IoCManager.InjectDependencies(this);
        Stylesheet = ZenResearchUi.Styles(_resources);
        MinSize = new Vector2(340, 270);
        SetSize = new Vector2(390, 330);
        var root = ZenResearchUi.Column(10);
        root.AddChild(_target);
        root.AddChild(_status);
        root.AddChild(ZenResearchUi.Rule());
        root.AddChild(new Label { Text = Loc.GetString("zen-lab-ui-power") });
        var power = new BoxContainer { SeparationOverride = 8 };
        power.AddChild(_power);
        power.AddChild(_powerValue);
        root.AddChild(power);
        _thermostat.AddChild(new Label { Text = Loc.GetString("zen-lab-ui-thermostat") });
        var temperature = new BoxContainer { SeparationOverride = 8 };
        temperature.AddChild(_temperature);
        temperature.AddChild(_temperatureValue);
        _thermostat.AddChild(temperature);
        root.AddChild(_thermostat);
        root.AddChild(_dose);
        root.AddChild(_resource);
        root.AddChild(_toggle);
        _power.OnValueChanged += _ => Changed();
        _temperature.OnValueChanged += _ => Changed();
        _toggle.OnPressed += _ => Command?.Invoke(new ZenCrystalEmitterCommandMessage(true, _power.Value, _temperature.Value));
        Contents.AddChild(ZenResearchUi.Screen(root));
    }

    private void Changed()
    {
        _powerValue.Text = MathF.Round(_power.Value, 1).ToString("0.0");
        _temperatureValue.Text = $"{(int) _temperature.Value} °C";
        if (!_updating)
            Command?.Invoke(new ZenCrystalEmitterCommandMessage(false, _power.Value, _temperature.Value));
    }

    public void UpdateState(ZenCrystalEmitterState state)
    {
        _updating = true;
        Title = Loc.GetString("zen-lab-apparatus-" + state.Exposure.ToString().ToLowerInvariant());
        _target.SetMessage(state.Connected ? state.Target : Loc.GetString("zen-lab-link-required"));
        _status.SetMessage(!state.Powered ? Loc.GetString("zen-crystal-ui-no-power") : state.Status);
        _power.SetValueWithoutEvent(state.Power);
        _temperature.SetValueWithoutEvent(state.TargetTemperature);
        Changed();
        _power.Disabled = state.Active || !state.Powered;
        _temperature.Disabled = state.Active || !state.Powered;
        _thermostat.Visible = state.Exposure is ZenCrystalExposure.Heat or ZenCrystalExposure.Cold;
        _dose.SetMessage(Loc.GetString("zen-lab-ui-emitter-readings", ("dose", MathF.Round(state.Dose, 1)),
            ("instability", (int) state.Instability)));
        _resource.Visible = state.Exposure is ZenCrystalExposure.Electricity or ZenCrystalExposure.Cold;
        _resource.SetMessage(state.Consumable < 0 ? Loc.GetString("zen-lab-resource-missing") :
            Loc.GetString(state.Exposure == ZenCrystalExposure.Cold ? "zen-lab-coolant-left" : "zen-lab-capacitor-left",
                ("amount", (int) state.Consumable)));
        _toggle.Text = Loc.GetString(state.Active ? "zen-crystal-ui-stop" : "zen-lab-ui-start-apparatus");
        _toggle.Disabled = !state.Powered || (!state.Active && !state.Connected);
        _updating = false;
    }
}
