namespace Content.Shared._BlackM.Evac;

/// <summary>
/// Маркер места где появится портал эвакуации.
/// Ставится на карте маппером.
/// </summary>
[RegisterComponent]
public sealed partial class EvacPortalMarkerComponent : Component
{
    /// <summary>
    /// Если true — это маркер на карте эвакуации (куда телепортируют).
    /// Если false — это маркер на станции (откуда телепортируют).
    /// </summary>
    [DataField]
    public bool IsDestination = false;
}

/// <summary>
/// Компонент портала эвакуации.
/// Телепортирует игроков через DoAfter при приближении.
/// </summary>
[RegisterComponent]
public sealed partial class EvacPortalComponent : Component
{
    /// <summary>
    /// Список игроков у которых сейчас идёт DoAfter телепортации.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> ActiveDoAfters = new();
}