using System.Numerics;
using Content.Client._Shitmed.UserInterface.Systems.Targeting.Widgets;
using Content.Client.Alerts;
using Content.Client.Damage.Systems;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.NeedsHud;

public sealed partial class VitalsHud : UIWidget
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _cache = default!;

    private const string SpritePath = "/Textures/_BlackM/Interface/vitals_hud.rsi";

    private static readonly AlertKey HealthCategory = AlertKey.ForCategory("Health");
    private static readonly AlertKey StaminaCategory = AlertKey.ForCategory("Stamina");

    private const float PreferredScale = 1.25f;
    private const float CompactViewportWidth = 1440f;
    private const float VitalMeterGap = 12f;
    private const float NeedsMeterGap = 8f;
    private const float SlotGapPixels = 10f;
    private const float BottomOffsetPixels = 10f;

    private const float BlockHeight = 32f;
    private const float DigitWidth = 20f;

    private static readonly Color HudOrange = Color.FromHex("#FFA000");
    private static readonly Color PainRed = Color.FromHex("#FA0000");

    private readonly HealthBlock _health;
    private readonly StaminaBlock _stamina;
    private readonly BoxContainer _needsMeters;
    private readonly BoxContainer _vitalMeters;
    private readonly Control _groupSpace;
    private float _untilRefresh;
    private (UIBox2 Bounds, float Scale) _vitalLayout;
    private (UIBox2 Bounds, float Scale) _needsLayout;

    public VitalsHud()
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 0;
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;

        var sprites = new HudSprites(_cache.GetResource<RSIResource>(SpritePath).RSI);
        _health = new HealthBlock(sprites);
        _stamina = new StaminaBlock(sprites);
        _food = new NeedBlock(sprites, "blackm-needs-food",
            _cache.GetResource<RSIResource>("/Textures/Interface/Alerts/hunger.rsi").RSI["peckish"].Frame0,
            new UIBox2(0f, 9f, 32f, 32f));
        _water = new NeedBlock(sprites, "blackm-needs-water",
            _cache.GetResource<RSIResource>("/Textures/Interface/Alerts/thirst.rsi").RSI["thirsty"].Frame0,
            new UIBox2(8f, 5f, 24f, 28f));
        _needsMeters = CreateGroup(_food, _water);
        _vitalMeters = CreateGroup(_health, _stamina);
        _groupSpace = new Control { HorizontalExpand = true, MouseFilter = MouseFilterMode.Ignore };
        AddChild(_vitalMeters);
        AddChild(_groupSpace);
        AddChild(_needsMeters);
    }

    private static BoxContainer CreateGroup(HudBlock first, HudBlock second)
    {
        return new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            VerticalAlignment = VAlignment.Bottom,
            MouseFilter = MouseFilterMode.Ignore,
            Children = { first, second },
        };
    }

    private static bool IsHealthAlert(string id)
    {
        return id is "HumanHealth" or "HumanCrit" or "HumanDead" or "BorgHealth" or "BorgCrit" or "BorgDead";
    }

    public static bool ReplacesAlert(string id)
    {
        return IsHealthAlert(id) || id == "Stamina";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _health.Tick(args.DeltaSeconds);
        _stamina.Tick(args.DeltaSeconds);
        _food.Tick(args.DeltaSeconds);
        _water.Tick(args.DeltaSeconds);

        _untilRefresh -= args.DeltaSeconds;
        if (_untilRefresh <= 0)
        {
            _untilRefresh = 0.1f;
            RefreshVitals();
            RefreshNeeds();
        }

        LayoutNextToHotbar();
    }

    private void RefreshVitals()
    {
        var alerts = _entities.System<ClientAlertsSystem>();
        if (_player.LocalEntity is not { Valid: true } uid || alerts.ActiveAlerts is not { } active)
        {
            _health.Visible = false;
            _stamina.Visible = false;
            return;
        }

        _health.Visible = active.TryGetValue(HealthCategory, out var health) && IsHealthAlert(health.Type.Id);
        var dead = _health.Visible && health.Type.Id is "HumanDead" or "BorgDead";
        if (_health.Visible)
        {
            var critical = health.Type.Id is "HumanCrit" or "BorgCrit";
            var value = dead || critical ? 0f : GetHealth(uid, alerts, health);
            _health.SetValue(ToPercent(value));
        }

        if (dead || !active.TryGetValue(StaminaCategory, out var stamina) || stamina.Type.Id != "Stamina")
        {
            _stamina.Visible = false;
            return;
        }

        _stamina.Visible = true;
        _stamina.SetValue(ToPercent(GetStamina(uid, alerts, stamina)));
    }

    private void LayoutNextToHotbar()
    {
        if ((!_health.Visible && !_stamina.Visible && !_food.Visible && !_water.Visible) ||
            Parent is not LayoutContainer parent ||
            _ui.ActiveScreen is not { } screen ||
            screen.GetWidget<HotbarGui>() is not { } hotbar ||
            hotbar.HandContainer.Parent is not { Width: > 0f, Height: > 0f } row)
        {
            _vitalMeters.Visible = false;
            _needsMeters.Visible = false;
            _groupSpace.Visible = false;
            return;
        }

        var origin = parent.GlobalPosition;
        var bottom = parent.Height - BottomOffsetPixels / UIScale;
        var leftEdge = 0f;
        if (screen.GetWidget<InventoryGui>() is { Visible: true, Width: > 0f, Height: > 0f } inventory)
        {
            leftEdge = GetInventoryEdge(inventory, origin, bottom);
        }

        var rightEdge = parent.Width;
        if (screen.GetWidget<TargetingControl>() is { VisibleInTree: true, Width: > 0f, Height: > 0f } targeting)
        {
            var targetPosition = targeting.GlobalPosition - origin;
            if (targetPosition.Y < bottom && targetPosition.Y + targeting.Height > bottom - BlockHeight * PreferredScale)
                rightEdge = targetPosition.X;
        }

        var hotbarLeft = row.GlobalPosition.X - origin.X;
        var vitalWidth = (_health.Visible ? _health.NativeWidth : 0f) +
                         (_stamina.Visible ? _stamina.NativeWidth : 0f);
        var needsWidth = (_food.Visible ? _food.NativeWidth : 0f) +
                         (_water.Visible ? _water.NativeWidth : 0f);
        var maxVitalWidth = (_health.Visible ? _health.MaxNativeWidth : 0f) +
                            (_stamina.Visible ? _stamina.MaxNativeWidth : 0f);
        var maxNeedsWidth = (_food.Visible ? _food.MaxNativeWidth : 0f) +
                            (_water.Visible ? _water.MaxNativeWidth : 0f);
        var vitalGap = _health.Visible && _stamina.Visible ? VitalMeterGap : 0f;
        var needsGap = _food.Visible && _water.Visible ? NeedsMeterGap : 0f;
        var vitalLayout = CalculateLayout(parent.Size, UIScale, leftEdge, hotbarLeft, vitalWidth,
            maxVitalWidth, vitalGap, alignRight: true);
        var needsLayout = CalculateLayout(parent.Size, UIScale, hotbarLeft + row.Width, rightEdge, needsWidth,
            maxNeedsWidth, needsGap, alignRight: false);

        _vitalMeters.Visible = vitalLayout.Scale > 0f;
        _needsMeters.Visible = needsLayout.Scale > 0f;
        _groupSpace.Visible = _vitalMeters.Visible && _needsMeters.Visible;
        if ((!_vitalMeters.Visible && !_needsMeters.Visible) ||
            (vitalLayout == _vitalLayout && needsLayout == _needsLayout))
            return;

        _vitalLayout = vitalLayout;
        _needsLayout = needsLayout;
        _health.SetScale(vitalLayout.Scale);
        _stamina.SetScale(vitalLayout.Scale);
        _food.SetScale(needsLayout.Scale);
        _water.SetScale(needsLayout.Scale);
        _vitalMeters.SeparationOverride = (int) (vitalGap * vitalLayout.Scale);
        _needsMeters.SeparationOverride = (int) (needsGap * needsLayout.Scale);

        var bounds = _vitalMeters.Visible ? vitalLayout.Bounds : needsLayout.Bounds;
        if (_groupSpace.Visible)
        {
            bounds = new UIBox2(bounds.Left, MathF.Min(bounds.Top, needsLayout.Bounds.Top),
                needsLayout.Bounds.Right, bounds.Bottom);
        }

        LayoutContainer.SetMarginLeft(this, bounds.Left);
        LayoutContainer.SetMarginTop(this, bounds.Top);
        LayoutContainer.SetMarginRight(this, bounds.Right);
        LayoutContainer.SetMarginBottom(this, bounds.Bottom);
    }

    private static float GetInventoryEdge(InventoryGui inventory, Vector2 origin, float bottom)
    {
        var inventoryPos = inventory.GlobalPosition - origin;
        var edge = inventoryPos.X + inventory.Width;
        if (!inventory.InventoryHotbar.VisibleInTree)
            return edge;

        float? lastSlotEdge = null;
        var top = bottom - BlockHeight * PreferredScale;
        foreach (var slot in inventory.InventoryHotbar.Children)
        {
            if (!slot.VisibleInTree || slot.Width <= 0f)
                continue;

            var slotPos = slot.GlobalPosition - origin;
            if (slotPos.Y < bottom && slotPos.Y + slot.Height > top)
                lastSlotEdge = MathF.Max(lastSlotEdge ?? inventoryPos.X, slotPos.X + slot.Width);
        }

        return lastSlotEdge ?? edge;
    }

    private static (UIBox2 Bounds, float Scale) CalculateLayout(Vector2 viewport, float uiScale, float leftEdge,
        float rightEdge, float metersWidth, float maxMetersWidth, float spacing, bool alignRight)
    {
        if (metersWidth <= 0f)
            return default;

        var gap = SlotGapPixels / uiScale;
        var left = Math.Clamp(leftEdge + gap, 0f, viewport.X);
        var right = Math.Clamp(rightEdge - gap, left, viewport.X);
        var bottom = MathF.Max(0f, viewport.Y - BottomOffsetPixels / uiScale);
        var viewportFactor = Math.Clamp(viewport.X * uiScale / CompactViewportWidth, 0.7f, 1f);
        var scale = MathF.Min((right - left) / (maxMetersWidth + spacing), PreferredScale * viewportFactor);
        scale = MathF.Min(scale, bottom / BlockHeight);
        var width = metersWidth * scale + (int) (spacing * scale);
        var height = BlockHeight * scale;
        var x = alignRight ? right - width : left;
        var bounds = UIBox2.FromDimensions(x, bottom - height, width, height);
        return (bounds, scale);
    }

    private static int ToPercent(float fraction)
    {
        return Math.Clamp((int) MathF.Ceiling(fraction * 100f - 0.0001f), 0, 100);
    }

    private float GetHealth(EntityUid uid, ClientAlertsSystem alerts, AlertState alert)
    {
        var thresholds = _entities.System<MobThresholdSystem>();
        if (_entities.TryGetComponent(uid, out MobThresholdsComponent? limits) &&
            _entities.TryGetComponent(uid, out DamageableComponent? damageable) &&
            thresholds.TryGetIncapThreshold(uid, out var threshold, limits) && threshold.Value > 0)
        {
            var damage = thresholds.CheckVitalDamage(uid, damageable);
            return Math.Clamp(1f - damage.Float() / threshold.Value.Float(), 0f, 1f);
        }

        return 1f - GetAlertFraction(alerts, alert);
    }

    private float GetStamina(EntityUid uid, ClientAlertsSystem alerts, AlertState alert)
    {
        if (_entities.TryGetComponent(uid, out StaminaComponent? stamina) && stamina.CritThreshold > 0f)
        {
            if (stamina.Critical)
                return 0f;

            var damage = _entities.System<StaminaSystem>().GetStaminaDamage(uid, stamina);
            return Math.Clamp(1f - damage / stamina.CritThreshold, 0f, 1f);
        }

        return GetAlertFraction(alerts, alert);
    }

    private static float GetAlertFraction(ClientAlertsSystem alerts, AlertState alert)
    {
        if (alert.Severity is not { } severity || !alerts.TryGet(alert.Type, out var prototype) ||
            prototype.MaxSeverity <= prototype.MinSeverity)
            return 0f;

        return Math.Clamp((float) (severity - prototype.MinSeverity) / (prototype.MaxSeverity - prototype.MinSeverity), 0f, 1f);
    }

    #region Blocks

    private sealed class HudSprites
    {
        public readonly Texture[] Digits = new Texture[10];
        public readonly Texture Cross;
        public readonly Texture Stamina;

        public HudSprites(RSI rsi)
        {
            for (var i = 0; i < Digits.Length; i++)
            {
                Digits[i] = Get(rsi, $"num_{i}");
            }

            Cross = Get(rsi, "cross");
            Stamina = Get(rsi, "stamina");
        }

        private static Texture Get(RSI rsi, string state)
        {
            return rsi.TryGetState(state, out var found) ? found.Frame0 : Texture.Transparent;
        }
    }

    private abstract class HudBlock : Control
    {
        private const float IdleAlpha = 0.26f;
        private const float ChangeAlpha = 0.56f;
        private const float FadeSeconds = 1.8f;

        protected readonly HudSprites Sprites;
        protected int Value = -1;
        private readonly float _numberX;
        private readonly float _trailingWidth;
        private float _scale;
        private float _fade;
        protected float NumberRight => _numberX + DigitWidth * (Value >= 100 ? 3 : Value >= 10 ? 2 : 1);
        public float NativeWidth => NumberRight + _trailingWidth;
        public float MaxNativeWidth => _numberX + DigitWidth * 3 + _trailingWidth;

        protected HudBlock(HudSprites sprites, string tooltip, float numberX, float trailingWidth = 0f)
        {
            Sprites = sprites;
            _numberX = numberX;
            _trailingWidth = trailingWidth;
            Visible = false;
            MouseFilter = MouseFilterMode.Pass;
            ToolTip = Loc.GetString(tooltip);
            VerticalAlignment = VAlignment.Bottom;
            SetScale(PreferredScale);
        }

        public void SetScale(float scale)
        {
            _scale = scale;
            MinSize = new Vector2(NativeWidth, BlockHeight) * scale;
        }

        public void SetValue(int value)
        {
            if (value == Value)
                return;

            Value = value;
            _fade = 1f;
        }

        public void Tick(float delta)
        {
            if (_fade > 0f)
                _fade = MathF.Max(0f, _fade - delta / FadeSeconds);
        }

        protected float Alpha => MathHelper.Lerp(IdleAlpha, ChangeAlpha, _fade);
        protected float Unit => _scale * UIScale;

        protected void DrawSprite(DrawingHandleScreen handle, Texture texture, float x, Color color)
        {
            var unit = Unit;
            var left = MathF.Round(x * unit);
            handle.DrawTextureRect(texture, new UIBox2(left, 0f,
                left + MathF.Round(texture.Width * unit), MathF.Round(texture.Height * unit)), color);
        }

        protected void DrawNumber(DrawingHandleScreen handle, Color color)
        {
            var value = Math.Max(Value, 0);
            var x = _numberX;
            if (value >= 100)
            {
                DrawSprite(handle, Sprites.Digits[value / 100 % 10], x, color);
                x += DigitWidth;
            }
            if (value >= 10)
            {
                DrawSprite(handle, Sprites.Digits[value / 10 % 10], x, color);
                x += DigitWidth;
            }
            DrawSprite(handle, Sprites.Digits[value % 10], x, color);
        }
    }

    private sealed class HealthBlock : HudBlock
    {
        private const float NumberX = 22f;
        private const float DividerGap = 2f;

        public HealthBlock(HudSprites sprites)
            : base(sprites, "blackm-vitals-health-tooltip", NumberX, DividerGap + 1f)
        {
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var value = Math.Max(Value, 0);
            var alpha = value <= 15 ? MathF.Max(Alpha, 0.62f) : Alpha;
            var color = (value > 25 ? HudOrange : PainRed).WithAlpha(alpha);

            DrawSprite(handle, Sprites.Cross, 0f, color);
            DrawNumber(handle, color);
            var left = MathF.Round((NumberRight + DividerGap) * Unit);
            var top = MathF.Round(10f * Unit);
            handle.DrawRect(new UIBox2(left, top, left + MathF.Max(1f, MathF.Round(Unit)), MathF.Round(24f * Unit)),
                HudOrange.WithAlpha(alpha * 0.6f));
        }
    }

    private sealed class StaminaBlock : HudBlock
    {
        private const float IconWidth = 24f;
        private const float IconScale = 0.875f;

        public StaminaBlock(HudSprites sprites)
            : base(sprites, "blackm-vitals-stamina-tooltip", IconWidth)
        {
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var alpha = Alpha;
            var color = HudOrange.WithAlpha(alpha);

            var icon = Sprites.Stamina;
            var unit = Unit;
            var width = icon.Width * IconScale;
            var height = icon.Height * IconScale;
            var left = (IconWidth - width) / 2f;
            var top = (BlockHeight - height) / 2f;
            var bounds = new UIBox2(MathF.Round(left * unit), MathF.Round(top * unit),
                MathF.Round((left + width) * unit), MathF.Round((top + height) * unit));
            handle.DrawTextureRect(icon, bounds, HudOrange.WithAlpha(alpha * 0.3f));

            var fraction = Math.Clamp(Value / 100f, 0f, 1f);
            var cut = MathF.Round(icon.Height * (1f - fraction));
            if (cut < icon.Height)
            {
                var fillTop = MathF.Round(MathHelper.Lerp(bounds.Top, bounds.Bottom, cut / icon.Height));
                handle.DrawTextureRectRegion(icon, new UIBox2(bounds.Left, fillTop, bounds.Right, bounds.Bottom),
                    new UIBox2(0f, cut, icon.Width, icon.Height), color);
            }

            DrawNumber(handle, color);
        }
    }

    #endregion
}
