using System.Text.Json.Serialization;

namespace Content.Server._BlackM.DiscordAuth;

public sealed class DiscordLinkRequest
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("discordId")]
    public string DiscordId { get; set; } = string.Empty;
}