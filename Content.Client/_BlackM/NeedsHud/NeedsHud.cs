using Content.Client.Popups;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.NeedsHud;

public sealed class NeedsHud : UIWidget
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resources = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color FoodColor = Color.FromHex("#dfaa60");
    private static readonly Color WaterColor = Color.FromHex("#66c5da");
    private static readonly Color WarningColor = Color.FromHex("#e8bb58");
    private static readonly Color CriticalColor = Color.FromHex("#f07064");
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WarningCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CriticalCooldown = TimeSpan.FromSeconds(90);

    private readonly BoxContainer _container;
    private readonly NeedRow _food;
    private readonly NeedRow _water;
    private readonly NoticeState _foodNotice = new();
    private readonly NoticeState _waterNotice = new();
    private EntityUid? _lastPlayer;
    private TimeSpan _readyAt;
    private float _updateAccumulator;

    public NeedsHud()
    {
        IoCManager.InjectDependencies(this);
        var font = new VectorFont(_resources.GetResource<FontResource>("/Fonts/_BlackM/Bubble/Grand9KPixelCyrillic.ttf"), 11);
        _food = new NeedRow(Loc.GetString("blackm-needs-food"), FoodColor, font);
        _water = new NeedRow(Loc.GetString("blackm-needs-water"), WaterColor, font);
        _container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            VerticalAlignment = VAlignment.Bottom,
            Children = { _food, _water },
        };
        AddChild(_container);
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _updateAccumulator += args.DeltaSeconds;
        if (_updateAccumulator < 0.2f)
            return;
        _updateAccumulator = 0f;

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

    private sealed class NeedRow : PanelContainer
    {
        private readonly Color _normalColor;
        private readonly Label _status;
        private readonly Label _percent;
        private readonly ProgressBar _bar;
        private readonly StyleBoxFlat _fill;

        public NeedRow(string title, Color normalColor, Font font)
        {
            _normalColor = normalColor;
            MouseFilter = MouseFilterMode.Ignore;
            MinWidth = 132;
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#222935").WithAlpha(0.94f),
                BorderColor = Color.FromHex("#566273"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 4,
            };

            var name = new Label { Text = title, FontOverride = font, FontColorOverride = Color.FromHex("#e5e8ed") };
            _status = new Label { FontOverride = font, Margin = new Thickness(5, 0, 0, 0) };
            _percent = new Label { FontOverride = font, FontColorOverride = Color.FromHex("#c6cfdb") };
            _fill = new StyleBoxFlat { BackgroundColor = normalColor };
            _bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                MinHeight = 6,
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#10151d") },
                ForegroundStyleBoxOverride = _fill,
            };

            var heading = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Children = { name, new Control { HorizontalExpand = true }, _percent },
            };
            AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 2,
                Children =
                {
                    heading,
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        Children = { _bar, _status },
                    },
                },
            });
        }

        public void SetState(float value, float maximum, NeedStage stage)
        {
            var fraction = Math.Clamp(value / maximum, 0f, 1f);
            _bar.Value = fraction;
            _percent.Text = $"{(int) MathF.Round(fraction * 100f)}%";
            var color = stage switch
            {
                NeedStage.Critical => CriticalColor,
                NeedStage.Warning => WarningColor,
                _ => _normalColor,
            };
            _fill.BackgroundColor = color;
            _status.FontColorOverride = color;
            _status.Text = Loc.GetString(stage switch
            {
                NeedStage.Critical => "blackm-needs-state-critical",
                NeedStage.Warning => "blackm-needs-state-warning",
                _ => "blackm-needs-state-normal",
            });
        }
    }
}
