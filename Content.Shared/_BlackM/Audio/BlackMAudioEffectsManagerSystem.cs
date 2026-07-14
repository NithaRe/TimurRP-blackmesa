using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared._BlackM.Audio;

public sealed class BlackMAudioEffectsManagerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly Dictionary<ProtoId<AudioPresetPrototype>, EntityUid> _cachedEffects = new();

    private CancellationTokenSource _tokenSource = new();

    private static readonly TimeSpan ServerAssignDelay = TimeSpan.FromTicks(10L);

    private bool _efxAvailable = true;

    private EntityQuery<AudioComponent> _audioQuery;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Clear());
        _audioQuery = GetEntityQuery<AudioComponent>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Clear();
    }

    private void Clear()
    {
        _cachedEffects.Clear();
        _tokenSource.Cancel();
        _tokenSource = new();
    }

    public bool TryAddEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!_cachedEffects.TryGetValue(preset, out var aux) && !TryBuildEffect(preset, out aux))
            return false;

        var attempt = new BlackMAudioEffectAttemptEvent(preset);
        RaiseLocalEvent(sound, ref attempt);
        if (attempt.Cancelled)
            return false;

        if (_net.IsServer)
            Timer.Spawn(ServerAssignDelay, () => AssignEffect(sound, aux, preset), _tokenSource.Token);
        else
            AssignEffect(sound, aux, preset);

        return true;
    }

    public bool TryRemoveEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!_cachedEffects.TryGetValue(preset, out var aux))
            return false;

        if (sound.Comp.Auxiliary != aux)
            return false;

        var attempt = new BlackMAudioEffectAttemptEvent(preset);
        RaiseLocalEvent(sound, ref attempt);
        if (attempt.Cancelled)
            return false;

        RemoveAllEffects(sound.AsNullable());
        return true;
    }

    public void RemoveAllEffects(Entity<AudioComponent?> sound)
    {
        if (!_audioQuery.Resolve(sound, ref sound.Comp, false))
            return;

        _audio.SetAuxiliary(sound, sound.Comp, null);

        var ev = new BlackMAudioEffectAppliedEvent(null);
        RaiseLocalEvent(sound, ref ev);
    }

    public bool HasEffect(Entity<AudioComponent> sound, ProtoId<AudioPresetPrototype> preset)
    {
        if (!_cachedEffects.TryGetValue(preset, out var aux))
            return false;

        return sound.Comp.Auxiliary == aux;
    }

    public bool TryGetEffect(Entity<AudioComponent> sound, [NotNullWhen(true)] out ProtoId<AudioPresetPrototype>? preset)
    {
        preset = null;
        foreach (var (storedPreset, aux) in _cachedEffects)
        {
            if (sound.Comp.Auxiliary != aux)
                continue;

            preset = storedPreset;
            return true;
        }
        return false;
    }

    private void AssignEffect(Entity<AudioComponent> sound, EntityUid aux, ProtoId<AudioPresetPrototype> preset)
    {
        _audio.SetAuxiliary(sound, sound, aux);

        var ev = new BlackMAudioEffectAppliedEvent(preset);
        RaiseLocalEvent(sound, ref ev);
    }

    public bool TryBuildEffect(ProtoId<AudioPresetPrototype> preset, out EntityUid aux)
    {
        aux = default;

        if (!_prototype.TryIndex(preset, out var proto))
            return false;

        if (!_efxAvailable)
            return false;

        (EntityUid Entity, AudioEffectComponent Component)? effect;
        try
        {
            effect = _audio.CreateEffect();
        }
        catch (Exception e)
        {
            Log.Info($"[BlackM] EFX effect creation failed: {e}. Disabling until next success.");
            _efxAvailable = false;
            return false;
        }

        _efxAvailable = true;

        var auxiliary = _audio.CreateAuxiliary();
        _audio.SetEffectPreset(effect.Value.Entity, effect.Value.Component, proto);
        _audio.SetEffect(auxiliary.Entity, auxiliary.Component, effect.Value.Entity);

        if (!Exists(auxiliary.Entity))
            return false;

        if (!_cachedEffects.TryAdd(preset, auxiliary.Entity))
        {
            aux = _cachedEffects[preset];
            return true;
        }

        aux = auxiliary.Entity;
        return true;
    }
}

[ByRefEvent]
public record struct BlackMAudioEffectAttemptEvent(ProtoId<AudioPresetPrototype>? Preset)
{
    public bool Cancelled;
}

[ByRefEvent]
public record struct BlackMAudioEffectAppliedEvent(ProtoId<AudioPresetPrototype>? Preset);
