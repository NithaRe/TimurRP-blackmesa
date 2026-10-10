using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client._BlackM.Rules;

public sealed class RulesDossierStamp : Control
{
    private readonly Font _font;
    private readonly string[] _lines;

    public RulesDossierStamp()
    {
        var resources = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(resources.GetResource<FontResource>("/Fonts/RobotoMono/RobotoMono-Bold.ttf"), 20);
        _lines = new[]
        {
            Loc.GetString("blackm-arrival-stamp-top"),
            Loc.GetString("blackm-arrival-stamp-middle"),
            Loc.GetString("blackm-arrival-stamp-bottom"),
        };
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var oldTransform = handle.GetTransform();
        var center = (Vector2) PixelSize / 2;
        handle.SetTransform(Matrix3x2.CreateRotation(-0.12f) * Matrix3x2.CreateTranslation(center) * oldTransform);
        var ink = Color.FromHex("#B44850");
        var width = 218 * UIScale;
        var height = 128 * UIScale;
        var left = -width / 2;
        var top = -height / 2;

        // Broken ink strokes rather than a pristine UI border.
        for (var i = 0; i < 28; i++)
        {
            var x = left + i * width / 28;
            var length = width / 28 - (i % 4 == 0 ? 2 : 0) * UIScale;
            handle.DrawRect(UIBox2.FromDimensions(x, top, length, 3 * UIScale), ink);
            handle.DrawRect(UIBox2.FromDimensions(x, top + height, length, 3 * UIScale), ink);
        }
        handle.DrawRect(UIBox2.FromDimensions(left, top, 3 * UIScale, height), ink);
        handle.DrawRect(UIBox2.FromDimensions(-left, top, 3 * UIScale, height), ink);

        for (var i = 0; i < _lines.Length; i++)
        {
            var dimensions = handle.GetDimensions(_font, _lines[i], UIScale);
            var position = new Vector2(-dimensions.X / 2, top + (14 + i * 36) * UIScale);
            handle.DrawString(_font, position, _lines[i], UIScale, ink);
        }
        // Fine gaps in the impression also break up the letter strokes.
        for (var i = 0; i < 44; i++)
        {
            var x = left + 8 * UIScale + (i * 71 % 199) * UIScale;
            var y = top + (i * 37 % 126) * UIScale;
            handle.DrawRect(UIBox2.FromDimensions(x, y, (1 + i % 4) * UIScale, UIScale), Color.FromHex("#3B3C51"));
        }
        handle.SetTransform(oldTransform);
    }
}
