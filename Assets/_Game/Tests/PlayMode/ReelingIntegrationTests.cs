using System.Collections;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Enabled-mode saved-scene, Physics2D, UI and lifecycle acceptance for retrieval checkpoint one.</summary>
    public sealed class ReelingIntegrationTests : AbilityTestScene
    {
        protected override bool InteractiveReeling => true;
        private void Catch(int count)
        {
            for (int i = 0; i < count; i++) AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f);
            Steps(2);
        }
        /// <summary>Input changes green-bar motion while the main hook is not yet full.</summary>
        [UnityTest]
        public IEnumerator HoldingAndReleasingControlsBarAndSmoothSpeedBeforeFull()
        {
            Until(RunState.Ascending); Catch(1);
            Assert.That(Game.Run.Rules.Reeling.Enabled, Is.True); Assert.That(Game.Run.IsFull, Is.False);
            Game.SetReelingInput(true); Steps(35);
            float raised = Game.Run.Reeling.BarPosition;
            Assert.That(raised, Is.GreaterThan(.35f));
            Game.SetReelingInput(false); Steps(100);
            Assert.That(Game.Run.Reeling.BarPosition, Is.LessThan(raised));
            float depth = Game.Run.Depth; Steps(1);
            Assert.That(depth - Game.Run.Depth, Is.InRange(.069f, .141f));
            Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(1)); Assert.That(Game.AppliedCaptureEvents, Is.EqualTo(1));
            yield return null;
        }
        /// <summary>Reaching main capacity drops the bar and restores the reference five-times sprint.</summary>
        [UnityTest]
        public IEnumerator FullLoadEndsReelingAndRestoresFiveTimesSpeed()
        {
            Until(RunState.Ascending); Catch(10); Steps(30);
            Assert.That(Game.Run.IsFull, Is.True); Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(10));
            float depth = Game.Run.Depth; Steps(1);
            Assert.That(depth - Game.Run.Depth, Is.InRange(.60f, .70f));
            Assert.That(GameObject.Find("Reeling panel"), Is.Null);
            yield return null;
        }
        /// <summary>Pause freezes model and hook, ignores hidden input, and restart creates a clean inactive model.</summary>
        [UnityTest]
        public IEnumerator PauseAndRestartClearInputWithoutAdvancingTheControlBar()
        {
            Until(RunState.Ascending); Catch(1); Game.SetReelingInput(true); Steps(15);
            Game.PauseRun(); float green = Game.Run.Reeling.BarPosition, fish = Game.Run.Reeling.FishPosition;
            float speed = Game.Run.Reeling.Speed; Vector3 hook = Game.Hook.transform.position;
            Game.SetReelingInput(true); Steps(100);
            Assert.That(Game.Run.Reeling.BarPosition, Is.EqualTo(green)); Assert.That(Game.Run.Reeling.FishPosition, Is.EqualTo(fish));
            Assert.That(Game.Run.Reeling.Speed, Is.EqualTo(speed)); Assert.That(Game.Hook.transform.position, Is.EqualTo(hook));
            Game.ResumeRun(); Steps(100); Assert.That(Game.Run.Reeling.BarPosition, Is.LessThan(green));
            Game.BeginRun(); Assert.That(Game.Run.Reeling.Active, Is.False); Assert.That(Game.Run.TotalCaptureCount, Is.Zero);
            Assert.That(GameObject.Find("Reeling panel"), Is.Null);
            yield return null;
        }
        /// <summary>Retrieval uses one non-blocking bottom line, with full instructions kept in teaching instead of a floating tile.</summary>
        [UnityTest]
        public IEnumerator RetrievalUsesBottomTextAcrossInputPauseRestartAndFullLoad()
        {
            Until(RunState.Ascending); Catch(1); Game.Run.Reeling.GrantGrace(100);
            for (int i = 0; i < 90; i++) Game.Ui.Reeling(Game.Run, Game.Hook.transform.position, Vector2.zero, .02f);
            var text = GameObject.Find("Drag hint").GetComponent<Text>();
            Assert.That(text.raycastTarget, Is.False);
            Assert.That(text.rectTransform.anchorMin.y, Is.Zero, "The instruction stays at the screen bottom.");
            Assert.That(GameObject.Find("Reeling input hint"), Is.Null);
            Assert.That(GameObject.Find("Reeling input text"), Is.Null);
            Assert.That(GameObject.Find("Reeling short hint"), Is.Null);
            Game.PauseRun(); Game.SetReelingInput(true); Game.ResumeRun();
            Game.Ui.Reeling(Game.Run, Game.Hook.transform.position, Vector2.zero, .2f);
            Assert.That(text.gameObject.activeSelf, Is.True);
            Game.SetReelingInput(true); Game.SetReelingInput(false);
            Game.Ui.Reeling(Game.Run, Game.Hook.transform.position, Vector2.zero, .2f);
            Assert.That(text.gameObject.activeSelf, Is.True);
            Game.PauseRun(); Assert.That(text.gameObject.activeInHierarchy, Is.False); Game.ResumeRun();
            Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending); Catch(1);
            Game.Ui.Reeling(Game.Run, Game.Hook.transform.position, Vector2.zero, .02f);
            Assert.That(text.gameObject.activeInHierarchy, Is.True);
            Catch(9); Game.Ui.Reeling(Game.Run, Game.Hook.transform.position, Vector2.zero, .02f);
            Assert.That(GameObject.Find("Reeling panel"), Is.Null);
            Assert.That(GameObject.Find("Drag hint"), Is.Null);
            yield return null;
        }
        /// <summary>The scalable display stays inside simulated safe areas, left of a centred hook, and outside HUD/buttons.</summary>
        [UnityTest]
        public IEnumerator LeftBarFitsThreePortraitsWithoutBlockingTouches()
        {
            Until(RunState.Ascending); Catch(1);
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.Ui.ApplySafeArea(new Rect(8, 22, Screen.width - 16, Screen.height - 66));
                Game.CameraRig.Follow(Game.Run, 0, true);
                foreach (float x in new[] { -4.5f, 0, 4.5f })
                {
                    var hook = Game.Hook.transform.position; hook.x = x; Game.Hook.transform.position = hook;
                    Game.Ui.Reeling(Game.Run, hook, Vector2.zero, 1);
                    Canvas.ForceUpdateCanvases();
                    var panel = GameObject.Find("Reeling panel").GetComponent<RectTransform>();
                    var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                    var safe = new Vector3[4]; Game.Ui.SafeArea.GetWorldCorners(safe);
                    Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(safe[0].x));
                    Assert.That(corners[2].x, Is.LessThanOrEqualTo(safe[2].x));
                    Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(safe[0].y));
                    Assert.That(corners[2].y, Is.LessThanOrEqualTo(safe[2].y));
                    if (x == 0) Assert.That(corners[2].x, Is.LessThan(Camera.main.WorldToScreenPoint(hook).x));
                    foreach (var graphic in panel.GetComponentsInChildren<Graphic>()) Assert.That(graphic.raycastTarget, Is.False);
                    if (Game.Run.Reeling.InZone)
                    {
                        var marker = new Vector3[4]; var green = new Vector3[4];
                        GameObject.Find("Reeling fish marker").GetComponent<RectTransform>().GetWorldCorners(marker);
                        GameObject.Find("Reeling green zone").GetComponent<RectTransform>().GetWorldCorners(green);
                        Assert.That(marker[0].y, Is.GreaterThanOrEqualTo(green[0].y - .01f));
                        Assert.That(marker[2].y, Is.LessThanOrEqualTo(green[2].y + .01f));
                    }
                    var stats = GameObject.Find("Stats").GetComponent<RectTransform>(); var top = new Vector3[4]; stats.GetWorldCorners(top);
                    Assert.That(corners[2].y, Is.LessThan(top[0].y));
                }
            }
        }
        /// <summary>Losses remove records before settlement; the remaining records alone determine rewards.</summary>
        [UnityTest]
        public IEnumerator LossesReduceCapturedRecordsAndCompleteAFullRun()
        {
            // A narrow test-only window guarantees a sustained miss; fixed time without input can randomly recover.
            var config = System.Linq.Enumerable.Single(Resources.FindObjectsOfTypeAll<FishingConfig>());
            float easy = config.reelEasyHeight, hard = config.reelHardHeight;
            try { config.reelEasyHeight = config.reelHardHeight = .12f; Game.BeginRun(); }
            finally { config.reelEasyHeight = easy; config.reelHardHeight = hard; }
            Game.Spawner.Clear(); Until(RunState.Ascending); Catch(9); Game.SetReelingInput(true); Steps(800);
            Assert.That(Game.AppliedEscapeEvents, Is.GreaterThan(0));
            int retained = Game.Run.MainCaptureCount;
            Assert.That(retained, Is.InRange(1, 9)); Assert.That(Game.Run.Gold, Is.EqualTo(retained * 10));
            Game.Run.Reeling.GrantGrace(60);
            Until(RunState.Result); Assert.That(Game.Ui.ResultRows.Count, Is.EqualTo(1));
            Assert.That(Game.Ui.ResultRows[0].TotalCount, Is.EqualTo(retained));
            Assert.That(Game.Ui.ResultRows[0].Gold, Is.EqualTo(retained * 10));
            Game.BeginRun(); Assert.That(Game.AppliedCaptureEvents, Is.Zero); Assert.That(Game.AppliedEscapeEvents, Is.Zero);
            yield return null;
        }
        /// <summary>An empty returning hook needs no control input and retains its normal return speed.</summary>
        [UnityTest]
        public IEnumerator EmptyReturnKeepsTheBarHiddenAndUsesNormalSpeed()
        {
            Until(RunState.Ascending);
            float atBottom = Game.Run.Depth; Steps(1);
            Assert.That(atBottom - Game.Run.Depth, Is.InRange(0.00001f,.01f), "The first retrieval step must ease away from the bottom.");
            Steps(20); float depth = Game.Run.Depth; Steps(1);
            Assert.That(depth - Game.Run.Depth, Is.EqualTo(.13f).Within(.001));
            Assert.That(Game.Run.Reeling.Active, Is.False); Assert.That(GameObject.Find("Reeling panel"), Is.Null);
            yield return null;
        }

        /// <summary>Measures actual hook movement through braking, a brief hold, launch and a paused mid-launch frame.</summary>
        [UnityTest]
        public IEnumerator HookBrakesAndLaunchesSmoothlyWithTheShortConfiguredHold()
        {
            Until(RunState.Diving);
            for (int i = 0; i < 2000 && Game.Run.Depth < 95; i++) Steps(1);
            float lastSpeed = 6;
            while (Game.Run.State == RunState.Diving)
            {
                float y = Game.Hook.transform.position.y; Steps(1);
                float speed = (y-Game.Hook.transform.position.y)/.02f;
                Assert.That(speed, Is.InRange(0,lastSpeed+.01f)); lastSpeed = speed;
                Assert.That(Game.Hook.transform.position.y, Is.EqualTo(-Game.Run.Depth).Within(.001f));
            }
            Assert.That(lastSpeed, Is.LessThan(.1f));
            int heldFrames = 0;
            while (Game.Run.State == RunState.BottomHold && heldFrames < 30) { Steps(1); heldFrames++; }
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Ascending));
            float hold = Game.Run.Rules.BottomHoldSeconds;
            Assert.That(heldFrames*.02f, Is.InRange(hold, hold + .03f));
            lastSpeed = 0;
            for (int i = 0; i < 25; i++)
            {
                if (i == 4)
                {
                    var y = Game.Hook.transform.position; float depth = Game.Run.Depth;
                    Game.PauseRun(); Steps(20); Assert.That(Game.Hook.transform.position, Is.EqualTo(y));
                    Assert.That(Game.Run.Depth, Is.EqualTo(depth)); Game.ResumeRun();
                }
                float before = Game.Hook.transform.position.y; Steps(1);
                float speed = (Game.Hook.transform.position.y-before)/.02f;
                Assert.That(speed, Is.InRange(lastSpeed-.01f,6.51f));
                Assert.That(speed-lastSpeed, Is.LessThan(.7f)); lastSpeed = speed;
            }
            Assert.That(lastSpeed, Is.EqualTo(6.5f).Within(.01f));
            yield return null;
        }
    }
}
