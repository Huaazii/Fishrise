using System;
using System.Linq;
using Fishing.Game;
using UnityEngine;

namespace Fishing.Tests
{
    /// <summary>Restores saved rollout flags after explicit classic/new-mode comparisons; never saves the asset.</summary>
    internal sealed class ReelingComparisonScope : IDisposable
    {
        private readonly FishingConfig _config;
        private readonly bool _previous, _previousTreasures;
        /// <summary>Selects the mode before the fixture creates its test run.</summary>
        public ReelingComparisonScope(bool enabled, bool skillTreasures = false)
        {
            _config = Resources.FindObjectsOfTypeAll<FishingConfig>().Single();
            _previous = _config.enableReeling; _config.enableReeling = enabled;
            _previousTreasures = _config.enableSkillTreasures; _config.enableSkillTreasures = skillTreasures;
        }
        /// <summary>Restores the user's exact rollout flag even when a test failed.</summary>
        public void Dispose() { _config.enableReeling = _previous; _config.enableSkillTreasures = _previousTreasures; }
    }
}
