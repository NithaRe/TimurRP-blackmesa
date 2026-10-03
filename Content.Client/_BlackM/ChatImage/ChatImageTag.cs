using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.ChatImage;

public sealed class ChatImageTag : IMarkupTagHandler
{
    public string Name => "img";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!TryGetId(node, out var id))
            return false;

        control = new ChatImageControl(id);
        return true;
    }

    private static bool TryGetId(MarkupNode node, out int id)
    {
        id = 0;

        var value = node.Value;

        if (value.LongValue is { } l)
        {
            id = (int) l;
            return true;
        }

        return value.StringValue != null && int.TryParse(value.StringValue, out id);
    }
}
