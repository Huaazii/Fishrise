using System;
using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Unity-editable global configuration, converted to pure rules per run.</summary>
    [CreateAssetMenu(menuName = "Fishing/Global Configuration")]
    public sealed class FishingConfig : ScriptableObject
    {
        [Min(1)] public float maxDepth = 100;
        [Min(0)] public float bottomZoneOffset = 4;
        [Min(.1f)] public float diveSpeed = 6;
        [Min(0)] public float slowdownDistance = 5;
        [Min(.1f)] public float ascentSpeed = 6.5f;
        [Min(1)] public float fullMultiplier = 5;
        [Min(0)] public float bottomHoldSeconds = 1.2f;
        [Min(0)] public float countdownSeconds = 3;
        [Range(.8f, 2.5f)] public float surfacingSeconds = 2.3f;
        [Min(1)] public int mainCapacity = 10;
        [Min(0)] public int protectionCharges = 2;
        [Min(0)] public float invulnerabilitySeconds = 1;
        [Min(.01f)] public float unitsPerMetre = 1;
        [Min(1)] public float halfWidth = 4.5f;
        [Min(.1f)] public float horizontalSpeed = 13.5f;
        public bool followTouchPoint;
        public int randomSeed = 2718;
        [Min(2.5f)] public float passageWidthMultiplier = 2.5f;
        public FishDepthBand[] depthBands = Array.Empty<FishDepthBand>();
        /// <summary>Total population comes from the depth table, without a second count to maintain.</summary>
        public int FishCount
        {
            get
            {
                int count = 0;
                foreach (var band in depthBands) foreach (var entry in band.population) count += entry.count;
                return count;
            }
        }
        /// <summary>Copies numbers so subsequent asset edits cannot mutate an active run.</summary>
        public FishingRules Snapshot() => new FishingRules(maxDepth, Mathf.Max(0, maxDepth - bottomZoneOffset),
            diveSpeed, slowdownDistance, ascentSpeed, fullMultiplier, bottomHoldSeconds,
            countdownSeconds, surfacingSeconds, mainCapacity, protectionCharges, invulnerabilitySeconds);
    }

    /// <summary>One configured depth interval; row count controls vertical school density.</summary>
    [Serializable]
    public sealed class FishDepthBand
    {
        [Min(0)] public float minDepth;
        [Min(0)] public float maxDepth = 25;
        [Min(1)] public int rows = 5;
        public FishPopulationEntry[] population = Array.Empty<FishPopulationEntry>();
    }

    /// <summary>Exact species count and maximum school size within one depth interval.</summary>
    [Serializable]
    public sealed class FishPopulationEntry
    {
        public FishDefinition fish;
        [Min(1)] public int count = 1;
        [Min(1)] public int schoolSize = 1;
    }
}
