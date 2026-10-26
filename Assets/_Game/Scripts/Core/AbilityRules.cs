using System;

namespace Fishing.Core
{
    /// <summary>Immutable use, cooldown and state limits shared by both ability strategies.</summary>
    public sealed class AbilityRules
    {
        /// <summary>Total activation budget obtainable in one run.</summary>
        public int Uses { get; }
        /// <summary>Whether underwater pickups trigger the skill instead of granting initial manual uses.</summary>
        public bool RequiresTreasure { get; }
        /// <summary>Cooldown on the paused-aware game clock.</summary>
        public float CooldownSeconds { get; }
        /// <summary>Only this flow state can activate the slot.</summary>
        public RunState AllowedState { get; }
        /// <summary>Minimum interval between accepted button commands.</summary>
        public float DebounceSeconds { get; }
        /// <summary>Copies validated ability limits.</summary>
        public AbilityRules(int uses, float cooldownSeconds, RunState allowedState, float debounceSeconds = .2f,
            bool requiresTreasure = false)
        {
            if (uses < 0 || !Finite(cooldownSeconds) || cooldownSeconds < 0 || !Finite(debounceSeconds) || debounceSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(uses));
            Uses = uses; CooldownSeconds = cooldownSeconds; AllowedState = allowedState; DebounceSeconds = debounceSeconds;
            RequiresTreasure = requiresTreasure;
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Numeric sonar range, wave and scare parameters, without physics references.</summary>
    public sealed class SonarRules
    {
        /// <summary>Common slot limits.</summary>
        public AbilityRules Ability { get; }
        /// <summary>Distance in gameplay metres, evaluated at activation.</summary>
        public float Radius { get; }
        /// <summary>Visual expansion duration.</summary>
        public float WaveSeconds { get; }
        /// <summary>Head-contact exclusion duration.</summary>
        public float ScareSeconds { get; }
        /// <summary>Escape speed relative to the fish's sampled swim speed.</summary>
        public float SpeedMultiplier { get; }
        /// <summary>Game-clock interval between the three pulses from one activation.</summary>
        public float PulseIntervalSeconds { get; }
        /// <summary>Creates the default or configured sonar snapshot.</summary>
        public SonarRules(AbilityRules ability = null, float radius = 4, float waveSeconds = .4f,
            float scareSeconds = 2.5f, float speedMultiplier = 3, float pulseIntervalSeconds = 1)
        {
            if (!AbilityRules.Finite(radius) || radius <= 0 || !AbilityRules.Finite(waveSeconds) || waveSeconds <= 0 ||
                !AbilityRules.Finite(scareSeconds) || scareSeconds <= 0 || !AbilityRules.Finite(speedMultiplier) || speedMultiplier <= 0 ||
                !AbilityRules.Finite(pulseIntervalSeconds) || pulseIntervalSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            Ability = ability ?? new AbilityRules(2, 4, RunState.Diving);
            Radius = radius; WaveSeconds = waveSeconds; ScareSeconds = scareSeconds; SpeedMultiplier = speedMultiplier;
            PulseIntervalSeconds = pulseIntervalSeconds;
        }
    }

    /// <summary>Immutable branch count, independent capacity and optional higher-star contact risk.</summary>
    public sealed class BranchHookRules
    {
        /// <summary>Common slot limits.</summary>
        public AbilityRules Ability { get; }
        /// <summary>Positive hook IDs are one through this count.</summary>
        public int HookCount { get; }
        /// <summary>Capacity of each branch, excluding the main hook.</summary>
        public int Capacity { get; }
        /// <summary>Legacy option name: whether a higher-star fish contact retires that branch.</summary>
        public bool BreakOnLarge { get; }
        /// <summary>Creates independent branch capture limits.</summary>
        public BranchHookRules(AbilityRules ability = null, int hookCount = 2, int capacity = 3, bool breakOnLarge = false)
        {
            if (hookCount < 1 || hookCount > 8 || capacity < 1) throw new ArgumentOutOfRangeException(nameof(hookCount));
            Ability = ability ?? new AbilityRules(1, 0, RunState.Ascending);
            HookCount = hookCount; Capacity = capacity; BreakOnLarge = breakOnLarge;
        }
    }
}
