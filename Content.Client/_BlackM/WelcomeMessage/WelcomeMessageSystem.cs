using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared._BlackM.WelcomeMessage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._BlackM.WelcomeMessage;

public sealed partial class WelcomeMessageSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _res = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly string[] FontStack =
    {
        "/Fonts/_BlackM/Bubble/Grand9KPixelCyrillic.ttf",
        "/Fonts/NotoSans/NotoSans-Regular.ttf",
    };

    private const string SoundAppear = "/Audio/_BlackM/Radio/optional/radio_talk.ogg";
    private const string SoundDisappear = "/Audio/_BlackM/Radio/optional/radio_important.ogg";
    private const string SoundType = "/Audio/_BlackM/Interface/Terminal/term_type.ogg";

    private const float AppearVolume = -5f;
    private const float DisappearVolume = -7f;
    private const float TypeVolume = -9f;
    private const float GapSeconds = 0.35f;

    private readonly Random _rng = new();

    private WelcomeMessageControl? _current;
    private WelcomeMessageEvent? _pendingWelcome;
    private WelcomeFonts? _fonts;
    private float _gap;

    public bool IsShowing => _current != null || _pendingWelcome != null || _gap > 0f;

    public bool InGameplay => _state.CurrentState is GameplayStateBase && _player.LocalEntity != null;

    public bool CanPresent => InGameplay && !IsShowing;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WelcomeMessageEvent>(OnMessage);
        _state.OnStateChanged += OnStateChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _state.OnStateChanged -= OnStateChanged;
        Clear();
    }

    public WelcomeFonts GetFonts()
    {
        return _fonts ??= new WelcomeFonts(
            _res.GetFont(FontStack, 13),
            _res.GetFont(FontStack, 9),
            _res.GetFont(FontStack, 12));
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        Clear();
    }

    public void Clear()
    {
        if (_current != null)
        {
            _current.ForceClose();
            _current.Orphan();
        }

        _current = null;
        _pendingWelcome = null;
        _gap = 0f;
    }

    private void OnMessage(WelcomeMessageEvent ev)
    {
        if (!InGameplay)
            return;

        _pendingWelcome = ev;
    }

    public bool Present(HudMessage msg)
    {
        if (!InGameplay || GetParent() is not { } parent)
            return false;

        if (_current != null)
        {
            _current.ForceClose();
            _current.Orphan();
        }

        var control = new WelcomeMessageControl(msg, GetFonts(), parent.Width, parent.UIScale);
        control.PlaySound += PlayWelcomeSound;

        parent.AddChild(control);
        LayoutContainer.SetAnchorPreset(control, LayoutContainer.LayoutPreset.CenterLeft);
        LayoutContainer.SetGrowHorizontal(control, LayoutContainer.GrowDirection.End);
        LayoutContainer.SetGrowVertical(control, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetMarginLeft(control, WelcomeMessageControl.LeftMargin);
        LayoutContainer.SetMarginRight(control, WelcomeMessageControl.LeftMargin);
        LayoutContainer.SetMarginTop(control, 0);
        LayoutContainer.SetMarginBottom(control, 0);

        _current = control;
        return true;
    }

    public LayoutContainer? GetParent()
    {
        var screen = _ui.ActiveScreen;
        if (screen != null &&
            screen.TryGetWidget<MainViewport>(out var viewport) &&
            viewport.Parent is LayoutContainer container)
        {
            return container;
        }

        return null;
    }

    internal static Texture CropPortrait(Texture texture, Vector2i position, Vector2i sizeToKeep)
    {
        var size = texture.Size;
        if (sizeToKeep.X <= 0 || sizeToKeep.Y <= 0)
            return texture;

        var x = Math.Clamp(position.X, 0, Math.Max(0, size.X - 1));
        var y = Math.Clamp(position.Y, 0, Math.Max(0, size.Y - 1));
        var w = Math.Min(sizeToKeep.X, size.X - x);
        var h = Math.Min(sizeToKeep.Y, size.Y - y);

        if (w <= 0 || h <= 0)
            return texture;

        var region = UIBox2.FromDimensions(new Vector2(x, y), new Vector2(w, h));
        return texture is AtlasTexture atlas
            ? new AtlasTexture(atlas.SourceTexture, region.Translated(atlas.SubRegion.TopLeft))
            : new AtlasTexture(texture, region);
    }

    private void PlayWelcomeSound(WelcomeSound kind)
    {
        string path;
        float volume;
        var pitch = 1f;

        switch (kind)
        {
            case WelcomeSound.Appear:
                path = SoundAppear;
                volume = AppearVolume;
                break;
            case WelcomeSound.Disappear:
                path = SoundDisappear;
                volume = DisappearVolume;
                break;
            default:
                path = SoundType;
                volume = TypeVolume;
                pitch = 0.92f + (float) _rng.NextDouble() * 0.16f;
                break;
        }

        _audio.PlayGlobal(path, Filter.Local(), false,
            AudioParams.Default.WithVolume(volume).WithPitchScale(pitch));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!InGameplay)
        {
            if (_current != null || _pendingWelcome != null)
                Clear();
            return;
        }

        if (_current != null)
        {
            if (!_current.Finished)
                return;

            _current.Orphan();
            _current = null;
            _gap = GapSeconds;
        }

        if (_gap > 0f)
        {
            _gap -= frameTime;
            if (_gap > 0f)
                return;
            _gap = 0f;
        }

        if (_pendingWelcome is not { } ev)
            return;

        _pendingWelcome = null;
        Present(new HudMessage(
            ev.Sender,
            ev.Title,
            ev.Text,
            CropPortrait(_sprite.Frame0(ev.Portrait), ev.CropPos, ev.CropSize),
            null,
            ev.Duration));
    }
}
