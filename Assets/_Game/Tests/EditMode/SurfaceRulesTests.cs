using System;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Surface skip and timeout must publish exactly the same committed settlement.</summary>
    public sealed class SurfaceRulesTests
    {
        private static void Step(FishingFlowController flow, float dt, params ContactCandidate[] contacts)
        {
            long step = flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId, step, contacts,
                contacts.Select(c => new ContactKey(c.HookId, c.FishId))));
        }
        private static FishingFlowController Create(bool populated)
        {
            var fish = new[] { new FishRules("blue", "Blue", 10, 7), new FishRules("small", "Small", 5, 3, true, true) };
            var flow = new FishingFlowController(new FishingRules(maxDepth: 10, bottomZoneDepth: 9.6f,
                diveSpeed: 100, slowdownDistance: 0, ascentSpeed: 1, bottomHoldSeconds: 0, countdownSeconds: 0, surfacingSeconds: 2.3f), fish);
            flow.Start(); Step(flow, .02f); Step(flow, .1f); Step(flow, .02f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending));
            if (populated)
            {
                Assert.That(flow.Abilities.Branches.Activate(), Is.True);
                var main = Enumerable.Range(1, 10).Select(id => new ContactCandidate(0, id, "blue", id));
                var branch = Enumerable.Range(11, 6).Select(id => new ContactCandidate(id < 14 ? 1 : 2, id, "small", id));
                Step(flow, .02f, main.Concat(branch).ToArray());
                Assert.That(flow.Run.MainCaptureCount, Is.EqualTo(10)); Assert.That(flow.Run.BranchCaptureCount, Is.EqualTo(6));
            }
            Step(flow, 20); Assert.That(flow.Run.State, Is.EqualTo(RunState.Surfacing)); return flow;
        }
        [TestCase(0f)] [TestCase(.7f)] [TestCase(2.28f)]
        public void SkippingAtAnyPointKeepsAllSixteenRecordSnapshots(float elapsed)
        {
            var flow = Create(true); var records = flow.Run.Captures.ToArray();
            int finishes = 0; flow.OnRunFinished += _ => finishes++;
            Step(flow, elapsed); Assert.That(flow.SkipSurfacing(), Is.True);
            Assert.That(flow.SkipSurfacing(), Is.False); Step(flow, 5);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Result)); Assert.That(finishes, Is.EqualTo(1));
            Assert.That(flow.Run.Captures, Is.EqualTo(records)); Assert.That(flow.Run.Gold, Is.EqualTo(130)); Assert.That(flow.Run.Shells, Is.EqualTo(88));
            Assert.That(flow.Run.Depth, Is.Zero); Assert.That(flow.Run.ReachedDepth, Is.EqualTo(10));
        }
        [Test]
        public void NaturalTimeoutMatchesSkipAndEmptyCatchIsValid()
        {
            foreach (bool populated in new[] { false, true })
            {
                var normal = Create(populated); var skipped = Create(populated); int finishes = 0;
                normal.OnRunFinished += _ => finishes++; Step(normal, 2.28f); Assert.That(normal.Run.State, Is.EqualTo(RunState.Surfacing));
                Step(normal, .04f); Assert.That(skipped.SkipSurfacing(), Is.True);
                Assert.That(normal.Run.Gold, Is.EqualTo(skipped.Run.Gold)); Assert.That(normal.Run.Shells, Is.EqualTo(skipped.Run.Shells));
                Assert.That(Settlement.Summarize(normal.Run.Captures).Select(r => r.TotalCount),
                    Is.EqualTo(Settlement.Summarize(skipped.Run.Captures).Select(r => r.TotalCount)));
                Step(normal, 10); Assert.That(finishes, Is.EqualTo(1));
            }
        }
        [Test]
        public void SkipCannotBypassCountdownOrActiveGameplay()
        {
            var flow = new FishingFlowController(new FishingRules(), Array.Empty<FishRules>());
            Assert.That(flow.SkipSurfacing(), Is.False); flow.Start(); Assert.That(flow.SkipSurfacing(), Is.False);
            Step(flow, 3); Assert.That(flow.Run.State, Is.EqualTo(RunState.Diving));
            var state = flow.Run.State; Assert.That(flow.SkipSurfacing(), Is.False); Assert.That(flow.Run.State, Is.EqualTo(state));
        }
    }
}
