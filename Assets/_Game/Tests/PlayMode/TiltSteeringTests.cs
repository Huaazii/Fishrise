using Fishing.Game;
using NUnit.Framework;
using UnityEngine;

namespace Fishing.Tests
{
    /// <summary>Exercises gravity calibration and responsive steering independently of physical device sample timing.</summary>
    [Category("MobileControls")]
    public sealed class TiltSteeringTests
    {
        /// <summary>Loading stalls cannot complete a natural-grip average using a single reading.</summary>
        [Test]
        public void OneLongFrameCannotCompleteCalibration()
        {
            var tilt = new TiltSteering(); tilt.BeginCalibration(); tilt.Sample(Vector3.down, 2, 1);
            Assert.That(tilt.IsCalibrating, Is.True); Assert.That(tilt.IsCalibrated, Is.False);
            Assert.That(tilt.Horizontal, Is.Zero);
        }
        private static Vector3 Gravity(float angle)
        { float radians = angle * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(radians), -Mathf.Cos(radians), 0); }
        private static TiltSteering Calibrated(float neutral = 0)
        {
            var tilt = new TiltSteering(); tilt.BeginCalibration();
            for (int i = 0; i < 25; i++) tilt.Sample(Gravity(neutral), .02f, 1);
            return tilt;
        }
        /// <summary>A comfortable non-upright grip is neutral; deliberate tilts steer both directions.</summary>
        [Test]
        public void NaturalGripAndSmallJitterDoNotDriftButTiltsSteerBothWays()
        {
            var tilt = Calibrated(8);
            for (int i = 0; i < 40; i++) tilt.Sample(Gravity(8 + (i % 2 == 0 ? 1 : -1)), .02f, 1);
            Assert.That(tilt.IsCalibrated, Is.True); Assert.That(tilt.Horizontal, Is.Zero);
            for (int i = 0; i < 25; i++) tilt.Sample(Gravity(18), .02f, 1);
            Assert.That(tilt.Horizontal, Is.InRange(.1f, 1));
            for (int i = 0; i < 25; i++) tilt.Sample(Gravity(-2), .02f, 1);
            Assert.That(tilt.Horizontal, Is.InRange(-1, -.1f));
        }
        /// <summary>A small deliberate tilt reacts promptly in either direction while grip jitter still stops movement.</summary>
        [TestCase(2f)]
        [TestCase(-2f)]
        public void SmallDeliberateTiltRespondsWithoutLosingNeutralStop(float angle)
        {
            var tilt = Calibrated(8);
            for (int i = 0; i < 3; i++) tilt.Sample(Gravity(8 + angle), .02f, 1.5f);
            Assert.That(tilt.Horizontal * Mathf.Sign(angle), Is.GreaterThan(.05f));
            tilt.Sample(Gravity(8 + Mathf.Sign(angle) * .6f), .02f, 1.5f);
            Assert.That(tilt.Horizontal, Is.Zero);
        }
        /// <summary>Returning to the calibrated pose immediately stops rather than leaving a filter tail.</summary>
        [Test]
        public void NeutralStopsWithoutCoastingAndRecalibrationCannotMoveTheHook()
        {
            var tilt = Calibrated();
            for (int i = 0; i < 25; i++) tilt.Sample(Gravity(20), .02f, 1);
            Assert.That(tilt.Horizontal, Is.GreaterThan(.5f));
            tilt.Sample(Gravity(0), .02f, 1); Assert.That(tilt.Horizontal, Is.Zero);
            tilt.BeginCalibration(); tilt.Sample(Gravity(20), .1f, 1);
            Assert.That(tilt.IsCalibrating, Is.True); Assert.That(tilt.Horizontal, Is.Zero);
        }
        /// <summary>Missing startup sensor data does not create an arbitrary neutral pose.</summary>
        [Test]
        public void ZeroSensorSamplesDoNotCompleteCalibration()
        {
            var tilt = new TiltSteering(); tilt.BeginCalibration();
            for (int i = 0; i < 100; i++) tilt.Sample(Vector3.zero, .02f, 1);
            Assert.That(tilt.IsCalibrated, Is.False); Assert.That(tilt.Horizontal, Is.Zero);
            for (int i = 0; i < 25; i++) tilt.Sample(Gravity(5), .02f, 1);
            Assert.That(tilt.IsCalibrated, Is.True); Assert.That(tilt.Horizontal, Is.Zero);
        }
        /// <summary>Input strength is stable across sample rates and sensitivity has a bounded effect.</summary>
        [Test]
        public void SampleRateDoesNotChangeSteeringAndSensitivityKeepsItsBounds()
        {
            var fine = Calibrated(); var coarse = Calibrated();
            for (int i = 0; i < 40; i++) fine.Sample(Gravity(10), .01f, 1);
            for (int i = 0; i < 10; i++) coarse.Sample(Gravity(10), .04f, 1);
            Assert.That(fine.Horizontal, Is.EqualTo(coarse.Horizontal).Within(.001f));
            float normal = fine.Horizontal;
            for (int i = 0; i < 40; i++) fine.Sample(Gravity(10), .01f, 1.5f);
            Assert.That(fine.Horizontal, Is.GreaterThan(normal)); Assert.That(fine.Horizontal, Is.LessThanOrEqualTo(1));
            for (int i = 0; i < 40; i++) fine.Sample(Gravity(-40), .02f, 1.5f);
            Assert.That(fine.Horizontal, Is.EqualTo(-1));
        }
        /// <summary>Clearing input on pause retains the grip but removes the previous movement command.</summary>
        [Test]
        public void PauseResetPreservesCalibrationWithoutKeepingMotion()
        {
            var tilt = Calibrated(5);
            for (int i = 0; i < 25; i++) tilt.Sample(Gravity(18), .02f, 1);
            Assert.That(tilt.Horizontal, Is.GreaterThan(0));
            tilt.ResetMotion(); Assert.That(tilt.IsCalibrated, Is.True); Assert.That(tilt.Horizontal, Is.Zero);
            tilt.Sample(Gravity(5), .02f, 1); Assert.That(tilt.Horizontal, Is.Zero);
        }
    }
}
