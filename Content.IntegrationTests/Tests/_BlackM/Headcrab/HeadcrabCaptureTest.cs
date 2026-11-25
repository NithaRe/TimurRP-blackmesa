using System.Linq;
using Content.Client.UserInterface.Systems.Inventory;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Server.DoAfter;
using Content.Shared._BlackM.Headcrab;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._BlackM.Headcrab;

[TestFixture]
public sealed class HeadcrabCaptureTest
{
    [Test]
    public async Task CaptureKeepsGearOnTheBodyAndShowsOnlyOneHand()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var (server, client) = pair;
        var map = await pair.CreateTestMap();
        var body = EntityUid.Invalid;
        var crab = EntityUid.Invalid;
        var headwear = EntityUid.Invalid;
        var backpack = EntityUid.Invalid;
        var tool = EntityUid.Invalid;
        var mind = EntityUid.Invalid;
        var activeHand = string.Empty;
        InventoryGui hud = null!;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var inventory = server.System<InventorySystem>();
            var hands = server.System<SharedHandsSystem>();
            body = entities.SpawnEntity("MobHuman", map.GridCoords);
            crab = entities.SpawnEntity("MobHeadcrabBlackM", map.GridCoords);
            headwear = entities.SpawnEntity("ClothingHeadHelmetBasic", map.GridCoords);
            backpack = entities.SpawnEntity("ClothingBackpack", map.GridCoords);
            tool = entities.SpawnEntity("Crowbar", map.GridCoords);

            var originalHands = hands.EnumerateHands(body).ToArray();
            Assert.That(originalHands, Has.Length.EqualTo(2));
            activeHand = originalHands[0];
            Assert.That(inventory.TryEquip(body, headwear, "head", force: true), Is.True);
            Assert.That(inventory.TryEquip(body, backpack, "back", force: true), Is.True);
            Assert.That(hands.TryPickup(body, tool, originalHands[1], checkActionBlocker: false), Is.True);
            entities.AddComponent<UnremoveableComponent>(headwear);
            entities.AddComponent<UnremoveableComponent>(tool);

            var player = server.ResolveDependency<ISharedPlayerManager>().Sessions.Single();
            var minds = server.System<SharedMindSystem>();
            mind = minds.CreateMind(player.UserId);
            minds.TransferTo(mind, body);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var ui = client.ResolveDependency<IUserInterfaceManager>();
            var controller = ui.GetUIController<InventoryUIController>();
            hud = new InventoryGui();
            ui.WindowRoot.AddChild(hud);
            controller.RegisterInventoryButton(hud.InventoryButton);
            controller.ReloadSlots();
            Assert.That(hud.InventoryButton.Visible, Is.True);
        });

        await server.WaitAssertion(() =>
        {
            server.System<SharedMindSystem>().TransferTo(mind, crab);
            var args = new DoAfterArgs(server.EntMan, crab, TimeSpan.Zero, new HeadcrabAttachDoAfterEvent(), crab, body)
            {
                NeedHand = false,
            };
            Assert.That(server.System<DoAfterSystem>().TryStartDoAfter(args), Is.True);
        });
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var inventory = server.System<InventorySystem>();
            var hands = server.System<SharedHandsSystem>();
            var containers = server.System<SharedContainerSystem>();

            Assert.That(entities.HasComponent<HeadcrabCapturedComponent>(body), Is.True);
            Assert.That(hands.EnumerateHands(body), Is.EqualTo(new[] { activeHand }));
            Assert.That(inventory.TryGetSlotEntity(body, "head", out var headItem), Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(headItem!.Value).EntityPrototype?.ID, Is.EqualTo("ClothingHeadHeadcrab"));
            Assert.That(inventory.TryGetSlotEntity(body, "back", out var backItem), Is.True);
            Assert.That(backItem, Is.EqualTo(backpack));
            Assert.That(containers.TryGetContainingContainer(headwear, out var stash), Is.True);
            Assert.That(stash!.Owner, Is.EqualTo(body));
            Assert.That(stash.ContainedEntities, Does.Contain(tool));

            hands.AddHand(body, "extra", HandLocation.Middle);
            Assert.That(hands.EnumerateHands(body), Is.EqualTo(new[] { activeHand }));
        });
        await pair.RunTicksSync(5);

        var clientBody = pair.ToClientUid(body);
        await client.WaitAssertion(() =>
        {
            var hands = client.System<SharedHandsSystem>();
            var inventory = client.System<InventorySystem>();
            Assert.That(hands.EnumerateHands(clientBody), Is.EqualTo(new[] { activeHand }));
            Assert.That(inventory.TryGetSlotEntity(clientBody, "back", out _), Is.True);
            Assert.That(client.EntMan.HasComponent<HeadcrabCapturedComponent>(clientBody), Is.True);
            Assert.That(hud.InventoryButton.Visible, Is.False);
            hud.Orphan();
        });
        await pair.CleanReturnAsync();
    }
}
