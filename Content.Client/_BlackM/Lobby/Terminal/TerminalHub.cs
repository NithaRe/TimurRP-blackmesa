using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Stylesheets;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._BlackM.Lobby.Terminal;

public sealed class TerminalHub : PanelContainer
{
    [Dependency] private readonly IClientPreferencesManager _prefs = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public event Action<string?>? Activated;

    private sealed class Entry
    {
        public string Kind = string.Empty;
        public string Title = string.Empty;
        public string? TabId;
        public ContainerButton Row = default!;
        public Label Marker = default!;
    }

    private enum Phase : byte { Idle, Waiting, Playing }

    private const float Stagger = 0.07f;
    private const float FadeIn = 0.2f;

    private readonly List<Entry> _entries = new();
    private readonly TerminalTypewriterLabel _title;
    private readonly TerminalTypewriterLabel _previewTitle;
    private readonly TerminalTypewriterLabel _previewBody;
    private int _selected = -1;
    private Phase _phase = Phase.Idle;
    private float _t;
    private int _nextSound;

    public TerminalHub()
    {
        IoCManager.InjectDependencies(this);

        var big = TerminalFonts.Bold(_cache, 20);
        var rowFont = TerminalFonts.Bold(_cache, 14);
        var normal = TerminalFonts.Bold(_cache, 12);
        var small = TerminalFonts.Bold(_cache, 11);

        StyleClasses.Add(StyleNano.StyleClassPanelTerminalBlackM);
        MouseFilter = MouseFilterMode.Pass;

        _title = new TerminalTypewriterLabel
        {
            FontOverride = TerminalFonts.Bold(_cache, 16),
            FontColorOverride = StyleNano.TerminalGreen,
            HorizontalAlignment = HAlignment.Left,
        };

        // ---- left: catalog
        var list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinSize = new Vector2(300, 0),
        };
        list.AddChild(new Label
        {
            Text = Loc.GetString("blackm-hub-catalog"),
            FontOverride = small,
            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.6f),
            Margin = new Thickness(2, 0, 0, 4),
        });

        AddEntry(list, rowFont, small, 1, "appearance", "humanoid-profile-editor-appearance-tab");
        AddEntry(list, rowFont, small, 2, "jobs", "humanoid-profile-editor-jobs-tab");
        AddEntry(list, rowFont, small, 3, "antags", "humanoid-profile-editor-antags-tab");
        AddEntry(list, rowFont, small, 4, "traits", "humanoid-profile-editor-traits-tab");
        AddEntry(list, rowFont, small, 5, "markings", "humanoid-profile-editor-markings-tab");
        AddEntry(list, rowFont, small, 6, "setup", null);

        // R
        _previewTitle = new TerminalTypewriterLabel
        {
            FontOverride = big,
            FontColorOverride = StyleNano.TerminalGreen,
            HorizontalAlignment = HAlignment.Left,
            CharsPerSecond = 120f,
        };

        _previewBody = new TerminalTypewriterLabel
        {
            FontOverride = normal,
            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.85f),
            HorizontalAlignment = HAlignment.Left,
            CursorEnabled = false,
            TickSound = false,
            CharsPerSecond = 320f,
        };

        var openButton = new Button
        {
            Text = Loc.GetString("blackm-hub-open"),
            HorizontalAlignment = HAlignment.Right,
            StyleClasses = { StyleNano.StyleClassButtonTerminalBlackM },
        };
        openButton.OnPressed += _ =>
        {
            if (_selected >= 0)
                Activated?.Invoke(_entries[_selected].TabId);
        };

        var preview = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = new Color(0.02f, 0.06f, 0.03f, 0.9f),
                BorderColor = StyleNano.TerminalGreenDim,
                BorderThickness = new Thickness(1),
            },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 8,
                    Margin = new Thickness(14),
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("blackm-hub-preview"),
                            FontOverride = small,
                            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.6f),
                        },
                        _previewTitle,
                        new PanelContainer
                        {
                            MinSize = new Vector2(0, 2),
                            PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.TerminalGreenDim },
                        },
                        new BoxContainer
                        {
                            VerticalExpand = true,
                            Orientation = BoxContainer.LayoutOrientation.Vertical,
                            Children = { _previewBody },
                        },
                        openButton,
                    },
                },
            },
        };

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            MinSize = new Vector2(940, 400),
            Children = { list, preview },
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(14),
            Children =
            {
                _title,
                new PanelContainer
                {
                    MinSize = new Vector2(0, 2),
                    PanelOverride = new StyleBoxFlat { BackgroundColor = StyleNano.TerminalGreenDim },
                },
                body,
            },
        });
    }

    private void AddEntry(BoxContainer list, Font rowFont, Font small, int number, string kind, string? tabId)
    {
        var title = (tabId != null ? Loc.GetString(tabId) : Loc.GetString("blackm-hub-setup-title"))
            .ToUpperInvariant();

        var marker = new Label
        {
            Text = ">",
            FontOverride = rowFont,
            FontColorOverride = StyleNano.TerminalGreen,
            MinSize = new Vector2(16, 0),
            Modulate = new Color(1f, 1f, 1f, 0f),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        var row = new ContainerButton
        {
            MinSize = new Vector2(300, 48),
            HorizontalExpand = true,
            StyleClasses = { StyleNano.StyleClassJobButtonTerminalBlackM },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    MouseFilter = MouseFilterMode.Ignore,
                    Children =
                    {
                        marker,
                        new Label
                        {
                            Text = $"{number:00}",
                            FontOverride = small,
                            FontColorOverride = StyleNano.TerminalGreen.WithAlpha(0.5f),
                            MinSize = new Vector2(24, 0),
                            VerticalAlignment = VAlignment.Center,
                            MouseFilter = MouseFilterMode.Ignore,
                        },
                        new Label
                        {
                            Text = title,
                            FontOverride = rowFont,
                            FontColorOverride = StyleNano.TerminalGreen,
                            ClipText = true,
                            HorizontalExpand = true,
                            VerticalAlignment = VAlignment.Center,
                            MouseFilter = MouseFilterMode.Ignore,
                        },
                    },
                },
            },
        };

        var entry = new Entry { Kind = kind, Title = title, TabId = tabId, Row = row, Marker = marker };
        var index = _entries.Count;

        row.OnMouseEntered += _ => Select(index);
        row.OnPressed += _ => Activated?.Invoke(entry.TabId);

        _entries.Add(entry);
        list.AddChild(row);
    }

    private void Select(int index)
    {
        if (index == _selected)
            return;

        _selected = index;

        for (var i = 0; i < _entries.Count; i++)
        {
            var selected = i == index;
            _entries[i].Marker.Modulate = new Color(1f, 1f, 1f, selected ? 1f : 0f);
            _entries[i].Row.Pressed = selected;
        }

        var entry = _entries[index];
        _previewTitle.Reveal(entry.Title);
        _previewBody.Reveal(BuildBody(entry.Kind));
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
        _title.Reveal(">> " + Loc.GetString("blackm-hub-title"), 90f);

        _selected = -1;
        Select(Math.Max(0, _selected));
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_phase != Phase.Playing)
            return;

        _t += Math.Min(args.DeltaSeconds, 0.05f);

        for (var i = 0; i < _entries.Count; i++)
        {
            var a = Math.Clamp((_t - i * Stagger) / FadeIn, 0f, 1f);
            _entries[i].Row.Modulate = new Color(1f, 1f, 1f, a);

            if (a > 0f && i >= _nextSound)
            {
                _nextSound = i + 1;
                TerminalSounds.Play(TerminalSounds.Tick, -14f, 0.1f, 20);
            }
        }

        if (_t > _entries.Count * Stagger + FadeIn)
        {
            SetAlphaForAll(1f);
            _phase = Phase.Idle;
        }
    }

    private void SetAlphaForAll(float alpha)
    {
        foreach (var entry in _entries)
            entry.Row.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    private static string Field(string keyId, string value)
    {
        return (Loc.GetString(keyId) + " ").PadRight(15, '.') + " " + value;
    }

    private string BuildBody(string kind)
    {
        var desc = Loc.GetString($"blackm-hub-{kind}-desc");
        var none = Loc.GetString("blackm-hub-none");
        var profile = _prefs.Preferences?.SelectedCharacter as HumanoidCharacterProfile;
        var lines = new List<string> { desc, string.Empty };

        switch (kind)
        {
            case "appearance" when profile != null:
                lines.Add(Field("blackm-field-name", profile.Name));
                lines.Add(Field("blackm-field-species",
                    _proto.TryIndex(profile.Species, out var species)
                        ? Loc.GetString(species.Name)
                        : profile.Species.ToString()));
                lines.Add(Field("blackm-field-age", profile.Age.ToString()));
                lines.Add(Field("blackm-field-sex",
                    Loc.GetString($"blackm-sex-{profile.Sex.ToString().ToLowerInvariant()}")));
                break;

            case "jobs" when profile != null:
                var high = new List<string>();
                var medium = 0;
                var low = 0;
                foreach (var (jobId, priority) in profile.JobPriorities)
                {
                    switch (priority)
                    {
                        case JobPriority.High when _proto.TryIndex(jobId, out var job):
                            high.Add(job.LocalizedName);
                            break;
                        case JobPriority.Medium:
                            medium++;
                            break;
                        case JobPriority.Low:
                            low++;
                            break;
                    }
                }

                lines.Add(Field("blackm-field-high", high.Count == 0 ? none : string.Empty).TrimEnd());
                foreach (var name in high)
                    lines.Add("   - " + name);
                lines.Add(Field("blackm-field-medium", medium.ToString()));
                lines.Add(Field("blackm-field-low", low.ToString()));
                break;

            case "antags" when profile != null:
                var names = new List<string>();
                foreach (var id in profile.AntagPreferences)
                {
                    if (_proto.TryIndex(id, out var antag))
                        names.Add(Loc.GetString(antag.Name));
                }

                lines.Add(Field("blackm-field-antags", names.Count == 0 ? none : names.Count.ToString()).TrimEnd());
                foreach (var name in names)
                    lines.Add("   - " + name);
                break;

            case "setup":
                lines.Add(Field("blackm-field-slots", (_prefs.Preferences?.Characters.Count ?? 0).ToString()));
                break;
        }

        return string.Join("\n", lines);
    }
}
