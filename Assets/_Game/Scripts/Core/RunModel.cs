using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fishing.Core
{
    /// <summary>One run's authoritative data. Records are the only capture count/reward source.</summary>
    public sealed class RunModel
    {
        private readonly List<CaptureRecord> _captures = new List<CaptureRecord>();
        private readonly HashSet<int> _capturedIds = new HashSet<int>();
        private bool _fullPublished;
        /// <summary>Unique run identity.</summary>
        public Guid RunId { get; } = Guid.NewGuid();
        /// <summary>Current flow stage, changed by the flow controller only.</summary>
        public RunState State { get; internal set; }
        /// <summary>Whether gameplay time and input are suspended.</summary>
        public bool IsPaused { get; internal set; }
        /// <summary>Current depth in metres.</summary>
        public float Depth { get; private set; }
        /// <summary>Largest actually reached depth, used by settlement.</summary>
        public float ReachedDepth { get; private set; }
        /// <summary>Remaining protection charges, retained for display after the bottom boundary.</summary>
        public int Protection { get; private set; }
        /// <summary>Game-clock deadline for protection invulnerability.</summary>
        public double InvulnerableUntil { get; internal set; }
        /// <summary>Elapsed gameplay seconds; does not advance while paused.</summary>
        public double GameTime { get; internal set; }
        /// <summary>Read-only per-run rules.</summary>
        public FishingRules Rules { get; }
        /// <summary>Read-only capture history, ordered by commit.</summary>
        public ReadOnlyCollection<CaptureRecord> Captures { get; }
        /// <summary>Main-hook count, derived from records.</summary>
        public int MainCaptureCount => Count(CaptureSource.Main);
        /// <summary>Branch-hook count, derived from records.</summary>
        public int BranchCaptureCount => Count(CaptureSource.Branch);
        /// <summary>All captured fish.</summary>
        public int TotalCaptureCount => _captures.Count;
        /// <summary>Main capacity only; branch captures cannot make this true.</summary>
        public bool IsFull => MainCaptureCount >= Rules.MainCapacity;
        /// <summary>All-source gold.</summary>
        public int Gold { get { int sum = 0; foreach (var r in _captures) sum += r.Gold; return sum; } }
        /// <summary>All-source shells.</summary>
        public int Shells { get { int sum = 0; foreach (var r in _captures) sum += r.Shells; return sum; } }
        /// <summary>New depth value.</summary>
        public event Action<float> OnDepthChanged;
        /// <summary>New protection value.</summary>
        public event Action<int> OnProtectionChanged;
        /// <summary>Published once per run after the final main capture event.</summary>
        public event Action OnBecameFull;
        /// <summary>Creates fresh state and collections.</summary>
        public RunModel(FishingRules rules)
        {
            Rules = rules; Protection = rules.ProtectionCharges;
            Captures = _captures.AsReadOnly(); State = RunState.Menu;
        }
        /// <summary>Checks global cross-hook capture identity.</summary>
        public bool HasCaptured(int id) => _capturedIds.Contains(id);
        private int Count(CaptureSource source)
        { int count = 0; foreach (var r in _captures) if (r.Source == source) count++; return count; }
        /// <summary>Counts records assigned to one concrete hook, without a second capacity counter.</summary>
        public int CountForHook(int hookId)
        { int count = 0; foreach (var r in _captures) if (r.HookId == hookId) count++; return count; }
        internal void SetDepth(float depth)
        {
            if (Depth == depth) return;
            Depth = depth; ReachedDepth = Math.Max(ReachedDepth, depth); OnDepthChanged?.Invoke(depth);
        }
        internal void UseProtection()
        { Protection--; InvulnerableUntil = GameTime + Rules.InvulnerabilitySeconds; OnProtectionChanged?.Invoke(Protection); }
        internal bool AddCapture(CaptureRecord record)
        {
            if (_capturedIds.Contains(record.FishInstanceId) || (record.Source == CaptureSource.Main && IsFull)) return false;
            _capturedIds.Add(record.FishInstanceId); _captures.Add(record); return true;
        }
        internal void PublishFullIfNeeded()
        { if (!_fullPublished && IsFull) { _fullPublished = true; OnBecameFull?.Invoke(); } }
    }
}
