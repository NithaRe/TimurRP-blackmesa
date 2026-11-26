using System;
using System.Text;
using Robust.Shared.Maths;

namespace Content.Shared._BlackM.SecurityCommand;

public static class SecurityCommandConstants
{
    public const int MaxOrders = 40;
    public const int MaxSquads = 8;
    public const int MaxTextLength = 48;
    public const int MaxSquadNameLength = 20;
    public const int MaxRoutePoints = 12;
    public const int MaxPostDurationMinutes = 60;

    public const float ArrivalDistance = 1.5f;

    public static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(0.25);

    public static readonly (string Loc, Color Color)[] Palette =
    {
        ("security-command-color-red", Color.FromHex("#e04a40")),
        ("security-command-color-orange", Color.FromHex("#f0903a")),
        ("security-command-color-yellow", Color.FromHex("#e8d040")),
        ("security-command-color-green", Color.FromHex("#5bd35b")),
        ("security-command-color-cyan", Color.FromHex("#5fc8f0")),
        ("security-command-color-blue", Color.FromHex("#4a78e8")),
        ("security-command-color-purple", Color.FromHex("#b066e0")),
        ("security-command-color-white", Color.FromHex("#e8e8e8")),
    };

    public static bool IsValidPointCount(SecurityOrderType type, int count)
    {
        return type switch
        {
            SecurityOrderType.PatrolRoute => count >= 2 && count <= MaxRoutePoints,
            SecurityOrderType.PatrolZone or SecurityOrderType.Post => count == 2,
            _ => count == 1,
        };
    }

    public static string GetTypeLoc(SecurityOrderType type)
    {
        return type switch
        {
            SecurityOrderType.Move => "security-command-type-move",
            SecurityOrderType.Post => "security-command-type-post",
            SecurityOrderType.PatrolRoute => "security-command-type-route",
            SecurityOrderType.PatrolZone => "security-command-type-zone",
            SecurityOrderType.Danger => "security-command-type-danger",
            _ => "security-command-type-label",
        };
    }

    public static bool RequiresText(SecurityOrderType type)
    {
        return type == SecurityOrderType.Label;
    }

    public static string FormatPostTime(TimeSpan time)
    {
        var seconds = (int) Math.Max(0, Math.Ceiling(time.TotalSeconds));
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    public static string SanitizeText(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var c in text)
        {
            if (char.IsControl(c) || c == '[' || c == ']')
                continue;

            sb.Append(c);
            if (sb.Length >= maxLength)
                break;
        }

        return sb.ToString().Trim();
    }
}
