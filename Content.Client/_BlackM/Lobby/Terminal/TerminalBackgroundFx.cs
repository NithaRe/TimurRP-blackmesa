using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalBackgroundFx : Control
{
    [Dependency] private readonly IResourceCache _cache = default!;

    private static readonly Color Phosphor = new(0.2f, 1f, 0.4f);
    private static readonly string[] Glyphs = "0123456789ABCDEF".Select(c => c.ToString()).ToArray();

    private const int Slots = 16;
    private const int GridCell = 40;

    private struct Column
    {
        public float Y;
        public float Speed;
        public int Length;
    }

    private struct Flash
    {
        public float X;
        public float Y;
        public float Life;
    }

    private readonly Random _rng = new();
    private readonly Flash[] _flashes = new Flash[10];
    private readonly Font _font;

    private Column[] _columns = Array.Empty<Column>();
    private byte[] _glyphs = Array.Empty<byte>();
    private int _builtForWidth = -1;
    private float _time;
    private float _swapTimer;
    private float _flashTimer;

    public TerminalBackgroundFx()
    {
        IoCManager.InjectDependencies(this);
        MouseFilter = MouseFilterMode.Ignore;
        _font = TerminalFonts.Bold(_cache, 12);
    }

    private void Rebuild()
    {
        var w = PixelWidth;
        var h = Math.Max(PixelHeight, 1);
        _builtForWidth = w;

        var step = 44f * UIScale;
        var count = Math.Max(1, (int) (w / step));
        _columns = new Column[count];
        _glyphs = new byte[count * Slots];

        for (var i = 0; i < count; i++)
        {
            _columns[i] = NewColumn(_rng.NextSingle() * h);
            for (var k = 0; k < Slots; k++)
                _glyphs[i * Slots + k] = (byte) _rng.Next(Glyphs.Length);
        }
    }

    private Column NewColumn(float y)
    {
        return new Column
        {
            Y = y,
            Speed = (35f + _rng.NextSingle() * 70f) * UIScale,
            Length = 6 + _rng.Next(9),
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var w = PixelWidth;
        var h = PixelHeight;
        if (w <= 0 || h <= 0)
            return;

        if (w != _builtForWidth)
            Rebuild();

        var dt = Math.Min(args.DeltaSeconds, 0.05f);
        _time += dt;

        var lineH = 16f * UIScale;
        for (var i = 0; i < _columns.Length; i++)
        {
            var c = _columns[i];
            c.Y += c.Speed * dt;
            if (c.Y - c.Length * lineH > h)
                c = NewColumn(-_rng.NextSingle() * h * 0.5f);
            _columns[i] = c;
        }

        _swapTimer += dt;
        if (_swapTimer >= 0.12f)
        {
            _swapTimer = 0f;
            for (var n = 0; n < 8 && _glyphs.Length > 0; n++)
                _glyphs[_rng.Next(_glyphs.Length)] = (byte) _rng.Next(Glyphs.Length);
        }

        _flashTimer -= dt;
        if (_flashTimer <= 0f)
        {
            _flashTimer = 0.25f + _rng.NextSingle() * 0.35f;
            for (var i = 0; i < _flashes.Length; i++)
            {
                if (_flashes[i].Life > 0f)
                    continue;

                _flashes[i] = new Flash
                {
                    X = _rng.Next(Math.Max(1, w / GridCell)) * GridCell,
                    Y = _rng.Next(Math.Max(1, h / GridCell)) * GridCell,
                    Life = 1f,
                };
                break;
            }
        }

        for (var i = 0; i < _flashes.Length; i++)
            _flashes[i].Life = Math.Max(0f, _flashes[i].Life - dt * 1.1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var w = PixelWidth;
        var h = PixelHeight;
        if (w <= 0 || h <= 0 || _columns.Length == 0)
            return;

        var s = UIScale;

        // blinking grid cells
        foreach (var flash in _flashes)
        {
            if (flash.Life <= 0f)
                continue;

            handle.DrawRect(new UIBox2(flash.X, flash.Y, flash.X + GridCell, flash.Y + GridCell),
                Phosphor.WithAlpha(flash.Life * 0.09f));
        }

        // hex rain
        var step = 44f * s;
        var lineH = 16f * s;
        for (var i = 0; i < _columns.Length; i++)
        {
            var col = _columns[i];
            var x = i * step + step * 0.25f;

            for (var k = 0; k < col.Length; k++)
            {
                var y = col.Y - k * lineH;
                if (y < -lineH || y > h)
                    continue;

                var alpha = k == 0 ? 0.30f : 0.13f * (1f - k / (float) col.Length);
                handle.DrawString(_font, new Vector2(x, y), Glyphs[_glyphs[i * Slots + (k % Slots)]], s,
                    Phosphor.WithAlpha(alpha));
            }
        }

        // thin scan line
        var scanY = (_time * 0.07f % 1f) * h;
        handle.DrawRect(new UIBox2(0, scanY, w, scanY + 1f), Phosphor.WithAlpha(0.08f));

        // oscilloscope
        const int points = 120;
        var baseY = h * 0.94f;
        var x0 = w * 0.27f;
        var x1 = w * 0.73f;
        var amp = 14f * s;
        var prev = Vector2.Zero;
        for (var i = 0; i <= points; i++)
        {
            var t01 = i / (float) points;
            var env = MathF.Sin(MathF.PI * t01);
            var wave = 0.6f * MathF.Sin(t01 * 38f + _time * 4f)
                       + 0.3f * MathF.Sin(t01 * 91f - _time * 7f)
                       + 0.1f * MathF.Sin(t01 * 210f + _time * 13f);
            var p = new Vector2(x0 + (x1 - x0) * t01, baseY - env * amp * wave);

            if (i > 0)
                handle.DrawLine(prev, p, Phosphor.WithAlpha(0.35f));

            prev = p;
        }
    }
}
