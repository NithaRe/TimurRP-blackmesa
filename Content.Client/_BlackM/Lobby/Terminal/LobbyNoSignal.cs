using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class LobbyNoSignal : Control
{
    private const string RsiPath = "/Textures/_BlackM/Interface/Lobby/signal_logo.rsi";

    private const int FrameWidth = 96;
    private const int FrameHeight = 96;
    private const float SpriteScale = 3f;
    private const float RightPanelShare = 0.26f;
    private const float TopShare = 0.14f;
    private const float BottomShare = 0.04f;

    private const float AcquiredHold = 1.0f;
    private const float FadeTime = 0.6f;

    private readonly AnimatedTextureRect _sprite;
    private bool _acquired;
    private float _timer;

    public LobbyNoSignal()
    {
        MouseFilter = MouseFilterMode.Ignore;

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#060504") },
            HorizontalExpand = true,
            VerticalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        });

        _sprite = new AnimatedTextureRect
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Top,
            MinSize = new Vector2(FrameWidth * SpriteScale, FrameHeight * SpriteScale),
            MouseFilter = MouseFilterMode.Ignore,
        };

        _sprite.DisplayRect.TextureScale = new Vector2(SpriteScale, SpriteScale);
        _sprite.DisplayRect.Stretch = TextureRect.StretchMode.KeepCentered;

        AddChild(_sprite);

        ShowSearching();
    }

    public void ShowSearching()
    {
        _acquired = false;
        _timer = 0f;
        Modulate = Color.White;
        Visible = true;
        SetState("searching");
    }

    public void Acquire()
    {
        if (_acquired || !Visible)
            return;

        _acquired = true;
        _timer = 0f;
        SetState("acquired");
    }

    private void SetState(string state)
    {
        _sprite.SetFromSpriteSpecifier(new SpriteSpecifier.Rsi(new ResPath(RsiPath), state));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var centerX = Size.X * (1f - RightPanelShare) * 0.5f;
        var centerY = Size.Y * (TopShare + (1f - BottomShare)) * 0.5f;
        _sprite.Margin = new Thickness(
            centerX - FrameWidth * SpriteScale * 0.5f,
            centerY - FrameHeight * SpriteScale * 0.5f,
            0,
            0);

        if (!_acquired)
            return;

        _timer += args.DeltaSeconds;
        if (_timer < AcquiredHold)
            return;

        var k = Math.Clamp((_timer - AcquiredHold) / FadeTime, 0f, 1f);
        Modulate = Color.White.WithAlpha(1f - k);

        if (k >= 1f)
            Visible = false;
    }
}
