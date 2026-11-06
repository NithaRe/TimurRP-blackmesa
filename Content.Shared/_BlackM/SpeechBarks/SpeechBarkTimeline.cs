namespace Content.Shared._BlackM.SpeechBarks;

public readonly record struct BarkCue(int Index, float Time, float PitchShift);

public sealed class SpeechBarkTimeline
{
    public const float MaxTypeDuration = 2.5f;
    public const float MinTypeInterval = 0.02f;
    public const float MaxTypeInterval = 0.09f;
    public const float MaxTotalDuration = 7f;
    public const float MinCueGap = 0.045f;
    public const int MaxDigitCues = 6;

    private const string Vowels = "аеёиоуыэюяaeiouy";

    public static readonly SpeechBarkTimeline Empty = new(Array.Empty<float>(), new List<BarkCue>(), 0f);

    public readonly float[] RevealTimes;
    public readonly IReadOnlyList<BarkCue> Cues;
    public readonly float Duration;

    public int Length => RevealTimes.Length;

    private SpeechBarkTimeline(float[] revealTimes, List<BarkCue> cues, float duration)
    {
        RevealTimes = revealTimes;
        Cues = cues;
        Duration = duration;
    }

    public static float GetTypeInterval(int visibleChars, float speed = 1f)
    {
        var interval = Math.Clamp(MaxTypeDuration / Math.Max(visibleChars, 1), MinTypeInterval, MaxTypeInterval);
        return interval / Math.Max(speed, 0.1f);
    }

    public static SpeechBarkTimeline Build(string text, float speed, BarkMode mode, IReadOnlySet<int>? lineStarts = null)
    {
        if (string.IsNullOrEmpty(text))
            return Empty;

        var times = BuildRevealTimes(text, speed, out var duration);
        var cues = BuildCues(text, times, mode, lineStarts);
        ApplyMinGap(cues);
        ApplyIntonation(text, cues);
        return new SpeechBarkTimeline(times, cues, duration);
    }

    public int RevealedAt(float time)
    {
        var count = 0;
        while (count < RevealTimes.Length && RevealTimes[count] <= time)
            count++;

        return count;
    }

    private static float[] BuildRevealTimes(string text, float speed, out float duration)
    {
        var visible = 0;
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
                visible++;
        }

        var interval = GetTypeInterval(visible, speed);
        var pauseScale = 1f / Math.Max(speed, 0.1f);
        var times = new float[text.Length];
        var lastLetter = LastLetterIndex(text);
        var seenLetter = false;
        var t = 0f;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            times[i] = t;

            if (char.IsLetterOrDigit(c))
                seenLetter = true;

            t += char.IsWhiteSpace(c) ? interval * 0.5f : interval;

            var pause = GetPause(c);
            if (pause <= 0f || !seenLetter || i >= lastLetter)
                continue;

            if (i + 1 < text.Length && GetPause(text[i + 1]) > 0f)
                continue;

