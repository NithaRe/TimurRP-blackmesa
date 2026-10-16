using System.IO;
using System.Text.Json;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._BlackM.Achievements;
using Content.Shared.GameTicking;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._BlackM.Achievements;
public sealed class PlayerAchievementData
{
    public Dictionary<string, long> Stats { get; set; } = new();
    public List<string> Granted { get; set; } = new();
    public List<string> Selected { get; set; } = new();
}
public sealed class AchievementsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager       _proto    = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly IResourceManager        _res      = default!;
    [Dependency] private readonly IChatManager            _chat     = default!;

    private static readonly ResPath FilePath = new("/blackm/achievements.json");
    private static readonly ResPath DirPath = new("/blackm");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private const float FlushInterval = 60f;
    private readonly Dictionary<NetUserId, PlayerAchievementData> _data = new();
    private readonly HashSet<NetUserId> _roundParticipants = new();
    private bool _dirty;
    private bool _saveDisabled;
    private float _flushTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AchievementsRequestEvent>(OnRequest);
        SubscribeNetworkEvent<AchievementSelectionEvent>(OnSelection);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _roundParticipants.Clear());

        Load();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Save();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _flushTimer += frameTime;
        if (_flushTimer < FlushInterval)
            return;

        _flushTimer = 0f;
        Save();
    }
    public void AddStat(ICommonSession session, string stat, long amount = 1)
    {
        var data = GetData(session.UserId);
        data.Stats.TryGetValue(stat, out var before);
        var after = before + amount;
        data.Stats[stat] = after;
        MarkDirty(session.UserId);
        var unlocked = false;
        foreach (var proto in _proto.EnumeratePrototypes<AchievementPrototype>())
        {
            if (proto.Kind != AchievementKind.Stat || proto.Stat != stat || data.Granted.Contains(proto.ID))
                continue;

            if (before < proto.Count && after >= proto.Count)
            {
                Notify(session, proto);
                unlocked = true;
            }
        }

        if (unlocked)
            SendState(session);
    }
    public void AddStat(EntityUid mob, string stat, long amount = 1)
    {
        if (TryGetSession(mob, out var session))
            AddStat(session, stat, amount);
    }

    public long GetStat(ICommonSession session, string stat)
    {
        return GetData(session.UserId).Stats.TryGetValue(stat, out var value) ? value : 0;
    }
    public bool Grant(ICommonSession session, string id, bool select = false)
    {
        if (!_proto.TryIndex<AchievementPrototype>(id, out var proto))
            return false;

        var data = GetData(session.UserId);
        var isNew = !data.Granted.Contains(id);

        if (isNew)
            data.Granted.Add(id);

        if (select && !data.Selected.Contains(id) && data.Selected.Count < AchievementsConst.MaxBadges)
            data.Selected.Add(id);

        MarkDirty(session.UserId);

        if (isNew)
            Notify(session, proto);

        SendState(session);
        return true;
    }
    public bool Revoke(ICommonSession session, string id)
    {
        var data = GetData(session.UserId);
        var removed = data.Granted.Remove(id);
        data.Selected.Remove(id);

        if (removed)
            MarkDirty(session.UserId);

        SendState(session);
        return removed;
    }

    public bool IsUnlocked(ICommonSession session, string id)
    {
        return _proto.TryIndex<AchievementPrototype>(id, out var proto)
               && IsUnlocked(GetData(session.UserId), GetTimes(session), proto);
    }

    public float GetProgress(ICommonSession session, AchievementPrototype proto)
    {
        return GetProgress(GetData(session.UserId), GetTimes(session), proto);
    }
    public List<string> GetBadgesFor(EntityUid mob)
    {
        return TryGetSession(mob, out var session)
            ? FilterSelected(GetData(session.UserId), GetTimes(session))
            : new List<string>();
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var session = ev.Player;
        if (_roundParticipants.Add(session.UserId))
            AddStat(session, AchievementStats.RoundsPlayed);
        GrantDateAchievements(session);
    }

    private void GrantDateAchievements(ICommonSession session)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var proto in _proto.EnumeratePrototypes<AchievementPrototype>())
        {
            if (proto.Kind != AchievementKind.Date)
                continue;

            if (!DateOnly.TryParseExact(proto.DateFrom, "yyyy-MM-dd", out var from))
                continue;

            var to = from;
            if (!string.IsNullOrWhiteSpace(proto.DateTo) &&
                !DateOnly.TryParseExact(proto.DateTo, "yyyy-MM-dd", out to))
                continue;

            if (today >= from && today <= to)
                Grant(session, proto.ID);
        }
    }

    private void OnRequest(AchievementsRequestEvent ev, EntitySessionEventArgs args)
    {
        SendState(args.SenderSession);
    }

    private void OnSelection(AchievementSelectionEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var data = GetData(session.UserId);

        var clean = new List<string>();
        foreach (var id in ev.Selected)
        {
            if (clean.Count >= AchievementsConst.MaxBadges)
                break;

            if (clean.Contains(id) || !_proto.HasIndex<AchievementPrototype>(id))
                continue;

            clean.Add(id);
        }

        data.Selected = clean;
        MarkDirty(session.UserId);
        SendState(session);
    }

    private void SendState(ICommonSession session)
    {
        var data = GetData(session.UserId);
        var times = GetTimes(session);

        var progress = new Dictionary<string, float>();
        var unlocked = new List<string>();

        foreach (var proto in _proto.EnumeratePrototypes<AchievementPrototype>())
        {
            progress[proto.ID] = GetProgress(data, times, proto);

            if (IsUnlocked(data, times, proto))
                unlocked.Add(proto.ID);
        }

        RaiseNetworkEvent(new AchievementsStateEvent(progress, unlocked, FilterSelected(data, times)), session);
    }

    private void Notify(ICommonSession session, AchievementPrototype proto)
    {
        _chat.DispatchServerMessage(session,
            Loc.GetString("blackm-achievements-unlocked-chat", ("name", Loc.GetString(proto.Name))));
    }

    private bool IsUnlocked(PlayerAchievementData data, Dictionary<string, TimeSpan> times, AchievementPrototype proto)
    {
        if (data.Granted.Contains(proto.ID))
            return true;

        switch (proto.Kind)
        {
            case AchievementKind.Playtime:
                return times.TryGetValue(proto.Tracker, out var t) && t.TotalHours >= proto.Hours;

            case AchievementKind.Stat:
                return data.Stats.TryGetValue(proto.Stat, out var v) && v >= proto.Count;

            default:
                return false;
        }
    }

    private float GetProgress(PlayerAchievementData data, Dictionary<string, TimeSpan> times, AchievementPrototype proto)
    {
        switch (proto.Kind)
        {
            case AchievementKind.Playtime:
                return times.TryGetValue(proto.Tracker, out var t) ? (float) t.TotalHours : 0f;

            case AchievementKind.Stat:
                return data.Stats.TryGetValue(proto.Stat, out var v) ? v : 0f;

            default:
                return 0f;
        }
    }

    private List<string> FilterSelected(PlayerAchievementData data, Dictionary<string, TimeSpan> times)
    {
        var result = new List<string>();

        foreach (var id in data.Selected)
        {
            if (result.Count >= AchievementsConst.MaxBadges)
                break;

            if (_proto.TryIndex<AchievementPrototype>(id, out var proto) && IsUnlocked(data, times, proto))
                result.Add(id);
        }

        return result;
    }

    private Dictionary<string, TimeSpan> GetTimes(ICommonSession session)
    {
        return _playTime.TryGetTrackerTimes(session, out var times)
            ? times
            : new Dictionary<string, TimeSpan>();
    }

    private bool TryGetSession(EntityUid mob, out ICommonSession session)
    {
        if (TryComp<ActorComponent>(mob, out var actor))
        {
            session = actor.PlayerSession;
            return true;
        }

        session = default!;
        return false;
    }
    private sealed class AchievementsFile
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, PlayerAchievementData> Players { get; set; } = new();
    }

    private PlayerAchievementData GetData(NetUserId id)
    {
        if (!_data.TryGetValue(id, out var data))
        {
            data = new PlayerAchievementData();
            _data[id] = data;
        }

        return data;
    }

    private void MarkDirty(NetUserId id) => _dirty = true;

    private void Load()
    {
        try
        {
            if (!_res.UserData.Exists(FilePath))
                return;

            using var stream = _res.UserData.OpenRead(FilePath);
            var file = JsonSerializer.Deserialize<AchievementsFile>(stream);

            if (file == null)
                return;

            foreach (var (key, value) in file.Players)
            {
                if (Guid.TryParse(key, out var guid))
                    _data[new NetUserId(guid)] = value;
            }

            Log.Info($"Achiv load: {_data.Count}");
        }
        catch (Exception e)
        {
            _saveDisabled = true;
        }
    }

    private void Save()
    {
        if (!_dirty || _saveDisabled)
            return;

        try
        {
            var file = new AchievementsFile();
            foreach (var (id, data) in _data)
            {
                if (data.Stats.Count == 0 && data.Granted.Count == 0 && data.Selected.Count == 0)
                    continue;

                file.Players[id.UserId.ToString()] = data;
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(file, Json);

            _res.UserData.CreateDir(DirPath);
            using var stream = _res.UserData.OpenWrite(FilePath);
            stream.Write(bytes, 0, bytes.Length);

            _dirty = false;
        }
        catch (Exception e)
        {
            Log.Error($"fail {FilePath}: {e}");
        }
    }
}
