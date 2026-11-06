using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._BlackM.EyeTrail;

// Glowing streak trailing from the wearer's eyes while running. Client-only visuals.
[RegisterComponent]
public sealed partial class EyeTrailComponent : Component
{
    // Head -> tail palette.
    [DataField]
    public List<Color> Colors = new()
    {
        Color.FromHex("#ff7a7a"),
        Color.FromHex("#ff2424"),
        Color.FromHex("#c40000"),
        Color.FromHex("#6e0000"),
    };

    // Eye pixel in the 32x32 equipped state, per facing.
    [DataField]
    public Dictionary<Direction, List<Vector2>> Eyes = new()
    {
        [Direction.South] = new() { new Vector2(11f, 5f) },
        [Direction.North] = new() { new Vector2(21f, 4.5f) },
        [Direction.East] = new() { new Vector2(16f, 6f) },
        [Direction.West] = new() { new Vector2(15f, 6f) },
    };

    [DataField]
    public string Slot = "head";

    [DataField]
    public float MinSpeed = 3f;

    [DataField]
    public float Lifetime = 0.35f;

    // Tail older than this fraction of lifetime is drawn checkerboard.
    [DataField]
    public float DitherFrom = 0.6f;

    [DataField]
    public float PixelSize = 1f / 32f;

    [DataField]
    public float SampleDistance = 0.015f;

    [DataField]
    public int MaxPoints = 96;
}
