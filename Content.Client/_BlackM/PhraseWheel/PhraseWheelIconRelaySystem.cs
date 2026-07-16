using Content.Shared._BlackM.PhraseWheel;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.PhraseWheel;

public sealed class PhraseWheelIconOverlaySystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PhraseWheelIconEvent>(OnIconEvent);
    }

    private void OnIconEvent(PhraseWheelIconEvent ev)
    {
        var uid = GetEntity(ev.Source);
        if (!Exists(uid)) return;

        Texture? texture;
        try
        {
            texture = _resCache.GetResource<TextureResource>(ev.IconPath).Texture;
        }
        catch
        {
            return;
        }

        PhraseWheelIconRegistry.Register(uid, texture, _timing.CurTime);
    }
}
