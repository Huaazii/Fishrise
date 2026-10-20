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
        [Min(0)] public float surfacingSeconds = .6f;
        [Min(1)] public int mainCapacity = 10;
        [Min(0)] public int protectionCharges = 2;
        [Min(0)] public float invulnerabilitySeconds = 1;
        [Min(.01f)] public float unitsPerMetre = 1;
        [Min(1)] public float halfWidth = 4.5f;
        [Min(.1f)] public float horizontalSpeed = 13.5f;
        public bool followTouchPoint;
        public int randomSeed = 2718;
        [Range(10, 60)] public int fishCount = 54;
        /// <summary>Copies numbers so subsequent asset edits cannot mutate an active run.</summary>
        public FishingRules Snapshot() => new FishingRules(maxDepth, Mathf.Max(0, maxDepth - bottomZoneOffset),
            diveSpeed, slowdownDistance, ascentSpeed, fullMultiplier, bottomHoldSeconds,
            countdownSeconds, surfacingSeconds, mainCapacity, protectionCharges, invulnerabilitySeconds);
    }
}
