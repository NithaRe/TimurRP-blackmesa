namespace Content.Client._BlackM.Audio.Echo;

/// <summary>
/// Маркер того, что звук должен получить эффект реверберации.
/// Добавляется автоматически через BlackMReverbEntryPointSystem.
/// </summary>
[RegisterComponent]
public sealed partial class BlackMAudioEffectedComponent : Component
{
}