            t += pause * pauseScale;
        }

        if (t > MaxTotalDuration)
        {
            var scale = MaxTotalDuration / t;
            for (var i = 0; i < times.Length; i++)
            {
                times[i] *= scale;
            }

            t = MaxTotalDuration;
        }

        duration = t;
        return times;
    }

    private static List<BarkCue> BuildCues(string text, float[] times, BarkMode mode, IReadOnlySet<int>? lineStarts)
    {
        var cues = new List<BarkCue>();
        var i = 0;

        while (i < text.Length)
        {
            if (!char.IsLetterOrDigit(text[i]))
            {
                i++;
                continue;
            }

            var start = i;
            i++;
            while (i < text.Length && char.IsLetterOrDigit(text[i]) && (lineStarts == null || !lineStarts.Contains(i)))
            {
                i++;
            }

            AddWordCues(text, times, start, i, mode, cues);
        }

        return cues;
    }

    private static void AddWordCues(string text, float[] times, int start, int end, BarkMode mode, List<BarkCue> cues)
    {
        switch (mode)
        {
            case BarkMode.Letters:
                for (var i = start; i < end; i++)
                {
                    cues.Add(new BarkCue(i, times[i], VowelShift(text[i])));
                }
                return;

            case BarkMode.Words:
                cues.Add(new BarkCue(start, times[start], FirstVowelShift(text, start, end)));
                return;
        }

        var added = false;
        var digits = 0;
        var i2 = start;

        while (i2 < end)
        {
            var c = text[i2];

            if (char.IsDigit(c))
            {
                if (digits < MaxDigitCues)
                {
                    cues.Add(new BarkCue(i2, times[i2], 0f));
                    digits++;
                    added = true;
                }

                i2++;
                continue;
            }

            if (!IsVowel(c))
            {
                i2++;
                continue;
            }

            var onset = i2;
            if (!added)
                onset = start;
            else if (i2 - 1 >= start && char.IsLetter(text[i2 - 1]) && !IsVowel(text[i2 - 1]))
                onset = i2 - 1;

            cues.Add(new BarkCue(onset, times[onset], VowelShift(c)));
            added = true;

            while (i2 < end && IsVowel(text[i2]))
            {
                i2++;
            }
        }

        if (!added)
            cues.Add(new BarkCue(start, times[start], 0f));
    }

    private static void ApplyMinGap(List<BarkCue> cues)
    {
        if (cues.Count < 2)
            return;

        var last = cues[0].Time;
        var write = 1;

        for (var read = 1; read < cues.Count; read++)
        {
            if (cues[read].Time - last < MinCueGap)
                continue;

            last = cues[read].Time;
            cues[write++] = cues[read];
        }

        cues.RemoveRange(write, cues.Count - write);
    }

    private static void ApplyIntonation(string text, List<BarkCue> cues)
    {
        if (cues.Count == 0)
            return;

        var sentenceStart = -1;
        var cue = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '?' && c != '!' && c != '.' && c != '…')
                continue;

            if (i > 0 && IsTerminal(text[i - 1]))
                continue;

            var end = i;
            while (end < text.Length && IsTerminal(text[end]))
            {
                end++;
            }

            var hasQuestion = false;
            var hasExclaim = false;
            for (var j = i; j < end; j++)
            {
                hasQuestion |= text[j] == '?';
                hasExclaim |= text[j] == '!';
            }

            while (cue < cues.Count && cues[cue].Index < i)
            {
                cue++;
            }

            var lastCue = cue - 1;
            if (lastCue >= 0 && cues[lastCue].Index > sentenceStart)
            {
                if (hasQuestion)
                {
                    Shift(cues, lastCue, 0.14f);
                    if (lastCue - 1 >= 0 && cues[lastCue - 1].Index > sentenceStart)
                        Shift(cues, lastCue - 1, 0.06f);
                }
                else if (hasExclaim)
                {
                    Shift(cues, lastCue, 0.07f);
                }
                else
                {
                    Shift(cues, lastCue, -0.04f);
                }
            }

            sentenceStart = end - 1;
        }
    }

    private static void Shift(List<BarkCue> cues, int index, float shift)
    {
        cues[index] = cues[index] with { PitchShift = cues[index].PitchShift + shift };
    }

    private static bool IsTerminal(char c)
    {
        return c is '?' or '!' or '.' or '…';
    }

    private static float GetPause(char c)
    {
        return c switch
        {
            ',' or ';' or ':' => 0.12f,
            '.' or '!' or '?' or '…' => 0.25f,
            '—' or '–' => 0.08f,
            _ => 0f,
        };
    }

    private static int LastLetterIndex(string text)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (char.IsLetterOrDigit(text[i]))
                return i;
        }

        return -1;
    }

    private static bool IsVowel(char c)
    {
        return Vowels.IndexOf(char.ToLowerInvariant(c)) >= 0;
    }

    private static float FirstVowelShift(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (IsVowel(text[i]))
                return VowelShift(text[i]);
        }

        return 0f;
    }

    private static float VowelShift(char c)
    {
        return char.ToLowerInvariant(c) switch
        {
            'и' or 'i' => 0.07f,
            'е' or 'e' => 0.04f,
            'y' => 0.05f,
            'э' => 0.02f,
            'я' => 0.01f,
            'ё' => -0.02f,
            'о' or 'o' => -0.04f,
            'ы' or 'ю' => -0.05f,
            'у' or 'u' => -0.07f,
            _ => 0f,
        };
    }
}
