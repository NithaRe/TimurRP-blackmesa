using System.Linq;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.StartingLoot;

public sealed class StartingLootPanel : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntitySystemManager _sys = default!;

    private static readonly ResPath DiceRsi = new("/Textures/Objects/Fun/dice.rsi");
    private const string BagState = "dicebag";
    private const string FaceStatePrefix = "d6_";

    private const float BagScale = 3f;
    private const float DiceSize = 128f;
    private const float ItemSize = 128f;

    private const float FallTime = 0.6f;
    private const float Bounce1Time = 0.28f;
    private const float Bounce1Height = 34f;
    private const float Bounce2Time = 0.18f;
    private const float Bounce2Height = 12f;

    private const float ThrowDuration = 2.2f;
    private const float DiceFlight = 1.5f;
    private const float HoldTime = 7f;
    private const float FadeTime = 0.9f;
    private const float IdleTimeout = 120f;

    private static readonly Vector2 BagPos = new(0, 20);
    private static readonly Vector2 DicePos = new(0, -140);
    private static readonly Vector2[] ItemPos = { new(-120, 30), new(120, 30) };

    private enum Phase { Drop, Idle, Shake, Throw, Done }

    public event Action? BagDropped;
    public event Action? BagLanded;
    public event Action? BagClicked;
    public event Action? DiceThrown;

    private readonly TextureRect _bag;
    private readonly TextureRect _dice;
    private readonly List<OutlinedSprite> _items = new();
    private readonly List<Label> _names = new();
    private readonly List<Texture> _faces = new();
    private readonly Label _clickHint;
    private readonly Label _hint1;
    private readonly Label _hint2;
    private readonly Vector2 _bagBase;

    private Phase _phase = Phase.Drop;
    private float _time;
    private float _phaseTime;
    private float _doneTime;
    private float _faceTimer;
    private float _fade = 1f;
    private int _curFace = -1;
    private bool _closing;
    private bool _dropStarted;
    private bool _landed;
    private readonly int _finalFace;

    private Vector2 _bagOffset;
    private Vector2 _bagSquash = Vector2.One;
    private Vector2 _dicePos;
    private readonly Vector2[] _itemPos = new Vector2[2];

    public StartingLootPanel(List<EntProtoId> results)
    {
        IoCManager.InjectDependencies(this);

        MouseFilter = MouseFilterMode.Ignore;

        Texture? bagTex = null;
        if (_cache.TryGetResource<RSIResource>(DiceRsi, out var rsiRes))
        {
            var rsi = rsiRes.RSI;
            if (rsi.TryGetState(BagState, out var bagState))
                bagTex = bagState.Frame0;

            for (var i = 1; i <= 6; i++)
            {
                if (rsi.TryGetState(FaceStatePrefix + i, out var face))
                    _faces.Add(face.Frame0);
            }
        }

        _finalFace = _faces.Count > 0 ? _random.Next(_faces.Count) : 0;
        _bagBase = bagTex != null
            ? new Vector2(bagTex.Width, bagTex.Height) * BagScale
            : new Vector2(96, 96);

        _bag = new TextureRect
        {
            Texture = bagTex,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore
        };
        _bag.OnKeyBindDown += OnBagKeyDown;

        _dice = new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepCentered,
            TextureScale = new Vector2(4, 4),
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false
        };

        AddChild(_bag);
        AddChild(_dice);

        var sprites = _sys.GetEntitySystem<SpriteSystem>();

        foreach (var id in results.Take(2))
        {
            var proto = _proto.Index<EntityPrototype>(id);

            Texture? tex = null;
            try
            {
                tex = sprites.Frame0(proto);
            }
            catch (Exception)
            {
            }

            var item = new OutlinedSprite { Texture = tex, Scale = 3f, Visible = false };
            var name = new Label
            {
                Text = proto.Name,
                FontColorOverride = Color.FromHex("#f2f2f2"),
                MouseFilter = MouseFilterMode.Ignore,
                Visible = false
            };

            AddChild(item);
            AddChild(name);
            _items.Add(item);
            _names.Add(name);
        }

        _clickHint = MakeLabel(Loc.GetString("blackm-startloot-click"), "#c9ccd6");
        _hint1 = MakeLabel(Loc.GetString("blackm-startloot-hint-1"), "#9aa0ab");
        _hint2 = MakeLabel(Loc.GetString("blackm-startloot-hint-2"), "#9aa0ab");
        _clickHint.Modulate = Color.Transparent;
        _hint1.Modulate = Color.Transparent;
        _hint2.Modulate = Color.Transparent;

        AddChild(_clickHint);
        AddChild(_hint1);
        AddChild(_hint2);

        _bagOffset = new Vector2(0, -3000);
    }

    private static Label MakeLabel(string text, string color)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = Color.FromHex(color),
            MouseFilter = MouseFilterMode.Ignore
        };
    }

    private Color A(float alpha) => Color.White.WithAlpha(Clamp01(alpha) * _fade);

    private void OnBagKeyDown(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick || _phase != Phase.Idle)
            return;

        args.Handle();
        BagClicked?.Invoke();
        SetPhase(Phase.Shake);
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        _phaseTime = 0f;

        switch (phase)
        {
            case Phase.Idle:
                _bagOffset = Vector2.Zero;
                _bagSquash = Vector2.One;
                _bag.MouseFilter = MouseFilterMode.Stop;
                break;

            case Phase.Shake:
                _bag.MouseFilter = MouseFilterMode.Ignore;
                _clickHint.Visible = false;
                break;

            case Phase.Throw:
                DiceThrown?.Invoke();
                _dice.Visible = _faces.Count > 0;
                foreach (var i in _items)
                    i.Visible = true;
                foreach (var n in _names)
                    n.Visible = true;
                break;
        }
    }

    public void Close()
    {
        _closing = true;
        if (Parent != null)
            Orphan();
    }

    private void RequestClose()
    {
        if (_closing)
            return;

        _closing = true;
        Timer.Spawn(0, () =>
        {
            if (Parent != null)
                Orphan();
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_closing)
            return;

        if (_phase == Phase.Drop && Size.Y <= 0f)
        {
            InvalidateArrange();
            return;
        }

        if (_phase == Phase.Drop && !_dropStarted)
        {
            _dropStarted = true;
            BagDropped?.Invoke();
        }

        var dt = args.DeltaSeconds;
        _time += dt;
        _phaseTime += dt;

        switch (_phase)
        {
            case Phase.Drop:
                UpdateDrop();
                break;

            case Phase.Idle:
                _bagOffset = new Vector2(0, MathF.Sin(_time * 2.2f) * 4f);
                _clickHint.Modulate = A(Clamp01(_phaseTime / 0.4f) * (0.55f + 0.45f * MathF.Sin(_time * 3f)));
                if (_time > IdleTimeout)
                    RequestClose();
                break;

            case Phase.Shake:
            {
                const float dur = 0.35f;
                var p = MathF.Min(_phaseTime / dur, 1f);
                _bagOffset = new Vector2(MathF.Sin(_phaseTime * 70f) * 6f * (1f - p), 0);
                if (_phaseTime >= dur)
                {
                    _bagOffset = Vector2.Zero;
                    SetPhase(Phase.Throw);
                }
                break;
            }

            case Phase.Throw:
            case Phase.Done:
                UpdateThrow(dt);
                break;
        }

        InvalidateArrange();
    }

    private void UpdateDrop()
    {
        var startY = -(Size.Y / 2f + _bagBase.Y);
        var t = _phaseTime;

        if (t < FallTime)
        {
            var u = t / FallTime;
            _bagOffset = new Vector2(0, startY * (1f - u * u));
            _bagSquash = new Vector2(0.92f, 1.1f);
            return;
        }

        if (!_landed)
        {
            _landed = true;
            BagLanded?.Invoke();
        }

        var t1 = t - FallTime;
        float y;
        float lift;

        if (t1 < Bounce1Time)
        {
            var u = t1 / Bounce1Time;
            lift = 4f * u * (1f - u);
            y = -Bounce1Height * lift;
        }
        else if (t1 < Bounce1Time + Bounce2Time)
        {
            var u = (t1 - Bounce1Time) / Bounce2Time;
            lift = 4f * u * (1f - u);
            y = -Bounce2Height * lift;
        }
        else
        {
            SetPhase(Phase.Idle);
            return;
        }

        var sinceImpact = t1 < Bounce1Time ? t1 : t1 - Bounce1Time;
        var squash = MathF.Sin(MathF.PI * Clamp01(sinceImpact / 0.14f)) * (t1 < Bounce1Time ? 0.28f : 0.12f);

        _bagOffset = new Vector2(0, y);
        _bagSquash = new Vector2(1f + squash, 1f - squash);
    }

    private void UpdateThrow(float dt)
    {
        var ba = 1f - Clamp01(_phaseTime / 0.45f);
        _bagSquash = Vector2.One;
        if (_bag.Visible)
        {
            _bag.Modulate = A(ba);
            if (ba <= 0f)
                _bag.Visible = false;
        }

        var dp = Clamp01(_phaseTime / DiceFlight);
        var de = EaseOutCubic(dp);
        _dicePos = Vector2.Lerp(BagPos, DicePos, de) + new Vector2(0, -MathF.Sin(MathF.PI * dp) * 50f);
        _dice.Modulate = A(1f);

        if (_faces.Count > 0)
        {
            if (dp >= 1f)
            {
                _dice.Texture = _faces[_finalFace];
            }
            else
            {
                _faceTimer -= dt;
                if (_faceTimer <= 0f)
                {
                    _faceTimer = 0.05f + 0.16f * dp * dp;
                    int next;
                    do
                    {
                        next = _random.Next(_faces.Count);
                    } while (next == _curFace && _faces.Count > 1);

                    _curFace = next;
                    _dice.Texture = _faces[next];
                }
            }
        }

        for (var i = 0; i < _items.Count; i++)
        {
            var start = 0.2f + 0.25f * i;
            var p = Clamp01((_phaseTime - start) / 0.8f);
            var e = EaseOutCubic(p);

            var pos = Vector2.Lerp(BagPos, ItemPos[i], e) + new Vector2(0, -MathF.Sin(MathF.PI * p) * 60f);
            if (p >= 1f)
                pos += new Vector2(0, MathF.Sin(_time * 2f + i * 1.7f) * 3f);

            _itemPos[i] = pos;
            _items[i].Alpha = Clamp01(p * 3f) * _fade;
            _names[i].Modulate = A((p - 0.6f) * 2.5f);
        }

        var ta = Clamp01((_phaseTime - 1.5f) / 0.7f);
        _hint1.Modulate = A(ta);
        _hint2.Modulate = A(ta);

        if (_phase == Phase.Throw && _phaseTime >= ThrowDuration)
        {
            _phase = Phase.Done;
            _doneTime = 0f;
        }
        else if (_phase == Phase.Done)
        {
            _doneTime += dt;
            if (_doneTime > HoldTime)
                _fade = 1f - Clamp01((_doneTime - HoldTime) / FadeTime);

            if (_doneTime > HoldTime + FadeTime)
                RequestClose();
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _bag.Measure(_bagBase * 1.5f);
        _dice.Measure(new Vector2(DiceSize, DiceSize));

        foreach (var i in _items)
            i.Measure(new Vector2(ItemSize, ItemSize));
        foreach (var n in _names)
            n.Measure(availableSize);

        _clickHint.Measure(availableSize);
        _hint1.Measure(availableSize);
        _hint2.Measure(availableSize);

        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var o = finalSize / 2f;

        var bagSize = _bagBase * _bagSquash;
        var bagCenter = o + BagPos + _bagOffset + new Vector2(0, (_bagBase.Y - bagSize.Y) / 2f);
        Place(_bag, bagCenter, bagSize);

        Place(_dice, o + _dicePos, new Vector2(DiceSize, DiceSize));

        for (var i = 0; i < _items.Count; i++)
        {
            Place(_items[i], o + _itemPos[i], new Vector2(ItemSize, ItemSize));
            PlaceDesired(_names[i], o + _itemPos[i] + new Vector2(0, 76));
        }

        PlaceDesired(_clickHint, o + new Vector2(0, 100));
        PlaceDesired(_hint1, o + new Vector2(0, 170));
        PlaceDesired(_hint2, o + new Vector2(0, 192));

        return finalSize;
    }

    private static void Place(Control c, Vector2 center, Vector2 size)
    {
        c.Arrange(UIBox2.FromDimensions(center - size / 2f, size));
    }

    private static void PlaceDesired(Control c, Vector2 center)
    {
        Place(c, center, c.DesiredSize);
    }

    private static float Clamp01(float x) => Math.Clamp(x, 0f, 1f);
    private static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - x, 3f);
}
