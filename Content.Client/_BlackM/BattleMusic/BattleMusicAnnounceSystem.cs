using Content.Shared._BlackM.BattleMusic;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Client._BlackM.BattleMusic;

public sealed class BattleMusicAnnounceSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;

    private const float ShowDuration = 3.5f;
    private const float FadeInDuration = 0.35f;
    private const float FadeOutDuration = 1f;

    private Label? _label;
    private float _timer;
    private bool _visible;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BattleMusicAnnounceMessage>(OnAnnounce);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _label?.Parent?.Orphan();
        _label = null;
    }

    private void OnAnnounce(BattleMusicAnnounceMessage msg)
    {
        EnsureLabel();

        _label!.Text = $"БОЙ: {msg.Attacker} vs {msg.Defender}";
        _label.Modulate = new Color(1f, 1f, 1f, 0f);
        _label.Visible = true;
        _timer = ShowDuration;
        _visible = true;
    }

    private void EnsureLabel()
    {
        if (_label != null)
            return;

        var font = new VectorFont(
            _resCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 22);

        _label = new Label
        {
            Text = "",
            HorizontalAlignment = Control.HAlignment.Center,
            FontColorOverride = new Color(255, 60, 60),
            FontOverride = font,
            ShadowOffsetXOverride = 2,
            ShadowOffsetYOverride = 2,
            FontColorShadowOverride = Color.Black,
        };

        var container = new PanelContainer
        {
            HorizontalExpand = true,
            MouseFilter = Control.MouseFilterMode.Ignore,
            Children = { _label },
        };

        _ui.PopupRoot.AddChild(container);

        LayoutContainer.SetAnchorLeft(container, 0f);
        LayoutContainer.SetAnchorRight(container, 1f);
        LayoutContainer.SetAnchorTop(container, 0.58f);
        LayoutContainer.SetAnchorBottom(container, 0.58f);
        LayoutContainer.SetMarginTop(container, 0f);
        LayoutContainer.SetMarginBottom(container, 0f);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_visible || _label == null)
            return;

        _timer -= frameTime;

        var elapsed = ShowDuration - _timer;

        float alpha;
        if (elapsed < FadeInDuration)
        {
            alpha = Math.Clamp(elapsed / FadeInDuration, 0f, 1f);
        }
        else if (_timer <= FadeOutDuration)
        {
            alpha = Math.Clamp(_timer / FadeOutDuration, 0f, 1f);
        }
        else
        {
            alpha = 1f;
        }

        _label.Modulate = new Color(1f, 1f, 1f, alpha);

        if (_timer <= 0f)
        {
            _label.Visible = false;
            _visible = false;
        }
    }
}
