// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using Content.Goobstation.Shared.PhraseWheel;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared.Input;
using Robust.Client.Input;

namespace Content.Client.UserInterface.Systems.PhraseWheel;

[UsedImplicitly]
public sealed class PhraseWheelUIController : UIController, IOnStateChanged<GameplayState>
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;

    private MenuButton? PhraseButton =>
        UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.PhraseWheelButton;

    private PhraseWheelWindow? _window;

    public void OnStateEntered(GameplayState state)
    {
        // Хоткей — клавиша открывает/закрывает меню
    }

    public void OnStateExited(GameplayState state)
    {
        CloseWindow();
    }

    public void LoadButton()
    {
        if (PhraseButton == null) return;
        PhraseButton.OnPressed += OnButtonPressed;
        UpdateButtonVisibility();
    }

    public void UnloadButton()
    {
        if (PhraseButton == null) return;
        PhraseButton.OnPressed -= OnButtonPressed;
    }

    public void UpdateButtonVisibility()
    {
        if (PhraseButton == null) return;
        var player = _playerManager.LocalSession?.AttachedEntity;
        PhraseButton.Visible = player.HasValue &&
                               _entityManager.HasComponent<PhraseWheelComponent>(player.Value);
    }

    private void OnButtonPressed(BaseButton.ButtonEventArgs args) => ToggleWindow();

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

        // Фильтруем фразы по разрешённым категориям
        var allPhrases = _prototypeManager.EnumeratePrototypes<PhraseWheelEntryPrototype>();
        var filtered = comp.AllowedCategories.Count == 0
            ? allPhrases
            : allPhrases.Where(p => comp.AllowedCategories.Contains(p.Category));

        _window = new PhraseWheelWindow(filtered, _resCache);
        _window.OnPhraseSelected += HandlePhraseSelected;
        _window.OnClose += OnWindowClosed;
        _window.OnOpen += OnWindowOpen;
        _window.OpenCentered();
    }

    private void HandlePhraseSelected(PhraseWheelEntryPrototype phrase)
    {
        _entityManager.RaisePredictiveEvent(new PlayPhraseWheelMessage { PhraseId = phrase.ID });
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
        _window.OnClose -= OnWindowClosed;
        _window.OnOpen -= OnWindowOpen;
        _window.Dispose();
        _window = null;
        if (PhraseButton != null) PhraseButton.SetClickPressed(false);
    }
}

