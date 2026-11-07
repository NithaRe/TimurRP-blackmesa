using System.Numerics;
using Content.Shared._BlackM.ZenCatalyst;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.ZenIntervention;

public sealed class ZenCatalystIndicatorControl : PanelContainer
{
    public const float DesignWidth = ZenInterventionBarControl.DesignWidth;
    public const float DesignHeight = ZenInterventionBarControl.DesignHeight;

    private static readonly Color ActiveColor = ZenInterventionPalette.Calm;
    private static readonly Color InactiveColor = Color.FromHex("#7B7E9E");
    private static readonly Color EmptyCrystal = Color.FromHex("#666B8A");
    private static readonly Color HoverColor = Color.FromHex("#9699BB");
    [Dependency] private readonly IResourceCache _resources = default!;

    private readonly Texture _icon;
    private readonly StyleBoxFlat _panelStyle;
    private bool _hovered;

    public IReadOnlyList<ZenCatalystWaveStatus> Catalysts { get; private set; } = Array.Empty<ZenCatalystWaveStatus>();
    public bool Active => Catalysts.Count > 0;

    public ZenCatalystIndicatorControl()
    {
        IoCManager.InjectDependencies(this);
        MinSize = new Vector2(DesignWidth, DesignHeight);
        MouseFilter = MouseFilterMode.Stop;
        TooltipDelay = 0.1f;
        TooltipSupplier = _ => new ZenCatalystTooltip(this);
        _icon = _resources.GetResource<TextureResource>("/Textures/_BlackM/Interface/ZenIntervention/zen_catalyst_hud.png").Texture;
        _panelStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#3C4350"),
            BorderColor = Color.FromHex("#242830"),
            BorderThickness = new Thickness(1),
        };
        PanelOverride = _panelStyle;
    }

    public void SetState(IReadOnlyList<ZenCatalystWaveStatus> catalysts)
    {
        Catalysts = catalysts;
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
        _panelStyle.BackgroundColor = Color.FromHex("#4A5266");
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
        _panelStyle.BackgroundColor = Color.FromHex("#3C4350");
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var scale = MathF.Min(PixelSize.X / DesignWidth, PixelSize.Y / DesignHeight);
        if (scale <= 0)
            return;

        UIBox2 B(float left, float top, float right, float bottom) => new(
            MathF.Round(left * scale), MathF.Round(top * scale),
            MathF.Round(right * scale), MathF.Round(bottom * scale));

        var iconColor = Active ? ActiveColor : _hovered ? HoverColor : EmptyCrystal;
        var iconLeft = ZenInterventionBarControl.IconLeft;
        var iconTop = ZenInterventionBarControl.IconTop;
        var iconSize = ZenInterventionBarControl.IconSize;
        handle.DrawTextureRect(_icon, B(iconLeft, iconTop, iconLeft + iconSize, iconTop + iconSize), iconColor);

        var statusY = ZenInterventionBarControl.DigitsTop + ZenInterventionBarControl.DigitCellH / 2f;
        var statusX = DesignWidth / 2f;
        handle.DrawRect(B(statusX - 6, statusY - 2, statusX + 6, statusY + 2), Active ? ActiveColor : InactiveColor);
    }
}
