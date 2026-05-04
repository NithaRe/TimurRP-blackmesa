using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.Overlays;

public sealed class DefaultOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    // use WorldSpace
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public DefaultOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index<ShaderPrototype>("DefaultAtmosphere").InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        // WorldSpace use WorldHandle
        var handle = args.WorldHandle;
        
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        
        handle.UseShader(_shader);
        // WorldBounds
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}