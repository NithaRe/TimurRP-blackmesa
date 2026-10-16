using Content.Shared._BlackM.Achievements;

namespace Content.Client._BlackM.Achievements;
public sealed class AchievementsClientSystem : EntitySystem
{
    public AchievementsStateEvent? State { get; private set; }

    public event Action? StateUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AchievementsStateEvent>(OnState);
    }

    private void OnState(AchievementsStateEvent ev)
    {
        State = ev;
        StateUpdated?.Invoke();
    }

    public void Request()
    {
        RaiseNetworkEvent(new AchievementsRequestEvent());
    }

    public void SetSelected(List<string> ids)
    {
        RaiseNetworkEvent(new AchievementSelectionEvent(ids));
    }
}
