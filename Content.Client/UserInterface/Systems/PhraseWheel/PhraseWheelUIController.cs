using System.Linq;
using Content.Client._BlackM.PhraseWheel;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._BlackM.PhraseWheel;
using JetBrains.Annotations;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.PhraseWheel;

[UsedImplicitly]
public sealed class PhraseWheelUIController : UIController, IOnStateChanged<GameplayState>
{
    private const string RecentId = "__recent";
    private const int MaxRecent = 8;
    private static readonly Color RecentAccent = new(0.55f, 0.55f, 0.62f);

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;

    [Dependency] private readonly IEntitySystemManager _systems = default!;

    private PhraseWheelClientSystem _phraseSystem => _systems.GetEntitySystem<PhraseWheelClientSystem>();

    private PhraseWheelWindow? _window;
    private MenuButton? PhraseButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.PhraseWheelButton;

    private bool _stateActive;
    private Color? _lastCustomColor;
    private string? _lastCategoryId;
    private readonly LinkedList<string> _recentPhraseIds = new();

    public void OnStateEntered(GameplayState state)
    {
        _stateActive = true;
        LoadButton();
    }

    public void OnStateExited(GameplayState state)
    {
        _stateActive = false;
        UnloadButton();
        CloseWindow();
    }

    public void LoadButton()
    {
        if (!_stateActive)
            return;

        if (PhraseButton == null)
        {
            Timer.Spawn(100, LoadButton);
            return;
        }

        PhraseButton.OnPressed += OnButtonPressed;
        UpdateButtonVisibility();
    }

    public void UnloadButton()
    {
        if (PhraseButton != null)
            PhraseButton.OnPressed -= OnButtonPressed;
    }

    public void HandleAttachedEntityChanged()
    {
        CloseWindow();
        UpdateButtonVisibility();
    }

    public void UpdateButtonVisibility()
    {
        if (PhraseButton == null)
            return;

        PhraseButton.Visible = _phraseSystem.TryGetLocal(out var uid, out _) && _phraseSystem.CanAct(uid);
    }

    public void ForceClose() => CloseWindow();

    private void OnButtonPressed(BaseButton.ButtonEventArgs args) => ToggleWindow();

    public void ToggleWindowFromKeybind() => ToggleWindow();

    private void ToggleWindow()
    {
        if (_window != null)
        {
            CloseWindow();
            return;
        }

        if (!_phraseSystem.TryGetLocal(out var uid, out var comp) || !_phraseSystem.CanAct(uid))
            return;

        var categories = BuildCategories(comp);
        if (categories.Count == 0)
            return;

        var initial = categories.FindIndex(c => c.Id == _lastCategoryId);
        if (initial < 0)
            initial = categories[0].Phrases.Count == 0 && categories.Count > 1 ? 1 : 0;

        _window = new PhraseWheelWindow(categories, _resCache, _phraseSystem.GetLocalCooldownSeconds,
            _lastCustomColor, initial);

        _window.OnPhraseSelected += HandlePhraseSelected;
        _window.OnColorChanged += color => _lastCustomColor = color;
        _window.OnClose += OnWindowClosed;

        _window.OpenCentered();
        PhraseButton?.SetClickPressed(true);
    }

    private void OnWindowClosed()
    {
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _lastCategoryId = _window.ActiveCategoryId;

        var window = _window;
        _window = null;

        window.OnPhraseSelected -= HandlePhraseSelected;
        window.OnClose -= OnWindowClosed;
        window.Dispose();

        PhraseButton?.SetClickPressed(false);
    }

    private void HandlePhraseSelected(PhraseWheelEntryPrototype phrase, Color? customColor)
    {
        if (!_phraseSystem.TryRequestPlay(phrase, customColor))
            return;

        _recentPhraseIds.Remove(phrase.ID);
        _recentPhraseIds.AddFirst(phrase.ID);
        while (_recentPhraseIds.Count > MaxRecent)
            _recentPhraseIds.RemoveLast();

        CloseWindow();
    }

    private List<PhraseWheelCategoryView> BuildCategories(PhraseWheelComponent comp)
    {
        var result = new List<PhraseWheelCategoryView>();

        var recent = _recentPhraseIds
            .Select(id => _prototypeManager.TryIndex<PhraseWheelEntryPrototype>(id, out var p) ? p : null)
            .Where(p => p != null && _phraseSystem.IsCategoryAllowed(comp, p.Category))
            .Select(p => p!)
            .ToList();
        result.Add(new PhraseWheelCategoryView(RecentId, Loc.GetString("phrase-wheel-recent"), null, RecentAccent, recent));

        var groups = _prototypeManager.EnumeratePrototypes<PhraseWheelEntryPrototype>()
            .Where(p => _phraseSystem.IsCategoryAllowed(comp, p.Category))
            .GroupBy(p => p.Category.Id);

        var real = new List<(PhraseWheelCategoryPrototype Proto, List<PhraseWheelEntryPrototype> Phrases)>();
        foreach (var group in groups)
        {
            if (!_prototypeManager.TryIndex<PhraseWheelCategoryPrototype>(group.Key, out var proto))
                continue;

            var phrases = group.OrderBy(p => p.Order).ThenBy(p => p.ID).ToList();
            real.Add((proto, phrases));
        }

        foreach (var (proto, phrases) in real
                     .OrderBy(r => r.Proto.Order)
                     .ThenBy(r => r.Proto.ID))
        {
            var name = string.IsNullOrEmpty(proto.Name) ? proto.ID : Loc.GetString(proto.Name);
            result.Add(new PhraseWheelCategoryView(proto.ID, name, proto.Icon, proto.Color, phrases));
        }

        return result;
    }
}
