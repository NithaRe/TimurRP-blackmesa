using Content.Shared._BlackM.Monorail;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._BlackM.Monorail;

public sealed class MonorailInteriorSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private MonorailInteriorOverlay _overlay = default!;

    public EntityUid? Car { get; private set; }

    public float Fade { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new MonorailInteriorOverlay(this);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var inside = FindCar();
        if (inside != null)
            Car = inside;

        if (Car is not { } car || !TryComp<MonorailCarComponent>(car, out var comp))
        {
            Car = null;
            Fade = 0f;
            return;
        }

        var step = comp.InteriorFadeTime > 0f ? frameTime / comp.InteriorFadeTime : 1f;
        Fade = inside != null
            ? MathF.Min(1f, Fade + step)
            : MathF.Max(0f, Fade - step);

        if (inside == null && Fade <= 0f)
            Car = null;
    }

    private EntityUid? FindCar()
    {
        if (_player.LocalEntity is not { } player)
            return null;

        var uid = player;
        for (var i = 0; i < 4; i++)
        {
            if (!TryComp(uid, out TransformComponent? xform) || !xform.ParentUid.IsValid())
                return null;

            uid = xform.ParentUid;
            if (TryComp<MonorailCarComponent>(uid, out var car))
                return car.InteriorDarkness > 0f ? uid : null;
        }

        return null;
    }
}
