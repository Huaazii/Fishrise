using System;
using Fishing.Core;
using NUnit.Framework;

namespace Fishing.Tests
{
    /// <summary>Device-independent checks for the first retrieval checkpoint and its explicit-clock contract.</summary>
    public sealed class ReelingRulesTests
    {
        /// <summary>Hold/release creates inertia while keeping the complete green bar inside the track.</summary>
        [Test]
        public void HoldRisesReleaseFallsAndTheBarStaysInsideTrack()
        {
            var reel = new ReelingModel(new ReelingRules(true)); reel.SetHeld(true);
            for (int i = 0; i < 40; i++) reel.Tick(.02f, 1, 16, 6.5f);
            float upper = reel.BarPosition; Assert.That(upper, Is.GreaterThan(.35f)); reel.SetHeld(false);
            for (int i = 0; i < 150; i++)
            {
                reel.Tick(.02f, 1, 16, 6.5f);
                Assert.That(reel.BarPosition - reel.BarHeight / 2, Is.GreaterThanOrEqualTo(-.00001f));
                Assert.That(reel.BarPosition + reel.BarHeight / 2, Is.LessThanOrEqualTo(1.00001f));
                Assert.That(reel.Speed, Is.InRange(2f, 6f));
            }
            Assert.That(reel.BarPosition, Is.LessThan(upper));
        }
        /// <summary>A press/release between simulation ticks still produces a short pull.</summary>
        [Test]
        public void QuickTapIsLatchedAndZeroDeltaDoesNotConsumeIt()
        {
            var reel = new ReelingModel(new ReelingRules(true)); reel.SetHeld(true); reel.SetHeld(false);
            reel.Tick(0, 1, 16, 6.5f); reel.Tick(.02f, 1, 16, 6.5f);
            Assert.That(reel.BarPosition, Is.GreaterThan(.35f));
        }
        /// <summary>Fish count changes only the normalized green height with linear interpolation.</summary>
        [TestCase(1, .42f)] [TestCase(6, .3733333f)] [TestCase(11, .3266667f)] [TestCase(16, .28f)]
        public void CountUsesLinearBarHeight(int count, float height)
        {
            var reel = new ReelingModel(new ReelingRules(true)); reel.Tick(.02f, count, 16, 6.5f);
            Assert.That(reel.BarHeight, Is.EqualTo(height).Within(.00001));
        }
        /// <summary>New catches ease the difficulty without teleporting the target or jumping the speed.</summary>
        [Test]
        public void NewCatchSmoothsDifficultyAndSpeed()
        {
            var reel = new ReelingModel(new ReelingRules(true)); reel.Tick(.02f, 1, 16, 6.5f);
            float height = reel.BarHeight, fish = reel.FishPosition, speed = reel.Speed;
            reel.Tick(.02f, 16, 16, 6.5f);
            Assert.That(reel.BarHeight, Is.InRange(.40f, height));
            Assert.That(Math.Abs(reel.FishPosition - fish), Is.LessThanOrEqualTo(.00481f));
            Assert.That(Math.Abs(reel.Speed - speed), Is.LessThan(.21f));
        }
        /// <summary>Fixed substeps keep deterministic motion stable across different caller frame sizes.</summary>
        [Test]
        public void EquivalentTimeChunksProduceEquivalentMotion()
        {
            var a = new ReelingModel(new ReelingRules(true)); var b = new ReelingModel(new ReelingRules(true));
            a.SetHeld(true); b.SetHeld(true);
            for (int i = 0; i < 100; i++) a.Tick(.02f, 10, 16, 6.5f);
            for (int i = 0; i < 20; i++) b.Tick(.1f, 10, 16, 6.5f);
            Assert.That(a.FishPosition, Is.EqualTo(b.FishPosition).Within(.003));
            Assert.That(a.BarPosition, Is.EqualTo(b.BarPosition).Within(.003));
            Assert.That(a.Speed, Is.EqualTo(b.Speed).Within(.003));
        }
        /// <summary>Empty hooks bypass the interaction and keep the normal empty-return speed.</summary>
        [Test]
        public void EmptyHookReturnsAutomaticallyAndClearsInput()
        {
            var reel = new ReelingModel(new ReelingRules(true)); reel.SetHeld(true); reel.Tick(.02f, 0, 16, 6.5f);
            Assert.That(reel.Active, Is.False); Assert.That(reel.Speed, Is.EqualTo(6.5f));
            reel.Tick(.02f, 1, 16, 6.5f); Assert.That(reel.BarPosition, Is.LessThan(.35f));
        }
        /// <summary>Invalid editable settings are rejected before a run can start.</summary>
        [Test]
        public void InvalidRulesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReelingRules(true, minSpeed: 7, maxSpeed: 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReelingRules(true, hardHeight: .5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ReelingRules(true, fishSpeed: float.NaN));
        }
    }
}
