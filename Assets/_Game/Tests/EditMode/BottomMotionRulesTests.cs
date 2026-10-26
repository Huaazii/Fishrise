using System;
using System.Linq;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Finite braking, short bottom pause and smooth retrieval launch on the authoritative depth path.</summary>
    public sealed class BottomMotionRulesTests
    {
        private static readonly FishRules Blue = new FishRules("blue", "Blue", 10, 7);
        private static FishingFlowController Create(int protection = 2)
        {
            var flow = new FishingFlowController(new FishingRules(countdownSeconds: 0, protectionCharges: protection, mainCapacity: 1), new[] { Blue });
            flow.Start(); Step(flow, 0); return flow;
        }
        private static void Step(FishingFlowController flow, float dt, params ContactCandidate[] hits)
        {
            long step = flow.BeginStep(dt);
            flow.ResolveContacts(new ContactBatch(flow.Run.RunId, step, hits, hits.Select(h => new ContactKey(h.HookId,h.FishId))));
        }

        /// <summary>Matches the analytic cruise-plus-braking duration across small and large fixed steps.</summary>
        [TestCase(.01f)] [TestCase(.02f)] [TestCase(.1f)]
        public void DescentBrakesToRestInFiniteTimeWithoutOvershooting(float dt)
        {
            var flow = Create(); float time = 0, previousSpeed = flow.Run.Rules.DiveSpeed;
            while (flow.Run.State == RunState.Diving && time < 25)
            {
                float depth = flow.Run.Depth; Step(flow, dt); time += dt;
                float speed = (flow.Run.Depth-depth)/dt;
                Assert.That(flow.Run.Depth, Is.InRange(depth,100));
                Assert.That(speed, Is.LessThanOrEqualTo(previousSpeed+.005f)); previousSpeed = speed;
            }
            Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold));
            Assert.That(time, Is.InRange(17.495f,17.505f+dt));
            Assert.That(previousSpeed, Is.LessThanOrEqualTo(3.6f*dt+.01f), "The last dive step must approach rest instead of a fixed 20% floor.");
        }

        /// <summary>Both empty and full hooks keep the brief hold, contact result and velocity continuity through pause.</summary>
        [TestCase(false)] [TestCase(true)]
        public void BottomLaunchIsSmoothAndItsHoldAndRampFreezeOnPause(bool full)
        {
            var flow = Create();
            Step(flow,20,full ? new[] { new ContactCandidate(0,1,"blue",0) } : Array.Empty<ContactCandidate>());
            Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold)); Assert.That(flow.Run.IsFull, Is.EqualTo(full));
            Step(flow,.34f); Assert.That(flow.Run.State, Is.EqualTo(RunState.BottomHold));
            flow.SetPaused(true); Step(flow,5); Assert.That(flow.Run.Depth, Is.EqualTo(100));
            flow.SetPaused(false); Step(flow,.02f); Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending));
            Assert.That(flow.Run.Depth, Is.EqualTo(100));
            float target = full ? 32.5f : 6.5f, previousSpeed = 0;
            for (int i = 0; i < 25; i++)
            {
                if (i == 4)
                {
                    double time = flow.Run.GameTime; float depth = flow.Run.Depth;
                    flow.SetPaused(true); Step(flow,5); Assert.That(flow.Run.GameTime, Is.EqualTo(time));
                    Assert.That(flow.Run.Depth, Is.EqualTo(depth)); flow.SetPaused(false);
                }
                float before = flow.Run.Depth; Step(flow,.02f); float speed = (before-flow.Run.Depth)/.02f;
                Assert.That(speed, Is.InRange(previousSpeed-.01f,target+.01f));
                Assert.That(speed-previousSpeed, Is.LessThan(target*.1f+.01f)); previousSpeed = speed;
            }
            Assert.That(previousSpeed, Is.EqualTo(target).Within(.01f));
            Assert.That(flow.Run.TotalCaptureCount, Is.EqualTo(full ? 1 : 0));
        }

        /// <summary>Integrating the launch ramp gives the same distance for different simulation step sizes.</summary>
        [TestCase(.02f,35)] [TestCase(.1f,7)]
        public void RetrievalLaunchDistanceDoesNotDependOnFixedStepSize(float dt, int count)
        {
            var flow = Create(); Step(flow,20); Step(flow,.36f);
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending));
            for (int i = 0; i < count; i++) Step(flow,dt);
            Assert.That(flow.Run.Depth, Is.EqualTo(100-6.5f*(.7f-.35f*.5f)).Within(.001f));
        }

        /// <summary>Only the maximum-depth turnaround receives the launch ramp.</summary>
        [Test]
        public void EarlyGuardedReturnRetainsItsExistingRetrievalSpeed()
        {
            var flow = Create(0); Step(flow,2,new ContactCandidate(0,1,"blue",0));
            Assert.That(flow.Run.State, Is.EqualTo(RunState.Ascending));
            float before = flow.Run.Depth; Step(flow,.1f);
            Assert.That(before-flow.Run.Depth, Is.EqualTo(3.25f).Within(.001f));
        }

        /// <summary>The optional ramp duration accepts zero for comparisons and rejects invalid values.</summary>
        [Test]
        public void LaunchDurationIsValidatedAndCanBeDisabled()
        {
            Assert.That(new FishingRules(ascentStartSeconds:0).AscentStartSeconds, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(()=>new FishingRules(ascentStartSeconds:-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new FishingRules(ascentStartSeconds:float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new FishingRules(ascentStartSeconds:float.PositiveInfinity));
        }
    }
}
