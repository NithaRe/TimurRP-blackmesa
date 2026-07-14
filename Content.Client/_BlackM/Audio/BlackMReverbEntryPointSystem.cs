using Content.Client._BlackM.Audio.Echo;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;

namespace Content.Client._BlackM.Audio;

public sealed class BlackMReverbEntryPointSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AudioComponent, ComponentAdd>(OnAudioAdd);
    }

    private void OnAudioAdd(Entity<AudioComponent> ent, ref ComponentAdd args)
    {
        if (ent.Comp.Global)
            return;

        if (!_player.LocalEntity.HasValue)
            return;

        if (!IsAllowedToHear(ent, _player.LocalEntity.Value))
            return;

        if ((MetaData(ent).Flags & MetaDataFlags.Detached) != 0)
            return;

        AddComp<BlackMAudioEffectedComponent>(ent);
    }

    private bool IsAllowedToHear(Entity<AudioComponent> ent, EntityUid player)
    {
        if (ent.Comp.ExcludedEntity == player)
            return false;

        if (ent.Comp.IncludedEntities == null || ent.Comp.IncludedEntities.Count == 0)
            return true;

        foreach (var entity in ent.Comp.IncludedEntities)
        {
            if (entity == player)
                return true;
        }

        return false;
    }
}
