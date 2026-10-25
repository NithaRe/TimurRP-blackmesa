using System;
using System.Collections.Generic;
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
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ISawmill? _sawmill;
    private readonly object _lock = new();
    private Dictionary<string, DiscordAuthEntry> _entries = new();

    public DiscordAuthStorage(string dataDir, ISawmill? sawmill = null)
    {
        _sawmill = sawmill;
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "discord_auth.json");
        Load();
    }

    private string BackupPath => _path + ".bak";

    private void Load()
    {
        lock (_lock)
        {
            foreach (var candidate in new[] { _path, BackupPath })
            {
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, DiscordAuthEntry>>(File.ReadAllText(candidate));
                    if (loaded == null)
                        throw new JsonException("file contains null");

                    _entries = loaded;
                    if (candidate != _path)
                        _sawmill?.Warning($"Main storage was unreadable, restored from {candidate}");
                    return;
                }
                catch (Exception ex)
                {
                    var quarantine = $"{candidate}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
                    try { File.Copy(candidate, quarantine, true); } catch { /* ignore */ }
                    _sawmill?.Error($"Failed to read {candidate} (copy saved to {quarantine}): {ex.Message}");
                }
            }

            _entries = new Dictionary<string, DiscordAuthEntry>();
        }
    }

    // _lock
    private void SaveLocked()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, JsonOptions));

        if (File.Exists(_path))
            File.Replace(tmp, _path, BackupPath);
        else
            File.Move(tmp, _path);
    }

    public bool IsVerified(string userId)
    {
        lock (_lock)
            return _entries.TryGetValue(userId, out var v) && v.Verified;
    }

    public bool TryGetUserIdByDiscordId(string discordId, out string userId)
    {
        lock (_lock)
            return TryGetUserIdByDiscordIdLocked(discordId, out userId);
    }

    private bool TryGetUserIdByDiscordIdLocked(string discordId, out string userId)
    {
        foreach (var kv in _entries)
        {
            if (kv.Value.Verified && kv.Value.DiscordId == discordId)
            {
                userId = kv.Key;
                return true;
            }
        }

        userId = string.Empty;
        return false;
    }

    public bool TryLink(string userId, string ckey, string discordId, out string conflictUserId)
    {
        lock (_lock)
        {
            if (TryGetUserIdByDiscordIdLocked(discordId, out conflictUserId) && conflictUserId != userId)
                return false;

            _entries.TryGetValue(userId, out var previous);
            _entries[userId] = new DiscordAuthEntry { Verified = true, Ckey = ckey, DiscordId = discordId };

            try
            {
                SaveLocked();
            }
            catch
            {
                if (previous != null)
                    _entries[userId] = previous;
                else
                    _entries.Remove(userId);
                throw;
            }

            conflictUserId = string.Empty;
            return true;
        }
    }

    public void SetVerified(string userId, string ckey, string discordId, bool verified)
    {
        lock (_lock)
        {
            _entries[userId] = new DiscordAuthEntry { Verified = verified, Ckey = ckey, DiscordId = discordId };
            SaveLocked();
        }
    }

    public bool RemoveVerification(string userId)
    {
        lock (_lock)
        {
            if (!_entries.Remove(userId))
                return false;

            SaveLocked();
            return true;
        }
    }
}