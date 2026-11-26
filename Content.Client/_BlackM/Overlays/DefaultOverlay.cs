using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._BlackM.Overlays;

public sealed partial class DefaultOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "DefaultAtmosphere";

    [Dependency] private IPrototypeManager _prototype = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public float Strength = 1f;

    public float Grain = 0f;

    public float Vignette = 0.5f;

    public DefaultOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototype.Index(Shader).InstanceUnique();

        ZIndex = 1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0.001f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("strength", MathHelper.Clamp(Strength, 0f, 1f));
        _shader.SetParameter("grain_amount", MathHelper.Clamp(Grain, 0f, 0.5f));
        _shader.SetParameter("vignette_amount", MathHelper.Clamp(Vignette, 0f, 1f));

        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
