using Robust.Shared.Map;

namespace Content.Shared.Xenoarchaeology.Equipment;

/// <summary>Allows a crushed object to supply its own output instead of artifact fragments.</summary>
[ByRefEvent]
public struct ArtifactCrusherOutputEvent(EntityCoordinates coordinates)
{
    public readonly EntityCoordinates Coordinates = coordinates;
    public readonly List<EntityUid> Output = new();
    public bool Handled;
}
