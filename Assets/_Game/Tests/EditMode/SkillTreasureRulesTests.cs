using System;
using System.Collections.Generic;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Atomic treasure activation, triple-pulse timing, budgets and run identity acceptance.</summary>
    public sealed class SkillTreasureRulesTests
    {
        private static FishingFlowController Create(bool treasures = true, float diveSpeed = 10)
        {
            var flow = new FishingFlowController(new FishingRules(maxDepth: 20, bottomZoneDepth: 18,
                diveSpeed: diveSpeed, slowdownDistance: 0, bottomHoldSeconds: 0, countdownSeconds: 0, ascentStartSeconds: 0),
                new[] { new FishRules("small", "Small", 5, 5, true) }, new SonarRules(new AbilityRules(2, 4, RunState.Diving, requiresTreasure: treasures)),
                new BranchHookRules(new AbilityRules(1, 0, RunState.Ascending, requiresTreasure: treasures)));
            flow.Start(); Step(flow, .01f); return flow;
        }
        private static void Step(FishingFlowController flow, float dt)
        {
            long step = flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId, step, Array.Empty<ContactCandidate>(), Array.Empty<ContactKey>()));
        }
        private static bool Collect(FishingFlowController flow, int id, SkillTreasureKind kind = SkillTreasureKind.Sonar)
            => flow.Abilities.TryCollectTreasure(flow.Run.RunId, id, kind);

        [Test]
        public void InterleavedEchoesKeepTheirPickupIdentityIndexAndActualRunTime()
        {
            var flow = Create(diveSpeed: 1); var pulses = new List<SonarPulse>();
            flow.Abilities.Sonar.PulseRequested += pulses.Add; double start = flow.Run.GameTime;
            Collect(flow, 9);
            CollectionAssert.AreEqual(new[] { 9, 9 }, flow.Abilities.Sonar.PendingPulses.Select(p => p.TreasureId));
            Step(flow, .25f); Collect(flow, 3);
            Step(flow, .75f); Step(flow, .25f); Step(flow, .75f); Step(flow, .25f);
            CollectionAssert.AreEqual(new[] { "9:0", "3:0", "9:1", "3:1", "9:2", "3:2" },
                pulses.Select(p => p.TreasureId + ":" + p.Index));
            var times = new[] { 0d, .25d, 1d, 1.25d, 2d, 2.25d };
            for (int i = 0; i < pulses.Count; i++)
            {
                Assert.That(pulses[i].RunId, Is.EqualTo(flow.Run.RunId));
                Assert.That(pulses[i].GameTime - start, Is.EqualTo(times[i]).Within(.00001));
            }
            Assert.That(flow.Abilities.Sonar.PendingPulses, Is.Empty);
        }

        [Test]
        public void FreshTreasureRunHasNoManualUsesAndPickupImmediatelyActivatesWithoutFishRewards()
        {
            var flow = Create(); var sonar = flow.Abilities.Sonar;
            Assert.That(sonar.RemainingUses, Is.Zero); Assert.That(flow.Abilities.Branches.RemainingUses, Is.Zero);
            Assert.That(sonar.Activate(), Is.False); int activated = 0; sonar.Activated += () => activated++;
            Assert.That(Collect(flow, 1), Is.True); Assert.That(sonar.RemainingUses, Is.Zero);
            Assert.That(activated, Is.EqualTo(1)); Assert.That(sonar.CooldownRemaining, Is.Zero);
            Assert.That(sonar.Activate(), Is.False);
            Assert.That(flow.Run.TotalCaptureCount, Is.Zero); Assert.That(flow.Run.Gold, Is.Zero); Assert.That(flow.Run.Shells, Is.Zero);
        }
        [Test]
        public void ConsecutivePickupsBothActivateWithoutCooldownAndDoNotReopenTheBudget()
        {
            var flow = Create(); var sonar = flow.Abilities.Sonar;
            int activated = 0; sonar.Activated += () => activated++;
            Assert.That(Collect(flow, 1), Is.True);
            float cooldown = sonar.CooldownRemaining;
            Assert.That(Collect(flow, 1), Is.False); Assert.That(Collect(flow, 1, SkillTreasureKind.Branches), Is.False);
            Assert.That(Collect(flow, 2), Is.True); Assert.That(sonar.CooldownRemaining, Is.EqualTo(cooldown));
            Assert.That(sonar.AcquiredUses, Is.EqualTo(2)); Assert.That(sonar.RemainingUses, Is.Zero);
            Assert.That(activated, Is.EqualTo(2));
            Assert.That(Collect(flow, 3), Is.False); Assert.That(flow.Abilities.CollectedTreasureCount, Is.EqualTo(2));
        }
        [Test]
        public void PausedStaleInvalidAndCancelledPickupsCannotChangeCounts()
        {
            var flow = Create(); var abilities = flow.Abilities;
            Assert.That(abilities.TryCollectTreasure(Guid.NewGuid(), 1, SkillTreasureKind.Sonar), Is.False);
            Assert.That(Collect(flow, 0), Is.False); Assert.That(Collect(flow, -1), Is.False);
            Assert.That(Collect(flow, 1, (SkillTreasureKind)99), Is.False);
            flow.SetPaused(true); Assert.That(Collect(flow, 1), Is.False); flow.SetPaused(false);
            Assert.That(Collect(flow, 1), Is.True); abilities.Cancel();
            Assert.That(Collect(flow, 2), Is.False); Assert.That(Collect(flow, 3, SkillTreasureKind.Branches), Is.False);
            Assert.That(abilities.CollectedTreasureCount, Is.EqualTo(1));
        }
        [Test]
        public void BranchPickupIsRejectedUntilAscentThenDeploysImmediatelyOnlyOnce()
        {
            var flow = Create(); var branch = flow.Abilities.Branches;
            Assert.That(Collect(flow, 1, SkillTreasureKind.Branches), Is.False);
            Assert.That(branch.Activate(), Is.False); Assert.That(branch.Model.Deployed, Is.False);
            for (int i = 0; i < 40 && flow.Run.State != RunState.Ascending; i++) Step(flow, .1f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending)); Assert.That(Collect(flow, 2), Is.False);
            Assert.That(Collect(flow, 1, SkillTreasureKind.Branches), Is.True); Assert.That(branch.Model.Active, Is.True);
            Assert.That(branch.Activate(), Is.False);
            Assert.That(branch.RemainingUses, Is.Zero); Assert.That(Collect(flow, 3, SkillTreasureKind.Branches), Is.False);
        }
        [Test]
        public void BottomZoneRejectsSonarTreasureEvenBeforeTheTurn()
        {
            var flow = Create(); Step(flow, 1.8f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Diving));
            Assert.That(flow.Run.Depth, Is.GreaterThanOrEqualTo(flow.Run.Rules.BottomZoneDepth));
            Assert.That(Collect(flow, 1), Is.False); Assert.That(Collect(flow, 2, SkillTreasureKind.Branches), Is.False);
        }
        [Test]
        public void MissedBranchCanBeCollectedDuringAscentAndResultRejectsAllPickups()
        {
            var flow = Create();
            for (int i = 0; i < 40 && flow.Run.State != RunState.Ascending; i++) Step(flow, .1f);
            Assert.That(Collect(flow, 1, SkillTreasureKind.Branches), Is.True);
            for (int i = 0; i < 100 && flow.Run.State != RunState.Result; i++) Step(flow, .1f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Result)); Assert.That(Collect(flow, 2), Is.False);
        }
        [Test]
        public void NewRunResetsEarnedUsesAndRejectsDelayedOldRunIds()
        {
            var old = Create(); Assert.That(Collect(old, 1), Is.True);
            var next = Create(); Assert.That(next.Abilities.CollectedTreasureCount, Is.Zero);
            Assert.That(next.Abilities.TryCollectTreasure(old.Run.RunId, 1, SkillTreasureKind.Sonar), Is.False);
            Assert.That(Collect(next, 1), Is.True); Assert.That(next.Abilities.Sonar.AcquiredUses, Is.EqualTo(1));
        }
        [Test]
        public void ClassicComparisonStillStartsWithTheConfiguredUses()
        {
            var flow = Create(false);
            Assert.That(flow.Abilities.Sonar.RemainingUses, Is.EqualTo(2));
            Assert.That(flow.Abilities.Branches.RemainingUses, Is.EqualTo(1)); Assert.That(Collect(flow, 1), Is.False);
        }
        [Test]
        public void SonarPulsesImmediatelyAtOneAndTwoSecondsWithFreshTargetsEachTime()
        {
            var flow = Create(diveSpeed: 1); var sonar = flow.Abilities.Sonar; int pulses = 0, scared = 0;
            sonar.Pulsed += () =>
            {
                pulses++;
                scared += sonar.ResolveTargets(new[] { new SonarTarget(pulses, "small", 0, 1) }).Count;
                Assert.That(sonar.ResolveTargets(new[] { new SonarTarget(99, "small", 0, 1) }), Is.Empty);
            };
            Assert.That(Collect(flow, 1), Is.True); Assert.That(pulses, Is.EqualTo(1));
            Step(flow, .99f); Assert.That(pulses, Is.EqualTo(1)); Step(flow, .01f);
            Assert.That(pulses, Is.EqualTo(2)); Assert.That(scared, Is.EqualTo(2));
            Step(flow, .99f); Assert.That(pulses, Is.EqualTo(2)); Step(flow, .01f);
            Assert.That(pulses, Is.EqualTo(3)); Assert.That(scared, Is.EqualTo(3));
            Step(flow, 2); Assert.That(pulses, Is.EqualTo(3));
        }
        [Test]
        public void PauseFreezesTheEchoAndResumeWaitsForTheRemainingInterval()
        {
            var flow = Create(diveSpeed: 1); int pulses = 0; flow.Abilities.Sonar.Pulsed += () => pulses++;
            Collect(flow, 1); Step(flow, .4f); flow.SetPaused(true); Step(flow, 20);
            Assert.That(pulses, Is.EqualTo(1)); flow.SetPaused(false);
            Step(flow, .59f); Assert.That(pulses, Is.EqualTo(1)); Step(flow, .01f);
            Assert.That(pulses, Is.EqualTo(2));
            Step(flow, .4f); flow.SetPaused(true); Step(flow, 20); Assert.That(pulses, Is.EqualTo(2));
            flow.SetPaused(false); Step(flow, .59f); Assert.That(pulses, Is.EqualTo(2)); Step(flow, .01f);
            Assert.That(pulses, Is.EqualTo(3));
        }
        [Test]
        public void OverlappingPickupsKeepBothEchoesAndReentrantDuplicatesAreRejected()
        {
            var flow = Create(diveSpeed: 1); int pulses = 0;
            flow.Abilities.Sonar.Pulsed += () =>
            { pulses++; Assert.That(Collect(flow, 1), Is.False); };
            Collect(flow, 1); Step(flow, .2f); Collect(flow, 2);
            Assert.That(pulses, Is.EqualTo(2)); Step(flow, .8f); Assert.That(pulses, Is.EqualTo(3));
            Step(flow, .2f); Assert.That(pulses, Is.EqualTo(4));
            Step(flow, .8f); Assert.That(pulses, Is.EqualTo(5)); Step(flow, .2f);
            Assert.That(pulses, Is.EqualTo(6)); Step(flow, 1); Assert.That(pulses, Is.EqualTo(6));
        }
        [Test]
        public void LeavingDescentAndCancellingOldRunDiscardDelayedPulses()
        {
            var flow = Create(); Step(flow, 1.7f); int pulses = 0;
            flow.Abilities.Sonar.Pulsed += () => pulses++; Collect(flow, 1); Step(flow, .4f);
            Step(flow, 1); Assert.That(pulses, Is.EqualTo(1));
            var old = Create(diveSpeed: 1); int oldPulses = 0; old.Abilities.Sonar.Pulsed += () => oldPulses++;
            Collect(old, 1); old.Abilities.Cancel(); Step(old, 2.1f);
            Assert.That(oldPulses, Is.EqualTo(1));
        }
        [Test]
        public void ThirdPulseCanFindAFishWhenTheFirstTwoQueriesAreEmpty()
        {
            var flow=Create(diveSpeed: 1); var sonar=flow.Abilities.Sonar; int pulses=0,scared=0;
            sonar.Pulsed+=()=>
            {
                pulses++;
                scared+=sonar.ResolveTargets(pulses<3 ? Array.Empty<SonarTarget>() :
                    new[]{new SonarTarget(8,"small",0,1)}).Count;
            };
            Collect(flow,1); Step(flow,1); Assert.That(scared,Is.Zero);
            Step(flow,1); Assert.That(pulses,Is.EqualTo(3)); Assert.That(scared,Is.EqualTo(1)); Assert.That(sonar.IsScared(8),Is.True);
        }
        [Test]
        public void DelayedTickDeliversBothRemainingPulsesWithoutAnExtraFourthPulse()
        {
            var flow=Create(diveSpeed: 1); int pulses=0; flow.Abilities.Sonar.Pulsed+=()=>pulses++;
            Collect(flow,1); Step(flow,2.1f); Assert.That(pulses,Is.EqualTo(3));
            Step(flow,1); Assert.That(pulses,Is.EqualTo(3));
        }
        [Test]
        public void EnteringTheBottomZoneAfterTheSecondPulseDiscardsTheThird()
        {
            var flow=Create(); int pulses=0; flow.Abilities.Sonar.Pulsed+=()=>pulses++;
            Collect(flow,1); Step(flow,1); Assert.That(pulses,Is.EqualTo(2));
            Step(flow,.81f); Step(flow,1); Assert.That(pulses,Is.EqualTo(2));
        }
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(float.NaN)]
        public void InvalidPulseIntervalIsRejected(float interval)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new SonarRules(pulseIntervalSeconds: interval)); }
    }
}
