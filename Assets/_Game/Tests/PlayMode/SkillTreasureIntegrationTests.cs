using System.Collections;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Fishing.Tests
{
    /// <summary>Actual automatic skill pickups, phase visibility, echo queries and lifecycle acceptance.</summary>
    public sealed class SkillTreasureIntegrationTests : AbilityTestScene
    {
        protected override bool InteractiveReeling => true;
        protected override bool SkillTreasures => true;
        private void TravelTo(SkillTreasureView treasure)
        {
            for (int i = 0; i < 5000 && !treasure.Collected &&
                (Game.Run.State == RunState.Diving || Game.Run.State == RunState.Ascending); i++)
            {
                var hook = Game.Hook.transform.position; hook.x = treasure.Position.x; Game.Hook.transform.position = hook;
                Game.AdvanceGameplay(.02f);
            }
            Assert.That(treasure.Collected, Is.True, "Controlled main-hook route must acquire the visible treasure.");
        }

        [UnityTest]
        public IEnumerator CollectsBothSonarsAndAscentBranchesAutomaticallyThenFinishesMixedCaptureRun()
        {
            Assert.That(Game.Abilities.Sonar.RemainingUses, Is.Zero); Assert.That(Game.Abilities.Branches.RemainingUses, Is.Zero);
            Assert.That(Game.Treasures.Views.Count, Is.EqualTo(3));
            Until(RunState.Diving); Steps(1);
            Assert.That(Game.Ui.Canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Sonar button" || b.name == "Branch button"), Is.False);
            var sonar = Game.Treasures.Views.Where(v => v.Kind == SkillTreasureKind.Sonar).OrderByDescending(v => v.SpawnPosition.y).ToArray();
            var branch = Game.Treasures.Views.Single(v => v.Kind == SkillTreasureKind.Branches);
            Assert.That(branch.gameObject.activeSelf, Is.False);
            TravelTo(sonar[0]); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.Zero);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(1));
            TravelTo(sonar[1]); Steps(101);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(6));
            Assert.That(branch.gameObject.activeSelf || branch.Collected, Is.False);
            Until(RunState.Ascending); Assert.That(branch.gameObject.activeSelf, Is.True); TravelTo(branch); Steps(25);
            Assert.That(Game.Abilities.CollectedTreasureCount, Is.EqualTo(3));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Treasure), Is.EqualTo(3));
            Assert.That(Game.Run.TotalCaptureCount, Is.Zero); Assert.That(Game.Run.Gold, Is.Zero);
            Assert.That(Game.Abilities.Branches.Model.Active, Is.True); Assert.That(Game.Abilities.Branches.RemainingUses, Is.Zero);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Branches), Is.EqualTo(1));
            AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(2);
            Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(1)); Game.Run.Reeling.GrantGrace(100);
            AtHead(Small, Game.AbilityView.Branches[0].transform.position + Vector3.up * .13f); Steps(2);
            Assert.That(Game.Run.BranchCaptureCount, Is.EqualTo(1));
            Until(RunState.Result); Assert.That(Game.Run.Gold, Is.EqualTo(Blue.gold + Small.gold));
            Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(2));
            Assert.That(Game.Treasures.Views.All(v => !v.gameObject.activeSelf), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RelativeSweepFindsOvershootsRejectsOutsideAndDoesNotDoubleGrant()
        {
            Until(RunState.Diving); var view = Game.Treasures.Views.First();
            Vector2 centre = view.Position;
            Game.Treasures.Tick(Game.Run, centre + Vector2.up * 3, centre - Vector2.up * 3, Game.Hook.Radius, .02f);
            Assert.That(view.Collected, Is.True); Assert.That(Game.Abilities.Sonar.AcquiredUses, Is.EqualTo(1));
            Game.Treasures.Tick(Game.Run, centre, centre, Game.Hook.Radius, .02f);
            Assert.That(Game.Abilities.Sonar.AcquiredUses, Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(1));
            var other = Game.Treasures.Views.Where(v => v.Kind == SkillTreasureKind.Sonar && !v.Collected).Single();
            Vector2 outside = (Vector2)other.Position + Vector2.right * 1.1f;
            Game.Treasures.Tick(Game.Run, outside + Vector2.up, outside - Vector2.up, Game.Hook.Radius, .02f);
            Assert.That(other.Collected, Is.False); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Treasure), Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainHookColliderOffsetAndScaleDefinePickupInsteadOfTheSpriteRoot()
        {
            Until(RunState.Diving); var shape = Game.Hook.Head;
            Vector2 offset = shape.offset; Vector3 scale = shape.transform.localScale;
            try
            {
                shape.offset = new Vector2(2, 0); shape.transform.localScale = Vector3.one * 1.3f;
                var first = Game.Treasures.Views.First(v => v.Kind == SkillTreasureKind.Sonar);
                first.ResetView(first.Id, first.Kind, shape.transform.TransformPoint(shape.offset) + Vector3.down * .1f, .85f);
                Steps(1); Assert.That(first.Collected, Is.True, "Actual offset circle must pick up the treasure.");
                var second = Game.Treasures.Views.Single(v => v.Kind == SkillTreasureKind.Sonar && !v.Collected);
                second.ResetView(second.Id, second.Kind, Game.Hook.transform.position + Vector3.down * .1f, .85f);
                Steps(1); Assert.That(second.Collected, Is.False, "A sprite-root overlap outside the actual circle must not collect.");
                Assert.That(Game.Abilities.Sonar.AcquiredUses, Is.EqualTo(1));
                Assert.That(Game.Abilities.Sonar.RemainingUses, Is.Zero);
            }
            finally { shape.offset = offset; shape.transform.localScale = scale; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseFreezesShimmerAndPickupAndRestartReusesUncollectedViews()
        {
            Until(RunState.Diving); var views = Game.Treasures.Views.ToArray(); var view = views[0];
            Game.AdvanceGameplay(.02f); var position = view.Position; var colour = view.GetComponentsInChildren<SpriteRenderer>()[0].color;
            Game.PauseRun();
            Game.Treasures.Tick(Game.Run, position, position, Game.Hook.Radius, 10);
            Game.AdvanceGameplay(10);
            Assert.That(view.Position, Is.EqualTo(position)); Assert.That(view.Collected, Is.False);
            Assert.That(view.GetComponentsInChildren<SpriteRenderer>()[0].color, Is.EqualTo(colour));
            Game.ResumeRun(); Game.Treasures.Tick(Game.Run, position, position, Game.Hook.Radius, .02f);
            Assert.That(view.Collected, Is.True); var old = Game.Run;
            for (int i = 0; i < 20; i++) Game.BeginRun();
            Assert.That(Game.Treasures.Views.Count, Is.EqualTo(3)); Assert.That(Game.Treasures.Views, Is.EquivalentTo(views));
            Assert.That(Game.Treasures.Views.All(v => !v.Collected), Is.True); Assert.That(Game.Abilities.Sonar.RemainingUses, Is.Zero);
            Assert.That(Game.Abilities.TryCollectTreasure(old.RunId, view.Id, view.Kind), Is.False);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Treasure), Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedSeedReplaysSafeDepthBandsWithoutCollidersOrGlobalRandomConsumption()
        {
            var field = typeof(GameBootstrap).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic);
            var saved = (FishingConfig)field.GetValue(Game); var copy = Object.Instantiate(saved);
            copy.useFixedPopulationSeed = true; field.SetValue(Game, copy);
            try
            {
                Game.BeginRun(); var points = Game.Treasures.Views.Select(v => v.SpawnPosition).ToArray();
                var random = Random.state; Game.BeginRun(); Assert.That(Random.state, Is.EqualTo(random));
                Assert.That(Game.Treasures.Views.Select(v => v.SpawnPosition), Is.EqualTo(points));
                foreach (var view in Game.Treasures.Views)
                {
                    Assert.That(view.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                    float depth = -view.SpawnPosition.y / copy.unitsPerMetre;
                    Vector2 limits = view.Kind == SkillTreasureKind.Sonar ? copy.sonarTreasureDepths : copy.branchTreasureDepths;
                    Assert.That(depth, Is.InRange(limits.x, limits.y));
                    Assert.That(Mathf.Abs(view.Position.x), Is.LessThan(copy.halfWidth - copy.treasureVisualSize * .5f));
                    foreach (var fish in Game.Spawner.Spawned)
                    {
                        float radius = HeadRadius(fish);
                        Assert.That(Vector2.Distance(view.SpawnPosition, HeadCentre(fish)), Is.GreaterThan(radius + copy.treasurePickupRadius));
                    }
                }
                for (int seed = 0; seed < 20; seed++)
                {
                    copy.randomSeed = seed * 7919 + 17; Game.BeginRun();
                    foreach (var view in Game.Treasures.Views)
                        foreach (var fish in Game.Spawner.Spawned)
                            Assert.That(Vector2.Distance(view.SpawnPosition, HeadCentre(fish)),
                                Is.GreaterThan(HeadRadius(fish) + copy.treasurePickupRadius), "Seed " + copy.randomSeed);
                }
                Assert.That(Game.Treasures.Views.Count, Is.EqualTo(3));
            }
            finally { field.SetValue(Game, saved); Object.Destroy(copy); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreePortraitRatiosKeepGlowInsideWaterAndTheSkillButtonLanesClear()
        {
            Until(RunState.Diving);
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; Game.CameraRig.Follow(Game.Run, 0, true); Game.Ui.ApplySafeArea();
                var view = Game.Treasures.Views[0];
                Camera.main.transform.position = new Vector3(0, view.SpawnPosition.y, -10);
                Game.Treasures.Tick(Game.Run, Vector2.zero, Vector2.zero, Game.Hook.Radius, .2f);
                Vector3 screen = Camera.main.WorldToViewportPoint(view.Position);
                Assert.That(screen.x, Is.InRange(.05f, .95f)); Assert.That(screen.y, Is.InRange(.1f, .9f));
                Assert.That(view.GetComponentsInChildren<SpriteRenderer>().All(r => r.sharedMaterial.shader.isSupported), Is.True);
                Assert.That(Game.Ui.Canvas.GetComponentsInChildren<Button>(true).Any(b => b.name == "Sonar button" || b.name == "Branch button"), Is.False);
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = new Vector2(Screen.width * .2f, Screen.height * .25f) };
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Empty, "The old skill area must no longer intercept gameplay input.");
            }
        }
        [UnityTest]
        public IEnumerator BothEchoesQueryTheCurrentHookPositionAtOneAndTwoUnpausedSeconds()
        {
            Until(RunState.Diving); var treasure = Game.Treasures.Views.First(v => v.Kind == SkillTreasureKind.Sonar);
            treasure.ResetView(treasure.Id, treasure.Kind, Game.Hook.transform.position + Vector3.down * .1f, .85f);
            var first = AtHead(Small, Game.Hook.transform.position + Vector3.right * 2);
            Steps(1); Assert.That(treasure.Collected && first.IsScared, Is.True);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(1));
            Steps(20); Game.PauseRun(); double time = Game.Run.GameTime; Steps(100);
            Assert.That(Game.Run.GameTime, Is.EqualTo(time)); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(1));
            Game.ResumeRun(); Steps(29); Assert.That(Game.AbilityView.Wave.enabled, Is.False);
            Vector3 now = Game.Hook.transform.position;
            var later = AtHead(Small, now + Vector3.right * 2);
            Assert.That(later.IsScared, Is.False); Steps(1);
            Assert.That(later.IsScared, Is.True); Assert.That(Game.AbilityView.Wave.enabled, Is.True);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(2));
            var ring = Game.AbilityView.Wave.GetPosition(0);
            Assert.That(ring.y, Is.EqualTo(now.y).Within(.001f));
            Steps(49); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.EqualTo(2));
            Vector3 thirdPosition=Game.Hook.transform.position;
            var third=AtHead(Small,thirdPosition+Vector3.left*2);
            Assert.That(third.IsScared,Is.False); Steps(1);
            Assert.That(third.IsScared,Is.True); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar),Is.EqualTo(3));
            Assert.That(Game.AbilityView.Wave.GetPosition(0).y,Is.EqualTo(thirdPosition.y).Within(.001f));
            Steps(51); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar),Is.EqualTo(3));
            Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Diving); Steps(110);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Sonar), Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator BranchTreasureCannotBeSeenOrCollectedOnDescentAndAdaptsToEarlyReturn()
        {
            Until(RunState.Diving); Steps(1); var branch = Game.Treasures.Views.Single(v => v.Kind == SkillTreasureKind.Branches);
            Assert.That(branch.gameObject.activeSelf, Is.False);
            Game.Treasures.Tick(Game.Run, branch.Position, branch.Position, Game.Hook.Radius, .02f);
            Assert.That(branch.Collected || Game.Abilities.Branches.Model.Deployed, Is.False);
            while (Game.Run.Depth < 10) Steps(1);
            for (int i = 0; i < 3; i++)
            { AtHead(Blue, Game.Hook.transform.position + Vector3.down * .13f); Steps(1); if (i < 2) Steps(60); }
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Ascending)); Assert.That(Game.Run.Depth, Is.LessThan(40));
            Assert.That(branch.gameObject.activeSelf, Is.True);
            float depth = -branch.SpawnPosition.y;
            Assert.That(depth, Is.GreaterThan(0).And.LessThan(Game.Run.Depth - 3));
            Game.Run.Reeling.GrantGrace(100); TravelTo(branch);
            Assert.That(branch.Collected && Game.Abilities.Branches.Model.Active, Is.True);
            Assert.That(Game.Run.Reeling.GraceRemaining, Is.GreaterThanOrEqualTo(Game.Run.Rules.Reeling.AbilityGraceSeconds));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Branches), Is.EqualTo(1));
            yield return null;
        }
    }
}
