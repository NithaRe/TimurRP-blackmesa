using System.Linq;
using System.Numerics;
using Content.Shared._BlackM.EngineeringBreakdown;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.EngineeringBreakdown;

public sealed partial class RepairNavigatorOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transform;
    private readonly Texture _arrow;
    private readonly Texture _marker;

    private const string Rsi = "/Textures/_BlackM/Interface/EngineeringBreakdown/navigator.rsi";
    private const int MaxArrows = 4;
    private const float ArrowRadius = 92f;
    private const float HideArrowDistance = 1.5f;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public RepairNavigatorOverlay()
    {
        IoCManager.InjectDependencies(this);
        var sprite = _entMan.System<SpriteSystem>();
        _transform = _entMan.System<SharedTransformSystem>();
        _arrow = sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(Rsi), "arrow"));
        _marker = sprite.Frame0(new SpriteSpecifier.Rsi(new ResPath(Rsi), "marker"));
    }

    private bool TryGetNavigator(out EntityUid local, out RepairNavigatorComponent nav)
    {
        nav = default!;
        local = default;

        if (_player.LocalEntity is not { } ent
            || _entMan.HasComponent<GhostComponent>(ent)
            || !_entMan.TryGetComponent(ent, out RepairNavigatorComponent? comp)
            || comp.Markers.Count == 0)
            return false;

        local = ent;
        nav = comp;
        return true;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return TryGetNavigator(out _, out _) && base.BeforeDraw(in args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!TryGetNavigator(out var local, out var nav))
            return;

        var playerMap = _transform.GetMapCoordinates(local);
        if (playerMap.MapId != args.MapId)
            return;

        var handle = args.ScreenHandle;
        var uiScale = (args.ViewportControl as Control)?.UIScale ?? 1f;
        var playerScreen = _eye.MapToScreen(playerMap).Position;
        var time = (float) _timing.RealTime.TotalSeconds;
        var bounds = args.ViewportBounds;

        var markers = nav.Markers
            .Where(m => m.Map == playerMap.MapId)
            .OrderBy(m => (m.Position - playerMap.Position).LengthSquared())
            .ToList();

        foreach (var m in markers)
        {
            var screen = _eye.MapToScreen(new MapCoordinates(m.Position, m.Map)).Position;
            if (!bounds.Contains((Vector2i) screen))
                continue;

            var phase = time * (2.5f + m.Severity * 0.8f);
            var bob = MathF.Sin(phase) * 3f * uiScale;
            var pulse = 1f + 0.08f * MathF.Sin(phase * 1.5f);
            var alpha = 0.75f + 0.25f * MathF.Sin(phase * 1.5f);
            var size = new Vector2(32f, 32f) * uiScale * pulse;
            var pos = screen - new Vector2(size.X / 2f, size.Y + 22f * uiScale + bob);
            handle.DrawTextureRect(_marker, UIBox2.FromDimensions(pos, size), m.Color.WithAlpha(alpha));
        }

        for (var i = 0; i < Math.Min(MaxArrows, markers.Count); i++)
        {
            var m = markers[i];
            var distance = (m.Position - playerMap.Position).Length();
            if (distance < HideArrowDistance)
                continue;

            var targetScreen = _eye.MapToScreen(new MapCoordinates(m.Position, m.Map)).Position;
            var delta = targetScreen - playerScreen;
            if (delta.LengthSquared() < 0.01f)
                continue;

            var dir = Vector2.Normalize(delta);
            var nearest = i == 0;
            var scale = (nearest ? 1.1f : 0.8f) * uiScale;
            var alpha = nearest ? 0.9f : 0.5f;
            var radius = ArrowRadius * uiScale + (nearest ? MathF.Sin(time * 4f) * 4f * uiScale : 0f);
            var center = playerScreen + dir * radius;
            var angle = new Angle(MathF.Atan2(dir.X, -dir.Y));
            var size = new Vector2(32f, 32f) * scale;

            handle.SetTransform(Matrix3Helpers.CreateTransform(center, angle));
            handle.DrawTextureRect(_arrow, UIBox2.FromDimensions(-size / 2f, size), m.Color.WithAlpha(alpha));
            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}
