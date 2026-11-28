using Content.Client._BlackM.WelcomeMessage;
using Content.Client.Resources;
using Content.Shared._BlackM.Announcement;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;

namespace Content.Client._BlackM.Announcement;

public sealed partial class AnnouncementBroadcastSystem : EntitySystem
{
    [Dependency] private IResourceCache _res = default!;
    [Dependency] private WelcomeMessageSystem _welcome = default!;

    private const string FallbackPortrait = "/Textures/_BlackM/Interface/welcome_portrait.png";

    private const float EntityWaitSeconds = 1.5f;
    private const int MaxQueue = 6;

    private readonly Queue<AnnouncementBroadcastEvent> _queue = new();
    private AnnouncementBroadcastEvent? _pending;
    private float _pendingWait;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<AnnouncementBroadcastEvent>(OnMessage);
    }

    private void OnMessage(AnnouncementBroadcastEvent ev)
    {
        if (!_welcome.InGameplay)
            return;

        while (_queue.Count >= MaxQueue)
        {
            _queue.Dequeue();
        }

        _queue.Enqueue(ev);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_welcome.InGameplay)
        {
            Reset();
            return;
        }

        if (!_welcome.CanPresent)
            return;

        if (_pending == null)
        {
            if (!_queue.TryDequeue(out var next))
                return;

            _pending = next;
            _pendingWait = 0f;
        }

        _pendingWait += frameTime;

        EntityUid? speaker = null;
        if (_pending.Speaker is { } netSpeaker &&
            TryGetEntity(netSpeaker, out var uid) &&
            HasComp<SpriteComponent>(uid.Value))
        {
            speaker = uid.Value;
        }

        if (speaker == null && _pendingWait < EntityWaitSeconds)
            return;

        var ev = _pending;
        _pending = null;

        var portrait = speaker == null
            ? WelcomeMessageSystem.CropPortrait(_res.GetTexture(FallbackPortrait), new Vector2i(4, 0), new Vector2i(24, 18))
            : null;
        _welcome.Present(new HudMessage(
            ev.Sender,
            ev.Title,
            ev.Text,
            portrait,
            speaker,
            ev.Duration,
            ChannelColor: ev.Accent));
    }

    private void Reset()
    {
        _pending = null;
        _pendingWait = 0f;
        _queue.Clear();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Reset();
    }
}
