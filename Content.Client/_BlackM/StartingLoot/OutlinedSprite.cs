using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._BlackM.StartingLoot;

public sealed class OutlinedSprite : Control
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly Vector2[] Dirs =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        new(0.7071f, 0.7071f), new(0.7071f, -0.7071f),
        new(-0.7071f, 0.7071f), new(-0.7071f, -0.7071f)
    };

    private readonly ShaderInstance? _shader;

    public Texture? Texture;
    public float Scale = 3f;
    public float Alpha = 1f;
    public Color OutlineColor = Color.FromHex("#f2f2f2");

    public OutlinedSprite()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;

        if (_proto.TryIndex<ShaderPrototype>("BlackMLootOutline", out var shaderProto))
        {
            _shader = shaderProto.InstanceUnique();
        }
        else
        {
            _shader = null;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Texture == null || Alpha <= 0f)
            return;

        var scale = Scale;
        var size = new Vector2(Texture.Width, Texture.Height) * scale;
        var center = Size / 2f;
        var pos = center - size / 2f;
        var thickness = scale;

        Color outlineModulate;
        if (_shader != null)
        {
            _shader.SetParameter("outline_color", OutlineColor);
            _shader.SetParameter("outline_alpha", Alpha);
            handle.UseShader(_shader);
            outlineModulate = Color.White;
        }
        else
        {
            outlineModulate = Color.Black.WithAlpha(Alpha);
        }

        foreach (var d in Dirs)
        {
            handle.DrawTextureRect(Texture, UIBox2.FromDimensions(pos + d * thickness, size), outlineModulate);
        }

        handle.UseShader(null);
        handle.DrawTextureRect(Texture, UIBox2.FromDimensions(pos, size), Color.White.WithAlpha(Alpha));
    }
}