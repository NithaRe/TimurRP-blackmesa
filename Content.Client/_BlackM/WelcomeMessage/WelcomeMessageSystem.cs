using System.Numerics;
using Content.Shared._BlackM.WelcomeMessage;
using Robust.Client.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.WelcomeMessage;

public sealed class WelcomeMessageSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IResourceCache _res = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private const string SoundAppear = "/Audio/_BlackM/Welcome/appear.ogg";
    private const string SoundDisappear = "/Audio/_BlackM/Welcome/disappear.ogg";
    private static readonly string[] SoundType =
    {
        "/Audio/_BlackM/Welcome/type1.ogg",
        "/Audio/_BlackM/Welcome/type2.ogg",
        "/Audio/_BlackM/Welcome/type3.ogg",
    };

    private readonly Random _rng = new();

    private static readonly string[] FontPaths =
    {
        "/Fonts/_BlackM/welcome.ttf",
        "/Fonts/NotoSansMono/NotoSansMono-Regular.ttf",
        "/Fonts/NotoSans/NotoSansMono-Regular.ttf",
        "/Fonts/NotoSans/NotoSans-Regular.ttf",
        "/EngineFonts/NotoSans/NotoSans-Regular.ttf",
    };

    private WelcomeMessageControl? _current;
    private Font? _font;
    private string? _fontPath;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WelcomeMessageEvent>(OnMessage);
    }

    private void ResolveFont()
    {
        if (_font != null && _fontPath != null)
            return;

        foreach (var path in FontPaths)
        {
            if (_res.TryGetResource<FontResource>(path, out var resource))
            {
                _font = new VectorFont(resource, 12);
                _fontPath = path;
                return;
            }
        }

        _fontPath = FontPaths[^1];
        _font = new VectorFont(_res.GetResource<FontResource>(_fontPath), 12);
    }

    private void OnMessage(WelcomeMessageEvent ev)
    {
        _current?.Orphan();
        _current = null;

        ResolveFont();

        Texture portrait = _sprite.Frame0(ev.Portrait);

        if (ev.CropSize.X > 0 && ev.CropSize.Y > 0)
        {
            portrait = new AtlasTexture(portrait, UIBox2.FromDimensions(
                new Vector2(ev.CropPos.X, ev.CropPos.Y),
                new Vector2(ev.CropSize.X, ev.CropSize.Y)));
        }

        var control = new WelcomeMessageControl(ev.Sender, ev.Title, ev.Text, portrait,
            ev.Duration, _font!, _fontPath!);

        control.PlaySound += PlayWelcomeSound;

        _ui.PopupRoot.AddChild(control);
        LayoutContainer.SetAnchorAndMarginPreset(control, LayoutContainer.LayoutPreset.TopLeft, margin: 20);

        _current = control;
    }

    private void PlayWelcomeSound(WelcomeSound kind)
    {
        string path;
        float volume;
        float pitch = 1f;

        switch (kind)
        {
            case WelcomeSound.Appear:
                path = SoundAppear;
                volume = -4f;
                break;
            case WelcomeSound.Disappear:
                path = SoundDisappear;
                volume = -5f;
                break;
            default:
                path = SoundType[_rng.Next(SoundType.Length)];
                volume = -12f;
                pitch = 0.93f + (float) _rng.NextDouble() * 0.14f;
                break;
        }

        _audio.PlayGlobal(path, Filter.Local(), false,
            AudioParams.Default.WithVolume(volume).WithPitchScale(pitch));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_current is { Finished: true })
        {
            _current.Orphan();
            _current = null;
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _current?.Orphan();
        _current = null;
    }
}