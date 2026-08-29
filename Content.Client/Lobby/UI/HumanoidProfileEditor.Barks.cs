// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Zekins <zekins3366@gmail.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later
//using Content.Goobstation.Common.Barks;
using System.Linq;
using Content.Client._BlackM.SpeechBarks;
using Content.Shared._BlackM.CCVar;
using Content.Shared._BlackM.SpeechBarks;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<BlackMBarkPrototype> _barkList = new();

    private void InitializeBarks()
    {
        if (!_cfgManager.GetCVar(BlackMCVars.BarksEnabled))
        {
            BarksContainer.Visible = false;
            BarkPitchContainer.Visible = false;
            return;
        }

        BarksContainer.Visible = true;
        BarkPitchContainer.Visible = true;

        _barkList = _prototypeManager
            .EnumeratePrototypes<BlackMBarkPrototype>()
            .Where(b => b.RoundStart)
            .OrderBy(b => Loc.GetString(b.Name))
            .ToList();

        BarkVoiceButton.Clear();

        for (var i = 0; i < _barkList.Count; i++)
        {
            BarkVoiceButton.AddItem(Loc.GetString(_barkList[i].Name), i);
        }

        BarkVoiceButton.OnItemSelected += args =>
        {
            BarkVoiceButton.SelectId(args.Id);
            SetBarkProto(_barkList[args.Id].ID);
        };

        BarkVoicePlayButton.OnPressed += _ => PlayPreviewBark();
        BarkPitchSlider.OnValueChanged += _ => SetBarkPitch(BarkPitchSlider.Value);
        BarkResetButton.OnPressed += _ => ResetBark();
    }

    private void UpdateBarkVoiceControls()
    {
        if (Profile is null || !_cfgManager.GetCVar(BlackMCVars.BarksEnabled))
            return;

        var index = _barkList.FindIndex(b => b.ID == Profile.Bark.Proto);
        if (index != -1)
            BarkVoiceButton.SelectId(index);

        BarkPitchSlider.Value = Profile.Bark.Pitch;
        BarkPitchLabel.Text = Profile.Bark.Pitch.ToString("F2");
    }

    private void SetBarkProto(string barkId)
    {
        Profile = Profile?.WithBark(Profile.Bark.WithProto(barkId));
        IsDirty = true;
    }

    private void SetBarkPitch(float pitch)
    {
        Profile = Profile?.WithBark(Profile.Bark.WithPitch(pitch));
        BarkPitchLabel.Text = pitch.ToString("F2");
        IsDirty = true;
    }

    private void ResetBark()
    {
        Profile = Profile?.WithBark(new BarkData(Profile.Bark.Proto, 1f, 0.1f, 0.5f));
        BarkPitchSlider.Value = 1f;
        BarkPitchLabel.Text = "1.00";
        IsDirty = true;
    }

    private void PlayPreviewBark()
    {
        if (Profile is null)
            return;

        _entManager.System<SpeechBarksSystem>().PlayDataPreview(
            Profile.Bark.Proto,
            Profile.Bark.Pitch,
            Profile.Bark.MinVar,
            Profile.Bark.MaxVar);
    }
}