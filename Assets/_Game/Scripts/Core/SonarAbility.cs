using System.Collections.Generic;

namespace Fishing.Core
{
    /// <summary>Pure spatial-query result supplied by the Game adapter.</summary>
    public readonly struct SonarTarget
    {
        /// <summary>Current spawn identity.</summary>
        public int FishId { get; }
        /// <summary>Species key used against the immutable run snapshot.</summary>
        public string SpeciesId { get; }
        /// <summary>Head-centre distance squared in gameplay metres.</summary>
        public float DistanceSquared { get; }
        /// <summary>Horizontal direction away from the activation centre.</summary>
        public float Direction { get; }
        /// <summary>Copies numeric query information only.</summary>
        public SonarTarget(int fishId, string speciesId, float distanceSquared, float direction)
        { FishId = fishId; SpeciesId = speciesId; DistanceSquared = distanceSquared; Direction = direction; }
    }
    /// <summary>Accepted scare effect, containing no scene object.</summary>
    public readonly struct ScareCommand
    {
        /// <summary>Target spawn identity.</summary>
        public int FishId { get; }
        /// <summary>Unit horizontal escape direction.</summary>
        public float Direction { get; }
        /// <summary>Seconds on the paused-aware clock.</summary>
        public float Duration { get; }
        /// <summary>Multiplier on the sampled fish speed.</summary>
        public float SpeedMultiplier { get; }
        /// <summary>Creates an accepted scare command.</summary>
        public ScareCommand(int fishId, float direction, float duration, float speedMultiplier)
        { FishId = fishId; Direction = direction; Duration = duration; SpeedMultiplier = speedMultiplier; }
    }
    /// <summary>Restricts activation and arbitrates small-fish scare queries against run snapshots.</summary>
    public sealed class SonarAbility : AbilitySlot
    {
        private readonly Dictionary<string, FishRules> _fish = new Dictionary<string, FishRules>();
        private readonly Dictionary<int, double> _scaredUntil = new Dictionary<int, double>();
        private bool _awaitingTargets;
        /// <summary>Immutable sonar effect settings.</summary>
        public SonarRules Effect { get; }
        /// <summary>Creates a fresh sonar strategy.</summary>
        public SonarAbility(RunModel run, SonarRules rules, IEnumerable<FishRules> fish) : base(run, rules.Ability)
        { Effect = rules; foreach (var definition in fish) _fish.Add(definition.Id, definition); }
        /// <summary>Bottom-zone diving is deliberately excluded.</summary>
        public override bool CanActivate(RunModel run) => base.CanActivate(run) && run.Depth < run.Rules.BottomZoneDepth;
        protected override void OnActivate() { _awaitingTargets = true; }
        /// <summary>Accepts the activation's one query, rejecting large, distant, duplicate and captured fish.</summary>
        public IReadOnlyList<ScareCommand> ResolveTargets(IEnumerable<SonarTarget> targets)
        {
            var commands = new List<ScareCommand>();
            if (!_awaitingTargets || Run.IsPaused) return commands;
            _awaitingTargets = false; var seen = new HashSet<int>();
            foreach (var target in targets)
            {
                if (target.FishId <= 0 || !seen.Add(target.FishId) || Run.HasCaptured(target.FishId) ||
                    !_fish.TryGetValue(target.SpeciesId, out var fish) || !fish.IsSmall || !fish.IsCatchable ||
                    !AbilityRules.Finite(target.DistanceSquared) || target.DistanceSquared < 0 ||
                    target.DistanceSquared > Effect.Radius * Effect.Radius || !AbilityRules.Finite(target.Direction)) continue;
                float direction = target.Direction == 0 ? (target.FishId % 2 == 0 ? 1 : -1) : (target.Direction > 0 ? 1 : -1);
                _scaredUntil[target.FishId] = Run.GameTime + Effect.ScareSeconds;
                commands.Add(new ScareCommand(target.FishId, direction, Effect.ScareSeconds, Effect.SpeedMultiplier));
            }
            return commands;
        }
        /// <summary>Core independently excludes scared IDs even if an adapter submits an old contact.</summary>
        public bool IsScared(int fishId) => _scaredUntil.TryGetValue(fishId, out var until) && Run.GameTime + .000001 < until;
        /// <summary>Cancels a pending query and clears this disposed run's scare identities.</summary>
        public override void Cancel() { _awaitingTargets = false; _scaredUntil.Clear(); base.Cancel(); }
    }
}
