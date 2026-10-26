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
            if (Rules.SlowdownDistance <= 0)
                remaining = Math.Max(0, remaining - Rules.DiveSpeed * dt);
            else
            {
                // Integrate cruise and constant braking separately, including steps crossing into the brake zone.
                // v² = 2ad reaches zero in finite time instead of crawling at a minimum speed then snapping still.
                float cruise = Math.Min(Math.Max(0, remaining - Rules.SlowdownDistance), Rules.DiveSpeed * dt);
                remaining -= cruise;
                float brakingTime = Math.Max(0, dt - cruise / Rules.DiveSpeed);
                float acceleration = Rules.DiveSpeed * Rules.DiveSpeed / (2 * Rules.SlowdownDistance);
                float speed = (float)Math.Sqrt(2 * acceleration * remaining);
                float stopTime = speed / acceleration;
                if (brakingTime >= stopTime) remaining = 0;
                else remaining = Math.Max(0, remaining - speed * brakingTime + .5f * acceleration * brakingTime * brakingTime);
            }
            Flow.MoveDepth(Rules.MaxDepth - remaining);
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
        private bool _fromBottom;
        public AscendingState(FishingFlowController f) : base(f) { }
        public override void Enter()
        { base.Enter(); _fromBottom = Run.Depth >= Rules.MaxDepth; }
        public override void Tick(float dt)
        {
            // A full main hook ends the reeling phase: the original five-times sprint takes over,
            // so the player gets the same "victory lap" beat as the reference instead of steering a bar.
            float speed = Rules.AscentSpeed * (Run.IsFull ? Rules.FullMultiplier : 1);
            if (Rules.Reeling.Enabled && !Run.IsFull)
            {
                var branches = Flow.Abilities.Branches.Effect;
                Run.Reeling.Tick(dt, Run.TotalCaptureCount, Rules.MainCapacity + branches.HookCount * branches.Capacity, Rules.AscentSpeed);
                Flow.ResolveEscape();
                speed = Run.TotalCaptureCount == 0 ? Rules.AscentSpeed : Run.Reeling.Speed;
            }
            float movingTime = LaunchIntegral(Flow.StateElapsed) - LaunchIntegral(Flow.StateElapsed - dt);
            Flow.MoveDepth(Math.Max(0, Run.Depth - speed * movingTime));
            // Contacts along the final swept path are resolved before entering Surfacing.
            Flow.PendingSurface = Run.Depth <= 0;
        }
        private float LaunchIntegral(float time)
        {
            time = Math.Max(0, time);
            float duration = Rules.AscentStartSeconds;
            if (!_fromBottom || duration <= 0) return time;
            if (time >= duration) return time - duration * .5f;
            float t = time / duration;
            // Integral of SmoothStep: fixed steps of different lengths cover the same launch distance.
            return duration * t * t * t * (1 - .5f * t);
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
