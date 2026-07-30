using System.Collections.Generic;
using Content.Shared._BlackM.Ams;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Ams;

public sealed class AmsVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int Layer = 0;
    private const float SyncIntroDuration = 1.6f;
    private const string StateBase = "base";
    private const string StateSync = "sync";
    private const string StateSyncStatic = "sync-2";
    private const string StateOn = "on";

    private readonly Dictionary<EntityUid, string> _currentState = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmsComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, AmsComponent component, ComponentShutdown args)
    {
        _currentState.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AmsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var ams, out var sprite))
        {
            UpdateSprite(uid, ams, sprite);
        }
    }

    private void UpdateSprite(EntityUid uid, AmsComponent ams, SpriteComponent sprite)
    {
        var state = GetDesiredState(ams);

        if (_currentState.TryGetValue(uid, out var current) && current == state)
            return;

        _currentState[uid] = state;
        _sprite.LayerSetRsiState((uid, sprite), Layer, state);
    }

    private string GetDesiredState(AmsComponent ams)
    {
        switch (ams.Status)
        {
            case AmsStatus.Synchronizing:
                if (ams.SyncStartTime.HasValue &&
                    (_timing.CurTime - ams.SyncStartTime.Value).TotalSeconds < SyncIntroDuration)
                {
                    return StateSync;
                }

                return StateSyncStatic;

            case AmsStatus.Ready:
                return StateSyncStatic;

            case AmsStatus.Active:
                return StateOn;

            default: // Inactive, Used
                return StateBase;
        }
    }
}