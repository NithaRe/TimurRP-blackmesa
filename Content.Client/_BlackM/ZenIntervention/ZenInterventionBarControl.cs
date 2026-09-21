using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.ZenIntervention;

public sealed class ZenInterventionBarControl : LayoutContainer
{
    private const string TrackTexturePath = "/Textures/_BlackM/Interface/zen_bar_track.png";
    private const string FillTexturePath = "/Textures/_BlackM/Interface/zen_bar_fill.png";

    private const string BreachRsiPath = "/Textures/_BlackM/Effects/spawn_zap.rsi";
    private const string BreachState = "zap";
    private const float BreachFrameDelay = 0.095f;

    private const string HeadcrabRsiPath = "/Textures/_BlackM/Mobs/headcrab.rsi";
    private const string HeadcrabState = "walking";

    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private static readonly Color FillLow = new(80, 220, 110, 255);
    private static readonly Color FillMid = new(230, 190, 30, 255);
    private static readonly Color FillHigh = new(230, 40, 40, 255);

    private readonly Label _label;

    private Texture? _trackTexture;
    private Texture? _fillTexture;
    private Texture[] _headcrabFrames = System.Array.Empty<Texture>();
    private Texture[] _breachFrames = System.Array.Empty<Texture>();

    private const float HeadcrabFrameDelay = 0.1f;

    private float _animTime;
    private int _frameIndex;

    private float _breachAnimTime;
    private int _breachFrameIndex;

    public float Fraction { get; private set; }

    public float BreachFraction { get; set; } = 0.6f;

    public ZenInterventionBarControl()
    {
        IoCManager.InjectDependencies(this);

        MinHeight = 32;

        _label = new Label
        {
            Align = Label.AlignMode.Center,
            VAlign = Label.VAlignMode.Bottom,
            FontColorOverride = Color.White,
            Text = "0%",
            MouseFilter = MouseFilterMode.Ignore,
        };
        SetAnchorPreset(_label, LayoutPreset.Wide);
        AddChild(_label);

        LoadTextures();
    }

    private void LoadTextures()
    {
        if (_resourceCache.TryGetResource<TextureResource>(TrackTexturePath, out var trackRes))
            _trackTexture = trackRes.Texture;

        if (_resourceCache.TryGetResource<TextureResource>(FillTexturePath, out var fillRes))
            _fillTexture = fillRes.Texture;

        if (_resourceCache.TryGetResource<RSIResource>(BreachRsiPath, out var breachRsiRes)
            && breachRsiRes.RSI.TryGetState(BreachState, out var breachState))
        {
            _breachFrames = breachState.GetFrames(Robust.Shared.Graphics.RSI.RsiDirection.South);
        }

        if (_resourceCache.TryGetResource<RSIResource>(HeadcrabRsiPath, out var rsiRes)
            && rsiRes.RSI.TryGetState(HeadcrabState, out var state))
        {
            _headcrabFrames = state.GetFrames(Robust.Shared.Graphics.RSI.RsiDirection.East);

            if (_headcrabFrames.Length == 0)
                _headcrabFrames = state.GetFrames(Robust.Shared.Graphics.RSI.RsiDirection.South);
        }
    }

    public void SetProgress(float level, float maxLevel)
    {
        var fraction = maxLevel > 0f ? level / maxLevel : 0f;
        Fraction = fraction < 0f ? 0f : fraction > 1f ? 1f : fraction;

        _label.Text = $"{level:0}%";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_headcrabFrames.Length > 0)
        {
            _animTime += args.DeltaSeconds;

            if (_animTime >= HeadcrabFrameDelay)
            {
                _animTime -= HeadcrabFrameDelay;
                _frameIndex = (_frameIndex + 1) % _headcrabFrames.Length;
            }
        }

        if (_breachFrames.Length > 0)
        {
            _breachAnimTime += args.DeltaSeconds;

            if (_breachAnimTime >= BreachFrameDelay)
            {
                _breachAnimTime -= BreachFrameDelay;
                _breachFrameIndex = (_breachFrameIndex + 1) % _breachFrames.Length;
            }
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var sizeInt = PixelSize;
        if (sizeInt.X <= 0 || sizeInt.Y <= 0)
            return;

        var size = new Vector2(sizeInt.X, sizeInt.Y);

        var textAreaHeight = size.Y * 0.26f;
        var barArea = size.Y - textAreaHeight;

        var barHeight = barArea * 0.42f;
        var barTop = barArea - barHeight;

        var breachSize = barArea;
        var trackWidth = size.X - breachSize;
        if (trackWidth < 1f)
            trackWidth = 1f;

        var fillColor = Fraction >= 0.999f ? FillHigh : Fraction >= BreachFraction ? FillMid : FillLow;

        if (_trackTexture != null)
        {
            handle.DrawTextureRect(_trackTexture, new UIBox2(0, barTop, trackWidth, barArea));
        }

        if (_fillTexture != null && Fraction > 0.01f)
        {
            var fillWidth = Fraction * trackWidth;
            var srcWidth = Fraction * _fillTexture.Width;
            var srcBox = new UIBox2(0, 0, srcWidth, _fillTexture.Height);
            var fillBox = new UIBox2(0, barTop, fillWidth, barArea);
            handle.DrawTextureRectRegion(_fillTexture, fillBox, srcBox, fillColor);
        }

        if (_breachFrames.Length > 0)
        {
            var breachBox = new UIBox2(size.X - breachSize, 0, size.X, breachSize);
            handle.DrawTextureRect(_breachFrames[_breachFrameIndex], breachBox);
        }

        if (_headcrabFrames.Length > 0)
        {
            var texture = _headcrabFrames[_frameIndex];

            var iconSize = size.Y * 1.4f;
            var travel = trackWidth - iconSize * 0.6f;
            if (travel < 0f)
                travel = 0f;

            var iconX = Fraction * travel;

            var iconY = barTop - iconSize * 0.55f;

            var dstBox = new UIBox2(iconX, iconY, iconX + iconSize, iconY + iconSize);

            handle.DrawTextureRect(texture, dstBox);
        }
    }

}
