namespace Content.Shared._BlackM.Underground;

[RegisterComponent]
public sealed partial class UndergroundTabletComponent : Component
{
    // Set by the server when a tablet is issued. Never populated by the client.
    public EntityUid? Console;
    public long Generation;
    public TimeSpan NextLocatorPing;
}
