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
    /// <summary>Saved-scene presentation acceptance with actual physics and real UI callbacks.</summary>
    public sealed class PresentationIntegrationTests : AbilityTestScene
    {
        private void Sixteen()
        {
            Until(RunState.Ascending);
            for (int i = 0; i < 10; i++) AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f);
            Steps(1); Click("Branch button"); Steps(20);
            for (int i = 0; i < 3; i++) AtHead(Small, Game.AbilityView.Branches[0].transform.position + Vector3.up * .325f);
            for (int i = 0; i < 3; i++) AtHead(Silver, Game.AbilityView.Branches[1].transform.position + Vector3.up * .325f);
            Steps(1); Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(16));
        }
        [UnityTest]
        public IEnumerator AllFishJumpWithDelayedBranchesAndSameImmutableRewards()
        {
            Sixteen(); Assert.That(Game.Ui.SpeedLinesVisible, Is.True); Assert.That(Game.Ui.CapturePopups, Is.GreaterThan(0));
            var records = Game.Run.Captures.ToArray(); int gold = Game.Run.Gold, shells = Game.Run.Shells;
            Until(RunState.Surfacing); Assert.That(Game.Ui.Surface.PlannedCount, Is.EqualTo(16));
            Assert.That(GameObject.Find("Skip surfacing"), Is.Not.Null);
            Assert.That(Game.Spawner.Spawned.Where(f => f.IsCaptured).All(f => !f.gameObject.activeSelf), Is.True);
            Assert.That(GameObject.Find("Surface fish 10"), Is.Null, "First branch must wait 0.3 seconds.");
            Steps(10); Assert.That(GameObject.Find("Surface fish 10"), Is.Null);
            Steps(5); Assert.That(GameObject.Find("Surface fish 10"), Is.Not.Null);
            Until(RunState.Result); Steps(40); yield return null;
            Assert.That(Game.Ui.Surface.LaunchedCount, Is.EqualTo(16)); Assert.That(Game.Ui.Surface.Elapsed, Is.LessThanOrEqualTo(2.5f));
            Assert.That(Game.Ui.Surface.ActiveCount, Is.Zero); Assert.That(Game.Ui.SpeedLinesVisible, Is.False);
            Assert.That(Game.Run.Captures, Is.EqualTo(records)); Assert.That(Game.Run.Gold, Is.EqualTo(gold)); Assert.That(Game.Run.Shells, Is.EqualTo(shells));
            Assert.That(Game.Ui.ResultRows.Sum(r => r.TotalCount), Is.EqualTo(16));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Capture), Is.EqualTo(16));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Dive), Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Full), Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Surface), Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Result), Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator SkipAtStartMiddleAndEndClearsScheduledViewsExactlyOnce()
        {
            foreach (int steps in new[] { 0, 35, 112 })
            {
                Game.BeginRun(); Game.Spawner.Clear(); Sixteen(); Until(RunState.Surfacing); Steps(steps);
                var records = Game.Run.Captures.ToArray(); Click("Skip surfacing"); Game.SkipSurfacing(); Steps(150);
                Assert.That(Game.Run.State, Is.EqualTo(RunState.Result)); Assert.That(Game.Ui.Surface.ActiveCount, Is.Zero);
                Assert.That(Game.Run.Captures, Is.EqualTo(records)); Assert.That(Game.Ui.ResultRows.Sum(r => r.TotalCount), Is.EqualTo(16));
                Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Result), Is.EqualTo(1));
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator PauseFreezesHangPopAndSpeedLinesAndRestartClearsEveryEffect()
        {
            Sixteen(); var fish = Game.Spawner.Spawned.First(f => f.IsCaptured);
            var angle = fish.transform.localRotation; Steps(4); Assert.That(fish.transform.localRotation, Is.Not.EqualTo(angle));
            var mouth = fish.transform.TransformPoint(fish.Definition.mouthPosition);
            Assert.That(Vector3.Distance(mouth, Game.Hook.transform.TransformPoint(Game.Hook.HangPosition(0))), Is.LessThan(.001f));
            Click("Pause button"); var rotation = fish.transform.localRotation; double time = Game.Run.GameTime;
            var line = GameObject.Find("Full speed line 0").transform.localPosition; Steps(70); yield return null;
            Assert.That(fish.transform.localRotation, Is.EqualTo(rotation)); Assert.That(Game.Run.GameTime, Is.EqualTo(time));
            Assert.That(GameObject.Find("Full speed line 0").transform.localPosition, Is.EqualTo(line));
            Click("Resume"); Steps(1); Assert.That(fish.transform.localRotation, Is.Not.EqualTo(rotation));
            int allocated = Game.Spawner.AllocatedCount;
            for (int i = 0; i < 20; i++)
            {
                Game.BeginRun(); Assert.That(Game.Ui.Surface.ActiveCount, Is.Zero); Assert.That(Game.Ui.CapturePopups, Is.Zero);
                Assert.That(Game.Ui.SpeedLinesVisible, Is.False); Assert.That(Game.Run.TotalCaptureCount, Is.Zero);
                Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Capture), Is.Zero);
            }
            Assert.That(Game.Spawner.AllocatedCount, Is.EqualTo(allocated));
        }
        [UnityTest]
        public IEnumerator SettingsCallbacksMuteAudioAndHapticsAndProtectionFiresOnce()
        {
            bool sound = Game.Feedback.SoundEnabled, haptics = Game.Feedback.HapticsEnabled;
            bool hadSound = PlayerPrefs.HasKey(GameFeedback.SoundKey), hadHaptics = PlayerPrefs.HasKey(GameFeedback.HapticsKey);
            try
            {
                Until(RunState.Diving); Click("Pause button"); Click("Pause settings");
                if (Game.Feedback.SoundEnabled) Click("Toggle sound");
                if (Game.Feedback.HapticsEnabled) Click("Toggle haptics");
                Click("Close settings"); Click("Resume");
                var target = AtHead(Blue, Game.Hook.transform.position + Vector3.down * .12f); Steps(1);
                Assert.That(Game.Run.Protection, Is.EqualTo(1)); Steps(10);
                Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Protection), Is.EqualTo(1));
                Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Protection), Is.Zero); Assert.That(Game.Feedback.HapticCommands, Is.Zero);
                Click("Pause button"); Click("Pause settings"); Click("Toggle sound"); Click("Toggle haptics"); Click("Close settings"); Click("Resume");
                Game.Spawner.Clear(); Until(RunState.Ascending); AtHead(Small, Game.Hook.transform.position + Vector3.up * .13f); Steps(1);
                Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Capture), Is.EqualTo(1));
                Assert.That(Game.Feedback.PlayedCount(GameFeedback.Cue.Capture), Is.EqualTo(1)); Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(1));
                yield return null;
            }
            finally
            {
                if (Game.Feedback.SoundEnabled != sound) Game.Feedback.ToggleSound();
                if (Game.Feedback.HapticsEnabled != haptics) Game.Feedback.ToggleHaptics();
                if (!hadSound) PlayerPrefs.DeleteKey(GameFeedback.SoundKey); if (!hadHaptics) PlayerPrefs.DeleteKey(GameFeedback.HapticsKey); PlayerPrefs.Save();
            }
        }
        [UnityTest]
        public IEnumerator BackgroundDuringSurfacingFreezesPresentationAndResumesTheSameSettlement()
        {
            Sixteen(); Until(RunState.Surfacing); Steps(12);
            var callback = typeof(GameBootstrap).GetMethod("OnApplicationPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var records = Game.Run.Captures.ToArray(); double time = Game.Run.GameTime; float elapsed = Game.Ui.Surface.Elapsed;
            callback.Invoke(Game, new object[] { true }); Steps(150); yield return null;
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Surfacing)); Assert.That(Game.Run.GameTime, Is.EqualTo(time));
            Assert.That(Game.Ui.Surface.Elapsed, Is.EqualTo(elapsed)); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Result), Is.Zero);
            callback.Invoke(Game, new object[] { false }); Until(RunState.Result);
            Assert.That(Game.Run.Captures, Is.EqualTo(records)); Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Result), Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator ThreePortraitRatiosKeepSurfaceSkipAndSettingsInsideSafeArea()
        {
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.Ui.ApplySafeArea(new Rect(15, 28, 420, height - 64)); Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Surfacing);
                Canvas.ForceUpdateCanvases(); var button = GameObject.Find("Skip surfacing").GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Game.Ui.SafeArea, button);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(Game.Ui.SafeArea.rect.xMin)); Assert.That(bounds.max.x, Is.LessThanOrEqualTo(Game.Ui.SafeArea.rect.xMax));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(Game.Ui.SafeArea.rect.yMin)); Assert.That(bounds.max.y, Is.LessThanOrEqualTo(Game.Ui.SafeArea.rect.yMax));
                Assert.That(button.GetComponentInChildren<Text>().cachedTextGenerator.characterCountVisible, Is.GreaterThan(0));
                Click("Skip surfacing"); Assert.That(Game.Ui.ResultRows.Count, Is.Zero); Assert.That(Game.Run.Gold, Is.Zero);
            }
        }
    }
}
