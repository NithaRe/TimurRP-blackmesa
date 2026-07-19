using Content.Client._BlackM.Audio.Echo;
using Content.Shared._BlackM;
using Content.Shared._BlackM.Audio;
using Content.Shared._BlackM.CCVar;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._BlackM.Audio;

public sealed class BlackMEchoSystem : EntitySystem
{
    [Dependency] private readonly BlackMAudioEffectStateSystem _effectState = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private static readonly ProtoId<AudioPresetPrototype> StandardPreset = "Room";
    private static readonly ProtoId<AudioPresetPrototype> StrongPreset = "Bathroom";

    private bool _enabled;
    private bool _useStrong;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BlackMAudioEffectedComponent, ComponentStartup>(OnEffectedStartup,
            after: [typeof(SharedAudioSystem)]);

        Subs.CVar(_cfg, BlackMCVars.EchoEnabled,      OnEnabledChanged, invokeImmediately: true);
        Subs.CVar(_cfg, BlackMCVars.EchoStrongPreset, OnPresetChanged,  invokeImmediately: true);
    }

    private void OnEffectedStartup(Entity<BlackMAudioEffectedComponent> ent, ref ComponentStartup args)
    {
        if (!_enabled)
            return;

        if (!TryComp<AudioComponent>(ent, out var audio))
            return;

        if (IsDryExempt((ent.Owner, audio)))
            return;

        Apply((ent.Owner, audio));
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;

        if (enabled)
            ApplyToAll();
        else
            RemoveFromAll();
    }

    private void OnPresetChanged(bool useStrong)
    {
        _useStrong = useStrong;

        if (!_enabled)
            return;

        var query = AllEntityQuery<BlackMAudioEffectedComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out _, out var audio))
            Apply((uid, audio));
    }

    private void Apply(Entity<AudioComponent> sound)
    {
        if (TerminatingOrDeleted(sound))
            return;

        _effectState.SetBaseEffect(sound, _useStrong ? StrongPreset : StandardPreset);
    }

    private void Remove(Entity<AudioComponent> sound)
    {
        if (TerminatingOrDeleted(sound))
            return;

        _effectState.SetBaseEffect(sound, null);
    }

    private void ApplyToAll()
    {
        var query = AllEntityQuery<BlackMAudioEffectedComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out _, out var audio))
        {
            if (!IsDryExempt((uid, audio)))
                Apply((uid, audio));
        }
    }

    private void RemoveFromAll()
    {
        var query = AllEntityQuery<BlackMAudioEffectedComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out _, out var audio))
            Remove((uid, audio));
    }

    private bool IsDryExempt(Entity<AudioComponent> sound)
    {
        var xform = Transform(sound);
        if (xform.ParentUid == EntityUid.Invalid)
            return false;

        if (!TryComp<BlackMEchoDryComponent>(xform.ParentUid, out var dry))
            return false;

        if (dry.DryPaths.Count == 0)
            return false;

        var path = sound.Comp.FileName;
        if (string.IsNullOrEmpty(path))
            return false;

        foreach (var exemptPath in dry.DryPaths)
        {
            if (exemptPath == path)
                return true;
        }

        return false;
    }
}
