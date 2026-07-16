using System.Numerics;
using Content.Shared._BlackM.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Client.ResourceManagement;

namespace Content.Client._BlackM.Radio;

public sealed class MusicRadioWindow : DefaultWindow
{
    public event Action? OnTogglePlaying;
    public event Action? OnPrevTrack;
    public event Action? OnNextTrack;
    public event Action<int>? OnTrackSelected;

    private readonly RadioTextureButton _playButton;
    private readonly RadioTextureButton _prevButton;
    private readonly RadioTextureButton _nextButton;
    private readonly Label _trackLabel;
    private readonly Label _statusLabel;
    private readonly ItemList _trackList;

    private const string SpritePath = "/Textures/_BlackM/Interface/MusicRadio/";

    public MusicRadioWindow()
    {
        Title = Loc.GetString("music-radio-ui-title");
        MinSize = new Vector2(280, 340);

        var resCache = IoCManager.Resolve<IResourceCache>();

        Texture LoadTex(string name)
        {
            var path = SpritePath + name;
            return resCache.TryGetResource<Robust.Client.ResourceManagement.TextureResource>(path, out var tex)
                ? (Texture) tex
                : Texture.Transparent;
        }

        var background = new PanelContainer
        {
            PanelOverride = new StyleBoxTexture
            {
                Texture = LoadTex("window_background.png"),
                PatchMarginLeft = 8,
                PatchMarginRight = 8,
                PatchMarginTop = 8,
                PatchMarginBottom = 8,
            },
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
        };

        background.AddChild(root);

        var display = new PanelContainer
        {
            PanelOverride = new StyleBoxTexture
            {
                Texture = LoadTex("display_panel.png"),
                PatchMarginLeft = 8,
                PatchMarginRight = 8,
                PatchMarginTop = 8,
                PatchMarginBottom = 8,
            },
            MinHeight = 48,
        };

        var displayBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
        };

        _trackLabel = new Label { Text = "---", HorizontalAlignment = Control.HAlignment.Center };
        _statusLabel = new Label { Text = Loc.GetString("music-radio-ui-stopped"), HorizontalAlignment = Control.HAlignment.Center };

        displayBox.AddChild(_trackLabel);
        displayBox.AddChild(_statusLabel);
        display.AddChild(displayBox);
        root.AddChild(display);

        var controlRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalAlignment = Control.HAlignment.Center,
        };

        _prevButton = new RadioTextureButton
        {
            BaseTexture = LoadTex("button_prev.png"),
            HoverTexture = LoadTex("button_prev_hover.png"),
            PressedTexture = LoadTex("button_prev_pressed.png"),
            MinSize = new Vector2(32, 32),
            ToolTip = Loc.GetString("music-radio-ui-prev"),
        };
        _prevButton.ShowBase();
        _prevButton.OnPressed += _ => OnPrevTrack?.Invoke();

        _playButton = new RadioTextureButton
        {
            BaseTexture = LoadTex("button_play.png"),
            HoverTexture = LoadTex("button_play_hover.png"),
            PressedTexture = LoadTex("button_play_pressed.png"),
            MinSize = new Vector2(32, 32),
            ToolTip = Loc.GetString("music-radio-ui-toggle"),
        };
        _playButton.ShowBase();
        _playButton.OnPressed += _ => OnTogglePlaying?.Invoke();

        _nextButton = new RadioTextureButton
        {
            BaseTexture = LoadTex("button_next.png"),
            HoverTexture = LoadTex("button_next_hover.png"),
            PressedTexture = LoadTex("button_next_pressed.png"),
            MinSize = new Vector2(32, 32),
            ToolTip = Loc.GetString("music-radio-ui-next"),
        };
        _nextButton.ShowBase();
        _nextButton.OnPressed += _ => OnNextTrack?.Invoke();

        controlRow.AddChild(_prevButton);
        controlRow.AddChild(_playButton);
        controlRow.AddChild(_nextButton);
        root.AddChild(controlRow);

        root.AddChild(new Label { Text = Loc.GetString("music-radio-ui-frequencies") });

        _trackList = new ItemList
        {
            VerticalExpand = true,
            MinHeight = 160,
        };
        _trackList.OnItemSelected += args => OnTrackSelected?.Invoke(args.ItemIndex);
        root.AddChild(_trackList);

        Contents.AddChild(background);
    }

    private void UpdatePlayButtonTexture(bool playing)
    {
        var resCache = IoCManager.Resolve<IResourceCache>();

        Texture LoadTex(string name)
        {
            var path = SpritePath + name;
            return resCache.TryGetResource<Robust.Client.ResourceManagement.TextureResource>(path, out var tex)
                ? (Texture) tex
                : Texture.Transparent;
        }

        if (playing)
        {
            _playButton.BaseTexture = LoadTex("button_stop.png");
            _playButton.HoverTexture = LoadTex("button_stop_hover.png");
            _playButton.PressedTexture = LoadTex("button_stop_pressed.png");
        }
        else
        {
            _playButton.BaseTexture = LoadTex("button_play.png");
            _playButton.HoverTexture = LoadTex("button_play_hover.png");
            _playButton.PressedTexture = LoadTex("button_play_pressed.png");
        }

        _playButton.ShowBase();
    }

    public void UpdateState(MusicRadioBoundUserInterfaceState state)
    {
        _trackList.Clear();
        for (var i = 0; i < state.TrackNames.Count; i++)
        {
            var label = i == state.CurrentTrack
                ? "▶ " + state.TrackNames[i]
                : "    " + state.TrackNames[i];

            _trackList.AddItem(label);
        }

        _trackLabel.Text = state.CurrentTrack >= 0 && state.CurrentTrack < state.TrackNames.Count
            ? state.TrackNames[state.CurrentTrack]
            : "---";

        _statusLabel.Text = state.Playing
            ? Loc.GetString("music-radio-ui-playing")
            : Loc.GetString("music-radio-ui-stopped");

        UpdatePlayButtonTexture(state.Playing);
    }
}
