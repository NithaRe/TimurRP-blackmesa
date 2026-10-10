using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Graphics.RSI;

namespace Content.Client._BlackM.Rules;

/// <summary>Renders the signed folder through a burn mask without altering the rules document.</summary>
public sealed class RulesDossierBurn : Control
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private IRenderTexture? _paper;
    private ShaderInstance? _shader;
    private readonly Texture _closedLighter;
    private readonly Texture _openLighter;
    private readonly Texture _lid;
    private readonly Texture[] _flames;
    private float _time;
    private bool _captured;
    public Control? Folder { get; set; }
    public bool Burning { get; private set; }
    public float Progress { get; set; }

    public RulesDossierBurn()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        var rsi = _resources.GetResource<RSIResource>("/Textures/Objects/Tools/Lighters/lighters.rsi").RSI;
        _closedLighter = rsi["zippo_engraved_icon_base"].GetFrame(RsiDirection.South, 0);
        _openLighter = rsi["zippo_engraved_open"].GetFrame(RsiDirection.South, 0);
        _lid = rsi["zippo_top"].GetFrame(RsiDirection.South, 0);
        _flames = rsi["lighter_flame"].GetFrames(RsiDirection.South);
    }

    public void Ignite()
    {
        _shader ??= _prototypes.Index<ShaderPrototype>("BlackMRulesDossierBurn").InstanceUnique();
        Burning = true;
        Progress = 0;
        _time = 0;
        _captured = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    protected override void Draw(IRenderHandle renderHandle)
    {
        if (Folder == null || Folder.PixelSize.X <= 0 || Folder.PixelSize.Y <= 0)
            return;

        var handle = renderHandle.DrawingHandleScreen;
        var position = (Vector2) (Folder.GlobalPixelPosition - GlobalPixelPosition);
        var bounds = UIBox2.FromDimensions(position, Folder.PixelSize);

        if (Burning && _shader != null)
        {
            if (!_captured)
            {
                _paper?.Dispose();
                _paper = _clyde.CreateRenderTarget(Folder.PixelSize,
                    new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "rules-dossier-burn");
                var transform = handle.GetTransform();
                renderHandle.RenderInRenderTarget(_paper, () =>
                {
                    handle.SetTransform(Matrix3x2.Identity);
                    UserInterfaceManager.RenderControl(renderHandle, Folder, Vector2i.Zero);
                }, Color.Transparent);
                handle.SetTransform(transform);
                _captured = true;
            }

            if (_paper != null)
            {
                _shader.SetParameter("progress", Progress);
                _shader.SetParameter("burn_time", _time);
                _shader.SetParameter("aspect", (float) _paper.Size.X / _paper.Size.Y);
                handle.UseShader(_shader);
                handle.DrawTextureRect(_paper.Texture, bounds);
                handle.UseShader(null);
            }
        }

        var size = 176 * UIScale;
        var lift = Burning ? Math.Clamp(_time / 0.35f, 0, 1) * 26 * UIScale : 0;
        var lighterPosition = new Vector2(bounds.Left + bounds.Width * 0.94f - size * 0.5f, bounds.Bottom - size * 0.1f - lift);
        var lighterBounds = UIBox2.FromDimensions(lighterPosition, new Vector2(size));
        var opacity = Burning ? 1 - Math.Clamp((Progress - 0.65f) / 0.25f, 0, 1) : 1;
        var tint = Color.White.WithAlpha(opacity);
        handle.DrawTextureRect(Burning ? _openLighter : _closedLighter, lighterBounds, tint);
        if (Burning)
        {
            handle.DrawTextureRect(_lid, lighterBounds, tint);
            handle.DrawTextureRect(_flames[(int) (_time * 10) % _flames.Length], lighterBounds, tint);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _paper?.Dispose();
            _shader?.Dispose();
            Folder = null;
        }
        base.Dispose(disposing);
    }
}
