using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Content.Server._BlackM.DiscordAuth;

public sealed class DiscordAuthEntry
{
    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("ckey")]
    public string Ckey { get; set; } = string.Empty;

    [JsonPropertyName("discord_id")]
    public string DiscordId { get; set; } = string.Empty;
}

public sealed class DiscordAuthStorage
{
    private readonly string _path;
    private readonly object _lock = new();
    private System.Collections.Generic.Dictionary<string, DiscordAuthEntry> _verified = new();

    public DiscordAuthStorage(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "discord_auth.json");
        Load();
    }

    private void Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path))
            {
                _verified = new System.Collections.Generic.Dictionary<string, DiscordAuthEntry>();
                return;
            }
            try
            {
                _verified = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, DiscordAuthEntry>>(File.ReadAllText(_path))
                            ?? new System.Collections.Generic.Dictionary<string, DiscordAuthEntry>();
            }
            catch
            {
                _verified = new System.Collections.Generic.Dictionary<string, DiscordAuthEntry>();
            }
        }
    }

    private void Save()
    {
        lock (_lock)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_verified, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public bool IsVerified(string userId)
    {
        lock (_lock)
            return _verified.TryGetValue(userId, out var v) && v.Verified;
    }

    public bool TryGetUserIdByDiscordId(string discordId, out string userId)
    {
        lock (_lock)
        {
            foreach (var kv in _verified)
            {
                if (kv.Value.Verified
                    && !string.IsNullOrEmpty(kv.Value.DiscordId)
                    && kv.Value.DiscordId == discordId)
                {
                    userId = kv.Key;
                    return true;
                }
            }
        }

        userId = string.Empty;
        return false;
    }

    public void SetVerified(string userId, string ckey, string discordId, bool verified)
    {
        lock (_lock)
            _verified[userId] = new DiscordAuthEntry { Verified = verified, Ckey = ckey, DiscordId = discordId };
        Save();
    }

    public bool RemoveVerification(string userId)
    {
        lock (_lock)
        {
            if (!_verified.Remove(userId))
                return false;
        }
        Save();
        return true;
    }
}