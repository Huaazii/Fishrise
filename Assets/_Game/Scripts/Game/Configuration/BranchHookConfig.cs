using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Independent branch capacities and width-relative fan/follow presentation.</summary>
    [CreateAssetMenu(menuName = "Fishing/Abilities/Branch Hooks")]
    public sealed class BranchHookConfig : AbilityDefinition
    {
        [Range(1, 8)] public int hookCount = 2;
        [Min(1)] public int capacity = 3;
        [Range(.05f, .4f)] public float radiusScreenFraction = .18f;
        [Min(.01f)] public float deploySeconds = .25f;
        [Min(.01f)] public float followSmoothTime = .06f;
        public bool breakOnLarge;
        /// <summary>Copies only branch gameplay limits into Core.</summary>
        public BranchHookRules CreateRules(bool requiresTreasure = false) => new BranchHookRules(Snapshot(requiresTreasure), hookCount, capacity, breakOnLarge);
    }
}
