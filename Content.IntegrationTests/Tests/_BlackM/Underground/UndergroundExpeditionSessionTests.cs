using System;
using Content.Server._BlackM.Underground;
using Content.Shared._BlackM.Underground;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests._BlackM.Underground;

[TestFixture]
public sealed class UndergroundExpeditionSessionTests
{
    [Test]
    public void TimerStartsOnlyAfterArrivalAndReturnIsIdempotent()
    {
        var run = new UndergroundExpeditionSession();
        Assert.That(run.TryBeginGeneration(out var id), Is.True);
        Assert.That(run.TryBeginGeneration(out _), Is.False);
        Assert.That(run.GenerationFinished(id + 1, true), Is.False);
        Assert.That(run.GenerationFinished(id, true), Is.True);
        Assert.That(run.TryBeginOutbound(), Is.True);
        Assert.That(run.DepartureDeadline, Is.Null);
        Assert.That(run.OutboundFinished(true, TimeSpan.FromSeconds(100), UndergroundExpeditionSession.DefaultDuration), Is.True);
        Assert.That(run.DepartureDeadline, Is.EqualTo(TimeSpan.FromSeconds(700)));
        Assert.That(run.TryBeginReturn(TimeSpan.FromSeconds(699)), Is.False);
        Assert.That(run.TryBeginReturn(TimeSpan.FromSeconds(700)), Is.True);
        Assert.That(run.TryBeginReturn(TimeSpan.FromSeconds(700)), Is.False);
        Assert.That(run.CleanupFinished(), Is.False);
        Assert.That(run.ReturnFinished(true), Is.True);
        Assert.That(run.CleanupFinished(), Is.True);
        Assert.That(run.Phase, Is.EqualTo(UndergroundExpeditionPhase.Idle));
    }

    [Test]
    public void EarlyReturnIsRejectedAndFailureNeverPermitsCleanup()
    {
        var run = new UndergroundExpeditionSession();
        run.TryBeginGeneration(out var id);
        run.GenerationFinished(id, true);
        run.TryBeginOutbound();
        run.OutboundFinished(true, TimeSpan.Zero, UndergroundExpeditionSession.DefaultDuration);
        Assert.That(run.TryBeginReturn(TimeSpan.FromMinutes(1)), Is.False);
        Assert.That(run.TryBeginReturn(TimeSpan.FromMinutes(10)), Is.True);
        Assert.That(run.ReturnFinished(false), Is.True);
        Assert.That(run.Phase, Is.EqualTo(UndergroundExpeditionPhase.Faulted));
        Assert.That(run.CleanupFinished(), Is.False);
        Assert.That(run.TryBeginGeneration(out _), Is.False);
    }

    [Test]
    public void WarningsAreNotRepeatedOrFloodedAfterLag()
    {
        var run = new UndergroundExpeditionSession();
        run.TryBeginGeneration(out var id);
        run.GenerationFinished(id, true);
        run.TryBeginOutbound();
        run.OutboundFinished(true, TimeSpan.Zero, UndergroundExpeditionSession.DefaultDuration);
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(299)), Is.Null);
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(300)), Is.EqualTo(300));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(300)), Is.Null);
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(420)), Is.EqualTo(180));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(480)), Is.EqualTo(120));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(540)), Is.EqualTo(60));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(575)), Is.EqualTo(30));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(575)), Is.Null);
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(590)), Is.EqualTo(10));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(600)), Is.Null);
    }
    [Test]
    public void SkippedWarningsSendOnlyTheMostUrgentOne()
    {
        var run = new UndergroundExpeditionSession();
        run.TryBeginGeneration(out var id);
        run.GenerationFinished(id, true);
        run.TryBeginOutbound();
        run.OutboundFinished(true, TimeSpan.Zero, UndergroundExpeditionSession.DefaultDuration);
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(575)), Is.EqualTo(30));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(575)), Is.Null);
    }

    [Test]
    public void ShortMissionUsesItsOwnDeadlineAndWarnings()
    {
        var run = new UndergroundExpeditionSession();
        run.TryBeginGeneration(out var id);
        run.GenerationFinished(id, true);
        run.TryBeginOutbound();
        run.OutboundFinished(true, TimeSpan.FromSeconds(100), TimeSpan.FromMinutes(5));
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(100)), Is.Null);
        Assert.That(run.ConsumeWarning(TimeSpan.FromSeconds(220)), Is.EqualTo(180));
        Assert.That(run.TryBeginReturn(TimeSpan.FromSeconds(399)), Is.False);
        Assert.That(run.TryBeginReturn(TimeSpan.FromSeconds(400)), Is.True);
    }

    [Test]
    public void FailedOutboundCannotCleanOrRestart()
    {
        var run = new UndergroundExpeditionSession();
        run.TryBeginGeneration(out var id);
        run.GenerationFinished(id, true);
        run.TryBeginOutbound();
        Assert.That(run.OutboundFinished(false, TimeSpan.Zero, UndergroundExpeditionSession.DefaultDuration), Is.True);
        Assert.That(run.Phase, Is.EqualTo(UndergroundExpeditionPhase.Faulted));
        Assert.That(run.CleanupFinished(), Is.False);
        Assert.That(run.TryBeginGeneration(out _), Is.False);
    }

    [Test]
    public void ExplicitFailureBlocksCleanup()
    {
        var run = new UndergroundExpeditionSession();
        run.Fail();
        Assert.That(run.CleanupFinished(), Is.False);
        Assert.That(run.TryBeginGeneration(out _), Is.False);
    }
    [TestCase(UndergroundMission.LostResearchDisk, "BlackMUndergroundResearchDisk")]
    [TestCase(UndergroundMission.LostMultitool, "BlackMUndergroundLostMultitool")]
    [TestCase(UndergroundMission.LostScannerFrame, "BlackMUndergroundLostScannerFrame")]
    [TestCase(UndergroundMission.LostPulseRifle, "BlackMUndergroundLostPulseRifle")]
    public void LostItemsUseDeliveryRulesAndDistinctPrototypes(UndergroundMission mission, string prototype)
    {
        Assert.That(UndergroundMissions.IsValid(mission), Is.True);
        Assert.That(UndergroundMissions.IsItem(mission), Is.True);
        Assert.That(UndergroundMissions.IsDelivery(mission), Is.True);
        Assert.That(UndergroundMissions.IsSample(mission), Is.False);
        Assert.That(UndergroundMissions.Goal(mission), Is.EqualTo(1));
        Assert.That(UndergroundMissions.DurationSeconds(mission), Is.EqualTo(600));
        Assert.That(UndergroundMissions.RewardAmount(mission), Is.EqualTo(2500));
        Assert.That(UndergroundMissions.ItemPrototype(mission), Is.EqualTo(prototype));
    }

    [Test]
    public void NonItemMissionsCannotResolveAnItemPrototype()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UndergroundMissions.ItemPrototype(UndergroundMission.HeadcrabHunt));
    }

}
