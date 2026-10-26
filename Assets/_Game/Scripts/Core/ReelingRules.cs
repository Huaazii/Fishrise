using System;

namespace Fishing.Core
{
    /// <summary>Immutable interactive ascent and escape settings; no Unity or input-device references.</summary>
    public sealed class ReelingRules
    {
        public readonly bool Enabled;
        public readonly float MinSpeed, MaxSpeed, SpeedResponse, EasyHeight, HardHeight;
        public readonly float Acceleration, Damping, MaxBarSpeed, FishSpeed, DifficultyResponse;
        public readonly int Seed;
        public readonly float StabilityGain, StabilityLoss, RecoveryStability, EscapeGraceSeconds, AbilityGraceSeconds;
        /// <summary>Copies validated control, movement and difficulty settings.</summary>
        public ReelingRules(bool enabled = false, float minSpeed = 2, float maxSpeed = 6,
            float speedResponse = .4f, float easyHeight = .42f, float hardHeight = .28f,
            float acceleration = 3.2f, float damping = 3, float maxBarSpeed = .85f,
            float fishSpeed = .24f, float difficultyResponse = .6f, int seed = 2718,
            float stabilityGain = .18f, float stabilityLoss = .1f, float recoveryStability = .45f,
            float escapeGraceSeconds = .8f, float abilityGraceSeconds = .8f)
        {
            foreach (float value in new[] { minSpeed, maxSpeed, speedResponse, easyHeight, hardHeight,
                acceleration, damping, maxBarSpeed, fishSpeed, difficultyResponse, stabilityGain, stabilityLoss,
                recoveryStability, escapeGraceSeconds, abilityGraceSeconds })
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(minSpeed));
            if (maxSpeed < minSpeed || hardHeight > easyHeight || easyHeight >= 1 || hardHeight < .1f || recoveryStability > 1)
                throw new ArgumentOutOfRangeException(nameof(easyHeight));
            Enabled = enabled; MinSpeed = minSpeed; MaxSpeed = maxSpeed; SpeedResponse = speedResponse;
            EasyHeight = easyHeight; HardHeight = hardHeight; Acceleration = acceleration; Damping = damping;
            MaxBarSpeed = maxBarSpeed; FishSpeed = fishSpeed; DifficultyResponse = difficultyResponse; Seed = seed;
            StabilityGain = stabilityGain; StabilityLoss = stabilityLoss; RecoveryStability = recoveryStability;
            EscapeGraceSeconds = escapeGraceSeconds; AbilityGraceSeconds = abilityGraceSeconds;
        }
    }
}
