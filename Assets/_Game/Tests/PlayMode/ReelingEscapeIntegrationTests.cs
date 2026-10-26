using System.Collections;
using System.Linq;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Fishing.Tests
{
    /// <summary>Enabled-mode physics, lost-fish presentation, source slots, HUD and real settlement.</summary>
    public sealed class ReelingEscapeIntegrationTests : AbilityTestScene
    {
        protected override bool InteractiveReeling => true;
        private void FastFailure()
        {
            var config = Resources.FindObjectsOfTypeAll<FishingConfig>().Single();
            float easy = config.reelEasyHeight, hard = config.reelHardHeight, loss = config.reelStabilityLoss;
            try
            { config.reelEasyHeight = config.reelHardHeight = .12f; config.reelStabilityLoss = 2; Game.BeginRun(); }
            finally { config.reelEasyHeight = easy; config.reelHardHeight = hard; config.reelStabilityLoss = loss; }
            Game.Spawner.Clear(); Until(RunState.Ascending);
        }
        private void LoseOne()
        {
            int before = Game.AppliedEscapeEvents; Game.SetReelingInput(true);
            for (int i = 0; i < 700 && Game.AppliedEscapeEvents == before; i++) Steps(1);
            Assert.That(Game.AppliedEscapeEvents, Is.EqualTo(before + 1));
        }
        private void AssertSlot(FishActor fish, Transform hook, Vector3 localSlot)
        { Assert.That(Vector3.Distance(fish.Mouth.position, hook.TransformPoint(localSlot)), Is.LessThan(.002f)); }
        /// <summary>The valuable fish leaves first, remaining mouths compact, and only retained records reach the result card.</summary>
        [UnityTest]
        public IEnumerator ValuableMainFishLeavesAndRemainingSlotsAndSettlementStayConsistent()
        {
            FastFailure();
            var eel = Resources.FindObjectsOfTypeAll<FishDefinition>().Single(f => f.speciesId == "eel");
            var yellow = Resources.FindObjectsOfTypeAll<FishDefinition>().Single(f => f.speciesId == "yellow");
            var first = AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f);
            var lost = AtHead(eel, Game.Hook.transform.position + Vector3.up * .13f);
            var third = AtHead(yellow, Game.Hook.transform.position + Vector3.up * .13f); Steps(2);
            Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(3));
            Assert.That(Label("Reeling title").text, Is.EqualTo("保住鳗鱼"));
            Assert.That(GameObject.Find("Reeling fish marker").GetComponent<Image>().sprite, Is.EqualTo(eel.sprite));
            LoseOne();
            Assert.That(lost.IsEscaping, Is.True); Assert.That(lost.IsCaptured, Is.False); Assert.That(lost.Head.enabled, Is.False);
            Assert.That(lost.transform.parent, Is.Not.EqualTo(Game.Hook.transform)); Assert.That(Game.Run.HasCaptured(lost.InstanceId), Is.True);
            Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(2));
            Assert.That(Game.Run.Gold, Is.EqualTo(Blue.gold + yellow.gold));
            AssertSlot(first, Game.Hook.transform, Game.Hook.HangPosition(0));
            AssertSlot(third, Game.Hook.transform, Game.Hook.HangPosition(1));
            Assert.That(Label("Reeling title").text, Is.EqualTo("保住黄蝴蝶鱼"));
            var fresh = AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(2);
            Assert.That(fresh.IsCaptured, Is.True); AssertSlot(fresh, Game.Hook.transform, Game.Hook.HangPosition(2));
            Assert.That(Game.Run.Captures.Any(r => r.FishInstanceId == lost.InstanceId), Is.False);
            Game.Run.Reeling.GrantGrace(100); Until(RunState.Result);
            Assert.That(Game.Ui.ResultRows.Sum(r => r.TotalCount), Is.EqualTo(3));
            Assert.That(Game.Ui.ResultRows.Any(r => r.SpeciesId == "eel"), Is.False);
            Assert.That(Game.Run.Gold, Is.EqualTo(Blue.gold * 2 + yellow.gold));
            Assert.That(Game.Run.Shells, Is.EqualTo(Blue.shells * 2 + yellow.shells)); yield return null;
        }
        /// <summary>Real branch capture loses the highest-value small fish and frees just that hook's next slot.</summary>
        [UnityTest]
        public IEnumerator BranchLossCompactsItsOwnSlotsAndCanRefillBeforeFull()
        {
            FastFailure();
            for (int i = 0; i < 9; i++) AtHead(Silver, Game.Hook.transform.position + Vector3.up * .13f);
            Steps(2); Game.DeployBranches(); Steps(30);
            var branch = Game.AbilityView.Branches[0];
            var lost = AtHead(Small, branch.transform.position + Vector3.up * .13f); Steps(1);
            var retained = AtHead(Silver, branch.transform.position + Vector3.up * .13f); Steps(1);
            Assert.That(Game.Run.CountForHook(1), Is.EqualTo(2)); Assert.That(Game.Run.IsFull, Is.False);
            LoseOne(); Assert.That(lost.IsEscaping, Is.True);
            Assert.That(Game.Run.CountForHook(1), Is.EqualTo(1)); Assert.That(Game.Run.IsFull, Is.False);
            Assert.That(Game.Abilities.Branches.RemainingCapacity(1), Is.EqualTo(2));
            AssertSlot(retained, branch.transform, branch.HangPosition(0));
            var fresh = AtHead(Silver, branch.transform.position + Vector3.up * .13f); Steps(1);
            Assert.That(fresh.IsCaptured, Is.True); AssertSlot(fresh, branch.transform, branch.HangPosition(1));
            Assert.That(Game.Run.Captures.Single(r => r.FishInstanceId == fresh.InstanceId).HookId, Is.EqualTo(1));
            Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(9)); yield return null;
        }
        /// <summary>Pause freezes an escaped view and recovery timer, and a new run resets all loss state and pooled art.</summary>
        [UnityTest]
        public IEnumerator PauseFreezesLossAnimationAndRestartClearsLossStateAndPoolTint()
        {
            FastFailure(); var fish = AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(2); LoseOne();
            Vector3 position = fish.transform.position; float grace = Game.Run.Reeling.GraceRemaining;
            Game.PauseRun(); Steps(100);
            Assert.That(fish.transform.position, Is.EqualTo(position)); Assert.That(Game.Run.Reeling.GraceRemaining, Is.EqualTo(grace));
            Assert.That(GameObject.Find("Reeling panel"), Is.Null); Game.ResumeRun(); Steps(40);
            Assert.That(fish.gameObject.activeSelf, Is.False); Assert.That(fish.Head.enabled, Is.False);
            Assert.That(Game.Spawner.TryGet(fish.InstanceId, out _), Is.False);
            Game.BeginRun(); Assert.That(Game.AppliedEscapeEvents, Is.Zero); Assert.That(Game.Run.Reeling.GraceRemaining, Is.Zero);
            Assert.That(Game.Run.TotalCaptureCount, Is.Zero); Assert.That(Game.Run.HasCaptured(fish.InstanceId), Is.False);
            Assert.That(Game.Spawner.Spawned.All(f => !f.IsEscaping && !f.IsCaptured), Is.True);
            Assert.That(Game.Spawner.Spawned.All(f => f.GetComponentInChildren<SpriteRenderer>().color.a == 1), Is.True); yield return null;
        }
        /// <summary>All lost fish leave a hidden challenge, a normal-speed empty return and zero result totals.</summary>
        [UnityTest]
        public IEnumerator AllFishEscapingCompletesAnEmptyResultWithoutStallingTheRun()
        {
            FastFailure(); AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(2); LoseOne(); Steps(2);
            float depth = Game.Run.Depth; Steps(1);
            Assert.That(depth - Game.Run.Depth, Is.EqualTo(.13f).Within(.001));
            Assert.That(Game.Run.Reeling.Active, Is.False); Assert.That(GameObject.Find("Reeling panel"), Is.Null);
            Until(RunState.Result); Assert.That(Game.Ui.ResultRows, Is.Empty);
            Assert.That(Game.Run.Gold, Is.Zero); Assert.That(Game.Run.Shells, Is.Zero);
            Assert.That(Label("Gold total").text, Is.EqualTo("0")); yield return null;
        }
        /// <summary>The actual branch command grants grace once; pause and a rejected repeat cannot consume or refresh it.</summary>
        [UnityTest]
        public IEnumerator BranchButtonGrantsVisibleGraceAndRejectedRepeatDoesNotRefreshIt()
        {
            FastFailure(); AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(2);
            Game.SetReelingInput(true); Steps(30); Game.DeployBranches(); Steps(1);
            Assert.That(Game.Run.Reeling.GraceRemaining, Is.GreaterThan(.7f));
            Assert.That(Label("Reeling status").text, Does.StartWith("缓冲"));
            float stability = Game.Run.Reeling.Stability; Game.PauseRun(); float grace = Game.Run.Reeling.GraceRemaining;
            Steps(100); Assert.That(Game.Run.Reeling.GraceRemaining, Is.EqualTo(grace)); Game.ResumeRun(); Steps(20);
            Assert.That(Game.AppliedEscapeEvents, Is.Zero); Assert.That(Game.Run.Reeling.Stability, Is.GreaterThanOrEqualTo(stability));
            grace = Game.Run.Reeling.GraceRemaining; Game.DeployBranches(); Assert.That(Game.Run.Reeling.GraceRemaining, Is.EqualTo(grace));
            yield return null;
        }
    }
}
