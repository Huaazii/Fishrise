using Fishing.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fishing.Tests
{
    /// <summary>Verifies consumer-visible gesture boundaries with deterministic touch times and distances.</summary>
    [Category("MobileControls")]
    public sealed class FishingTouchRouterTests
    {
        private static readonly Rect Area = new Rect(0f, 0f, 720f, 1602f);
        private const float Threshold = 20f;

        /// <summary>A stationary drag-mode touch confirms at 0.1 seconds, then yields to a later drag.</summary>
        [Test]
        public void ConfirmedHoldBecomesSteeringWhenItCrossesThreshold()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 0f, Threshold);
            router.Process(4, new Vector2(100f, 200f), TouchPhase.Began, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);
            Assert.That(router.Steering, Is.False);

            router.BeginFrame(Area, true, true, 0.099f, Threshold);
            router.Process(4, new Vector2(100f, 200f), TouchPhase.Stationary, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);

            router.BeginFrame(Area, true, true, 0.1f, Threshold);
            router.Process(4, new Vector2(100f, 200f), TouchPhase.Stationary, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.True);

            router.BeginFrame(Area, true, true, 0.11f, Threshold);
            router.Process(4, new Vector2(121f, 200f), TouchPhase.Moved, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);
            Assert.That(router.Steering, Is.True);
            Assert.That(router.SteeringStarted, Is.True);
            Assert.That(router.SteeringOriginX, Is.EqualTo(100f));
            Assert.That(router.PointerX, Is.EqualTo(121f));
        }

        /// <summary>Releasing an already confirmed hold ends reel instead of adding a synthetic tap frame.</summary>
        [Test]
        public void ConfirmedHoldStopsReelingOnRelease()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 0f, Threshold);
            router.Process(4, new Vector2(100f, 200f), TouchPhase.Began, false);
            router.EndFrame();
            router.BeginFrame(Area, true, true, 0.1f, Threshold);
            router.Process(4, new Vector2(100f, 200f), TouchPhase.Stationary, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.True);

            router.BeginFrame(Area, true, true, 0.11f, Threshold);
            router.Process(4, new Vector2(100f, 200f), TouchPhase.Ended, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);
        }

        /// <summary>The supplied threshold is exclusive; a short release at the boundary pulses reel without steering.</summary>
        [Test]
        public void ExactThresholdReleaseIsTapWithoutPhantomSteering()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 2f, Threshold);
            router.Process(1, new Vector2(200f, 300f), TouchPhase.Began, false);
            router.EndFrame();

            router.BeginFrame(Area, true, true, 2.05f, Threshold);
            router.Process(1, new Vector2(220f, 300f), TouchPhase.Ended, false);
            router.EndFrame();

            Assert.That(router.Reeling, Is.True);
            Assert.That(router.Steering, Is.False);
            Assert.That(router.SteeringStarted, Is.False);
        }

        /// <summary>A fast movement over the threshold starts from the original press and never emits a reel pulse.</summary>
        [Test]
        public void FastDragNeverEmitsReel()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 3f, Threshold);
            router.Process(8, new Vector2(300f, 400f), TouchPhase.Began, false);
            router.EndFrame();

            router.BeginFrame(Area, true, true, 3.02f, Threshold);
            router.Process(8, new Vector2(330f, 400f), TouchPhase.Moved, false);
            router.EndFrame();
            Assert.That(router.Steering, Is.True);
            Assert.That(router.Reeling, Is.False);
            Assert.That(router.SteeringOriginX, Is.EqualTo(300f));
            Assert.That(router.PointerX, Is.EqualTo(330f));

            router.BeginFrame(Area, true, true, 3.04f, Threshold);
            router.Process(8, new Vector2(330f, 400f), TouchPhase.Ended, false);
            router.EndFrame();
            Assert.That(router.Steering, Is.False);
            Assert.That(router.Reeling, Is.False);
        }

        /// <summary>A second stationary finger can hold reel while the steering owner continues to drag.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void StationarySecondFingerCanReelWhileOwnerDrags(bool stationaryFirst)
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 4f, Threshold);
            if (stationaryFirst)
                router.Process(2, new Vector2(600f, 1200f), TouchPhase.Began, false);
            router.Process(9, new Vector2(100f, 500f), TouchPhase.Began, false);
            if (!stationaryFirst)
                router.Process(2, new Vector2(600f, 1200f), TouchPhase.Began, false);
            router.EndFrame();

            router.BeginFrame(Area, true, true, 4.11f, Threshold);
            if (!stationaryFirst)
                router.Process(9, new Vector2(130f, 500f), TouchPhase.Moved, false);
            router.Process(2, new Vector2(600f, 1200f), TouchPhase.Stationary, false);
            if (stationaryFirst)
                router.Process(9, new Vector2(130f, 500f), TouchPhase.Moved, false);
            router.EndFrame();

            Assert.That(router.Steering, Is.True);
            Assert.That(router.PointerX, Is.EqualTo(130f));
            Assert.That(router.Reeling, Is.True);
        }

        /// <summary>Every part of the safe area accepts the same short-tap reel gesture without legacy zones.</summary>
        [TestCase(1f, 1f)]
        [TestCase(719f, 1f)]
        [TestCase(1f, 1601f)]
        [TestCase(719f, 1601f)]
        [TestCase(360f, 801f)]
        public void ShortTapReelsAcrossTheWholeSafeArea(float x, float y)
        {
            var router = new FishingTouchRouter();
            var point = new Vector2(x, y);
            router.BeginFrame(Area, true, true, 5f, Threshold);
            router.Process(3, point, TouchPhase.Began, false);
            router.EndFrame();
            router.BeginFrame(Area, true, true, 5.05f, Threshold);
            router.Process(3, point, TouchPhase.Ended, false);
            router.EndFrame();

            Assert.That(router.Reeling, Is.True);
            Assert.That(router.Steering, Is.False);
        }

        /// <summary>UI and out-of-area beginnings are ignored, while an accepted finger remains captured across both.</summary>
        [Test]
        public void AdmissionBoundariesDoNotReleaseAnAcceptedFinger()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, false, true, 6f, Threshold);
            router.Process(1, new Vector2(100f, 200f), TouchPhase.Began, true);
            router.Process(2, new Vector2(-1f, 200f), TouchPhase.Began, false);
            router.Process(3, new Vector2(200f, 300f), TouchPhase.Began, false);
            router.EndFrame();
            Assert.That(router.Steering, Is.True);
            Assert.That(router.PointerX, Is.EqualTo(200f));

            router.BeginFrame(Area, false, true, 6.01f, Threshold);
            router.Process(3, new Vector2(900f, 300f), TouchPhase.Moved, true);
            router.EndFrame();
            Assert.That(router.Steering, Is.True);
            Assert.That(router.PointerX, Is.EqualTo(900f));
        }

        /// <summary>Canceled, missing and reset pointers neither synthesize taps nor return on non-begin samples.</summary>
        [Test]
        public void CanceledMissingAndResetPointersCannotReelOrResurrect()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 7f, Threshold);
            router.Process(1, new Vector2(100f, 100f), TouchPhase.Began, false);
            router.EndFrame();
            router.BeginFrame(Area, true, true, 7.05f, Threshold);
            router.Process(1, new Vector2(100f, 100f), TouchPhase.Canceled, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);

            router.BeginFrame(Area, true, true, 8f, Threshold);
            router.Process(2, new Vector2(200f, 200f), TouchPhase.Began, false);
            router.EndFrame();
            router.BeginFrame(Area, true, true, 8.05f, Threshold);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);
            router.BeginFrame(Area, true, true, 8.1f, Threshold);
            router.Process(2, new Vector2(200f, 200f), TouchPhase.Stationary, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);

            router.BeginFrame(Area, true, true, 9f, Threshold);
            router.Process(3, new Vector2(300f, 300f), TouchPhase.Began, false);
            router.EndFrame();
            router.Reset();
            router.BeginFrame(Area, true, true, 9.2f, Threshold);
            router.Process(3, new Vector2(300f, 300f), TouchPhase.Stationary, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);
            Assert.That(router.Steering, Is.False);
        }

        /// <summary>New owner selection is order-independent, and a later lower ID cannot steal until release.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void SteeringOwnerIsStableAndReplacementOrderIndependent(bool replacementFirst)
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, false, true, 10f, Threshold);
            router.Process(7, new Vector2(100f, 400f), TouchPhase.Began, false);
            router.EndFrame();
            Assert.That(router.PointerX, Is.EqualTo(100f));

            router.BeginFrame(Area, false, true, 10.01f, Threshold);
            router.Process(2, new Vector2(500f, 400f), TouchPhase.Began, false);
            router.Process(7, new Vector2(110f, 400f), TouchPhase.Moved, false);
            router.EndFrame();
            Assert.That(router.PointerX, Is.EqualTo(110f));
            Assert.That(router.SteeringStarted, Is.False);

            router.BeginFrame(Area, false, true, 10.02f, Threshold);
            if (replacementFirst)
                router.Process(2, new Vector2(510f, 400f), TouchPhase.Moved, false);
            router.Process(7, new Vector2(110f, 400f), TouchPhase.Ended, false);
            if (!replacementFirst)
                router.Process(2, new Vector2(510f, 400f), TouchPhase.Moved, false);
            router.EndFrame();

            Assert.That(router.Steering, Is.True);
            Assert.That(router.SteeringStarted, Is.True);
            Assert.That(router.SteeringOriginX, Is.EqualTo(500f));
            Assert.That(router.PointerX, Is.EqualTo(510f));
        }

        /// <summary>Simultaneous immediate-drag candidates choose the same stable ID in either sample order.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void InitialSteeringSelectionIsIndependentOfSampleOrder(bool lowerIdFirst)
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, false, true, 10f, Threshold);
            if (lowerIdFirst)
                router.Process(2, new Vector2(500f, 400f), TouchPhase.Began, false);
            router.Process(7, new Vector2(100f, 400f), TouchPhase.Began, false);
            if (!lowerIdFirst)
                router.Process(2, new Vector2(500f, 400f), TouchPhase.Began, false);
            router.EndFrame();

            Assert.That(router.Steering, Is.True);
            Assert.That(router.SteeringStarted, Is.True);
            Assert.That(router.SteeringOriginX, Is.EqualTo(500f));
            Assert.That(router.PointerX, Is.EqualTo(500f));
        }

        /// <summary>Tilt mode immediately reels from accepted touches and retains them while moving over UI.</summary>
        [Test]
        public void TiltTouchImmediatelyHoldsReelWhileMoving()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, false, 11f, Threshold);
            router.Process(5, new Vector2(100f, 500f), TouchPhase.Began, false);
            router.EndFrame();
            Assert.That(router.Reeling, Is.True);
            Assert.That(router.Steering, Is.False);

            router.BeginFrame(Area, true, false, 11.01f, Threshold);
            router.Process(5, new Vector2(900f, 500f), TouchPhase.Moved, true);
            router.EndFrame();
            Assert.That(router.Reeling, Is.True);
            Assert.That(router.Steering, Is.False);

            router.BeginFrame(Area, true, false, 11.02f, Threshold);
            router.Process(5, new Vector2(900f, 500f), TouchPhase.Ended, true);
            router.EndFrame();
            Assert.That(router.Reeling, Is.False);
        }

        /// <summary>Reel output stays off outside the reel phase in both drag and tilt control modes.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void ReelIsDisabledOutsideAllowedPhase(bool dragSteering)
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, false, dragSteering, 12f, Threshold);
            router.Process(6, new Vector2(400f, 600f), TouchPhase.Began, false);
            router.EndFrame();

            Assert.That(router.Reeling, Is.False);
            Assert.That(router.Steering, Is.EqualTo(dragSteering));
        }
        /// <summary>A tap spanning the hold threshold still pulses once when no intermediate hold sample was delivered.</summary>
        [Test]
        public void TapAcrossHoldConfirmationSampleGapStillPulsesOnce()
        {
            var router = new FishingTouchRouter();
            router.BeginFrame(Area, true, true, 0, Threshold);
            router.Process(1, new Vector2(100, 200), TouchPhase.Began, false); router.EndFrame();
            router.BeginFrame(Area, true, true, .12f, Threshold);
            router.Process(1, new Vector2(100, 200), TouchPhase.Ended, false); router.EndFrame();
            Assert.That(router.Reeling, Is.True); Assert.That(router.Steering, Is.False);
            router.BeginFrame(Area, true, true, .14f, Threshold); router.EndFrame();
            Assert.That(router.Reeling, Is.False);
        }
    }
}
