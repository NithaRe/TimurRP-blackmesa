using Content.Shared._BlackM.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Client._BlackM.Audio;

/// <summary>
/// Reconcile-система: синхронизирует желаемое состояние EFX эффекта с реально назначенным.
/// Работает через FrameUpdate чтобы не зависеть от порядка событий при старте звука.
/// </summary>
public sealed class BlackMAudioEffectStateSystem : EntitySystem
{
    [Dependency] private readonly BlackMAudioEffectsManagerSystem _effectsManager = default!;

    private EntityQuery<BlackMAudioEffectStateComponent> _stateQuery;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<AudioComponent, BlackMAudioEffectAppliedEvent>(OnEffectApplied);

        _stateQuery = GetEntityQuery<BlackMAudioEffectStateComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<BlackMAudioEffectStateComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out var state, out var audio))
        {
            if (!state.NeedsReconcile)
                continue;

            Reconcile((uid, audio), state);
        }
    }

    // ──────────────────────────────────────────────
    // ПУБЛИЧНЫЙ API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Устанавливает базовый пресет реверба. Возвращает false если состояние не изменилось.
    /// </summary>
    public bool SetBaseEffect(Entity<AudioComponent> ent, ProtoId<AudioPresetPrototype>? preset)
    {
        var state = EnsureComp<BlackMAudioEffectStateComponent>(ent);
        if (state.BasePreset == preset)
            return false;

        state.BasePreset = preset;
        state.NeedsReconcile = true;
        Reconcile(ent, state);
        return true;
    }

    /// <summary>
    /// Устанавливает приоритетный пресет (заглушение и т.п.).
    /// Перекрывает базовый пока активен.
    /// </summary>
    public bool SetOverrideEffect(Entity<AudioComponent> ent, ProtoId<AudioPresetPrototype>? preset)
    {
        var state = EnsureComp<BlackMAudioEffectStateComponent>(ent);
        if (state.OverridePreset == preset)
            return false;

        state.OverridePreset = preset;
        state.NeedsReconcile = true;
        Reconcile(ent, state);
        return true;
    }

    // ──────────────────────────────────────────────
    // ВНУТРЕННЕЕ
    // ──────────────────────────────────────────────

    private void OnEffectApplied(Entity<AudioComponent> ent, ref BlackMAudioEffectAppliedEvent args)
    {
        if (!_stateQuery.TryComp(ent, out var state))
            return;

        state.AppliedPreset = args.Preset;
        state.NeedsReconcile = GetTarget(state) != state.AppliedPreset;
    }

    private void Reconcile(Entity<AudioComponent> ent, BlackMAudioEffectStateComponent? state = null)
    {
        if (!_stateQuery.Resolve(ent, ref state, false))
            return;

        var target = GetTarget(state);

        // Если текущий эффект не совпадает с целевым — снимаем
        if (state.AppliedPreset != target || target == null)
            _effectsManager.RemoveAllEffects(ent.AsNullable());

        if (target == null)
        {
            state.NeedsReconcile = false;
            return;
        }

        if (state.AppliedPreset != target)
            _effectsManager.TryAddEffect(ent, target.Value);

        state.NeedsReconcile = state.AppliedPreset != target;
    }

    private static ProtoId<AudioPresetPrototype>? GetTarget(BlackMAudioEffectStateComponent state)
        => state.OverridePreset ?? state.BasePreset;
}
