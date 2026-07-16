using Robust.Shared.Serialization;

namespace Content.Shared._BlackM.Radio;

[Serializable, NetSerializable]
public sealed class MusicRadioBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<string> TrackNames;
    public readonly int CurrentTrack;
    public readonly bool Playing;

    public MusicRadioBoundUserInterfaceState(List<string> trackNames, int currentTrack, bool playing)
    {
        TrackNames = trackNames;
        CurrentTrack = currentTrack;
        Playing = playing;
    }
}

[Serializable, NetSerializable]
public sealed class MusicRadioTogglePlayingMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class MusicRadioSetTrackMessage : BoundUserInterfaceMessage
{
    public readonly int TrackIndex;

    public MusicRadioSetTrackMessage(int trackIndex)
    {
        TrackIndex = trackIndex;
    }
}

[Serializable, NetSerializable]
public sealed class MusicRadioStepTrackMessage : BoundUserInterfaceMessage
{
    public readonly int Direction;

    public MusicRadioStepTrackMessage(int direction)
    {
        Direction = direction;
    }
}
