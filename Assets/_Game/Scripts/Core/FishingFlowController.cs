using System;
using System.Collections.Generic;

namespace Fishing.Core
{
    /// <summary>Pure state-machine owner and sole contact/capture rule coordinator.</summary>
    public sealed class FishingFlowController
    {
        private readonly Dictionary<RunState, IFlowState> _states;
        private readonly Dictionary<string, FishRules> _fish = new Dictionary<string, FishRules>();
        private readonly HashSet<ContactKey> _contacts = new HashSet<ContactKey>();
        private readonly Dictionary<ContactKey, ContactCandidate> _unique = new Dictionary<ContactKey, ContactCandidate>();
        private readonly List<ContactCandidate> _ordered = new List<ContactCandidate>();
        private long _resolvedStep;
        private int _phaseVersion;
        /// <summary>Authoritative run.</summary>
        public RunModel Run { get; }
        /// <summary>Current fixed-step sequence.</summary>
        public long StepSequence { get; private set; }
        /// <summary>Time within this state, for countdown presentation.</summary>
        public float StateElapsed { get; internal set; }
        internal bool PendingBottom { get; set; }
        internal bool PendingSurface { get; set; }
        /// <summary>State after transition.</summary>
        public event Action<RunState> OnStateChanged;
        /// <summary>Committed record; model has already changed.</summary>
        public event Action<CaptureRecord> OnFishCaptured;
        /// <summary>Result is ready, once on entering Result.</summary>
        public event Action<RunModel> OnRunFinished;
        /// <summary>Creates a controller with copied species lookup and independent state instances.</summary>
        public FishingFlowController(FishingRules rules, IEnumerable<FishRules> fish)
        {
            Run = new RunModel(rules);
            foreach (var f in fish) _fish.Add(f.Id, f);
            _states = new Dictionary<RunState, IFlowState>
            {
                { RunState.Menu, new MenuState(this) }, { RunState.Countdown, new CountdownState(this) },
                { RunState.Diving, new DivingState(this) }, { RunState.BottomHold, new BottomHoldState(this) },
                { RunState.Ascending, new AscendingState(this) }, { RunState.Surfacing, new SurfacingState(this) },
                { RunState.Result, new ResultState(this) }
            };
        }
        /// <summary>Starts this fresh controller; restarting requires a new controller/run.</summary>
        public void Start()
        { if (Run.State == RunState.Menu) ChangeState(RunState.Countdown); }
        /// <summary>Pause changes neither flow state nor remaining timers.</summary>
        public void SetPaused(bool paused)
        {
            if (Run.State != RunState.Diving && Run.State != RunState.BottomHold && Run.State != RunState.Ascending) return;
            Run.IsPaused = paused;
        }
        /// <summary>Moves time/state/depth before the adapter moves bodies and simulates physics.</summary>
        public long BeginStep(float dt)
        {
            if (Run.IsPaused) return StepSequence;
            if (dt < 0 || float.IsNaN(dt) || float.IsInfinity(dt)) throw new ArgumentOutOfRangeException(nameof(dt));
            StepSequence++; Run.GameTime += dt; StateElapsed += dt;
            _states[Run.State].Tick(dt); return StepSequence;
        }
        /// <summary>Resolves a complete batch once, then applies deferred boundary transitions.</summary>
        public void ResolveContacts(ContactBatch batch)
        {
            if (Run.IsPaused || batch.RunId != Run.RunId || batch.Step != StepSequence || batch.Step <= _resolvedStep) return;
            _resolvedStep = batch.Step;
            int phase = _phaseVersion;
            _unique.Clear(); _ordered.Clear();
            foreach (var candidate in batch.Candidates)
            {
                // Branch allocation will be inserted before this main-only stage in milestone 03.
                if (candidate.HookId != 0 || Run.HasCaptured(candidate.FishId) ||
                    !_fish.TryGetValue(candidate.SpeciesId, out var fish) || !fish.IsCatchable ||
                    float.IsNaN(candidate.DistanceSquared) || candidate.DistanceSquared < 0) continue;
                var key = new ContactKey(candidate.HookId, candidate.FishId);
                if (!_unique.TryGetValue(key, out var previous) || candidate.DistanceSquared < previous.DistanceSquared)
                    _unique[key] = candidate;
            }
            _ordered.AddRange(_unique.Values); _ordered.Sort(Compare);
            bool guardedDive = Run.State == RunState.Diving && Run.Depth < Run.Rules.BottomZoneDepth;
            if (guardedDive)
            {
                foreach (var candidate in _ordered)
                {
                    if (_contacts.Contains(new ContactKey(candidate.HookId, candidate.FishId))) continue;
                    if (Run.GameTime < Run.InvulnerableUntil) break;
                    if (Run.Protection > 0) Run.UseProtection();
                    else { Capture(candidate); ChangeState(RunState.Ascending); }
                    // All new contacts in this physical step belong to the same guarded-dive decision.
                    break;
                }
            }
            else if (Run.State == RunState.Diving || Run.State == RunState.BottomHold || Run.State == RunState.Ascending)
            {
                foreach (var candidate in _ordered)
                { if (Run.IsFull) break; Capture(candidate); }
            }
            _contacts.Clear();
            // Phase changes deliberately re-admit still-overlapping fish on the following step.
            if (phase == _phaseVersion)
                foreach (var contact in batch.EndContacts) _contacts.Add(contact);
            if (PendingBottom && Run.State == RunState.Diving) ChangeState(RunState.BottomHold);
            if (PendingSurface && Run.State == RunState.Ascending) ChangeState(RunState.Surfacing);
            PendingBottom = false; PendingSurface = false;
        }
        private void Capture(ContactCandidate candidate)
        {
            var record = new CaptureRecord(_fish[candidate.SpeciesId], candidate.FishId,
                CaptureSource.Main, candidate.HookId, Run.Depth, Run.GameTime);
            if (!Run.AddCapture(record)) return;
            OnFishCaptured?.Invoke(record);
            Run.PublishFullIfNeeded();
        }
        private static int Compare(ContactCandidate a, ContactCandidate b)
        {
            int distance = a.DistanceSquared.CompareTo(b.DistanceSquared);
            if (distance != 0) return distance;
            int fish = a.FishId.CompareTo(b.FishId);
            return fish != 0 ? fish : a.HookId.CompareTo(b.HookId);
        }
        internal void MoveDepth(float depth)
        {
            bool enteringBottom = Run.Depth < Run.Rules.BottomZoneDepth && depth >= Run.Rules.BottomZoneDepth;
            Run.SetDepth(depth);
            if (enteringBottom) ResetContactPhase();
        }
        private void ResetContactPhase()
        { Run.InvulnerableUntil = 0; _contacts.Clear(); _phaseVersion++; }
        internal void ChangeState(RunState state)
        {
            _states[Run.State].Exit(); Run.State = state; _states[state].Enter();
            if (state == RunState.Ascending) ResetContactPhase();
            OnStateChanged?.Invoke(state);
            if (state == RunState.Result) OnRunFinished?.Invoke(Run);
        }
    }
}
