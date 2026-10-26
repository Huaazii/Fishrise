namespace Fishing.Core
{
    /// <summary>Deployment and retirement only; all used capacities come from RunModel records.</summary>
    public sealed class BranchHookModel
    {
        private readonly bool[] _retired;
        /// <summary>Whether the branches have ever deployed in this run.</summary>
        public bool Deployed { get; internal set; }
        /// <summary>Whether deployment still accepts contacts.</summary>
        public bool Active { get; internal set; }
        /// <summary>Creates one state cell per configured hook.</summary>
        public BranchHookModel(int hookCount) { _retired = new bool[hookCount]; }
        /// <summary>Checks a known, non-retired branch ID.</summary>
        public bool IsActive(int hookId) => Active && hookId > 0 && hookId <= _retired.Length && !_retired[hookId - 1];
        internal void Retire(int hookId) { if (hookId > 0 && hookId <= _retired.Length) _retired[hookId - 1] = true; }
    }
    /// <summary>Branch activation and eligibility without committing capture records.</summary>
    public sealed class BranchHookController : AbilitySlot
    {
        /// <summary>Independent branch limits.</summary>
        public BranchHookRules Effect { get; }
        /// <summary>Per-run deployment and optional retirement state.</summary>
        public BranchHookModel Model { get; }
        /// <summary>Creates fresh branch state.</summary>
        public BranchHookController(RunModel run, BranchHookRules rules) : base(run, rules.Ability)
        { Effect = rules; Model = new BranchHookModel(rules.HookCount); }
        protected override void OnActivate()
        { Model.Deployed = true; Model.Active = true; Run.Reeling.GrantGrace(Run.Rules.Reeling.AbilityGraceSeconds); }
        /// <summary>Reads branch usage from the sole capture history.</summary>
        public int RemainingCapacity(int hookId) => Effect.Capacity - Run.CountForHook(hookId);
        /// <summary>Only a deployed non-retired branch can receive a one-star catchable fish on ascent.</summary>
        public bool CanReceive(int hookId, FishRules fish) => Run.State == RunState.Ascending && !Run.IsPaused &&
            Model.IsActive(hookId) && fish.Rarity == 1 && fish.IsCatchable && RemainingCapacity(hookId) > 0;
        /// <summary>The legacy risk option retires a hook on higher-star contact without removing its caught fish.</summary>
        public void LargeContact(int hookId)
        { if (Effect.BreakOnLarge && Model.IsActive(hookId)) Model.Retire(hookId); }
        /// <summary>Stops future capture while retaining deployment history and records.</summary>
        public override void Cancel() { Model.Active = false; base.Cancel(); }
    }
}
