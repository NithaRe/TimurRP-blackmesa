using Content.Client.Popups;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.NeedsHud;

public sealed partial class VitalsHud
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Color WarningColor = Color.FromHex("#e8bb58");
    private static readonly Color CriticalColor = Color.FromHex("#f07064");
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WarningCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CriticalCooldown = TimeSpan.FromSeconds(90);

    private readonly NeedBlock _food;
    private readonly NeedBlock _water;
    private readonly NoticeState _foodNotice = new();
    private readonly NoticeState _waterNotice = new();
    private EntityUid? _lastPlayer;
    private TimeSpan _readyAt;

    private void RefreshNeeds()
    {
        var player = _player.LocalEntity;
        if (player != _lastPlayer)
        {
            _lastPlayer = player;
            _readyAt = _timing.RealTime + InitialDelay;
            _foodNotice.Reset();
            _waterNotice.Reset();
        }

        if (player is not { Valid: true } uid || _entities.Deleted(uid))
        {
            _food.Visible = false;
            _water.Visible = false;
            return;
        }

        var hungerSystem = _entities.System<HungerSystem>();
        var thirstSystem = _entities.System<ThirstSystem>();
        var dead = _entities.System<MobStateSystem>().IsDead(uid);
        var ready = _timing.RealTime >= _readyAt && !dead;
        if (hungerSystem.TryGetNeedsHudValues(uid, out var food, out var foodMax, out var foodWarn, out var foodConsequence))
        {
            _food.Visible = !dead;
            var stage = GetStage(food, foodWarn, foodConsequence);
            _food.SetState(food, foodMax, stage);
            Notify(uid, _foodNotice, stage, ready, "blackm-needs-food-warning", "blackm-needs-food-critical");
        }
        else
        {
            _food.Visible = false;
            _foodNotice.Reset();
        }

        if (thirstSystem.TryGetNeedsHudValues(uid, out var water, out var waterMax, out var waterWarn, out var waterConsequence))
        {
            _water.Visible = !dead;
            var stage = GetStage(water, waterWarn, waterConsequence);
            _water.SetState(water, waterMax, stage);
            Notify(uid, _waterNotice, stage, ready, "blackm-needs-water-warning", "blackm-needs-water-critical");
        }
        else
        {
            _water.Visible = false;
            _waterNotice.Reset();
        }
    }

    private static NeedStage GetStage(float value, float warning, float consequence)
    {
        var critical = consequence + (warning - consequence) * 0.2f;
        return value <= critical ? NeedStage.Critical : value <= warning ? NeedStage.Warning : NeedStage.Normal;
    }

    private void Notify(EntityUid uid, NoticeState state, NeedStage stage, bool ready, string warningKey, string criticalKey)
    {
        if (!ready)
            return;

        var now = _timing.RealTime;
        var worsening = state.LastStage == null || stage > state.LastStage.Value;
        if (stage == NeedStage.Critical && now >= state.NextCritical)
        {
            _entities.System<PopupSystem>().PopupEntity(Loc.GetString(criticalKey), uid, PopupType.MediumCaution);
            state.NextCritical = now + CriticalCooldown;
        }
        else if (stage == NeedStage.Warning && worsening && now >= state.NextWarning)
        {
            _entities.System<PopupSystem>().PopupEntity(Loc.GetString(warningKey), uid, PopupType.Small);
            state.NextWarning = now + WarningCooldown;
        }

        state.LastStage = stage;
    }

    private enum NeedStage : byte { Normal, Warning, Critical }

    private sealed class NoticeState
    {
        public NeedStage? LastStage;
        public TimeSpan NextWarning;
        public TimeSpan NextCritical;

        public void Reset()
        {
            LastStage = null;
            NextWarning = TimeSpan.Zero;
            NextCritical = TimeSpan.Zero;
        }
    }

    private sealed class NeedBlock : HudBlock
    {
        private const float IconWidth = 32f;
        private const float IconHeight = 26f;
        private const float NumberGap = 2f;
        private readonly Texture _icon;
        private readonly UIBox2 _iconSource;
        private readonly UIBox2 _iconBounds;
        private NeedStage _stage;

        public NeedBlock(HudSprites sprites, string title, Texture icon, UIBox2 source)
            : this(sprites, title, icon, source, GetIconBounds(source))
        {
        }

        private NeedBlock(HudSprites sprites, string title, Texture icon, UIBox2 source, UIBox2 iconBounds)
            : base(sprites, title, iconBounds.Right + NumberGap)
        {
            _icon = icon;
            _iconSource = source;
            _iconBounds = iconBounds;
        }

        private static UIBox2 GetIconBounds(UIBox2 source)
        {
            var scale = MathF.Min(IconWidth / source.Width, IconHeight / source.Height);
            var width = source.Width * scale;
            var height = source.Height * scale;
            return UIBox2.FromDimensions((IconWidth - width) / 2f, (BlockHeight - height) / 2f, width, height);
        }

        public void SetState(float value, float maximum, NeedStage stage)
        {
            _stage = stage;
            SetValue(ToPercent(value / maximum));
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var color = _stage switch
            {
                NeedStage.Critical => CriticalColor,
                NeedStage.Warning => WarningColor,
                _ => HudOrange,
            };
            var alpha = _stage == NeedStage.Normal ? Alpha : MathF.Max(Alpha, 0.5f);
            var unit = Unit;
            var bounds = new UIBox2(MathF.Round(_iconBounds.Left * unit), MathF.Round(_iconBounds.Top * unit),
                MathF.Round(_iconBounds.Right * unit), MathF.Round(_iconBounds.Bottom * unit));
            handle.DrawTextureRectRegion(_icon, bounds, _iconSource, Color.White.WithAlpha(alpha));
            DrawNumber(handle, color.WithAlpha(alpha));
        }
    }
}
