using System.Collections.Generic;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Value-ordered loss, authoritative accounting, identity and recovery tolerance.</summary>
    public sealed class ReelingEscapeRulesTests
    {
        private static readonly FishRules Cheap = new FishRules("cheap", "Cheap", 1, 100, true);
        private static readonly FishRules Premium = new FishRules("premium", "Premium", 80, 50, true);
        private static readonly FishRules Shell = new FishRules("shell", "Shell", 80, 60, true);
        private static FishingFlowController Create(int capacity = 10)
        {
            var flow = new FishingFlowController(new FishingRules(maxDepth: 1000, bottomZoneDepth: 996,
                countdownSeconds: 0, slowdownDistance: 0, bottomHoldSeconds: 0, mainCapacity: capacity,
                reeling: new ReelingRules(true, easyHeight: .12f, hardHeight: .12f, stabilityLoss: 2)),
                new[] { Cheap, Premium, Shell });
            flow.Start(); Step(flow, .02f); Step(flow, 200); Step(flow, .02f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending)); return flow;
        }
        private static ContactCandidate Hit(int id, string species, int hook = 0) => new ContactCandidate(hook, id, species, 0);
        private static void Step(FishingFlowController flow, float dt = .02f, params ContactCandidate[] hits)
        {
            long step = flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId, step, hits, hits.Select(h => new ContactKey(h.HookId, h.FishId))));
        }
        private static CaptureRecord LoseOne(FishingFlowController flow)
        {
            CaptureRecord lost = null;
            System.Action<CaptureRecord> observe = record => lost = record;
            flow.OnFishEscaped += observe; flow.SetReelingInput(true);
            for (int i = 0; i < 700 && lost == null; i++) Step(flow);
            flow.OnFishEscaped -= observe; Assert.That(lost, Is.Not.Null); return lost;
        }
        /// <summary>Gold dominates shells; equal gold uses shells and exact ties use commit order, across hooks.</summary>
        [Test]
        public void MostValuableEscapesFirstAcrossSourcesWithStableTies()
        {
            var flow = Create(); flow.Abilities.Branches.Activate();
            Step(flow, .02f, Hit(1, "cheap"), Hit(2, "premium"), Hit(3, "shell", 1), Hit(4, "shell", 2));
            var losses = new List<int>();
            flow.OnFishEscaped += record =>
            {
                losses.Add(record.FishInstanceId);
                Assert.That(flow.Run.Captures.Contains(record), Is.False);
                Assert.That(flow.Run.Gold, Is.EqualTo(flow.Run.Captures.Sum(r => r.Gold)));
            };
            Assert.That(flow.Run.EscapeCandidate.FishInstanceId, Is.EqualTo(3));
            for (int i = 0; i < 4; i++) LoseOne(flow);
            CollectionAssert.AreEqual(new[] { 3, 4, 2, 1 }, losses);
            Assert.That(flow.Run.TotalCaptureCount, Is.Zero); Assert.That(flow.Run.Gold, Is.Zero); Assert.That(flow.Run.Shells, Is.Zero);
            Assert.That(Enumerable.Range(1, 4).All(flow.Run.HasCaptured), Is.True);
        }
        /// <summary>Losing before full frees capacity, keeps the lost identity reserved and publishes full once.</summary>
        [Test]
        public void LossBeforeFullFreesCapacityWithoutReplayingTheFullEvent()
        {
            var flow = Create(3); int full = 0, captures = 0;
            flow.Run.OnBecameFull += () => full++; flow.OnFishCaptured += _ => captures++;
            Step(flow, .02f, Hit(1, "cheap"), Hit(2, "premium"));
            Assert.That(flow.Run.IsFull, Is.False); Assert.That(LoseOne(flow).FishInstanceId, Is.EqualTo(2));
            Assert.That(flow.Run.CountForHook(0), Is.EqualTo(1));
            Step(flow, .02f, Hit(2, "premium"), Hit(3, "cheap"), Hit(4, "cheap"));
            Assert.That(flow.Run.IsFull, Is.True); Assert.That(captures, Is.EqualTo(4)); Assert.That(full, Is.EqualTo(1));
            Assert.That(flow.Run.Gold, Is.EqualTo(3)); Assert.That(flow.Run.Shells, Is.EqualTo(300));
            Assert.That(flow.Run.Captures.Any(r => r.FishInstanceId == 2), Is.False);
        }
        /// <summary>A branch loss only frees its own source capacity and leaves the main hook untouched.</summary>
        [Test]
        public void BranchLossFreesOnlyItsOriginalHookAndCanReceiveANewFish()
        {
            var flow = Create(3); flow.Abilities.Branches.Activate();
            Step(flow, .02f, Hit(1, "cheap"), Hit(2, "cheap"), Hit(3, "shell", 1), Hit(4, "premium", 2));
            Assert.That(LoseOne(flow).HookId, Is.EqualTo(1));
            Assert.That(flow.Abilities.Branches.RemainingCapacity(1), Is.EqualTo(3));
            Assert.That(flow.Abilities.Branches.RemainingCapacity(2), Is.EqualTo(2)); Assert.That(flow.Run.IsFull, Is.False);
            Step(flow, .02f, Hit(3, "shell", 1), Hit(5, "cheap", 1));
            Assert.That(flow.Run.CountForHook(1), Is.EqualTo(1));
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(4)); Assert.That(flow.Run.HasCaptured(3), Is.True);
        }
        /// <summary>Recovery pauses with gameplay and only a successful branch activation extends grace.</summary>
        [Test]
        public void RecoveryAndBranchGraceFreezeOnPauseAndRejectedCommandsDoNotRefreshIt()
        {
            var flow = Create(); Step(flow, .02f, Hit(1, "premium"), Hit(2, "cheap")); LoseOne(flow);
            float grace = flow.Run.Reeling.GraceRemaining, stability = flow.Run.Reeling.Stability;
            double time = flow.Run.GameTime; flow.SetPaused(true); Step(flow, 30);
            Assert.That(flow.Run.GameTime, Is.EqualTo(time)); Assert.That(flow.Run.Reeling.GraceRemaining, Is.EqualTo(grace));
            Assert.That(flow.Run.Reeling.Stability, Is.EqualTo(stability)); Assert.That(flow.Abilities.Branches.Activate(), Is.False);
            flow.SetPaused(false); Step(flow, .5f); Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1));
            Assert.That(flow.Abilities.Branches.Activate(), Is.True); Assert.That(flow.Run.Reeling.GraceRemaining, Is.EqualTo(.8f));
            Step(flow, .2f); grace = flow.Run.Reeling.GraceRemaining;
            Assert.That(flow.Abilities.Branches.Activate(), Is.False); Assert.That(flow.Run.Reeling.GraceRemaining, Is.EqualTo(grace));
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1));
        }
        /// <summary>After every fish leaves, the hook returns automatically with zero settlement and a new run is clean.</summary>
        [Test]
        public void EmptyAfterLossReturnsNormallyAndNewRunsDoNotInheritReservedIds()
        {
            var flow = Create(); Step(flow, .02f, Hit(1, "cheap"), Hit(2, "premium")); LoseOne(flow); LoseOne(flow);
            Step(flow); Assert.That(flow.Run.Reeling.Active, Is.False); Assert.That(flow.Run.Reeling.Speed, Is.EqualTo(6.5f));
            for (int i = 0; i < 10000 && flow.Run.State != RunState.Result; i++) Step(flow);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Result)); Assert.That(Settlement.Summarize(flow.Run.Captures), Is.Empty);
            var next = Create(); Assert.That(next.Run.RunId, Is.Not.EqualTo(flow.Run.RunId));
            Assert.That(next.Run.HasCaptured(1), Is.False); Assert.That(next.Run.Reeling.GraceRemaining, Is.Zero);
        }
        /// <summary>A long caller step latches a single loss until the flow consumes it; recovery prevents an immediate cascade.</summary>
        [Test]
        public void PendingLossIsConsumedOnceAndRecoveryCannotDrainDuringGrace()
        {
            var reel = new ReelingModel(new ReelingRules(true, easyHeight: .12f, hardHeight: .12f, stabilityLoss: 2));
            reel.SetHeld(true); reel.Tick(30, 3, 16, 6.5f); Assert.That(reel.EscapePending, Is.True);
            Assert.That(reel.ConsumeEscape(), Is.True); Assert.That(reel.ConsumeEscape(), Is.False);
            reel.Tick(.7f, 2, 16, 6.5f); Assert.That(reel.EscapePending, Is.False);
            Assert.That(reel.Stability, Is.GreaterThanOrEqualTo(.45f));
        }
    }
}
