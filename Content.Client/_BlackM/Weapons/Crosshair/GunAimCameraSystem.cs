using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Movement.Components;
using Content.Shared._BlackM.CCVar;
using Content.Shared.Camera;
using Content.Shared.Movement.Components;
using Content.Shared.Wieldable.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Client._BlackM.Weapons.Crosshair;

[RegisterComponent]
public sealed partial class GunAimCameraComponent : Component
{
    public Vector2 Offset;
}

public sealed class GunAimCameraSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly GunCrosshairSystem _crosshair = default!;

    private const float MaxDistance = 4f;
    private const float Deadzone = 0.1f;
    private const float EdgeFactor = 0.92f;
    private const float FollowSharpness = 7f;
    private const float ReturnSharpness = 9f;

    private bool _enabled;
    private float _strength;

    private EntityUid? _owner;
    private EntityUid? _gun;
    private Vector2 _offset;
    private Vector2 _target;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunAimCameraComponent, GetEyeOffsetEvent>(OnGetEyeOffset);

        Subs.CVar(_cfg, BlackMCVars.GunCameraFollow, v => _enabled = v, true);
        Subs.CVar(_cfg, BlackMCVars.GunCameraStrength, v => _strength = Math.Clamp(v, 0f, 1f), true);
    }

    public override void Shutdown()
    {
        Release();
        base.Shutdown();
    }

    // True while this system drives the camera instead of the scope.
    public bool Overrides(EntityUid item) => _gun == item;

    private void OnGetEyeOffset(Entity<GunAimCameraComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.Offset;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var local = _player.LocalEntity;
        if (_owner != null && _owner != local)
            Release();

        if (local is not { } user)
            return;

        if (_enabled && IsOwnEye(user) && _crosshair.TryGetAiming(user, out _, out var gunUid, out _))
        {
            if (_gun != gunUid)
                TakeOver(gunUid);

            if (TryGetTarget(GetMaxOffset(gunUid), out var target))
                _target = target;
        }
        else
        {
            HandBack();
            _target = Vector2.Zero;
        }

        var sharpness = _gun != null ? FollowSharpness : ReturnSharpness;
        _offset = Vector2.Lerp(_offset, _target, 1f - MathF.Exp(-sharpness * frameTime));

        if (_gun == null && _offset.LengthSquared() < 0.0001f)
            _offset = Vector2.Zero;

        if (_gun != null && TryGetScope(_gun.Value, out var scope))
            scope.CurrentPosition = _offset;

        Apply(user);
    }

    private bool IsOwnEye(EntityUid user)
    {
        return TryComp<EyeComponent>(user, out var eye) &&
               eye.Target == null &&
               _eye.CurrentEye == eye.Eye;
    }

    private void TakeOver(EntityUid gunUid)
    {
        HandBack();
        _gun = gunUid;

        // Start from the scope offset to avoid a jump.
        if (TryGetScope(gunUid, out var scope))
            _offset += scope.CurrentPosition;
    }

    private void HandBack()
    {
        if (_gun is not { } gun)
            return;

        _gun = null;

        if (!TryGetScope(gun, out var scope))
            return;

        scope.CurrentPosition = _offset;
        scope.TargetPosition = _offset;
        _offset = Vector2.Zero;
        _target = Vector2.Zero;
    }

    private bool TryGetScope(EntityUid gun, [NotNullWhen(true)] out EyeCursorOffsetComponent? scope)
    {
        if (!TryComp(gun, out scope))
            return false;

        if (HasComp<CursorOffsetRequiresWieldComponent>(gun) &&
            (!TryComp<WieldableComponent>(gun, out var wieldable) || !wieldable.Wielded))
        {
            scope = null;
            return false;
        }

        return true;
    }

    private float GetMaxOffset(EntityUid gun)
    {
        var zoom = Math.Clamp(_eye.CurrentEye.Zoom.X, 0.5f, 3f);
        var max = MaxDistance * _strength * zoom;

        if (TryGetScope(gun, out var scope))
            max = MathF.Max(max, scope.MaxOffset);

        return max;
    }

    // False when the cursor is off the game view: the camera holds.
    private bool TryGetTarget(float maxOffset, out Vector2 target)
    {
        target = Vector2.Zero;

        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid ||
            _eye.MainViewport is not Control viewport ||
            !ReferenceEquals(_ui.CurrentlyHovered, viewport))
            return false;

        var size = new Vector2(viewport.PixelSize.X, viewport.PixelSize.Y);
        var radius = MathF.Min(size.X, size.Y) * 0.5f * EdgeFactor;
        if (radius < 1f)
            return false;

        var center = new Vector2(viewport.GlobalPixelPosition.X, viewport.GlobalPixelPosition.Y) + size * 0.5f;
        var distance = (mouse.Position - center).Length() / radius;
        var strength = Math.Clamp((distance - Deadzone) / (1f - Deadzone), 0f, 1f);

        if (strength <= 0f || maxOffset <= 0f)
            return true;

        // Map through the eye so rotation and zoom stay correct.
        var from = _eye.PixelToMap(new ScreenCoordinates(center, mouse.Window));
        var to = _eye.PixelToMap(mouse);
        if (from.MapId == MapId.Nullspace || from.MapId != to.MapId)
            return false;

        var dir = to.Position - from.Position;
        if (dir.LengthSquared() < 0.0001f)
            return true;

        strength = strength * strength * (3f - 2f * strength);
        target = Vector2.Normalize(dir) * maxOffset * strength;
        return true;
    }

    private void Apply(EntityUid user)
    {
        if (_offset == Vector2.Zero && _gun == null)
        {
            if (_owner is { } owner && !TerminatingOrDeleted(owner))
                RemComp<GunAimCameraComponent>(owner);

            _owner = null;
            return;
        }

        _owner = user;
        EnsureComp<GunAimCameraComponent>(user).Offset = _offset;
    }

    private void Release()
    {
        if (_owner is { } owner && !TerminatingOrDeleted(owner))
            RemComp<GunAimCameraComponent>(owner);

        _owner = null;
        _gun = null;
        _offset = Vector2.Zero;
        _target = Vector2.Zero;
    }
}
