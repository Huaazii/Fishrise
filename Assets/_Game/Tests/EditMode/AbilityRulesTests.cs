using System;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Pure ability eligibility, immunity and deterministic multi-hook allocation acceptance.</summary>
    public sealed class AbilityRulesTests
    {
        private static readonly FishRules Small = new FishRules("small", "Small", 5, 3, true);
        private static readonly FishRules Blue = new FishRules("blue", "Higher star control", 10, 7, rarity: 2);
        private static readonly FishRules Decor = new FishRules("decor", "Decor", 0, 0, true, false);
        private static FishingFlowController Create(int protection = 2, int capacity = 10, SonarRules sonar = null, BranchHookRules branches = null)
        {
            var flow = new FishingFlowController(new FishingRules(slowdownDistance: 0, countdownSeconds: 0,
                protectionCharges: protection, mainCapacity: capacity), new[] { Small, Blue, Decor }, sonar, branches);
            flow.Start(); Step(flow, .02f); return flow;
        }
        private static ContactCandidate Hit(int id, int hook = 0, string species = "small", float distance = 0)
            => new ContactCandidate(hook, id, species, distance);
        private static void Step(FishingFlowController flow, float dt, params ContactCandidate[] hits)
        {
            long step = flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId, step, hits, hits.Select(h => new ContactKey(h.HookId, h.FishId))));
        }
        private static void Ascent(FishingFlowController flow)
        { Step(flow, 100f / 6); Step(flow, 1.2f); Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending)); }
        private static void Scare(FishingFlowController flow, int id = 1)
        {
            Assert.That(flow.Abilities.Sonar.Activate(), Is.True);
            Assert.That(flow.Abilities.Sonar.ResolveTargets(new[] { new SonarTarget(id, "small", 0, 1) }).Count, Is.EqualTo(1));
        }
        [Test]
        public void SonarUsesCooldownPauseAndRepeatedInputShareTheGameClock()
        {
            var flow = Create(); var sonar = flow.Abilities.Sonar;
            Assert.That(sonar.RemainingUses, Is.EqualTo(2)); Assert.That(sonar.Activate(), Is.True);
            Assert.That(sonar.Activate(), Is.False); Step(flow, .1f);
            float remaining = sonar.CooldownRemaining; double time = flow.Run.GameTime;
            flow.SetPaused(true); Step(flow, 20);
            Assert.That(flow.Run.GameTime, Is.EqualTo(time)); Assert.That(sonar.CooldownRemaining, Is.EqualTo(remaining));
            Assert.That(sonar.Activate(), Is.False); flow.SetPaused(false); Step(flow, 3.9f);
            Assert.That(sonar.Activate(), Is.True); Assert.That(sonar.RemainingUses, Is.Zero);
            Step(flow, 4); Assert.That(sonar.Activate(), Is.False); Assert.That(flow.Run.Protection, Is.EqualTo(2));
        }
        [Test]
        public void ZeroCooldownStillDebouncesCommandsAndRejectsOtherRuns()
        {
            var flow = Create(sonar: new SonarRules(new AbilityRules(2, 0, RunState.Diving)));
            Assert.That(flow.Abilities.Sonar.CanActivate(new RunModel(flow.Run.Rules)), Is.False);
            Assert.That(flow.Abilities.Sonar.Activate(), Is.True); Step(flow, .19f);
            Assert.That(flow.Abilities.Sonar.Activate(), Is.False); Step(flow, .011f);
            Assert.That(flow.Abilities.Sonar.Activate(), Is.True);
        }
        [Test]
        public void SonarOnlyAcceptsOneQueryWithSmallCatchableInRangeAndUniqueIds()
        {
            var flow = Create(); var sonar = flow.Abilities.Sonar;
            var targets = new[] { new SonarTarget(1, "small", 0, -1), new SonarTarget(1, "small", 0, 1),
                new SonarTarget(2, "small", 16, 0), new SonarTarget(3, "small", 16.01f, 1),
                new SonarTarget(4, "blue", 0, 1), new SonarTarget(5, "decor", 0, 1),
                new SonarTarget(6, "unknown", 0, 1), new SonarTarget(7, "small", float.NaN, 1) };
            Assert.That(sonar.ResolveTargets(targets), Is.Empty); Assert.That(sonar.Activate(), Is.True);
            var effects = sonar.ResolveTargets(targets);
            CollectionAssert.AreEqual(new[] { 1, 2 }, effects.Select(e => e.FishId));
            Assert.That(effects.All(e => e.Duration == 2.5f && e.SpeedMultiplier == 3 && Math.Abs(e.Direction) == 1), Is.True);
            Assert.That(sonar.ResolveTargets(targets), Is.Empty);
        }
        [TestCase(0)] [TestCase(2)]
        public void ScaredContactsDoNotSpendProtectionOrCaptureThenRecoveryReadmitsOverlap(int protection)
        {
            var flow = Create(protection); Scare(flow);
            Step(flow, .02f, Hit(1)); Step(flow, 2, Hit(1));
            Assert.That(flow.Run.Protection, Is.EqualTo(protection)); Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Diving));
            Step(flow, .5f, Hit(1)); Assert.That(flow.Abilities.Sonar.IsScared(1), Is.False);
            if (protection > 0) Assert.That(flow.Run.Protection, Is.EqualTo(protection - 1));
            else { Assert.That(flow.Run.MainCaptureCount, Is.EqualTo(1)); Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending)); }
        }
        [Test]
        public void LargeFishStillTriggersProtectionDuringSonarAndUncoveredSmallFishStillTriggersEarlyAscent()
        {
            var flow = Create(); Scare(flow); Step(flow, .02f, Hit(2, species: "blue"));
            Assert.That(flow.Run.Protection, Is.EqualTo(1)); Assert.That(flow.Run.MainCaptureCount, Is.Zero);
            var unprotected = Create(0); Scare(unprotected); Step(unprotected, .02f, Hit(9));
            Assert.That(unprotected.Run.State, Is.EqualTo(RunState.Ascending)); Assert.That(unprotected.Run.Captures[0].FishInstanceId, Is.EqualTo(9));
        }
        [Test]
        public void SonarIsUnavailableDuringCountdownBottomZoneHoldAscentAndAfterCancellation()
        {
            var initial = new FishingFlowController(new FishingRules(), new[] { Small });
            Assert.That(initial.Abilities.Sonar.Activate(), Is.False); initial.Start(); Assert.That(initial.Abilities.Sonar.Activate(), Is.False);
            var flow = Create(); Step(flow, 16); Assert.That(flow.Run.Depth, Is.EqualTo(96).Within(.001));
            Assert.That(flow.Abilities.Sonar.Activate(), Is.False); Step(flow, 1); Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold));
            Assert.That(flow.Abilities.Sonar.Activate(), Is.False); Step(flow, 1.2f); Assert.That(flow.Abilities.Sonar.Activate(), Is.False);
            flow.Abilities.Cancel(); Assert.That(flow.Abilities.Branches.Activate(), Is.False);
        }
        [Test]
        public void BranchesOnlyDeployOnUnpausedAscentAndOnlyConsumeOneUse()
        {
            var flow = Create(); var branches = flow.Abilities.Branches;
            Assert.That(branches.Activate(), Is.False); Step(flow, 100f / 6); Assert.That(branches.Activate(), Is.False);
            Step(flow, 1.2f); flow.SetPaused(true); Assert.That(branches.Activate(), Is.False); flow.SetPaused(false);
            Assert.That(branches.Activate(), Is.True); Assert.That(branches.Activate(), Is.False);
            Assert.That(branches.Model.Deployed, Is.True); Assert.That(branches.RemainingUses, Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AllocationPrioritizesBranchesAndIsIndependentOfReportOrder(int order)
        {
            var flow = Create(branches: new BranchHookRules(capacity: 1)); Ascent(flow); flow.Abilities.Branches.Activate();
            var hits = new[] { Hit(1, 0), Hit(1, 1, distance: .1f), Hit(1, 2, distance: .1f),
                Hit(2, 0), Hit(2, 1, distance: .2f), Hit(2, 2, distance: .3f), Hit(3, 1, "blue"), Hit(3, 0, "blue"), Hit(1, 1, distance: .4f) };
            if (order == 1) hits = hits.Reverse().ToArray();
            if (order == 2) hits = hits.OrderBy(h => (h.HookId + h.FishId * 7) % 5).ToArray();
            Step(flow, .02f, hits);
            CollectionAssert.AreEqual(new[] { "1:1", "2:2", "3:0" }, flow.Run.Captures.Select(r => r.FishInstanceId + ":" + r.HookId));
            Assert.That(flow.Run.MainCaptureCount, Is.EqualTo(1)); Assert.That(flow.Run.BranchCaptureCount, Is.EqualTo(2));
            Step(flow, .02f, hits); Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(3));
        }
        [Test]
        public void FullFirstBranchFallsBackToSecondThenFullBranchesFallBackToMain()
        {
            var flow = Create(branches: new BranchHookRules(capacity: 1)); Ascent(flow); flow.Abilities.Branches.Activate();
            Step(flow, .02f, Hit(1, 1)); Step(flow, .02f, Hit(2, 1), Hit(2, 2, distance: 2), Hit(2));
            Step(flow, .02f, Hit(3, 1), Hit(3, 2), Hit(3));
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, flow.Run.Captures.Select(r => r.HookId));
        }
        [Test]
        public void FullMainStillAllowsSixBranchFishWithoutOverfillingOrDuplicateCapture()
        {
            var flow = Create(capacity: 1); Ascent(flow); int fullEvents = 0; flow.Run.OnBecameFull += () => fullEvents++;
            Step(flow, .02f, Hit(100, species: "blue")); Assert.That(flow.Run.IsFull, Is.True);
            Assert.That(flow.Abilities.Branches.Activate(), Is.True);
            var hits = Enumerable.Range(1, 12).SelectMany(id => new[] { Hit(id), Hit(id, 1), Hit(id, 2) }).Concat(new[] { Hit(101, 1, "blue") }).ToArray();
            Step(flow, .02f, hits); Step(flow, .02f, hits);
            Assert.That(flow.Run.MainCaptureCount, Is.EqualTo(1)); Assert.That(flow.Run.BranchCaptureCount, Is.EqualTo(6));
            Assert.That(flow.Run.CountForHook(1), Is.EqualTo(3)); Assert.That(flow.Run.CountForHook(2), Is.EqualTo(3));
            Assert.That(flow.Run.Captures.Select(r => r.FishInstanceId).Distinct().Count(), Is.EqualTo(7)); Assert.That(fullEvents, Is.EqualTo(1));
        }
        [Test]
        public void MixedSourcesMergeIntoOneCardWithCorrectCountsAndBothRewards()
        {
            var flow = Create(); Ascent(flow); Step(flow, .02f, Hit(1), Hit(2), Hit(3, species: "blue"));
            flow.Abilities.Branches.Activate(); Step(flow, .02f, Hit(4, 1));
            var rows = Settlement.Summarize(flow.Run.Captures); var small = rows.Single(r => r.SpeciesId == "small");
            Assert.That(rows.Count, Is.EqualTo(2)); Assert.That(small.MainCount, Is.EqualTo(2)); Assert.That(small.BranchCount, Is.EqualTo(1));
            Assert.That(small.HasBranch, Is.True); Assert.That(rows.Single(r => r.SpeciesId == "blue").HasBranch, Is.False);
            Assert.That(rows.Sum(r => r.TotalCount), Is.EqualTo(flow.Run.TotalCaptureCount));
            Assert.That(flow.Run.Gold, Is.EqualTo(25)); Assert.That(flow.Run.Shells, Is.EqualTo(16));
        }
        [TestCase(false)] [TestCase(true)]
        public void OptionalLargeFishRiskPreservesAlreadyCaughtFishAndDefaultsToPassingThrough(bool risk)
        {
            var flow = Create(branches: new BranchHookRules(breakOnLarge: risk)); Ascent(flow); flow.Abilities.Branches.Activate();
            Step(flow, .02f, Hit(1, 1)); Step(flow, .02f, Hit(2, 1, "blue")); Step(flow, .02f, Hit(3, 1), Hit(3));
            Assert.That(flow.Run.Captures[0].FishInstanceId, Is.EqualTo(1));
            Assert.That(flow.Run.BranchCaptureCount, Is.EqualTo(risk ? 1 : 2)); Assert.That(flow.Run.MainCaptureCount, Is.EqualTo(risk ? 1 : 0));
        }
        [Test]
        public void CancelAndFreshRunResetSlotsAndIgnoreOldRunBranchReports()
        {
            var old = Create(); Scare(old); Ascent(old); old.Abilities.Branches.Activate();
            var fresh = Create(); long sequence = fresh.BeginStep(.02f);
            fresh.ResolveContacts(new ContactBatch(old.Run.RunId, sequence, new[] { Hit(90, 1) }, Array.Empty<ContactKey>()));
            Assert.That(fresh.Run.TotalCaptureCount, Is.Zero); Assert.That(fresh.Abilities.Sonar.RemainingUses, Is.EqualTo(2));
            Assert.That(fresh.Abilities.Branches.RemainingUses, Is.EqualTo(1)); Assert.That(fresh.Abilities.Branches.Model.Deployed, Is.False);
            Assert.That(fresh.Abilities.Sonar.IsScared(1), Is.False); old.Abilities.Cancel(); Assert.That(old.Abilities.Branches.Model.Active, Is.False);
        }
    }
}
