using System;
using System.Collections.Generic;

namespace Fishing.Core
{
    /// <summary>The two existing skills that an underwater treasure can trigger.</summary>
    public enum SkillTreasureKind { Sonar, Branches }

    /// <summary>Per-run command strategy; effects and cooldowns use the same paused-aware clock.</summary>
    public interface IAbility
    {
        /// <summary>Checks state, pause, uses and cooldown for this run.</summary>
        bool CanActivate(RunModel run);
        /// <summary>Accepts one activation command, consuming a use only on success.</summary>
        bool Activate();
        /// <summary>Updates the strategy after the run clock advances.</summary>
        void Tick(float dt);
        /// <summary>Cancels outstanding effects without refunding uses or capture records.</summary>
        void Cancel();
    }

    /// <summary>Shared slot bookkeeping for the two concrete strategies.</summary>
    public abstract class AbilitySlot : IAbility
    {
        protected readonly RunModel Run;
        private double _readyAt, _debounceUntil;
        private bool _cancelled, _lastCan;
        private int _lastTenth = -1;
        /// <summary>Immutable common slot settings.</summary>
        public AbilityRules Rules { get; }
        /// <summary>Unspent activations in this run.</summary>
        public int RemainingUses { get; private set; }
        /// <summary>Total pickups triggered this run, or the initial classic grant; spending never reopens the budget.</summary>
        public int AcquiredUses { get; private set; }
        internal bool CanAcquire => !_cancelled && Rules.RequiresTreasure && AcquiredUses < Rules.Uses;
        /// <summary>Seconds remaining, frozen by RunModel's paused clock.</summary>
        public float CooldownRemaining => (float)Math.Max(0, _readyAt - Run.GameTime);
        /// <summary>Accepted activation, after the slot and strategy model have changed.</summary>
        public event Action Activated;
        /// <summary>Changed count, eligibility or displayed cooldown tenth.</summary>
        public event Action Changed;
        /// <summary>Binds a slot to exactly one run.</summary>
        protected AbilitySlot(RunModel run, AbilityRules rules)
        { Run = run; Rules = rules; RemainingUses = AcquiredUses = rules.RequiresTreasure ? 0 : rules.Uses; }
        // A physical pickup is already a deliberate command. It spends its own finite budget,
        // without banking a charge or losing a pickup to the legacy button cooldown.
        internal void TriggerTreasure()
        { AcquiredUses++; OnActivate(); Activated?.Invoke(); Changed?.Invoke(); }
        /// <summary>Rejects other runs, inactive states, pause, exhaustion and repeated input.</summary>
        public virtual bool CanActivate(RunModel run) => ReferenceEquals(run, Run) && !_cancelled && !Run.IsPaused &&
            Run.State == Rules.AllowedState && RemainingUses > 0 && Run.GameTime + .000001 >= Math.Max(_readyAt, _debounceUntil);
        /// <summary>Commits the command before notifying adapters.</summary>
        public bool Activate()
        {
            if (!CanActivate(Run)) return false;
            RemainingUses--; _readyAt = Run.GameTime + Rules.CooldownSeconds;
            _debounceUntil = Run.GameTime + Rules.DebounceSeconds;
            OnActivate(); Activated?.Invoke(); Changed?.Invoke(); return true;
        }
        protected abstract void OnActivate();
        /// <summary>Publishes display changes without a separate cooldown clock.</summary>
        public virtual void Tick(float dt)
        {
            if (Run.IsPaused) return;
            int tenth = (int)Math.Ceiling(CooldownRemaining * 10); bool can = CanActivate(Run);
            if (tenth == _lastTenth && can == _lastCan) return;
            _lastTenth = tenth; _lastCan = can; Changed?.Invoke();
        }
        /// <summary>Disables further commands for the disposed or completed run.</summary>
        public virtual void Cancel() { _cancelled = true; Changed?.Invoke(); }
    }

    /// <summary>Owns the two ability strategies without depending on Unity resources or input.</summary>
    public sealed class AbilityController
    {
        private readonly RunModel _run;
        private readonly HashSet<int> _collectedTreasures = new HashSet<int>();
        /// <summary>Downward-phase active clearing ability.</summary>
        public SonarAbility Sonar { get; }
        /// <summary>Upward-phase independent small-fish hooks.</summary>
        public BranchHookController Branches { get; }
        /// <summary>HUD notification for either slot.</summary>
        public event Action Changed;
        /// <summary>Committed treasure count, separate from fish captures and rewards.</summary>
        public int CollectedTreasureCount => _collectedTreasures.Count;
        /// <summary>Creates fresh slots for one new run.</summary>
        public AbilityController(RunModel run, IEnumerable<FishRules> fish, SonarRules sonar, BranchHookRules branches)
        {
            _run = run; Sonar = new SonarAbility(run, sonar, fish); Branches = new BranchHookController(run, branches);
            Sonar.Changed += Notify; Branches.Changed += Notify;
        }
        private void Notify() { Changed?.Invoke(); }
        /// <summary>Checks phase and pickup budget before an atomic pickup-and-trigger operation.</summary>
        public bool CanCollectTreasure(SkillTreasureKind kind)
        {
            if (_run.IsPaused) return false;
            switch (kind)
            {
                case SkillTreasureKind.Sonar:
                    return Sonar.CanAcquire && _run.State == RunState.Diving && _run.Depth < _run.Rules.BottomZoneDepth;
                case SkillTreasureKind.Branches:
                    return Branches.CanAcquire && !Branches.Model.Deployed && _run.State == RunState.Ascending;
                default: return false;
            }
        }
        /// <summary>Commits a pickup and its activation once; rejected or stale commands have no effects.</summary>
        public bool TryCollectTreasure(Guid runId, int treasureId, SkillTreasureKind kind)
        {
            if (runId != _run.RunId || treasureId <= 0 || _collectedTreasures.Contains(treasureId) || !CanCollectTreasure(kind))
                return false;
            _collectedTreasures.Add(treasureId);
            if (kind == SkillTreasureKind.Sonar) Sonar.TriggerTreasure(treasureId); else Branches.TriggerTreasure();
            return true;
        }
        /// <summary>Advances effects only while gameplay is running.</summary>
        public void Tick(float dt) { if (!_run.IsPaused) { Sonar.Tick(dt); Branches.Tick(dt); } }
        /// <summary>Ends both strategies while preserving already committed records.</summary>
        public void Cancel() { Sonar.Cancel(); Branches.Cancel(); }
    }
}
