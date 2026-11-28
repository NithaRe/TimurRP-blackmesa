using System.Numerics;
using Content.Client.Lathe;
using Content.Client.Materials;
using Content.Client.Materials.UI;
using Content.Client.Power.EntitySystems;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Ams.UI;

public sealed class AmsPartFabricatorWindow : DefaultWindow
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceCache _resources = default!;

    private static readonly Color Amber = Color.FromHex("#D7AE66");
    private static readonly Color Green = Color.FromHex("#81B79A");
    private static readonly Color Red = Color.FromHex("#DB7165");
    private readonly MaterialStorageSystem _storage;
    private readonly LatheSystem _lathe;
    private readonly SpriteSystem _sprites;
    private readonly MaterialStorageUIController _materialUi;
    private readonly TextureRect _partIcon = new()
    {
        SetSize = new Vector2(192, 192),
        CanShrink = true,
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
        MouseFilter = MouseFilterMode.Ignore,
    };
    private readonly RichTextLabel _partName = new() { HorizontalExpand = true };
    private readonly Label _duration = new();
    private readonly BoxContainer _materialList = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        SeparationOverride = 8,
        HorizontalExpand = true,
    };
    private readonly Label _status = new() { HorizontalExpand = true, ClipText = true };
    private readonly Label _sets = new() { HorizontalExpand = true, ClipText = true };
    private readonly Label _queue = new() { ClipText = true };
    private readonly Button _fabricate = new() { HorizontalExpand = true, MinHeight = 34, Disabled = true };
    private readonly Button _clearQueue = new() { MinHeight = 34, Disabled = true };
    private readonly List<MaterialRow> _rows = new();
    private EntityUid? _owner;
    private LatheRecipePrototype? _recipe;
    private LatheUpdateState? _state;
    private float _refreshElapsed;

    public event Action<string>? Fabricate;
    public event Action? ClearQueue;

    public AmsPartFabricatorWindow()
    {
        IoCManager.InjectDependencies(this);
        _storage = _entities.System<MaterialStorageSystem>();
        _lathe = _entities.System<LatheSystem>();
        _sprites = _entities.System<SpriteSystem>();
        _materialUi = UserInterfaceManager.GetUIController<MaterialStorageUIController>();

        Title = Loc.GetString("ams-fabricator-title");
        MinSize = new Vector2(620, 390);
        SetSize = new Vector2(720, 520);
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(12),
        };
        root.AddChild(new Label { Text = Loc.GetString("ams-fabricator-heading"), FontColorOverride = Amber });
        root.AddChild(Rule());

        var body = new BoxContainer { SeparationOverride = 20, HorizontalExpand = true };
        var part = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            SetWidth = 192,
        };
        part.AddChild(_partIcon);
        part.AddChild(_partName);
        part.AddChild(_duration);
        body.AddChild(part);

        var materials = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            HorizontalExpand = true,
        };
        var headings = new BoxContainer { SeparationOverride = 8 };
        headings.AddChild(new Label { Text = Loc.GetString("ams-fabricator-material"), HorizontalExpand = true, ClipText = true });
        headings.AddChild(CountLabel(Loc.GetString("ams-fabricator-required")));
        headings.AddChild(CountLabel(Loc.GetString("ams-fabricator-stored")));
        headings.AddChild(new Control { SetWidth = 30 });
        materials.AddChild(headings);
        materials.AddChild(Rule());
        materials.AddChild(_materialList);
        body.AddChild(materials);
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(body);
        root.AddChild(scroll);
        root.AddChild(Rule());
        var summary = new BoxContainer { SeparationOverride = 12 };
        summary.AddChild(_sets);
        summary.AddChild(_queue);
        root.AddChild(summary);
        root.AddChild(_status);
        var commands = new BoxContainer { SeparationOverride = 8 };
        _fabricate.Text = Loc.GetString("ams-fabricator-fabricate");
        _clearQueue.Text = Loc.GetString("ams-fabricator-clear-queue");
        _clearQueue.ToolTip = Loc.GetString("ams-fabricator-clear-queue-tooltip");
        commands.AddChild(_fabricate);
        commands.AddChild(_clearQueue);
        root.AddChild(commands);
        Contents.AddChild(root);

        _fabricate.OnPressed += _ =>
        {
            if (_recipe != null)
                Fabricate?.Invoke(_recipe.ID);
        };
        _clearQueue.OnPressed += _ => ClearQueue?.Invoke();
    }

    public void SetEntity(EntityUid owner)
    {
        _owner = owner;
        if (_entities.TryGetComponent<MetaDataComponent>(owner, out var metadata))
            Title = metadata.EntityName;
    }

    public void UpdateState(LatheUpdateState state)
    {
        _state = state;
        var recipe = state.Recipes.Count == 1 ? _prototypes.Index(state.Recipes[0]) : null;
        if (_recipe == null || _recipe.ID != recipe?.ID)
        {
            _recipe = recipe;
            PopulateMaterials();
        }
        Refresh();
    }

    private void PopulateMaterials()
    {
        _materialList.DisposeAllChildren();
        _rows.Clear();
        _partIcon.Texture = null;
        if (_recipe == null)
        {
            _partName.SetMessage(Loc.GetString("ams-fabricator-no-recipe"));
            _duration.Text = string.Empty;
            return;
        }

        if (_recipe.Result is { } result)
        {
            var prototype = _prototypes.Index(result);
            _partName.SetMessage(prototype.Name);
            _partIcon.Texture = _sprites.GetPrototypeIcon(prototype).Default;
        }
        _duration.Text = Loc.GetString("ams-fabricator-duration", ("seconds", _recipe.CompleteTime.TotalSeconds));
        foreach (var (id, amount) in _recipe.Materials)
        {
            var material = _prototypes.Index(id);
            var row = new BoxContainer { SeparationOverride = 8, MinHeight = 34 };
            row.AddChild(new TextureRect
            {
                Texture = _sprites.Frame0(material.Icon),
                SetSize = new Vector2(24, 24),
                CanShrink = true,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MouseFilter = MouseFilterMode.Ignore,
            });
            var name = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            var materialName = new RichTextLabel { HorizontalExpand = true };
            materialName.SetMessage(Loc.GetString(material.Name));
            name.AddChild(materialName);
            name.AddChild(new Label { Text = Loc.GetString(material.Unit), FontColorOverride = Color.Gray });
            row.AddChild(name);
            var required = CountLabel();
            var stored = CountLabel();
            row.AddChild(required);
            row.AddChild(stored);
            var eject = new Button
            {
                SetWidth = 30,
                MinHeight = 30,
                ToolTip = Loc.GetString("ams-fabricator-eject", ("material", Loc.GetString(material.Name))),
            };
            eject.AddChild(new TextureRect
            {
                Texture = _resources.GetResource<TextureResource>("/Textures/Interface/VerbIcons/eject.svg.192dpi.png").Texture,
                SetSize = new Vector2(18, 18),
                CanShrink = true,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MouseFilter = MouseFilterMode.Ignore,
            });
            eject.OnPressed += _ =>
            {
                if (_owner is not { } owner)
                    return;
                var count = _storage.GetMaterialAmount(owner, material) / _storage.GetSheetVolume(material);
                if (count > 0)
                    _materialUi.SendLatheEjectMessage(owner, material.ID, count);
            };
            row.AddChild(eject);
            _materialList.AddChild(row);
            _rows.Add(new MaterialRow(material, amount, required, stored, eject));
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _refreshElapsed += args.DeltaSeconds;
        if (_refreshElapsed < 0.2f)
            return;
        _refreshElapsed = 0;
        Refresh();
    }

    private void Refresh()
    {
        if (_owner is not { } owner || _entities.Deleted(owner))
            return;

        var multiplier = _entities.TryGetComponent<LatheComponent>(owner, out var lathe) ? lathe.MaterialUseMultiplier : 1f;
        var sets = int.MaxValue;
        foreach (var row in _rows)
        {
            var cost = SharedLatheSystem.AdjustMaterial(row.Amount, _recipe!.ApplyMaterialDiscount, multiplier);
            var available = _storage.GetMaterialAmount(owner, row.Material);
            var volume = _storage.GetSheetVolume(row.Material);
            var unit = Loc.GetString(row.Material.Unit);
            row.Required.Text = ((double) cost / volume).ToString("0.##");
            row.Stored.Text = ((double) available / volume).ToString("0.##");
            row.Required.ToolTip = row.Stored.ToolTip = unit;
            row.Stored.FontColorOverride = available >= cost ? Green : Red;
            row.Eject.Disabled = available < volume;
            if (cost > 0)
                sets = Math.Min(sets, available / cost);
        }

        if (sets == int.MaxValue)
            sets = 0;
        var powered = _lathe.IsPowered(owner, _entities);
        var busy = _state?.CurrentlyProducing != null;
        var queued = _state?.Queue.Length ?? 0;
        _sets.Text = Loc.GetString("ams-fabricator-sets", ("count", sets));
        _queue.Text = Loc.GetString("ams-fabricator-queue", ("count", queued));
        var status = !powered ? "no-power" : _recipe == null ? "no-recipe" : busy ? "producing" : sets > 0 ? "ready" : "missing-materials";
        _status.Text = Loc.GetString("ams-fabricator-" + status);
        _status.FontColorOverride = !powered ? Red : busy ? Amber : sets > 0 ? Green : Color.Gray;
        _fabricate.Disabled = !powered || _recipe == null || sets == 0;
        _fabricate.Text = Loc.GetString(busy ? "ams-fabricator-enqueue" : "ams-fabricator-fabricate");
        _clearQueue.Disabled = queued == 0;
    }

    private static Label CountLabel(string text = "") => new()
    {
        Text = text,
        SetWidth = 76,
        Align = Label.AlignMode.Right,
        ClipText = true,
    };

    private static PanelContainer Rule() => new()
    {
        SetHeight = 1,
        HorizontalExpand = true,
        PanelOverride = new StyleBoxFlat(Color.FromHex("#45474B")),
    };

    private sealed record MaterialRow(MaterialPrototype Material, int Amount, Label Required, Label Stored, Button Eject);
}
