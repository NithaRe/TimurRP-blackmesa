using System;
using System.Collections.Generic;
using Content.Shared._BlackM.Access;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client._BlackM.Access;

public sealed class BadgePrinterWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public event Action<List<string>, string>? OnPrintPressed;
    public event Action? OnEjectPressed;

    private readonly Label _cardInfoLabel;
    private readonly Label _cardStatusLabel;
    private readonly BoxContainer _badgeList;
    private readonly LineEdit _reasonEdit;
    private readonly Button _printButton;
    private readonly Button _ejectButton;

    private readonly Dictionary<string, CheckBox> _checkboxes = new();

    private static readonly Color AccentColor = Color.FromHex("#5fa8d3");
    private static readonly Color PanelBg = Color.FromHex("#1b1f24");
    private static readonly Color PanelBgLight = Color.FromHex("#242a31");

    public BadgePrinterWindow()
    {
        IoCManager.InjectDependencies(this);

        MinSize = new Vector2i(520, 520);
        SetSize = new Vector2i(560, 620);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };

        // --- Верхняя панель ---
        var headerPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PanelBgLight,
                BorderColor = AccentColor,
                BorderThickness = new Thickness(0, 0, 0, 2),
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8,
            }
        };

        var headerBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        var icon = new TextureRect
        {
            TextureScale = new Vector2(2f, 2f),
            VerticalAlignment = VAlignment.Center,
            Texture = LoadIcon("_BlackM/Objects/Misc/access_card_holder.rsi", "icon"),
        };

        var infoBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };

        _cardStatusLabel = new Label
        {
            Text = Loc.GetString("badge-printer-no-card"),
            FontColorOverride = Color.Gray,
        };

        _cardInfoLabel = new Label
        {
            Text = string.Empty,
        };

        infoBox.AddChild(_cardStatusLabel);
        infoBox.AddChild(_cardInfoLabel);

        _ejectButton = new Button
        {
            Text = Loc.GetString("badge-printer-eject"),
            VerticalAlignment = VAlignment.Center,
            Disabled = true,
        };
        _ejectButton.OnPressed += _ => OnEjectPressed?.Invoke();

        headerBox.AddChild(icon);
        headerBox.AddChild(infoBox);
        headerBox.AddChild(_ejectButton);
        headerPanel.AddChild(headerBox);

        // --- Список значков ---
        var listPanel = new PanelContainer
        {
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PanelBg,
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6,
            }
        };

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _badgeList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        scroll.AddChild(_badgeList);
        listPanel.AddChild(scroll);

        // --- Причина выдачи ---
        var reasonLabel = new Label { Text = Loc.GetString("badge-printer-reason-label") };
        _reasonEdit = new LineEdit
        {
            PlaceHolder = Loc.GetString("badge-printer-reason-placeholder"),
            HorizontalExpand = true,
        };

        // --- Кнопка печати ---
        _printButton = new Button
        {
            Text = Loc.GetString("badge-printer-print-button"),
            HorizontalExpand = true,
        };
        _printButton.OnPressed += _ =>
        {
            var selected = new List<string>();
            foreach (var (protoId, box) in _checkboxes)
            {
                if (box.Pressed)
                    selected.Add(protoId);
            }

            OnPrintPressed?.Invoke(selected, _reasonEdit.Text);
        };

        root.AddChild(headerPanel);
        root.AddChild(listPanel);
        root.AddChild(reasonLabel);
        root.AddChild(_reasonEdit);
        root.AddChild(_printButton);

        Contents.AddChild(root);
    }

    public void UpdateState(BadgePrinterBuiState state)
    {
        _cardStatusLabel.Text = state.HasCard
            ? Loc.GetString("badge-printer-card-inserted")
            : Loc.GetString("badge-printer-no-card");
        _cardStatusLabel.FontColorOverride = state.HasCard ? AccentColor : Color.Gray;

        _cardInfoLabel.Text = state.HasCard
            ? Loc.GetString("badge-printer-card-owner", ("name", state.HolderName ?? "?"), ("job", state.HolderJob ?? "?"))
            : string.Empty;

        _ejectButton.Disabled = !state.HasCard;
        _printButton.Disabled = !state.HasCard;

        var previouslyChecked = new HashSet<string>();
        foreach (var (id, box) in _checkboxes)
        {
            if (box.Pressed)
                previouslyChecked.Add(id);
        }

        _badgeList.RemoveAllChildren();
        _checkboxes.Clear();

        foreach (var option in state.Options)
        {
            var row = BuildBadgeRow(option, previouslyChecked.Contains(option.ProtoId));
            _badgeList.AddChild(row);
        }
    }

    private Control BuildBadgeRow(BadgePrinterOptionData option, bool isChecked)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PanelBgLight,
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            }
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        var icon = new TextureRect
        {
            Texture = LoadIcon(option.IconRsi, option.IconState),
            TextureScale = new Vector2(2f, 2f),
            VerticalAlignment = VAlignment.Center,
        };

        var textBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            MinWidth = 0,
        };

        textBox.AddChild(new Label { Text = option.Name, ClipText = true });
        if (!string.IsNullOrWhiteSpace(option.Description))
        {
            textBox.AddChild(new Label
            {
                Text = option.Description,
                FontColorOverride = Color.DarkGray,
                ClipText = true,
            });
        }

        var checkBox = new CheckBox
        {
            Pressed = isChecked,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = false,
            MinWidth = 24,
        };
        _checkboxes[option.ProtoId] = checkBox;

        row.AddChild(icon);
        row.AddChild(textBox);
        row.AddChild(checkBox);
        panel.AddChild(row);

        return panel;
    }

    private Texture? LoadIcon(string rsiPath, string state)
    {
        try
        {
            var specifier = new SpriteSpecifier.Rsi(new ResPath(rsiPath), state);
            return _entMan.System<SpriteSystem>().Frame0(specifier);
        }
        catch
        {
            return null;
        }
    }
}
