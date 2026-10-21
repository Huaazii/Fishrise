using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Editable slot metadata; only validated numbers and state are copied into Core.</summary>
    public abstract class AbilityDefinition : ScriptableObject
    {
        public string displayName;
        public Sprite icon;
        [Min(0)] public int uses = 1;
        [Min(0)] public float cooldown;
        public RunState allowedState;
        [Min(0)] public float debounce = .2f;
        /// <summary>Copies common rules without retaining this asset or its icon.</summary>
        public AbilityRules Snapshot() => new AbilityRules(uses, cooldown, allowedState, debounce);
    }
}
