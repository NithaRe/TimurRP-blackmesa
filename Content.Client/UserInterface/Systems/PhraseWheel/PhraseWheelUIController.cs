using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Shared._BlackM.PhraseWheel;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;

// IMPORTANT: The namespace is intentionally Content.Client.UserInterface.Systems.PhraseWheel
// so that GameTopMenuBarUIController can find this class without engine changes.
namespace Content.Client.UserInterface.Systems.PhraseWheel;

[UsedImplicitly]
public sealed class PhraseWheelUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _cooldownUntil = TimeSpan.Zero;

    private MenuButton? PhraseButton =>
        UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.PhraseWheelButton;

    private PhraseWheelWindow? _window;
    private bool _buttonSubscribed = false;
    private string? _lastCustomColor;
    private EntityUid? _lastAttachedEntity;

    private readonly LinkedList<string> _recentPhraseIds = new();
    private const int MaxRecent = 8;

    public void OnStateEntered(GameplayState state) => LoadButton();

    public void OnStateExited(GameplayState state)
    {
        UnloadButton();
        CloseWindow();
    }

    public void LoadButton()
    {
        if (PhraseButton == null)
        {
            Timer.Spawn(100, LoadButton);
            return;
        }
        if (!_buttonSubscribed)
        {
            PhraseButton.OnPressed += OnButtonPressed;
            _buttonSubscribed = true;
        }
        UpdateButtonVisibility();
    }

    public void UnloadButton()
    {
        if (PhraseButton == null) return;
        PhraseButton.OnPressed -= OnButtonPressed;
        _buttonSubscribed = false;
    }

    public void UpdateButtonVisibility()
    {
        if (PhraseButton == null) return;

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (!player.HasValue || !_entityManager.HasComponent<PhraseWheelComponent>(player.Value))
        {
            PhraseButton.Visible = false;
            return;
        }

        var alive = true;
        if (_entityManager.TryGetComponent<MobStateComponent>(player.Value, out var mobState))
            alive = mobState.CurrentState == MobState.Alive;

        PhraseButton.Visible = alive;
    }

    public void ForceClose()
    {
        CloseWindow();
        if (PhraseButton != null)
            PhraseButton.Visible = false;
    }

    public void HandleAttachedEntityChanged(EntityUid? newUid)
    {
        if (_lastAttachedEntity == newUid) return;
        _lastAttachedEntity = newUid;
        CloseWindow();
        UpdateButtonVisibility();
    }

    private void OnButtonPressed(BaseButton.ButtonEventArgs args) => ToggleWindow();

    public void ToggleWindowFromKeybind() => ToggleWindow();

    private void ToggleWindow()
    {
        if (_window != null)
        {
            CloseWindow();
            return;
        }

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null || !_entityManager.TryGetComponent<PhraseWheelComponent>(player.Value, out var comp))
            return;

        if (_entityManager.TryGetComponent<MobStateComponent>(player.Value, out var mobState)
            && mobState.CurrentState != MobState.Alive)
            return;

        var allPhrases = _prototypeManager.EnumeratePrototypes<PhraseWheelEntryPrototype>();
        var filtered = comp.AllowedCategories.Count == 0
            ? allPhrases
            : allPhrases.Where(p => comp.AllowedCategories.Contains(p.Category));

        var recentPhrases = _recentPhraseIds
            .Select(id => _prototypeManager.TryIndex<PhraseWheelEntryPrototype>(id, out var p) ? p : null)
            .Where(p => p != null)
            .Select(p => p!);

        _window = new PhraseWheelWindow(filtered, _resCache, _lastCustomColor, recentPhrases);
        _window.OnPhraseSelected += HandlePhraseSelected;
        _window.OnColorChanged += HandleColorChanged;
        _window.OnClose += OnWindowClosed;
        _window.OnOpen += OnWindowOpen;
        _window.OpenCentered();

        var remaining = _cooldownUntil - _timing.CurTime;
        if (remaining > TimeSpan.Zero)
        {
            _window.SetPhraseButtonsEnabled(false);
            Timer.Spawn(remaining, () => _window?.SetPhraseButtonsEnabled(true));
        }
    }

    private void HandlePhraseSelected(PhraseWheelEntryPrototype phrase, string? customColor)
    {
        _entityManager.RaisePredictiveEvent(new PlayPhraseWheelMessage
        {
            PhraseId = phrase.ID,
            CustomColor = customColor,
        });

        _recentPhraseIds.Remove(phrase.ID);
        _recentPhraseIds.AddFirst(phrase.ID);
        while (_recentPhraseIds.Count > MaxRecent)
            _recentPhraseIds.RemoveLast();

        var recentPhrases = _recentPhraseIds
            .Select(id => _prototypeManager.TryIndex<PhraseWheelEntryPrototype>(id, out var p) ? p : null)
            .Where(p => p != null)
            .Select(p => p!);

        _window?.UpdateRecentTab(recentPhrases);

        _cooldownUntil = _timing.CurTime + PhraseWheelConstants.UseCooldown;
        _window?.SetPhraseButtonsEnabled(false);
        Timer.Spawn(PhraseWheelConstants.UseCooldown, () => _window?.SetPhraseButtonsEnabled(true));
    }

    private void HandleColorChanged(string? color)
    {
        _lastCustomColor = color;
    }

    private void OnWindowClosed()
    {
        if (PhraseButton != null) PhraseButton.Pressed = false;
        CloseWindow();
    }

    private void OnWindowOpen()
    {
        if (PhraseButton != null) PhraseButton.Pressed = true;
    }

    private void CloseWindow()
    {
        if (_window == null) return;
        _window.OnPhraseSelected -= HandlePhraseSelected;
        _window.OnColorChanged -= HandleColorChanged;
        _window.OnClose -= OnWindowClosed;
        _window.OnOpen -= OnWindowOpen;
        _window.Dispose();
        _window = null;
        if (PhraseButton != null) PhraseButton.SetClickPressed(false);
    }
}
