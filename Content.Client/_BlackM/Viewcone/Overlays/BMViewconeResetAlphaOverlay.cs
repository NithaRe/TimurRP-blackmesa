// Originally authored by mirrorcult for EphemeralSpace (https://github.com/EphemeralSpace/ephemeral-space)
// Ported to this project with modifications under the same license.

using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client._BlackM.Viewcone.Overlays;

/// <summary>
///     After <see cref="BMViewconeSetAlphaOverlay"/> has run, resets the alpha of affected entities back to normal.
/// </summary>
public sealed class BMViewconeResetAlphaOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _ent = default!;
    private readonly BMViewconeOverlayManagementSystem _cone;
    private readonly SpriteSystem _sprite;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public BMViewconeResetAlphaOverlay()
    {
        IoCManager.InjectDependencies(this);

        _cone = _ent.EntitySysManager.GetEntitySystem<BMViewconeOverlayManagementSystem>();
        _sprite = _ent.EntitySysManager.GetEntitySystem<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        foreach (var (ent, baseAlpha) in _cone.CachedBaseAlphas)
        {
            _sprite.SetColor(ent!, ent.Comp.Color.WithAlpha(baseAlpha));
        }

        _cone.CachedBaseAlphas.Clear();
    }
}
