using Robust.Shared.GameObjects;

namespace Content.Server._BlackM.SpawnEffects;

public sealed class MobSpawnZapSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobSpawnZapComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<MobSpawnZapComponent> ent, ref MapInitEvent args)
    {
        var coords = Transform(ent.Owner).Coordinates;
        Spawn("EffectSpawnZapBlackM", coords);
    }
}
