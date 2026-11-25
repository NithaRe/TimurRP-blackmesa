using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._BlackM.DeathSound;

public sealed partial class DeathSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathSoundComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, DeathSoundComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        var meta = MetaData(uid);
        if (meta.EntityLifeStage < EntityLifeStage.MapInitialized || meta.CreationTick == _timing.CurTick || Paused(uid))
            return;

        _audio.PlayPvs(component.Sound, uid);
    }
}
