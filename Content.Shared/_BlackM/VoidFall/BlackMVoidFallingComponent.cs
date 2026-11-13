using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._BlackM.VoidFall;

// Entity is falling into the void.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class BlackMVoidFallingComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan EndTime;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1.8);

    [DataField]
    public TimeSpan HangTime = TimeSpan.FromSeconds(0.45);

    public Vector2 OriginalScale = Vector2.One;
    public Vector2 OriginalOffset = Vector2.Zero;
    public Angle OriginalRotation = Angle.Zero;
    public Color OriginalColor = Color.White;
}
