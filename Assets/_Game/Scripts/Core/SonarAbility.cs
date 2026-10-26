using System;
using System.Collections.Generic;

namespace Fishing.Core
{
    /// <summary>One attributed pulse on the run clock; treasure zero denotes legacy manual activation.</summary>
    public readonly struct SonarPulse
    {
        /// <summary>Run that owns this pulse.</summary>
        public Guid RunId { get; }
        /// <summary>Source pickup identity, or zero for a manual activation.</summary>
        public int TreasureId { get; }
        /// <summary>Zero-based pulse within this activation.</summary>
        public int Index { get; }
        /// <summary>Scheduled time in the queue, actual time when emitted.</summary>
        public double GameTime { get; }
        /// <summary>Copies source identity and timing without scene references.</summary>
        public SonarPulse(Guid runId, int treasureId, int index, double gameTime)
        { RunId = runId; TreasureId = treasureId; Index = index; GameTime = gameTime; }
    }
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
        private readonly List<SonarPulse> _echoes = new List<SonarPulse>();
        private int _activatingTreasureId;
        private bool _awaitingTargets;
        /// <summary>One pulse requests one fresh spatial query at the current hook position.</summary>
        public event Action Pulsed;
        /// <summary>Attributed spatial query, emitted before the legacy pulse notification.</summary>
        public event Action<SonarPulse> PulseRequested;
        /// <summary>Remaining scheduled echoes, for adapters to avoid assigning their imminent targets twice.</summary>
        public IReadOnlyList<SonarPulse> PendingPulses => _echoes;
        /// <summary>Immutable sonar effect settings.</summary>
        public SonarRules Effect { get; }
        /// <summary>Creates a fresh sonar strategy.</summary>
        public SonarAbility(RunModel run, SonarRules rules, IEnumerable<FishRules> fish) : base(run, rules.Ability)
        { Effect = rules; foreach (var definition in fish) _fish.Add(definition.Id, definition); }
        /// <summary>Bottom-zone diving is deliberately excluded.</summary>
        public override bool CanActivate(RunModel run) => base.CanActivate(run) && run.Depth < run.Rules.BottomZoneDepth;
        internal void TriggerTreasure(int treasureId)
        {
            int previous = _activatingTreasureId; _activatingTreasureId = treasureId;
            try { base.TriggerTreasure(); }
            finally { _activatingTreasureId = previous; }
        }
        protected override void OnActivate()
        {
            int id = _activatingTreasureId;
            _echoes.Add(new SonarPulse(Run.RunId, id, 1, Run.GameTime + Effect.PulseIntervalSeconds));
            _echoes.Add(new SonarPulse(Run.RunId, id, 2, Run.GameTime + 2d * Effect.PulseIntervalSeconds));
            // Consecutive pickups interleave their echoes; process every deadline in time order.
            _echoes.Sort((a, b) =>
            {
                int time = a.GameTime.CompareTo(b.GameTime);
                return time != 0 ? time : a.TreasureId.CompareTo(b.TreasureId);
            });
            Pulse(id, 0);
        }
        private void Pulse(int treasureId, int index)
        {
            _awaitingTargets = true;
            PulseRequested?.Invoke(new SonarPulse(Run.RunId, treasureId, index, Run.GameTime));
            Pulsed?.Invoke();
        }
        /// <summary>Emits both pickup echoes on the run clock; leaving descent discards pending echoes.</summary>
        public override void Tick(float dt)
        {
            if (Run.IsPaused) return;
            if (Run.State != RunState.Diving || Run.Depth >= Run.Rules.BottomZoneDepth)
            { _echoes.Clear(); _awaitingTargets = false; }
            else
                while (_echoes.Count > 0 && Run.GameTime + .000001 >= _echoes[0].GameTime)
                { var echo = _echoes[0]; _echoes.RemoveAt(0); Pulse(echo.TreasureId, echo.Index); }
            base.Tick(dt);
        }
        /// <summary>Accepts one query per pulse, rejecting large, distant, duplicate and captured fish.</summary>
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
        public override void Cancel() { _echoes.Clear(); _awaitingTargets = false; _scaredUntil.Clear(); base.Cancel(); }
    }
}
