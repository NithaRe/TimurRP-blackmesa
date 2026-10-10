using Content.Shared._BlackM.PhraseWheel;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.PhraseWheel;

public sealed class PhraseWheelWindow : BaseWindow
{
    public event Action<PhraseWheelEntryPrototype, Color?>? OnPhraseSelected;
    public event Action<Color?>? OnColorChanged;

    private readonly PhraseWheelRadial _radial;
    private Color? _customColor;

    public string ActiveCategoryId => _radial.ActiveCategoryId;

    public PhraseWheelWindow(
        IReadOnlyList<PhraseWheelCategoryView> categories,
        IResourceCache resCache,
        Func<float> remainingCooldown,
        Color? initialColor,
        int initialCategory)
    {
        _customColor = initialColor;

        _radial = new PhraseWheelRadial(categories, resCache, remainingCooldown, initialCategory)
        {
            HorizontalAlignment = HAlignment.Center,
        };
        _radial.OnPhraseSelected += phrase => OnPhraseSelected?.Invoke(phrase, _customColor);
        _radial.OnCloseRequested += Close;

        var colorBar = new PhraseWheelColorBar(initialColor);
        colorBar.ColorChanged += color =>
        {
            _customColor = color;
            OnColorChanged?.Invoke(color);
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };
        root.AddChild(_radial);
        root.AddChild(colorBar);
        AddChild(root);
    }
}
