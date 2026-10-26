using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Turns calibrated gravity samples into a bounded horizontal velocity intent, without integrating gyro drift.</summary>
    public sealed class TiltSteering
    {
        private const float CalibrationSeconds = .35f;
        private const float DeadZoneDegrees = 1f;
        private const float FullTiltDegrees = 12f;
        private const float FilterSeconds = .04f;
        private float _neutral, _sum, _sampleTime, _filtered;
        /// <summary>Whether neutral-pose sampling is in progress.</summary>
        public bool IsCalibrating { get; private set; }
        /// <summary>Whether this run has a usable neutral pose.</summary>
        public bool IsCalibrated { get; private set; }
        /// <summary>Signed velocity intent in [-1,1]; neutral means stop where the hook is.</summary>
        public float Horizontal { get; private set; }
        /// <summary>Captures the current comfortable grip instead of assuming an upright phone.</summary>
        public void BeginCalibration()
        {
            IsCalibrating = true; IsCalibrated = false;
            _sum = _sampleTime = 0; ResetMotion();
        }
        /// <summary>Clears movement without discarding a calibrated grip, for pause and phase transitions.</summary>
        public void ResetMotion() { _filtered = Horizontal = 0; }
        /// <summary>Samples gravity with explicit unscaled time; zero startup samples cannot finish calibration.</summary>
        public void Sample(Vector3 gravity, float dt, float sensitivity)
        {
            if (dt <= 0 || gravity.sqrMagnitude < .01f) { ResetMotion(); return; }
            float angle = Mathf.Asin(Mathf.Clamp(gravity.normalized.x, -1, 1)) * Mathf.Rad2Deg;
            if (IsCalibrating)
            {
                // A scene-load hitch must not turn one sensor reading into a whole calibration window.
                float sampleDt = Mathf.Min(dt, .05f);
                _sum += angle * sampleDt; _sampleTime += sampleDt;
                if (_sampleTime >= CalibrationSeconds)
                {
                    _neutral = _sum / _sampleTime;
                    IsCalibrating = false; IsCalibrated = true;
                }
                ResetMotion(); return;
            }
            if (!IsCalibrated) { ResetMotion(); return; }
            float relative = angle - _neutral;
            // Returning to neutral stops immediately rather than coasting behind a smoothing tail.
            if (Mathf.Abs(relative) <= DeadZoneDegrees) { ResetMotion(); return; }
            _filtered = Mathf.Lerp(_filtered, relative, 1 - Mathf.Exp(-dt / FilterSeconds));
            float strength = Mathf.Clamp01((Mathf.Abs(_filtered) - DeadZoneDegrees) * sensitivity /
                (FullTiltDegrees - DeadZoneDegrees));
            Horizontal = Mathf.Sign(_filtered) * strength;
        }
    }
}
