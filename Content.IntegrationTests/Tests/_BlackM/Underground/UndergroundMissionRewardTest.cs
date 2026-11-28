using System.Linq;
using System.Numerics;
using Content.Server._BlackM.Underground;
using Content.Shared._BlackM.Underground;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._BlackM.Underground;

[TestFixture]
public sealed class UndergroundMissionRewardTest
{
    [Test]
    public async Task ManualTurnInDeliversOneAmsComponentAtTheConsole()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var console = entities.SpawnEntity("BlackMUndergroundConsole", map.GridCoords);
            var user = entities.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0.75f, 0.5f)));
            var sample = entities.SpawnEntity("BlackMUndergroundHeadcrabSample", map.GridCoords);
            var run = entities.GetComponent<UndergroundRunComponent>(console);
            run.Mission = UndergroundMission.FastHeadcrabSample;
            run.MissionReturned = true;

            var marker = entities.EnsureComponent<UndergroundMissionSampleComponent>(sample);
            marker.Console = console;
            marker.Generation = run.Session.GenerationId;
            marker.Mission = run.Mission;

            var turnIn = new InteractUsingEvent(user, sample, console, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(console, turnIn);
            Assert.That(turnIn.Handled, Is.True);
            Assert.That(run.MissionRewardAvailable, Is.True);

            var repeatedTurnIn = new InteractUsingEvent(user, sample, console, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(console, repeatedTurnIn);
            Assert.That(repeatedTurnIn.Handled, Is.False);

            var rewards = entities.EntityQuery<MetaDataComponent, TransformComponent>()
                .Where(reward => reward.Item1.EntityPrototype?.ID == "AmsCellComponent")
                .Select(reward => reward.Item2.Coordinates)
                .ToArray();
            var consoleCoordinates = entities.GetComponent<TransformComponent>(console).Coordinates;
            Assert.That(rewards, Is.EqualTo(new[] { consoleCoordinates }));
        });
        await pair.CleanReturnAsync();
    }
}
