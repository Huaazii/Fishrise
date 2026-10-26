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
        [Min(0)] public float bottomHoldSeconds = .35f;
        [Min(0)] public float ascentStartSeconds = .35f;
        [Min(0)] public float countdownSeconds = 3;
        [Range(.8f, 2.5f)] public float surfacingSeconds = 2.3f;
        [Min(1)] public int mainCapacity = 10;
        [Min(0)] public int protectionCharges = 2;
        [Min(0)] public float invulnerabilitySeconds = 1;
        [Min(.01f)] public float unitsPerMetre = 1;
        [Min(1)] public float halfWidth = 4.5f;
        [Min(.1f)] public float horizontalSpeed = 13.5f;
        public bool followTouchPoint;
        [Header("Underwater skill treasure")]
        [Tooltip("技能次数通过水中宝物获得；关闭用于旧玩法对照。")]
        public bool enableSkillTreasures = true;
        [Min(.05f)] public float treasurePickupRadius = .42f;
        [Min(.2f)] public float treasureVisualSize = .85f;
        [Tooltip("声呐总深度限制（米）；按全程等分投放，默认两枚各占前后半程，并为海底前完整三跳留出距离。")]
        public Vector2 sonarTreasureDepths = new Vector2(14, 100);
        [Tooltip("分散鱼钩宝物的生成深度区间（米），数量沿用 BranchHookConfig.uses。")]
        public Vector2 branchTreasureDepths = new Vector2(58, 84);
        [Header("Interactive retrieval")]
        public bool enableReeling = true;
        [Min(.1f)] public float reelMinSpeed = 3.5f;
        [Min(.1f)] public float reelMaxSpeed = 7;
        [Min(.05f)] public float reelSpeedResponse = .4f;
        [Range(.1f, .8f)] public float reelEasyHeight = .42f;
        [Range(.1f, .8f)] public float reelHardHeight = .28f;
        [Min(.1f)] public float reelAcceleration = 3.2f;
        [Min(.1f)] public float reelDamping = 3;
        [Min(.1f)] public float reelMaxBarSpeed = .85f;
        [Min(.01f)] public float reelFishSpeed = .24f;
        [Min(.05f)] public float reelDifficultyResponse = .6f;
        [Min(.01f)] public float reelStabilityGain = .18f;
        [Min(.01f)] public float reelStabilityLoss = .1f;
        [Range(.1f, 1)] public float reelRecoveryStability = .45f;
        [Min(.1f)] public float reelEscapeGraceSeconds = .8f;
        [Min(.1f)] public float reelAbilityGraceSeconds = .8f;
        [Header("Camera and accelerated retrieval feedback")]
        [Min(.01f)] public float cameraSmoothTime = .18f;
        [Min(.01f)] public float cameraTurnSmoothTime = .4f;
        [Min(0)] public float fastAscentShakeAmplitude = .035f;
        [Min(.1f)] public float fastAscentShakeFrequency = 17;
        [Range(1, 100)] public int fastAscentHapticMilliseconds = 35;
        [Range(1, 255)] public int fastAscentHapticAmplitude = 80;
        public int randomSeed = 2718;
        [Tooltip("勾选后使用 randomSeed 复现鱼群；正常游玩每局生成新种子，实际值保留在 FishSpawner.RunSeed。")]
        public bool useFixedPopulationSeed;
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
            countdownSeconds, surfacingSeconds, mainCapacity, protectionCharges, invulnerabilitySeconds,
            new ReelingRules(enableReeling, reelMinSpeed, reelMaxSpeed, reelSpeedResponse, reelEasyHeight, reelHardHeight,
                reelAcceleration, reelDamping, reelMaxBarSpeed, reelFishSpeed, reelDifficultyResponse, randomSeed,
                reelStabilityGain, reelStabilityLoss, reelRecoveryStability, reelEscapeGraceSeconds, reelAbilityGraceSeconds), ascentStartSeconds);
    }

    /// <summary>One depth interval; legacy row count now controls target school spacing without imposing grid rows.</summary>
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
