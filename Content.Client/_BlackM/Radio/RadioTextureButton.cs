using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._BlackM.Radio;

public sealed class RadioTextureButton : ContainerButton
{
    private readonly TextureRect _icon;

    public Texture? BaseTexture;
    public Texture? HoverTexture;
    public Texture? PressedTexture;

    public RadioTextureButton()
    {
        _icon = new TextureRect
        {
            Stretch = TextureRect.StretchMode.Scale,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_icon);

        OnMouseEntered += _ =>
        {
            if (HoverTexture != null)
                _icon.Texture = HoverTexture;
        };

        OnMouseExited += _ =>
        {
            if (BaseTexture != null)
                _icon.Texture = BaseTexture;
        };

        OnButtonDown += _ =>
        {
            if (PressedTexture != null)
                _icon.Texture = PressedTexture;
        };

        OnButtonUp += _ =>
        {
            if (HoverTexture != null)
                _icon.Texture = HoverTexture;
            else if (BaseTexture != null)
                _icon.Texture = BaseTexture;
        };
    }

    public void ShowBase()
    {
        if (BaseTexture != null)
            _icon.Texture = BaseTexture;
    }
}
