using System.Collections.Generic;
using System.Numerics;
using Content.Shared._BlackM.Events.Gman;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Events.Gman;

public sealed class GManEventSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ILightManager _lightMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IUserInterfaceManager _uiMan = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;

    private const string GlowShaderId = "GManGlowOutline";

    private GManOverlay? _overlay;

    private float _shakeTimeLeft;
    private const float ShakeDuration = 1.0f;
    private const float ShakeMagnitude = 0.25f;

    private bool _lightWasEnabled;

    private readonly HashSet<EntityUid> _hiddenSprites = new();
    private readonly HashSet<EntityUid> _glowingSprites = new();

    private bool _active;
    private TimeSpan _startTime;

    private const float RescanInterval = 0.5f;
    private float _rescanTimeLeft;

    private const float GlowPulseSpeed = 3.5f;
    private const float GlowWidthMin = 1.0f;
    private const float GlowWidthMax = 2.6f;
    private static readonly Color GlowColor = Color.FromHex("#FFD54A");

    private PanelContainer? _subtitleRoot;
    private Label? _subtitleLabel;
    private int _lastSubtitleIndex = -1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GManEventStartEvent>(OnStart);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        if (_overlay != null)
        {
            _overlayMan.RemoveOverlay(_overlay);
            _overlay = null;
        }

        if (_active)
            _lightMan.Enabled = _lightWasEnabled;

        RestoreSprites();
        RemoveSubtitleUi();
        _active = false;
    }

    private void OnStart(GManEventStartEvent ev)
    {
        if (_overlay != null)
            _overlayMan.RemoveOverlay(_overlay);

        _overlay = new GManOverlay(ev.Duration);
        _overlayMan.AddOverlay(_overlay);

        _shakeTimeLeft = ShakeDuration;
        _startTime = _timing.RealTime;

        _lightWasEnabled = _lightMan.Enabled;
        _lightMan.Enabled = false;

        ScanEntities();
        _active = true;
        _rescanTimeLeft = RescanInterval;

        CreateSubtitleUi();
        _lastSubtitleIndex = -1;
    }

    private void ScanEntities()
{
    var localEntity = _playerMan.LocalPlayer?.ControlledEntity;
    var isGhostViewer = localEntity is { } le && _entMan.HasComponent<GhostComponent>(le);

    var query = _entMan.AllEntityQueryEnumerator<SpriteComponent>();
    while (query.MoveNext(out var uid, out var sprite))
    {
        var isSelf = uid == localEntity;
        var isGManEntity = _entMan.HasComponent<GManWalkerComponent>(uid)
            || _entMan.HasComponent<GManVoidRectComponent>(uid);
        var isExempt = isSelf || (isGManEntity && !isGhostViewer);

        if (isExempt)
        {
            if (isSelf && !_glowingSprites.Contains(uid))
                AddGlow(uid, sprite);
            continue;
        }

        if (!sprite.Visible || _hiddenSprites.Contains(uid))
            continue;

        _sprite.SetVisible((uid, sprite), false);
        _hiddenSprites.Add(uid);
    }
}

    private void AddGlow(EntityUid uid, SpriteComponent sprite)
    {
        var shader = _protoMan.Index<ShaderPrototype>(GlowShaderId).InstanceUnique();
        shader.SetParameter("outline_width", GlowWidthMin);
        shader.SetParameter("outline_color", GlowColor.WithAlpha(0.7f));

#pragma warning disable CS0618
        sprite.PostShader = shader;
#pragma warning restore CS0618

        _glowingSprites.Add(uid);
    }

    private void UpdateGlowPulse()
    {
        if (_glowingSprites.Count == 0)
            return;

        var elapsed = (float)(_timing.RealTime - _startTime).TotalSeconds;
        var t = (System.MathF.Sin(elapsed * GlowPulseSpeed) + 1f) * 0.5f;
        var width = GlowWidthMin + (GlowWidthMax - GlowWidthMin) * t;
        var alpha = 0.45f + 0.35f * t;

        foreach (var uid in _glowingSprites)
        {
            if (!_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
                continue;

#pragma warning disable CS0618
            if (sprite.PostShader is not { } shader)
                continue;
            shader.SetParameter("outline_width", width);
            shader.SetParameter("outline_color", GlowColor.WithAlpha(alpha));
#pragma warning restore CS0618
        }
    }

    private void RestoreSprites()
    {
        foreach (var uid in _hiddenSprites)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
                _sprite.SetVisible((uid, sprite), true);
        }
        _hiddenSprites.Clear();

        foreach (var uid in _glowingSprites)
        {
            if (!_entMan.TryGetComponent(uid, out SpriteComponent? sprite))
                continue;
#pragma warning disable CS0618
            sprite.PostShader = null;
#pragma warning restore CS0618
        }
        _glowingSprites.Clear();
    }

    private void CreateSubtitleUi()
    {
        RemoveSubtitleUi();

        _subtitleLabel = new Label
        {
            HorizontalAlignment = Control.HAlignment.Center,
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.White,
            FontColorShadowOverride = Color.Black,
            ShadowOffsetXOverride = 1,
            ShadowOffsetYOverride = 1,
            Visible = false,
            MaxWidth = 900,
        };

        _subtitleRoot = new PanelContainer
        {
            HorizontalAlignment = Control.HAlignment.Stretch,
            Children = { _subtitleLabel },
        };

        _uiMan.PopupRoot.AddChild(_subtitleRoot);

        LayoutContainer.SetAnchorAndMarginPreset(
            _subtitleRoot,
            LayoutContainer.LayoutPreset.BottomWide,
            margin: 140);
    }

    private void RemoveSubtitleUi()
    {
        if (_subtitleRoot != null)
        {
            _subtitleRoot.Orphan();
            _subtitleRoot = null;
            _subtitleLabel = null;
        }

        _lastSubtitleIndex = -1;
    }

    private void UpdateSubtitles()
    {
        if (_subtitleLabel == null)
            return;

        var elapsed = (float)(_timing.RealTime - _startTime).TotalSeconds;
        var lines = GManSubtitles.Lines;

        var index = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (elapsed >= lines[i].Start && elapsed <= lines[i].End)
            {
                index = i;
                break;
            }
        }

        if (index == _lastSubtitleIndex)
            return;

        _lastSubtitleIndex = index;

        if (index == -1)
        {
            _subtitleLabel.Visible = false;
            return;
        }

        _subtitleLabel.Text = lines[index].Text;
        _subtitleLabel.Visible = true;
    }

    private void EnforceHiddenSprites()
    {
        foreach (var uid in _hiddenSprites)
        {
            if (_entMan.TryGetComponent(uid, out SpriteComponent? sprite) && sprite.Visible)
                _sprite.SetVisible((uid, sprite), false);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_overlay != null && _overlay.IsFinished)
        {
            _overlayMan.RemoveOverlay(_overlay);
            _overlay = null;

            _lightMan.Enabled = _lightWasEnabled;
            RestoreSprites();
            RemoveSubtitleUi();
            _active = false;
        }

        if (_shakeTimeLeft > 0f)
        {
            _shakeTimeLeft -= frameTime;

            var eye = _eyeManager.CurrentEye;
            if (eye != null)
            {
                var falloff = System.Math.Clamp(_shakeTimeLeft / ShakeDuration, 0f, 1f);

                eye.Offset = _shakeTimeLeft > 0f
                    ? new Vector2(
                        (_random.NextFloat() - 0.5f) * ShakeMagnitude * falloff,
                        (_random.NextFloat() - 0.5f) * ShakeMagnitude * falloff)
                    : Vector2.Zero;
            }
        }

        if (!_active)
            return;

        _rescanTimeLeft -= frameTime;
        if (_rescanTimeLeft <= 0f)
        {
            _rescanTimeLeft = RescanInterval;
            ScanEntities();
        }

        EnforceHiddenSprites();
        UpdateGlowPulse();
        UpdateSubtitles();
    }
}
