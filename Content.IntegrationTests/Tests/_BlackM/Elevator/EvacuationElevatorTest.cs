using Content.Server._BlackM.Elevator;
using Content.Shared._BlackM.Elevator;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._BlackM.Elevator;

[TestFixture]
public sealed class EvacuationElevatorTest
{
    [Test]
    public async Task AnnouncesTheSameSingleBoardingPointItActivates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var elevators = server.System<BlackMElevatorSystem>();
            var elevator = entities.SpawnEntity("BlackMElevatorEvacController", map.GridCoords);
            var controller = entities.GetComponent<BlackMElevatorComponent>(elevator);

            Assert.That(elevators.TryStartRandomEvacuation("evac", map.Grid, out _), Is.False);

            var first = entities.SpawnEntity("BlackMElevatorBoardZone", map.GridCoords);
            var firstZone = entities.GetComponent<BlackMElevatorBoardZoneComponent>(first);
            firstZone.PointId = "arrivals";
            firstZone.AnnouncementLocationName = "Arrivals elevator";

            var second = entities.SpawnEntity("BlackMElevatorBoardZone", map.GridCoords);
            var secondZone = entities.GetComponent<BlackMElevatorBoardZoneComponent>(second);
            secondZone.PointId = "science";
            secondZone.AnnouncementLocationName = "Science elevator";

            Assert.That(elevators.TryStartRandomEvacuation("evac", map.Grid, out var location), Is.True);
            var selectedZone = controller.ActiveBoardingZone;
            Assert.That(selectedZone, Is.EqualTo(first).Or.EqualTo(second));
            Assert.That(controller.Enabled, Is.True);
            Assert.That(location, Is.EqualTo(selectedZone == first ? firstZone.AnnouncementLocationName : secondZone.AnnouncementLocationName));

            Assert.That(elevators.TryStartRandomEvacuation("evac", map.Grid, out _), Is.False);
            Assert.That(controller.ActiveBoardingZone, Is.EqualTo(selectedZone));
        });
        await pair.CleanReturnAsync();
    }
}
