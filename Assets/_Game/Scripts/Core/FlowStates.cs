using System;

namespace Fishing.Core
{
    internal interface IFlowState
    {
        void Enter();
        void Tick(float dt);
        void Exit();
    }
    internal abstract class FlowState : IFlowState
    {
        protected readonly FishingFlowController Flow;
        protected RunModel Run => Flow.Run;
        protected FishingRules Rules => Run.Rules;
        protected FlowState(FishingFlowController flow) { Flow = flow; }
        public virtual void Enter() { Flow.StateElapsed = 0; }
        public abstract void Tick(float dt);
        public virtual void Exit() { }
    }
    internal sealed class MenuState : FlowState
    { public MenuState(FishingFlowController f) : base(f) { } public override void Tick(float dt) { } }
    internal sealed class CountdownState : FlowState
    {
        public CountdownState(FishingFlowController f) : base(f) { }
        public override void Tick(float dt)
        { if (Flow.StateElapsed >= Rules.CountdownSeconds) Flow.ChangeState(RunState.Diving); }
    }
    internal sealed class DivingState : FlowState
    {
        public DivingState(FishingFlowController f) : base(f) { }
        public override void Tick(float dt)
        {
            float remaining = Rules.MaxDepth - Run.Depth;
            // A non-zero minimum prevents asymptotic descent from never reaching the bottom.
            float ease = Rules.SlowdownDistance > 0 ? Math.Max(.2f, Math.Min(1, remaining / Rules.SlowdownDistance)) : 1;
            Flow.MoveDepth(Math.Min(Rules.MaxDepth, Run.Depth + Rules.DiveSpeed * ease * dt));
            Flow.PendingBottom = Run.Depth >= Rules.MaxDepth;
        }
    }
    internal sealed class BottomHoldState : FlowState
    {
        public BottomHoldState(FishingFlowController f) : base(f) { }
        public override void Tick(float dt)
        { if (Flow.StateElapsed >= Rules.BottomHoldSeconds) Flow.ChangeState(RunState.Ascending); }
    }
    internal sealed class AscendingState : FlowState
    {
        public AscendingState(FishingFlowController f) : base(f) { }
        public override void Tick(float dt)
        {
            float speed = Rules.AscentSpeed * (Run.IsFull ? Rules.FullMultiplier : 1);
            Flow.MoveDepth(Math.Max(0, Run.Depth - speed * dt));
            // Contacts along the final swept path are resolved before entering Surfacing.
            Flow.PendingSurface = Run.Depth <= 0;
        }
    }
    internal sealed class SurfacingState : FlowState
    {
        public SurfacingState(FishingFlowController f) : base(f) { }
        public override void Tick(float dt)
        { if (Flow.StateElapsed >= Rules.SurfacingSeconds) Flow.ChangeState(RunState.Result); }
    }
    internal sealed class ResultState : FlowState
    { public ResultState(FishingFlowController f) : base(f) { } public override void Tick(float dt) { } }
}
