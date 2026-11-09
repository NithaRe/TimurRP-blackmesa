using Content.Shared._BlackM.CityScanner;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.CityScanner;

public sealed class ScannerPhotoBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ScannerPhotoWindow? _window;

    public ScannerPhotoBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ScannerPhotoWindow>();
        if (EntMan.TryGetComponent<ScannerPhotoComponent>(Owner, out var photo))
            _window.SetPhoto(photo);
    }
}
