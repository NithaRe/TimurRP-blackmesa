using System.Numerics;
using Content.Client.Humanoid;
using Content.Client.Inventory;
using Content.Shared._BlackM.CityScanner;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._BlackM.CityScanner;

public sealed class ScannerPhotoWindow : DefaultWindow
{
    private static readonly Color PaperColor = Color.FromHex("#F2EFE6");
    private static readonly Color FrameColor = Color.FromHex("#2B2A28");
    private static readonly Color StampColor = Color.FromHex("#A3271E");
    private static readonly Color BackdropColor = Color.FromHex("#B9C0C6");

    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _proto;

    private readonly SpriteView _view;
    private readonly Label _unknown;
    private readonly Label _subject;

    private EntityUid? _dummy;

    public ScannerPhotoWindow()
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _proto = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("bm-scanner-photo-window-title");
        Resizable = false;

        var header = new Label
        {
            Text = Loc.GetString("bm-scanner-photo-header"),
            FontColorOverride = StampColor,
            HorizontalAlignment = HAlignment.Center,
        };

        _view = new SpriteView
        {
            OverrideDirection = Direction.South,
            Scale = new Vector2(6f, 6f),
            SetSize = new Vector2(240f, 240f),
            MouseFilter = MouseFilterMode.Ignore,
        };

        _unknown = new Label
        {
            Text = "?",
            FontColorOverride = FrameColor,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = BackdropColor },
        };
        backdrop.AddChild(_view);
        backdrop.AddChild(_unknown);

        var frame = new PanelContainer
        {
            HorizontalAlignment = HAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = FrameColor,
                ContentMarginLeftOverride = 2,
                ContentMarginRightOverride = 2,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            },
        };
        frame.AddChild(backdrop);

        _subject = new Label
        {
            FontColorOverride = FrameColor,
            HorizontalAlignment = HAlignment.Center,
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 244,
        };
        column.AddChild(header);
        column.AddChild(frame);
        column.AddChild(_subject);

        var paper = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PaperColor,
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 10,
                ContentMarginBottomOverride = 12,
            },
        };
        paper.AddChild(column);
        Contents.AddChild(paper);

        OnClose += ClearDummy;
    }

    public void SetPhoto(ScannerPhotoComponent photo)
    {
        _subject.Text = photo.Subject;

        ClearDummy();
        if (photo.Snapshot != null)
            _dummy = BuildDummy(photo.Snapshot);

        _view.SetEntity(_dummy);
        _unknown.Visible = _dummy == null;
    }

    // Recreates the subject as a client-side entity, like the lobby preview does.
    private EntityUid? BuildDummy(ScannerSnapshot snap)
    {
        EntityUid dummy;

        if (snap.Species is { } species && _proto.TryIndex(species, out var speciesProto))
        {
            dummy = _entMan.SpawnEntity(speciesProto.DollPrototype, MapCoordinates.Nullspace);

            if (_entMan.TryGetComponent<HumanoidAppearanceComponent>(dummy, out var humanoid) &&
                _entMan.TryGetComponent<SpriteComponent>(dummy, out var sprite))
            {
                humanoid.Species = species;
                humanoid.Sex = snap.Sex;
                humanoid.Gender = snap.Gender;
                humanoid.Age = snap.Age;
                humanoid.SkinColor = snap.SkinColor;
                humanoid.EyeColor = snap.EyeColor;
                humanoid.Height = snap.Height;
                humanoid.Width = snap.Width;
                humanoid.MarkingSet = new MarkingSet(snap.Markings);
                humanoid.CustomBaseLayers = new(snap.CustomBaseLayers);
                humanoid.PermanentlyHidden = new HashSet<HumanoidVisualLayers>();
                humanoid.HiddenLayers = new Dictionary<HumanoidVisualLayers, SlotFlags>();

                _entMan.System<HumanoidAppearanceSystem>().UpdateSprite((dummy, humanoid, sprite));
            }
        }
        else if (snap.Prototype != null && _proto.HasIndex<EntityPrototype>(snap.Prototype))
        {
            dummy = _entMan.SpawnEntity(snap.Prototype, MapCoordinates.Nullspace);
        }
        else
        {
            return null;
        }

        var inventory = _entMan.System<ClientInventorySystem>();
        foreach (var (slot, itemProto) in snap.Equipment)
        {
            if (!_proto.HasIndex<EntityPrototype>(itemProto))
                continue;

            var item = _entMan.SpawnEntity(itemProto, MapCoordinates.Nullspace);
            if (!inventory.TryEquip(dummy, item, slot, true, true))
                _entMan.DeleteEntity(item);
        }

        return dummy;
    }

    private void ClearDummy()
    {
        if (_dummy is { } dummy && !_entMan.Deleted(dummy))
            _entMan.DeleteEntity(dummy);

        _dummy = null;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            ClearDummy();
    }
}
