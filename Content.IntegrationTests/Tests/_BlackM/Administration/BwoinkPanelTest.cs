using Content.Client.Administration.UI.Bwoink;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared.Administration;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._BlackM.Administration;

[TestFixture]
public sealed class BwoinkPanelTest
{
    [Test]
    public async Task InvalidMarkupDoesNotHideMessages()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Client.WaitAssertion(() =>
        {
            var panel = new BwoinkPanel(_ => { });
            var output = panel.FindControl<OutputPanel>("TextOutput");

            panel.ReceiveLine(new SharedBwoinkSystem.BwoinkTextMessage(default, default, "broken ["), unread: true);
            panel.ReceiveLine(new SharedBwoinkSystem.BwoinkTextMessage(default, default, "[bold]next[/bold]"), unread: true);

            Assert.That(output.EntryCount, Is.EqualTo(2));
            Assert.That(output.GetMessage(0).ToString(), Does.Contain("broken ["));
            Assert.That(output.GetMessage(1).ToString(), Does.Contain("next"));
            Assert.That(panel.Unread, Is.EqualTo(2));
            panel.MarkRead();
            Assert.That(panel.Unread, Is.Zero);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FilteringAndRefreshKeepTheSelectedConversation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var channels = new PlayerListControl { KeepSelectionWhenFiltered = true };
            var player = new PlayerInfo("player", "Character", string.Empty, string.Empty, false, null, null, 0,
                null, new NetUserId(Guid.NewGuid()), true, true, false, null);
            PlayerInfo? selected = null;
            channels.OnSelectionChanged += info => selected = info;
            channels.PopulateList([player]);
            channels.PlayerListContainer.Select(new PlayerListData(player));
            Assert.That(selected, Is.SameAs(player));

            channels.FindControl<LineEdit>("FilterLineEdit").SetText("no match", invokeEvent: true);
            Assert.That(channels.PlayerListContainer.Data, Is.Empty);
            Assert.That(selected, Is.SameAs(player));

            var updated = player with { CharacterName = "Updated" };
            channels.PopulateList([updated]);
            channels.StopFiltering();
            Assert.That(channels.PlayerListContainer.Data, Has.Count.EqualTo(1));
            Assert.That(selected, Is.SameAs(updated));
        });
        await pair.CleanReturnAsync();
    }
}
