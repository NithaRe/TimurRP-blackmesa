using System.Numerics;
using Content.Shared._BlackM.ChatImage;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.ChatImage;

public sealed class ChatImageControl : Control
{
    private readonly IGameTiming _timing;
    private readonly IUserInterfaceManager _ui;
    private readonly int _id;
    private ChatImageData? _data;

    private Vector2 _displaySize = new(96, 64);

    public ChatImageControl(int id)
    {
        _id = id;
        _timing = IoCManager.Resolve<IGameTiming>();
        _ui = IoCManager.Resolve<IUserInterfaceManager>();

        if (ChatImageStore.TryGet(id, out var data))
            Apply(data);
        else
            MinSize = _displaySize;
    }

    private void Apply(ChatImageData data)
    {
        _data = data;

        var size = data.Frames[0].Size;
        var scale = MathF.Min(ChatImageConstants.MaxUpscale, MathF.Min(
            ChatImageConstants.MaxDisplayWidth / size.X,
            ChatImageConstants.MaxDisplayHeight / size.Y));

        _displaySize = new Vector2(size.X * scale, size.Y * scale);

        MinSize = _displaySize;
        MaxSize = _displaySize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (_data == null)
        {
            if (!ChatImageStore.TryGet(_id, out var late))
                return;

            Apply(late);
        }

        var data = _data!;

        var index = 0;
        if (data.Frames.Length > 1)
        {
            var t = (long) _timing.RealTime.TotalMilliseconds % data.TotalMs;
            for (var i = 0; i < data.Frames.Length; i++)
            {
                if (t < data.DelaysMs[i])
                {
                    index = i;
                    break;
                }

                t -= data.DelaysMs[i];
            }
        }

        var scale = _ui.RootControl.UIScale;
        var w = _displaySize.X * scale;
        var h = _displaySize.Y * scale;

        handle.DrawTextureRect(data.Frames[index], UIBox2.FromDimensions(0, 0, w, h));
    }
}