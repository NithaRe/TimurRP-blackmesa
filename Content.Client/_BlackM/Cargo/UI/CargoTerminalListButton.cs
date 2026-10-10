using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.Cargo.UI;

/// <summary>A whole selectable table row, including its trailing price or disclosure icon.</summary>
internal sealed class CargoTerminalListButton : ContainerButton
{
    private readonly Label _name;
    private readonly Label? _price;
    private readonly TextureRect? _arrow;
    private readonly RichTextLabel? _categoryName;
    private readonly string _text;

    public CargoTerminalListButton(string name, Texture? icon = null, string? price = null, Texture? arrow = null)
    {
        _text = name;
        ToggleMode = true;
        MinHeight = 34;
        var row = new BoxContainer { SeparationOverride = 8 };
        if (icon != null)
            row.AddChild(new TextureRect { Texture = icon, SetSize = new(24, 24), CanShrink = true, Stretch = TextureRect.StretchMode.KeepAspectCentered });
        _name = new Label { Text = name, ClipText = true, HorizontalExpand = true };
        row.AddChild(_name);
        if (price != null)
        {
            _price = new Label { Text = price, SetWidth = 82, Align = Label.AlignMode.Right };
            row.AddChild(_price);
        }
        if (arrow != null)
        {
            _name.Visible = false;
            _categoryName = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            row.AddChild(_categoryName);
            _arrow = new TextureRect { Texture = arrow, SetSize = new(12, 12), CanShrink = true, Stretch = TextureRect.StretchMode.KeepAspectCentered, VerticalAlignment = VAlignment.Center };
            row.AddChild(_arrow);
        }
        AddChild(row);
        UpdateColors();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        // Base constructors invoke this before the row is created.
        if (_name != null)
            UpdateColors();
    }

    private void UpdateColors()
    {
        var color = Pressed ? StyleNano.TerminalBlack : StyleNano.TerminalGreen;
        _name.FontColorOverride = color;
        _categoryName?.SetMessage(_text, defaultColor: color);
        if (_price != null)
            _price.FontColorOverride = color;
        if (_arrow != null)
            _arrow.ModulateSelfOverride = color;
    }
}
