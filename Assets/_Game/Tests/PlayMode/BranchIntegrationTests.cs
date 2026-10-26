using System.Collections;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Fishing.Tests
{
    /// <summary>Pooled branch physics, high-speed capture, real source cards and portrait command placement.</summary>
    public sealed class BranchIntegrationTests : AbilityTestScene
    {
        [UnityTest]
        public IEnumerator BranchesFanOutFollowAndStayInsideTheWaterAtTheHorizontalBoundary()
        {
            Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
            var views = Game.AbilityView.Branches;
            Assert.That(views.Count, Is.EqualTo(2)); Assert.That(views.All(v => v.gameObject.activeSelf), Is.True);
            Assert.That(views[0].transform.position.x, Is.EqualTo(-2.16f).Within(.02));
            Assert.That(views[1].transform.position.x, Is.EqualTo(2.16f).Within(.02));
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game);
            input.Reset(4.3f); Steps(50);
            foreach (var view in views) Assert.That(Mathf.Abs(view.transform.position.x) + view.Radius, Is.LessThanOrEqualTo(4.501f));
            int branch = LayerMask.NameToLayer("FishingBranchHook"), small = LayerMask.NameToLayer(FishActor.SmallFishLayer);
            Assert.That(Physics2D.GetLayerCollisionMask(branch), Is.EqualTo((1 << small) | 1)); yield return null;
        }
        [UnityTest]
        public IEnumerator SimultaneousMainAndBranchHeadContactGoesToBranchAndHigherStarFishPassesThrough()
        {
            Until(RunState.Ascending); Game.DeployBranches();
            var fish = AtHead(Small, Game.Hook.transform.position + Vector3.up * .02f); Steps(1);
            Assert.That(fish.IsCaptured, Is.True); Assert.That(Game.Run.MainCaptureCount, Is.Zero);
            Assert.That(Game.Run.Captures.Single().Source, Is.EqualTo(CaptureSource.Branch));
            Assert.That(fish.transform.parent.GetComponent<BranchHookView>(), Is.Not.Null);
            Steps(30); var branch = Game.AbilityView.Branches[0];
            var large = AtHead(Red, branch.transform.position + Vector3.up * .13f); Steps(1);
            Assert.That(large.IsCaptured, Is.False); Assert.That(Game.Abilities.Branches.Model.IsActive(1), Is.True);
            Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(1)); yield return null;
        }
        [UnityTest]
        public IEnumerator FullMainSweepsSixSmallFishIntoBranchesAndBuildsRealMixedSourceCards()
        {
            Until(RunState.Ascending);
            AtHead(Small, Game.Hook.transform.position + Vector3.up * .13f);
            for (int i = 0; i < 9; i++) AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f);
            Steps(1); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10)); Assert.That(Game.Run.IsFull, Is.True);
            Game.DeployBranches(); Steps(20);
            var left = Game.AbilityView.Branches[0]; var right = Game.AbilityView.Branches[1];
            var before = left.transform.position;
            var swept = AtHead(Small, before + Vector3.up * .325f);
            for (int i = 0; i < 4; i++) AtHead(Small, before + Vector3.up * .325f);
            for (int i = 0; i < 5; i++) AtHead(Silver, right.transform.position + Vector3.up * .325f);
            var centre = HeadCentre(swept); float combinedRadius = left.Radius + HeadRadius(swept);
            Assert.That(Vector3.Distance(before, centre), Is.GreaterThan(combinedRadius));
            float oldDepth = Game.Run.Depth; Steps(1);
            Assert.That(oldDepth - Game.Run.Depth, Is.EqualTo(.65f).Within(.001));
            Assert.That(Vector3.Distance(left.transform.position, centre), Is.GreaterThan(combinedRadius));
            Assert.That(swept.IsCaptured, Is.True); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10));
            Assert.That(Game.Run.BranchCaptureCount, Is.EqualTo(6)); Assert.That(Game.Run.CountForHook(1), Is.EqualTo(3)); Assert.That(Game.Run.CountForHook(2), Is.EqualTo(3));
            Assert.That(Label("HUD value 1").text, Is.EqualTo("10 / 10")); Assert.That(Label("HUD value 3").text, Is.EqualTo("6 / 6"));
            Assert.That(Game.AppliedCaptureEvents, Is.EqualTo(16));
            int gold = 9 * Blue.gold + 4 * Small.gold + 3 * Silver.gold;
            int shells = 9 * Blue.shells + 4 * Small.shells + 3 * Silver.shells;
            Until(RunState.Result); yield return null; yield return null;
            Assert.That(Game.Ui.ResultRows.Sum(r => r.TotalCount), Is.EqualTo(16)); Assert.That(Game.Run.Gold, Is.EqualTo(gold)); Assert.That(Game.Run.Shells, Is.EqualTo(shells));
            var small = Game.Ui.ResultRows.Single(r => r.SpeciesId == "small");
            Assert.That(small.MainCount, Is.EqualTo(1)); Assert.That(small.BranchCount, Is.EqualTo(3)); Assert.That(small.TotalCount, Is.EqualTo(4));
            Assert.That(GameObject.Find("Catch card small").transform.Find("Branch badge"), Is.Not.Null);
            Assert.That(GameObject.Find("Catch card blue").transform.Find("Branch badge"), Is.Null);
            Assert.That(Label("Gold total").text, Is.EqualTo(gold.ToString()));
            var old = Game.Run.RunId; Click("Dive again");
            Assert.That(Game.Run.RunId, Is.Not.EqualTo(old)); Assert.That(Game.Run.TotalCaptureCount, Is.Zero);
            Assert.That(Game.AbilityView.Branches.All(v => !v.gameObject.activeSelf && v.GetComponentsInChildren<FishActor>().Length == 0), Is.True);
        }
        [UnityTest]
        public IEnumerator TwentyRestartsReuseTwoBranchViewsAndPauseFreezesHangingFishAndTethers()
        {
            var objects = Game.AbilityView.Branches.Select(v => v.GetInstanceID()).ToArray(); int allocated = Game.Spawner.AllocatedCount;
            for (int run = 0; run < 20; run++)
            {
                if (run > 0) { Game.BeginRun(); Game.Spawner.Clear(); }
                Until(RunState.Ascending); Game.DeployBranches();
                AtHead(Small, Game.Hook.transform.position + Vector3.up * .02f); Steps(1);
                Assert.That(Game.Run.BranchCaptureCount, Is.EqualTo(1)); Assert.That(Game.AppliedCaptureEvents, Is.EqualTo(1));
                if (run == 0)
                {
                    Click("Pause button"); var positions = Game.AbilityView.Branches.Select(v => v.transform.position).ToArray(); double time = Game.Run.GameTime;
                    Steps(100); Assert.That(Game.Run.GameTime, Is.EqualTo(time));
                    CollectionAssert.AreEqual(positions, Game.AbilityView.Branches.Select(v => v.transform.position)); Click("Resume");
                }
                CollectionAssert.AreEqual(objects, Game.AbilityView.Branches.Select(v => v.GetInstanceID()));
                Assert.That(Object.FindObjectsOfType<BranchHookView>(true).Length, Is.EqualTo(2));
                Assert.That(Game.Spawner.AllocatedCount, Is.EqualTo(allocated));
            }
            Game.BeginRun(); Assert.That(Game.AbilityView.Branches.All(v => !v.gameObject.activeSelf && v.GetComponentsInChildren<FishActor>().Length == 0), Is.True);
            Assert.That(Game.Abilities.Branches.Model.Deployed, Is.False); Assert.That(Game.Abilities.Branches.RemainingUses, Is.EqualTo(1)); yield return null;
        }
        [UnityTest]
        public IEnumerator OptionalLargeFishRiskRetractsOnlyThatBranchAndKeepsItsCapturedFish()
        {
            var config = (BranchHookConfig)typeof(GameBootstrap).GetField("branchConfig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Game);
            bool previous = config.breakOnLarge;
            try
            {
                config.breakOnLarge = true; Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending);
                Game.DeployBranches(); AtHead(Small, Game.Hook.transform.position + Vector3.up * .02f); Steps(1);
                var record = Game.Run.Captures.Single(); var branch = Game.AbilityView.ForHook(record.HookId); Steps(30);
                AtHead(Red, branch.transform.position + Vector3.up * .13f); Steps(1);
                Assert.That(Game.Abilities.Branches.Model.IsActive(branch.HookId), Is.False); Assert.That(Game.Run.BranchCaptureCount, Is.EqualTo(1));
                Steps(30); Assert.That(Mathf.Abs(branch.transform.position.x - Game.Hook.transform.position.x), Is.LessThan(.05f));
                Assert.That(branch.GetComponentsInChildren<FishActor>().Length, Is.EqualTo(1));
            }
            finally { config.breakOnLarge = previous; }
            yield return null;
        }
        [UnityTest]
        public IEnumerator ThreePortraitRatiosKeepStatsAndPauseInSafeAreaWithNoSkillButtons()
        {
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Diving);
                Game.Ui.ApplySafeArea(new Rect(12, 28, 426, height - 92)); Canvas.ForceUpdateCanvases();
                Assert.That(Screen.width, Is.EqualTo(450)); Assert.That(Screen.height, Is.EqualTo(height));
                var corners = new Vector3[4]; Game.Ui.SafeArea.GetWorldCorners(corners);
                var safe = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                foreach (string name in new[] { "Pause button", "Stats" })
                {
                    var rect = GameObject.Find(name).GetComponent<RectTransform>(); rect.GetWorldCorners(corners);
                    Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(safe.xMin)); Assert.That(corners[2].x, Is.LessThanOrEqualTo(safe.xMax));
                    Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(safe.yMin)); Assert.That(corners[2].y, Is.LessThanOrEqualTo(safe.yMax));
                    if (name.EndsWith("button"))
                    {
                        Assert.That(corners[2].x < Screen.width * .4f || corners[0].x > Screen.width * .6f, Is.True);
                        Assert.That(corners[0].y, Is.GreaterThan(Screen.height * .2f + 20), "Keep the ascent hook lane visible: " + name);
                    }
                }
                foreach (var text in Game.Ui.Canvas.GetComponentsInChildren<Text>())
                { Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .1f), text.name + ": " + text.text);
                    if (text.text.Length > 0) Assert.That(text.cachedTextGenerator.characterCountVisible, Is.GreaterThan(0), text.name); }
                var hits = new System.Collections.Generic.List<RaycastResult>();
                var pointer = new PointerEventData(EventSystem.current) { position = GameObject.Find("Pause button").transform.position };
                EventSystem.current.RaycastAll(pointer, hits); Assert.That(hits.Any(h => h.gameObject.name == "Pause button"), Is.True);
                Assert.That(Game.Ui.Canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Sonar button" || b.name == "Branch button"), Is.False);
                Game.FireSonar(); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.EqualTo(1));
                Until(RunState.Ascending); Game.DeployBranches(); Steps(20); Assert.That(Game.Abilities.Branches.Model.Deployed, Is.True);
            }
        }
    }
}
