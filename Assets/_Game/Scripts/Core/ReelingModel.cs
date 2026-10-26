using System;

namespace Fishing.Core
{
    /// <summary>Explicit-clock control bar, smoothed ascent and one-at-a-time escape requests.</summary>
    public sealed class ReelingModel
    {
        /// <summary>Half the marker height in track units, shared with the precision-control display.</summary>
        public const float MarkerHalfHeight = .05f;
        private readonly ReelingRules _rules;
        private readonly Random _random;
        private bool _held, _pendingPress;
        private float _velocity, _difficulty, _target, _targetTime, _outside;
        /// <summary>One pending loss; the flow consumes it before notifying presentation.</summary>
        public bool EscapePending { get; private set; }
        /// <summary>Remaining gameplay seconds in which a miss cannot drain stability.</summary>
        public float GraceRemaining { get; private set; }
        /// <summary>Whether an ascending hook currently carries fish.</summary>
        public bool Active { get; private set; }
        /// <summary>Normalized vertical centre of the player's green bar.</summary>
        public float BarPosition { get; private set; }
        /// <summary>Normalized green bar height, interpolated from the current retained count.</summary>
        public float BarHeight { get; private set; }
        /// <summary>Normalized fish-marker centre.</summary>
        public float FishPosition { get; private set; }
        /// <summary>Whether the whole marker is inside the green bar.</summary>
        public bool InZone { get; private set; }
        /// <summary>Stability in [0,1]; reaching zero requests one escape.</summary>
        public float Stability { get; private set; }
        /// <summary>Current smoothed ascent speed in metres per second.</summary>
        public float Speed { get; private set; }
        /// <summary>Creates independent state and deterministic fish motion for one run.</summary>
        public ReelingModel(ReelingRules rules) { _rules = rules; _random = new Random(rules.Seed); }
        /// <summary>Latches short taps until a simulation step, while retaining the hold state.</summary>
        public void SetHeld(bool held) { if (held && !_held) _pendingPress = true; _held = held; }
        /// <summary>Clears device input across pause, state changes and restart.</summary>
        public void ClearInput() { _held = _pendingPress = false; }
        /// <summary>Grants bounded command/escape tolerance without restoring stability or stacking durations.</summary>
        public void GrantGrace(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (_rules.Enabled) GraceRemaining = Math.Max(GraceRemaining, seconds);
        }
        /// <summary>Consumes one loss and gives the remaining catch a recoverable stability reserve.</summary>
        public bool ConsumeEscape()
        {
            if (!EscapePending) return false;
            EscapePending = false; Stability = _rules.RecoveryStability; _outside = 0;
            GrantGrace(_rules.EscapeGraceSeconds); return true;
        }
        /// <summary>Advances only during ascent; empty hooks return automatically without a control challenge.</summary>
        public void Tick(float dt, int count, int capacity, float emptySpeed)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0 || count < 0 || capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(dt));
            if (dt == 0) return;
            if (!_rules.Enabled || count == 0)
            { Active = EscapePending = false; GraceRemaining = 0; Speed = emptySpeed; ClearInput(); return; }
            float difficulty = Clamp((count - 1f) / Math.Max(1, capacity - 1), 0, 1);
            if (!Active)
            {
                Active = true; _difficulty = difficulty; BarPosition = FishPosition = .35f;
                _velocity = _outside = _targetTime = 0; Stability = .6f; Speed = _rules.MinSpeed;
            }
            float remaining = dt;
            while (remaining > .000001f)
            {
                float step = Math.Min(.01f, remaining); remaining -= step;
                bool pressed = _held || _pendingPress;
                Advance(step, difficulty, pressed);
                // A tap spans one normal physics step even if press/release happened between steps.
                if (dt - remaining >= Math.Min(.02f, dt) - .000001f) _pendingPress = false;
            }
        }
        private void Advance(float dt, float difficulty, bool pressed)
        {
            _difficulty = Ease(_difficulty, difficulty, dt, _rules.DifficultyResponse);
            BarHeight = _rules.EasyHeight + (_rules.HardHeight - _rules.EasyHeight) * _difficulty;
            _velocity = Clamp((_velocity + (pressed ? 1 : -1) * _rules.Acceleration * dt) /
                (1 + _rules.Damping * dt), -_rules.MaxBarSpeed, _rules.MaxBarSpeed);
            BarPosition += _velocity * dt;
            float half = BarHeight * .5f;
            if (BarPosition < half) { BarPosition = half; _velocity = Math.Max(0, -_velocity * .12f); }
            if (BarPosition > 1 - half) { BarPosition = 1 - half; _velocity = Math.Min(0, _velocity); }
            _targetTime -= dt;
            if (_targetTime <= 0)
            { _target = .12f + (float)_random.NextDouble() * .76f; _targetTime = .8f + (float)_random.NextDouble() * .8f; }
            FishPosition += Clamp(_target - FishPosition, -_rules.FishSpeed * dt, _rules.FishSpeed * dt);
            InZone = Math.Abs(FishPosition - BarPosition) + MarkerHalfHeight <= half;
            bool grace = GraceRemaining > 0; GraceRemaining = Math.Max(0, GraceRemaining - dt);
            if (!EscapePending)
            {
                if (InZone) { _outside = 0; Stability = Math.Min(1, Stability + _rules.StabilityGain * dt); }
                else if (grace) _outside = 0;
                else { _outside += dt; if (_outside > .25f) Stability = Math.Max(0, Stability - _rules.StabilityLoss * dt); }
                if (Stability <= 0) EscapePending = true;
            }
            Speed = Ease(Speed, InZone ? _rules.MaxSpeed : _rules.MinSpeed, dt, _rules.SpeedResponse);
        }
        private static float Ease(float current, float target, float dt, float seconds) =>
            current + (target - current) * (1 - (float)Math.Exp(-dt / seconds));
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
