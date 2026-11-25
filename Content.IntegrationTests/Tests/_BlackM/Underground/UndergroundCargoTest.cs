using System.Numerics;
using Content.Server._BlackM.Underground;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._BlackM.Underground;

[TestFixture]
public sealed class UndergroundCargoTest
{
    [Test]
    public async Task XenCreaturesStayBehindEvenInsideCargo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var cargoSystem = server.System<UndergroundCargoSystem>();
            var containers = server.System<SharedContainerSystem>();
            var origin = entities.SpawnEntity(null, map.GridCoords);
            var destination = entities.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            var fuel = entities.SpawnEntity("AmeJarHighCapacity", map.GridCoords);
            entities.SpawnEntity("MobHeadcrabBlackM", map.GridCoords);
            var stowaway = entities.SpawnEntity("MobHoundeyeStationBlackM", map.GridCoords);
            var container = containers.EnsureContainer<Container>(fuel, "test");

            Assert.That(cargoSystem.TryCollect(origin, 1f, out var cargo), Is.True);
            Assert.That(cargo, Is.EqualTo(new[] { fuel }));
            Assert.That(containers.Insert(stowaway, container), Is.True);
            Assert.That(cargoSystem.TryCollect(origin, 1f, out var nextCargo), Is.True);
            Assert.That(nextCargo, Is.Empty);
            Assert.That(cargoSystem.MoveSnapshot(origin, destination, cargo, 1f), Is.EqualTo(UndergroundCargoMoveResult.Rejected));
        });
        await pair.CleanReturnAsync();
    }
}
