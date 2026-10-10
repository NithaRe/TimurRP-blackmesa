using Content.Shared._BlackM.PhraseWheel;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.PhraseWheel;

public sealed class PhraseWheelIconOverlaySystem : EntitySystem
{
    private static readonly TimeSpan MaxSoundHold = TimeSpan.FromSeconds(15);

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PhraseWheelIconEvent>(OnIcon);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        PhraseWheelIconRegistry.Clear();
    }

    private void OnIcon(PhraseWheelIconEvent ev)
    {
        var uid = GetEntity(ev.Source);
        if (!uid.IsValid())
            return;

        if (!_proto.TryIndex<PhraseWheelEntryPrototype>(ev.Phrase.Id, out var phrase))
            return;

        var now = _timing.CurTime;

        if (GetSoundLength(phrase) is { } length && length > TimeSpan.Zero)
        {
            var hold = length > MaxSoundHold ? MaxSoundHold : length;
            PhraseWheelIconRegistry.RegisterSoundDuration(uid, hold, now);
        }

        _proto.TryIndex<PhraseWheelCategoryPrototype>(phrase.Category.Id, out var category);

        var texture = PhraseWheelTextures.Resolve(_resCache, PhraseWheelTextures.GetIcon(phrase, category));
        if (texture != null)
            PhraseWheelIconRegistry.Register(uid, texture, now);
    }

    private TimeSpan? GetSoundLength(PhraseWheelEntryPrototype phrase)
    {
        if (phrase.Sound is not SoundPathSpecifier path)
            return null;

        return _resCache.TryGetResource<AudioResource>(path.Path, out var audio)
            ? audio.AudioStream.Length
            : null;
    }
}
