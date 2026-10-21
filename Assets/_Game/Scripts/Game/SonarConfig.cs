using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Sonar effect parameters and presentation metadata.</summary>
    [CreateAssetMenu(menuName = "Fishing/Abilities/Sonar")]
    public sealed class SonarConfig : AbilityDefinition
    {
        [Min(.01f)] public float radiusMetres = 4;
        [Min(.01f)] public float waveSeconds = .4f;
        [Min(.01f)] public float scareSeconds = 2.5f;
        [Min(.01f)] public float speedMultiplier = 3;
        /// <summary>Copies immutable gameplay and wave settings for one run.</summary>
        public SonarRules CreateRules() => new SonarRules(Snapshot(), radiusMetres, waveSeconds, scareSeconds, speedMultiplier);
    }
}
