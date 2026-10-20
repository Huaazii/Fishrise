using System;
using System.Collections.Generic;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Pure rule acceptance tests requiring no scenes, prefabs or Unity APIs.</summary>
    public sealed class CoreRulesTests
    {
        private static readonly FishRules Blue = new FishRules("blue", "Blue", 10, 7);
        private static readonly FishRules Small = new FishRules("small", "Small", 5, 3, true);
        private static readonly FishRules Decor = new FishRules("decor", "Decor", 0, 0, false, false);
        private static readonly ContactCandidate[] Empty = Array.Empty<ContactCandidate>();
        private static FishingFlowController Create(int protection = 2, int capacity = 10)
        {
            var flow = new FishingFlowController(new FishingRules(maxDepth: 100, bottomZoneDepth: 96,
                slowdownDistance: 0, countdownSeconds: 0, protectionCharges: protection, mainCapacity: capacity),
                new[] { Blue, Small, Decor });
            flow.Start(); Step(flow, .02f); return flow;
        }
        private static ContactCandidate Fish(int id, float distance = 0, int hook = 0, string species = "blue")
            => new ContactCandidate(hook, id, species, distance);
        private static void Step(FishingFlowController flow, float dt, ContactCandidate[] hits = null, bool overlapping = true)
        {
            hits = hits ?? Empty;
            long sequence = flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId, sequence, hits,
                overlapping ? hits.Select(c => new ContactKey(c.HookId, c.FishId)) : Array.Empty<ContactKey>()));
        }
        private static void ToBottom(FishingFlowController flow)
        { Step(flow, 100f / 6); Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold)); }
        private static void ToAscent(FishingFlowController flow)
        { ToBottom(flow); Step(flow, 1.2f); Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending)); }

        [Test] public void PersistentOverlapDoesNotSpendAgainAfterInvulnerabilityExpires()
        {
            var flow = Create();
            Step(flow, .02f, new[] { Fish(1) }); Step(flow, 2, new[] { Fish(1) });
            Assert.That(flow.Run.Protection, Is.EqualTo(1));
            Step(flow, .02f); Step(flow, .02f, new[] { Fish(1) });
            Assert.That(flow.Run.Protection, Is.Zero);
        }
        [Test] public void ContactBeginningDuringInvulnerabilityIsNotReplayedLater()
        {
            var flow = Create(); Step(flow, .02f, new[] { Fish(1) });
            Step(flow, .1f, new[] { Fish(2) }); Step(flow, 1.1f, new[] { Fish(2) });
            Assert.That(flow.Run.Protection, Is.EqualTo(1));
            Step(flow, .02f); Step(flow, .02f, new[] { Fish(2) });
            Assert.That(flow.Run.Protection, Is.Zero);
        }
        [Test] public void GuardedDiveSpendsOnlyOneChargeForAnEntireBatch()
        {
            var flow = Create(); Step(flow, .02f, new[] { Fish(3, 3), Fish(1, 1), Fish(2, 2) });
            Assert.That(flow.Run.Protection, Is.EqualTo(1)); Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
            Step(flow, 1.1f, new[] { Fish(1), Fish(2), Fish(3) });
            Assert.That(flow.Run.Protection, Is.EqualTo(1));
        }
        [Test] public void UnprotectedDiveCapturesOnlyNearestFishThenReevaluatesOverlapNextStep()
        {
            var flow = Create(0); var hits = new[] { Fish(3, 2), Fish(2, 0), Fish(1, 0) };
            Step(flow, .02f, hits);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending));
            Assert.That(flow.Run.Captures.Select(r => r.FishInstanceId), Is.EqualTo(new[] { 1 }));
            Step(flow, .02f, hits);
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(3));
        }
        [Test] public void SweptShortContactNeedsNoEndOverlapToSpendProtection()
        {
            var flow = Create(); Step(flow, .02f, new[] { Fish(1) }, false);
            Assert.That(flow.Run.Protection, Is.EqualTo(1));
            Step(flow, 1.1f, new[] { Fish(1) }, false);
            Assert.That(flow.Run.Protection, Is.Zero);
        }
        [Test] public void BottomBoundaryClearsInvulnerabilityAndReadmitsOverlappingFish()
        {
            var flow = Create(); Step(flow, 15.8f, new[] { Fish(1) });
            Assert.That(flow.Run.InvulnerableUntil, Is.GreaterThan(flow.Run.GameTime));
            Step(flow, .2f, new[] { Fish(1) });
            Assert.That(flow.Run.Depth, Is.EqualTo(96).Within(.001));
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1));
            Assert.That(flow.Run.InvulnerableUntil, Is.Zero);
            Assert.That(flow.Run.Protection, Is.EqualTo(1));
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Diving));
        }
        [Test] public void DepthLimitContactIsResolvedAsBottomBeforeEnteringHold()
        {
            var flow = Create(); Step(flow, 100f / 6, new[] { Fish(1), Fish(2) });
            Assert.That(flow.Run.Depth, Is.EqualTo(100)); Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(2));
            Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold)); Assert.That(flow.Run.Protection, Is.EqualTo(2));
        }
        [Test] public void BottomFullDoesNotSkipHoldOrAccelerateDescent()
        {
            var flow = Create(capacity: 1); Step(flow, 16, new[] { Fish(1) });
            Assert.That(flow.Run.IsFull, Is.True); Assert.That(flow.Run.State, Is.EqualTo(RunState.Diving));
            Step(flow, .1f); Assert.That(flow.Run.Depth, Is.EqualTo(96.6).Within(.001));
            Step(flow, 1); Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold));
            Step(flow, 1.19f); Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold));
            Step(flow, .02f); Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending));
            Step(flow, 1); Assert.That(flow.Run.Depth, Is.EqualTo(67.5).Within(.001));
        }
        [Test] public void DuplicateReportsAndHookReportsCommitOnlyOneRecord()
        {
            var flow = Create(); ToAscent(flow);
            Step(flow, .02f, new[] { Fish(1), Fish(1, .01f), Fish(1, 0, 1) });
            Step(flow, .02f, new[] { Fish(1) });
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1)); Assert.That(flow.Run.Gold, Is.EqualTo(10));
        }
        [Test] public void MainCapacityAndFullEventAreBoundedAndOrdered()
        {
            var flow = Create(); ToAscent(flow); int full = 0;
            var events = new List<string>();
            flow.OnFishCaptured += r => events.Add("capture:" + r.FishInstanceId);
            flow.Run.OnBecameFull += () => { full++; events.Add("full"); };
            var hits = Enumerable.Range(1, 12).Select(i => Fish(i, i)).ToArray();
            Step(flow, .02f, hits); Step(flow, .02f, hits);
            Assert.That(flow.Run.MainCaptureCount, Is.EqualTo(10)); Assert.That(full, Is.EqualTo(1));
            Assert.That(events[9], Is.EqualTo("capture:10")); Assert.That(events[10], Is.EqualTo("full"));
        }
        [TestCase(RunState.Diving)] [TestCase(RunState.BottomHold)] [TestCase(RunState.Ascending)]
        public void PauseFreezesTimeDepthProtectionAndContacts(RunState state)
        {
            var flow = Create();
            if (state == RunState.BottomHold) ToBottom(flow);
            if (state == RunState.Ascending) ToAscent(flow);
            float depth = flow.Run.Depth; double time = flow.Run.GameTime; long step = flow.StepSequence;
            flow.SetPaused(true); Step(flow, 5, new[] { Fish(1) });
            Assert.That(flow.Run.State, Is.EqualTo(state)); Assert.That(flow.Run.Depth, Is.EqualTo(depth));
            Assert.That(flow.Run.GameTime, Is.EqualTo(time)); Assert.That(flow.StepSequence, Is.EqualTo(step));
            Assert.That(flow.Run.Protection, Is.EqualTo(2)); Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
            flow.SetPaused(false); Step(flow, .02f); Assert.That(flow.Run.GameTime, Is.GreaterThan(time));
        }
        [Test] public void OldRunAndOldOrFutureStepBatchesAreRejected()
        {
            var flow = Create(0); var old = Create(0); flow.BeginStep(.02f);
            foreach (var batch in new[]
            {
                new ContactBatch(old.Run.RunId, flow.StepSequence, new[] { Fish(1) }, Array.Empty<ContactKey>()),
                new ContactBatch(flow.Run.RunId, flow.StepSequence - 1, new[] { Fish(1) }, Array.Empty<ContactKey>()),
                new ContactBatch(flow.Run.RunId, flow.StepSequence + 1, new[] { Fish(1) }, Array.Empty<ContactKey>())
            }) flow.ResolveContacts(batch);
            Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
            var valid = new ContactBatch(flow.Run.RunId, flow.StepSequence, new[] { Fish(1) }, Array.Empty<ContactKey>());
            flow.ResolveContacts(valid); flow.ResolveContacts(valid);
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1));
        }
        [Test] public void ReportPermutationsProduceIdenticalStableCaptureOrder()
        {
            var expected = new[] { 3, 1, 2 };
            var hits = new[] { Fish(2, 1), Fish(1, 1), Fish(3, 0), Fish(2, 1) };
            for (int rotation = 0; rotation < hits.Length; rotation++)
            {
                var flow = Create(capacity: 3); ToAscent(flow);
                Step(flow, .02f, hits.Skip(rotation).Concat(hits.Take(rotation)).ToArray());
                Assert.That(flow.Run.Captures.Select(r => r.FishInstanceId), Is.EqualTo(expected));
            }
        }
        [Test] public void DecorativeUnknownAndNotYetEnabledBranchCandidatesAreIgnored()
        {
            var flow = Create(0);
            Step(flow, .02f, new[] { Fish(1, species: "decor"), Fish(2, species: "unknown"), Fish(3, hook: 1) });
            Assert.That(flow.Run.Protection, Is.Zero); Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Diving));
        }
        [Test] public void SmallFishUseNormalProtectionRulesBeforeAbilitiesExist()
        {
            var flow = Create(); Step(flow, .02f, new[] { Fish(1, species: "small") });
            Assert.That(flow.Run.Protection, Is.EqualTo(1)); Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
        }
        [Test] public void FinalAscentSweepCapturesBeforeSurfacingAndFinishPublishesOnce()
        {
            var flow = Create(); ToAscent(flow); int finishes = 0; flow.OnRunFinished += _ => finishes++;
            Step(flow, 20, new[] { Fish(1) }, false);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Surfacing)); Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1));
            Step(flow, 1); Step(flow, 1);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Result)); Assert.That(finishes, Is.EqualTo(1));
        }
        [Test] public void EarlyReturnUsesActualReachedDepthAndCaptureTimeAndSource()
        {
            var flow = Create(0); Step(flow, 2, new[] { Fish(1) });
            var capture = flow.Run.Captures[0];
            Assert.That(flow.Run.ReachedDepth, Is.EqualTo(12)); Assert.That(capture.Depth, Is.EqualTo(12));
            Assert.That(capture.GameTime, Is.EqualTo(flow.Run.GameTime));
            Assert.That(capture.Source, Is.EqualTo(CaptureSource.Main)); Assert.That(capture.HookId, Is.Zero);
            Step(flow, 20); Assert.That(flow.Run.ReachedDepth, Is.EqualTo(12));
        }
        [Test] public void FreshRunsNeverReuseCaptureDeduplicationOrRunIdentity()
        {
            var runIds = new HashSet<Guid>();
            for (int i = 0; i < 20; i++)
            {
                var flow = Create(0); Assert.That(runIds.Add(flow.Run.RunId), Is.True);
                Assert.That(flow.Run.TotalCaptureCount, Is.Zero);
                Step(flow, .02f, new[] { Fish(1) }); Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(1));
            }
            // This checks fresh rule objects, not the separate scene/pool restart acceptance.
        }
        [Test] public void SourceAggregationMergesRowsAndIncludesBothCurrencies()
        {
            var records = new[]
            {
                new CaptureRecord(Blue, 1, CaptureSource.Main, 0, 10, 1),
                new CaptureRecord(Blue, 2, CaptureSource.Main, 0, 20, 2),
                new CaptureRecord(Blue, 3, CaptureSource.Branch, 1, 20, 2),
                new CaptureRecord(Small, 4, CaptureSource.Main, 0, 30, 3)
            };
            var rows = Settlement.Summarize(records);
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].MainCount, Is.EqualTo(2)); Assert.That(rows[0].BranchCount, Is.EqualTo(1));
            Assert.That(rows[0].TotalCount, Is.EqualTo(3)); Assert.That(rows[0].HasBranch, Is.True);
            Assert.That(rows[1].HasBranch, Is.False);
            Assert.That(rows.Sum(r => r.TotalCount), Is.EqualTo(records.Length));
            Assert.That(rows.Sum(r => r.Gold), Is.EqualTo(35)); Assert.That(rows.Sum(r => r.Shells), Is.EqualTo(24));
        }
        [Test] public void CoreAssemblyHasNoUnityOrGameReference()
        {
            var names = typeof(RunModel).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(names.Any(n => n.StartsWith("Unity") || n == "Fishing.Game"), Is.False);
        }
        [Test] public void DefaultDescentSlowdownStillReachesBottomInFiniteTime()
        {
            var flow = new FishingFlowController(new FishingRules(countdownSeconds: 0), new[] { Blue });
            flow.Start();
            for (int i = 0; i < 1500 && flow.Run.State != RunState.BottomHold; i++) Step(flow, .02f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold));
            Assert.That(flow.Run.Depth, Is.EqualTo(100));
        }
    }
}
