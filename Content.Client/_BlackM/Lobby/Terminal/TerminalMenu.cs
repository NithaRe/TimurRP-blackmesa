using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalMenu : BoxContainer
{
    private enum Phase : byte { Idle, Waiting, Playing }

    private const float Stagger = 0.07f;
    private const float FadeIn = 0.2f;

    private Phase _phase = Phase.Idle;
    private float _t;
    private float _time;
    private int _nextSound;
    private BaseButton? _hover;

    protected override void ChildAdded(Control newChild)
    {
        base.ChildAdded(newChild);

        if (newChild is not BaseButton button)
            return;

        button.OnMouseEntered += _ => _hover = button;
        button.OnMouseExited += _ =>
        {
            if (_hover == button)
                _hover = null;
        };
    }

    public void HoldHidden()
    {
        _phase = Phase.Waiting;
        SetAlphaForAll(0f);
    }

    public void PlayIntro()
    {
        _phase = Phase.Playing;
        _t = 0f;
        _nextSound = 0;
        SetAlphaForAll(0f);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += Math.Min(args.DeltaSeconds, 0.05f);

        if (_phase != Phase.Playing)
            return;

        _t += Math.Min(args.DeltaSeconds, 0.05f);

        var i = 0;
        foreach (var child in Children)
        {
            var a = Math.Clamp((_t - i * Stagger) / FadeIn, 0f, 1f);
            child.Modulate = new Color(1f, 1f, 1f, a);

            if (a > 0f && i >= _nextSound)
            {
                _nextSound = i + 1;
                TerminalSounds.Play(TerminalSounds.Tick, -14f, 0.12f, 20);
            }

            i++;
        }

        if (_t > ChildCount * Stagger + FadeIn)
        {
            SetAlphaForAll(1f);
            _phase = Phase.Idle;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_hover == null || _hover.Disabled || !_hover.Visible || _phase != Phase.Idle)
            return;

        var pos = _hover.PixelPosition;
        var size = _hover.PixelSize;
        var pulse = 0.65f + 0.35f * MathF.Sin(_time * 9f);
        var color = StyleNano.TerminalGreen.WithAlpha(pulse);

        handle.DrawRect(new UIBox2(pos.X - 10, pos.Y, pos.X - 6, pos.Y + size.Y), color);
    }

    private void SetAlphaForAll(float alpha)
    {
        foreach (var child in Children)
            child.Modulate = new Color(1f, 1f, 1f, alpha);
    }
}
