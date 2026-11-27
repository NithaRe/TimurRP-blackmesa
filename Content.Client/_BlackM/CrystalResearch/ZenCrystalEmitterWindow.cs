using System.Numerics;
using Content.Shared._BlackM.CrystalResearch;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._BlackM.CrystalResearch;

public sealed class ZenCrystalEmitterWindow : DefaultWindow
{
    public event Action<ZenCrystalEmitterCommandMessage>? Command;
    private readonly RichTextLabel _target = ZenResearchUi.Text();
    private readonly RichTextLabel _status = ZenResearchUi.Text();
    private readonly RichTextLabel _readings = ZenResearchUi.Text();
    private readonly RichTextLabel _rate = ZenResearchUi.Text();
    private readonly RichTextLabel _resource = ZenResearchUi.Text();
    private readonly RichTextLabel _hazard = ZenResearchUi.Text();
    private readonly ProgressBar _instability = ZenResearchUi.Gauge(ZenResearchUi.Amber);
    private readonly ProgressBar _reserve = ZenResearchUi.Gauge(ZenResearchUi.Green);
    private readonly Button _toggle = new() { HorizontalExpand = true, MinHeight = 30 };

    public ZenCrystalEmitterWindow()
    {
        MinSize = new Vector2(350, 300);
        SetSize = new Vector2(400, 350);
        var root = ZenResearchUi.Column(10);
        root.AddChild(_target);
        root.AddChild(_readings);
        root.AddChild(_instability);
        root.AddChild(_hazard);
        root.AddChild(ZenResearchUi.Rule());
        root.AddChild(_rate);
        root.AddChild(_resource);
        root.AddChild(_reserve);
        root.AddChild(ZenResearchUi.Scroll(_status));
        root.AddChild(_toggle);
        Contents.AddChild(ZenResearchUi.Screen(root));
        _toggle.OnPressed += _ => Command?.Invoke(new ZenCrystalEmitterCommandMessage());
    }

    public void UpdateState(ZenCrystalEmitterState state)
    {
        var thermal = state.Exposure is ZenCrystalExposure.Heat or ZenCrystalExposure.Cold;
        Title = Loc.GetString("zen-lab-apparatus-" + state.Exposure.ToString().ToLowerInvariant());
        _target.SetMessage(state.Connected ? state.Target : Loc.GetString("zen-lab-link-required"));
        _hazard.Visible = !string.IsNullOrEmpty(state.Hazard);
        _hazard.SetMessage(state.Hazard);
        _status.SetMessage(!state.Powered ? Loc.GetString("zen-crystal-ui-no-power") : state.Status);
        _readings.SetMessage(!state.HasSample ? Loc.GetString("zen-crystal-ui-no-crystal") :
            thermal ? Loc.GetString("zen-lab-thermal-readings", ("temperature", state.SampleTemperature.ToString("F1")),
                ("instability", (int) state.Instability)) :
            Loc.GetString("zen-lab-ui-emitter-readings", ("dose", state.Dose.ToString("F1")),
                ("instability", (int) state.Instability)));
        _rate.SetMessage(Loc.GetString("zen-lab-fixed-rate-" + state.Exposure.ToString().ToLowerInvariant(),
            ("rate", state.Power.ToString("F1"))));
        _instability.Visible = state.HasSample;
        _instability.Value = Math.Clamp(state.Instability / 100f, 0f, 1f);
        var consumable = state.Exposure is ZenCrystalExposure.Cold or ZenCrystalExposure.Electricity;
        _resource.Visible = consumable;
        _reserve.Visible = consumable && state.Consumable >= 0f;
        _reserve.Value = Math.Clamp(state.Consumable / Math.Max(1f, state.ConsumableCapacity), 0f, 1f);
        _resource.SetMessage(state.Consumable < 0f ? Loc.GetString("zen-lab-resource-missing") :
            Loc.GetString(state.Exposure == ZenCrystalExposure.Cold ? "zen-lab-coolant-left" : "zen-lab-capacitor-left",
                ("amount", (int) state.Consumable)));
        _toggle.Text = Loc.GetString(state.Active ? "zen-crystal-ui-stop" : "zen-lab-ui-start-apparatus");
        _toggle.Disabled = !state.Powered || (!state.Active && (!state.Connected || !state.HasSample));
    }
}
