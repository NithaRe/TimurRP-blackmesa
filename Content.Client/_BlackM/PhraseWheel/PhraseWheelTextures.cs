using Content.Shared._BlackM.PhraseWheel;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.PhraseWheel;

public static class PhraseWheelTextures
{
    public static Texture? Resolve(IResourceCache cache, SpriteSpecifier? spec)
    {
        switch (spec)
        {
            case SpriteSpecifier.Texture tex:
                return cache.TryGetResource<TextureResource>(tex.TexturePath, out var texRes)
                    ? texRes.Texture
                    : null;

            case SpriteSpecifier.Rsi rsi:
                if (cache.TryGetResource<RSIResource>(rsi.RsiPath, out var rsiRes)
                    && rsiRes.RSI.TryGetState(rsi.RsiState, out var state))
                {
                    return state.Frame0;
                }

                return null;

            default:
                return null;
        }
    }

    public static SpriteSpecifier? GetIcon(PhraseWheelEntryPrototype phrase, PhraseWheelCategoryPrototype? category)
    {
        return phrase.Icon ?? category?.Icon;
    }
}
