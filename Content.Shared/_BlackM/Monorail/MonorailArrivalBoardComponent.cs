using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Monorail;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MonorailArrivalBoardComponent : Component
{
    #region Config

    [DataField]
    public string? Line;

    [DataField]
    public float SearchRange = 8f;

    [DataField]
    public int RowLength = 13;

    [DataField]
    public Vector2 TextOffset = Vector2.Zero;

    [DataField]
    public float RowSpacing = 7f;

    [DataField]
    public float StationNamePeriod = 6f;

    [DataField]
    public Color ArrivingColor = Color.FromHex("#ffb000");

    [DataField]
    public Color BoardingColor = Color.FromHex("#6dff8e");

    [DataField]
    public Color DepartingColor = Color.FromHex("#ff7a2e");

    [DataField]
    public Color OfflineColor = Color.FromHex("#ff3b3b");

    [DataField]
    public Color StationNameColor = Color.FromHex("#ffe2b0");

    #endregion

    #region Networked state

    [ViewVariables, AutoNetworkedField]
    public MonorailBoardStatus Status = MonorailBoardStatus.Offline;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan TargetTime;

    [ViewVariables, AutoNetworkedField]
    public string? StationName;

    #endregion

    #region Server runtime

    [ViewVariables] public EntityUid? Stop;
    [ViewVariables] public TimeSpan NextUpdate;
    [ViewVariables] public TimeSpan NextStopSearch;

    #endregion
}

[Serializable, NetSerializable]
public enum MonorailBoardStatus : byte
{
    Offline,
    Arriving,
    Boarding,
    Departing,
}
