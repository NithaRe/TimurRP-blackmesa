using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._BlackM.ZenIntervention;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.ZenIntervention;

public sealed class ZenInterventionUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    private const float BarHeight = 42f;
    private const float BarWidth = 340f;
    private const float GapFromToolbar = 12f;

    [Dependency] private readonly IEntityManager _entityManager = default!;

    private ZenInterventionBarControl? _bar;

    private GameTopMenuBar? TopMenuBar => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>();

    public void OnStateEntered(GameplayState state)
    {
        _bar = new ZenInterventionBarControl
        {
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        LayoutContainer.SetAnchorLeft(_bar, 0f);
        LayoutContainer.SetAnchorRight(_bar, 0f);
        LayoutContainer.SetAnchorTop(_bar, 0f);
        LayoutContainer.SetAnchorBottom(_bar, 0f);

        UIManager.PopupRoot.AddChild(_bar);
    }

    public void OnStateExited(GameplayState state)
    {
        _bar?.Orphan();
        _bar = null;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_bar == null)
            return;

        RepositionBar();

        var query = _entityManager.EntityQueryEnumerator<ZenInterventionComponent>();
        if (query.MoveNext(out _, out var comp))
        {
            _bar.BreachFraction = comp.MaxLevel > 0f ? comp.BreachThreshold / comp.MaxLevel : 0.6f;
            _bar.SetProgress(comp.Level, comp.MaxLevel);
        }
    }

    private void RepositionBar()
    {
        if (_bar == null)
            return;

        var topBar = TopMenuBar;
        if (topBar == null || !topBar.Visible)
        {
            _bar.Visible = false;
            return;
        }

        if (IsAnyWindowOpen())
        {
            _bar.Visible = false;
            return;
        }

        _bar.Visible = true;

        var pos = topBar.GlobalPosition;
        var toolbarHeight = topBar.Height;

        var x = pos.X + topBar.Width + GapFromToolbar;
        var y = pos.Y + (toolbarHeight - BarHeight) / 2f;
        if (y < 0f)
            y = 0f;

        LayoutContainer.SetMarginLeft(_bar, x);
        LayoutContainer.SetMarginRight(_bar, x + BarWidth);
        LayoutContainer.SetMarginTop(_bar, y);
        LayoutContainer.SetMarginBottom(_bar, y + BarHeight);
    }

    private bool IsAnyWindowOpen()
    {
        foreach (var child in UIManager.WindowRoot.Children)
        {
            if (child is BaseWindow { Visible: true })
                return true;
        }

        return false;
    }
}
