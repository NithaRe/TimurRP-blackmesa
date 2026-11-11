using System.Numerics;
using Content.Shared._BlackM.Monorail;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._BlackM.Monorail;

public sealed class MonorailArrivalBoardVisualsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string TextPath = "Effects/text.rsi";
    private const string KeyPrefix = "mrb";
    private const string BlankState = "blank";
    private const int Rows = 2;
    private const int CharWidth = 4;
    private const float PixelSize = 1f / 32f;

    private static readonly Dictionary<char, string> CharStates = new()
    {
        { ':', "colon" },
        { '!', "exclamation" },
        { '?', "question" },
        { '*', "star" },
        { '+', "plus" },
        { '-', "dash" },
        { ' ', BlankState },
    };

    private readonly Dictionary<EntityUid, Drawn> _drawn = new();

    private sealed class Drawn
    {
        public readonly string[] Text = { "", "" };
        public readonly Color[] Colors = { Color.White, Color.White };
        public int Layers;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonorailArrivalBoardComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MonorailArrivalBoardComponent, ComponentShutdown>(OnShutdown);
    }

    private static string Key(int row, int col) => $"{KeyPrefix}{row}_{col}";

    private void OnStartup(Entity<MonorailArrivalBoardComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var drawn = new Drawn();
        for (var row = 0; row < Rows; row++)
        {
            for (var col = 0; col < ent.Comp.RowLength; col++)
            {
                var key = Key(row, col);
                var index = _sprite.LayerMapReserve((ent.Owner, sprite), key);
                _sprite.LayerSetRsi((ent.Owner, sprite), index, new ResPath(TextPath));
                _sprite.LayerSetRsiState((ent.Owner, sprite), index, BlankState);
                sprite.LayerSetShader(index, "unshaded");
            }
        }

        drawn.Layers = ent.Comp.RowLength;
        drawn.Text[0] = "\0";
        _drawn[ent] = drawn;
    }

    private void OnShutdown(Entity<MonorailArrivalBoardComponent> ent, ref ComponentShutdown args)
    {
        _drawn.Remove(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MonorailArrivalBoardComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var board, out var sprite))
        {
            if (!_drawn.TryGetValue(uid, out var drawn))
                continue;

            BuildText(board, now, out var top, out var bottom, out var topColor, out var bottomColor);

            if (drawn.Text[0] == top && drawn.Text[1] == bottom &&
                drawn.Colors[0] == topColor && drawn.Colors[1] == bottomColor)
                continue;

            DrawRow(uid, sprite, board, drawn, 0, top, topColor);
            DrawRow(uid, sprite, board, drawn, 1, bottom, bottomColor);
        }
    }

    private void BuildText(MonorailArrivalBoardComponent board, TimeSpan now,
        out string top, out string bottom, out Color topColor, out Color bottomColor)
    {
        var secs = now.TotalSeconds;
        var blink = secs % 1.0 < 0.5;
        var left = board.TargetTime - now;

        var showName = board.StationName != null &&
                       board.StationNamePeriod > 2f &&
                       secs % board.StationNamePeriod < 2.0;

        switch (board.Status)
        {
            case MonorailBoardStatus.Arriving when left <= TimeSpan.FromSeconds(1):
                top = Loc.GetString("monorail-board-arriving-now");
                bottom = blink ? "> > >" : " > > ";
                topColor = bottomColor = board.ArrivingColor;
                break;
            case MonorailBoardStatus.Arriving:
                top = Loc.GetString("monorail-board-arriving-in");
                bottom = FormatTime(left, blink);
                topColor = bottomColor = board.ArrivingColor;
                break;
            case MonorailBoardStatus.Boarding:
                top = Loc.GetString("monorail-board-boarding");
                bottom = FormatTime(left, blink);
                topColor = bottomColor = board.BoardingColor;
                break;
            case MonorailBoardStatus.Departing:
                top = Loc.GetString("monorail-board-doors");
                bottom = blink ? Loc.GetString("monorail-board-closing") : "";
                topColor = bottomColor = board.DepartingColor;
                showName = false;
                break;
            default:
                top = Loc.GetString("monorail-board-offline");
                bottom = blink ? "--:--" : "-- --";
                topColor = bottomColor = board.OfflineColor;
                showName = false;
                break;
        }

        if (showName)
        {
            top = board.StationName!;
            topColor = board.StationNameColor;
        }

        top = Clip(top, board.RowLength);
        bottom = Clip(bottom, board.RowLength);
    }

    private static string FormatTime(TimeSpan left, bool colon)
    {
        var total = Math.Max(0, (int) Math.Ceiling(left.TotalSeconds));
        var minutes = Math.Min(99, total / 60);
        var seconds = total % 60;
        return $"{minutes:D2}{(colon ? ':' : ' ')}{seconds:D2}";
    }

    private static string Clip(string text, int length)
    {
        text = text.Trim();
        return text.Length <= length ? text : text[..length];
    }

    private void DrawRow(EntityUid uid, SpriteComponent sprite, MonorailArrivalBoardComponent board,
        Drawn drawn, int row, string text, Color color)
    {
        drawn.Text[row] = text;
        drawn.Colors[row] = color;

        var len = text.Length;
        for (var col = 0; col < drawn.Layers; col++)
        {
            var key = Key(row, col);
            if (!_sprite.LayerMapTryGet((uid, sprite), key, out var index, false))
                continue;

            if (col >= len)
            {
                _sprite.LayerSetRsiState((uid, sprite), index, BlankState);
                continue;
            }

            var offset = new Vector2((col - len / 2f + 0.5f) * CharWidth, -row * board.RowSpacing) * PixelSize
                         + board.TextOffset;

            _sprite.LayerSetRsiState((uid, sprite), index, GetState(text[col]));
            _sprite.LayerSetOffset((uid, sprite), index, offset);
            _sprite.LayerSetColor((uid, sprite), index, color);
        }
    }

    private static string GetState(char c)
    {
        if (c == '>')
            return "dash";

        if (CharStates.TryGetValue(c, out var special))
            return special;

        if (c is >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё')
            return char.ToLowerInvariant(c).ToString();

        return BlankState;
    }
}
