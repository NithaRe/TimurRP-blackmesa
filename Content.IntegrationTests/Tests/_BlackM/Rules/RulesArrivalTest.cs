using System.Numerics;
using Content.Client._BlackM.Rules;
using Content.Client.UserInterface.Systems.Info;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._BlackM.Rules;

[TestFixture]
public sealed class RulesArrivalTest
{
    [Test]
    public async Task SceneAndRulesAcceptance()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        RulesArrivalPopup popup = null!;
        var accepted = 0;
        await client.WaitAssertion(() =>
        {
            var scene = new RulesArrivalScene();
            Assert.That(scene.TryStart(), Is.True, "The local evac map must load without requiring server entities.");
            scene.SetProgress(1, 0.5f);
            scene.Release();
            scene.Dispose();

            var ui = client.ResolveDependency<IUserInterfaceManager>();
            ui.GetUIController<InfoUIController>().RulesEntryId = "BlackMRuleset";
            popup = new RulesArrivalPopup();
            popup.Initialize(60, true);
            popup.OnAcceptPressed += () => accepted++;
            ui.WindowRoot.AddChild(popup);
            popup.Measure(new Vector2(800, 600));
            popup.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(800, 600)));
            Assert.That(popup.FindControl<PanelContainer>("Paper").Visible, Is.True);
            Assert.That(popup.FindControl<Button>("SignButton").Disabled, Is.True);
            Assert.That(accepted, Is.Zero, "Skipping the scene does not accept the rules.");
            popup.Initialize(0, true);
            Assert.That(popup.FindControl<Button>("SignButton").Disabled, Is.False);
        });
        var button = popup.FindControl<Button>("SignButton");
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            var coords = new ScreenCoordinates(button.GlobalPixelPosition + button.PixelSize / 2, button.Window?.Id ?? default);
            await client.DoGuiEvent(button, new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, state, coords, default, button.Size / 2, button.PixelSize / 2));
        }
        await client.WaitAssertion(() =>
        {
            Assert.That(accepted, Is.EqualTo(1));
            popup.Dispose();
        });
        await pair.CleanReturnAsync();
    }
}
