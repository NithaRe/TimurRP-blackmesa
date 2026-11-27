using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.CrystalResearch;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ZenCrystalEmitterComponent : Component
{
    [DataField, AutoNetworkedField] public ZenCrystalExposure Exposure;
    [DataField, AutoNetworkedField] public float Power = 5f;
    [DataField, AutoNetworkedField] public float TargetTemperature = 20f;
    [DataField, AutoNetworkedField] public bool Active;
    [DataField] public float Range = 4f;
    [ViewVariables] public EntityUid? Chamber;
    [ViewVariables] public EntityUid? Operator;
    [ViewVariables] public string Status = string.Empty;
    [ViewVariables] public float EffectAccumulator;
}

[Serializable, NetSerializable]
public enum ZenCrystalTool : byte
{
    Scanner,
    Scraper,
    Stabilizer,
}

[RegisterComponent]
public sealed partial class ZenCrystalLabToolComponent : Component
{
    [DataField] public ZenCrystalTool Tool;
    [DataField] public float Delay = 4f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ZenCrystalCoolantComponent : Component
{
    [DataField, AutoNetworkedField] public float Remaining = 120f;
}

[Serializable, NetSerializable]
public sealed partial class ZenCrystalToolDoAfterEvent : SimpleDoAfterEvent
{
    [DataField] public int Step;
    [DataField] public ZenCrystalStage Stage;
}
