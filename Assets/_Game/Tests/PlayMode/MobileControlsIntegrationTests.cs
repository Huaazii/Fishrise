using System.Collections;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Fishing.Tests
{
    /// <summary>Exercises shared settings, actual hook physics and controlled gravity samples without requiring phone hardware.</summary>
    [Category("MobileControls")]
    public sealed class MobileControlsIntegrationTests : AbilityTestScene
    {
        private bool _hadMode, _hadSensitivity;
        private int _mode;
        private float _sensitivity;
        protected override bool InteractiveReeling => true;
        /// <summary>Preserves the user's settings before saved-scene setup.</summary>
        [SetUp]
        public void RememberPreferences()
        {
            _hadMode = PlayerPrefs.HasKey(FishingControlSettings.ModeKey);
            _hadSensitivity = PlayerPrefs.HasKey(FishingControlSettings.SensitivityKey);
            _mode = PlayerPrefs.GetInt(FishingControlSettings.ModeKey);
            _sensitivity = PlayerPrefs.GetFloat(FishingControlSettings.SensitivityKey);
        }
        /// <summary>Restores preferences even if a scene assertion fails.</summary>
        [TearDown]
        public void RestorePreferences()
        {
            if (_hadMode) PlayerPrefs.SetInt(FishingControlSettings.ModeKey, _mode); else PlayerPrefs.DeleteKey(FishingControlSettings.ModeKey);
            if (_hadSensitivity) PlayerPrefs.SetFloat(FishingControlSettings.SensitivityKey, _sensitivity); else PlayerPrefs.DeleteKey(FishingControlSettings.SensitivityKey);
            PlayerPrefs.Save();
        }
        /// <summary>Menu and pause mutate the same persisted preference without restarting the active run.</summary>
        [UnityTest]
        public IEnumerator BothSettingsEntryPointsPersistModeAndPauseSwitchDoesNotRestartRun()
        {
            Game.Ui.ShowMenu(); Game.Controls.Mode = FishingControlMode.Drag;
            Click("Menu settings"); Click("Toggle control mode");
            Assert.That(Game.Controls.Mode, Is.EqualTo(FishingControlMode.Tilt));
            Assert.That(new FishingControlSettings().Mode, Is.EqualTo(FishingControlMode.Tilt));
            float beforeSensitivity = Game.Controls.Sensitivity; Click("Tilt sensitivity");
            Assert.That(new FishingControlSettings().Sensitivity, Is.Not.EqualTo(beforeSensitivity));
            Click("Close settings"); Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Diving);
            var id = Game.Run.RunId; double time = Game.Run.GameTime; int protection = Game.Run.Protection;
            Game.PauseRun(); Click("Pause settings"); Click("Toggle control mode");
            Assert.That(Game.Controls.Mode, Is.EqualTo(FishingControlMode.Drag));
            Assert.That(Game.Run.RunId, Is.EqualTo(id)); Assert.That(Game.Run.GameTime, Is.EqualTo(time));
            Assert.That(Game.Run.Protection, Is.EqualTo(protection)); Assert.That(Game.Run.IsPaused, Is.True);
            Assert.That(new FishingControlSettings().Mode, Is.EqualTo(FishingControlMode.Drag));
            Click("Close settings"); Click("Resume"); Steps(1);
            Assert.That(Game.Run.RunId, Is.EqualTo(id)); Assert.That(Game.Run.GameTime, Is.GreaterThan(time));
            yield return null;
        }
        /// <summary>Injected gravity drives the real production input adapter and physics, stops at neutral and clamps to the lake.</summary>
        [UnityTest]
        public IEnumerator ControlledGravityMovesActualHookAndNeutralNeverRecentersOrCoasts()
        {
            var capability = typeof(FishingControlSettings).GetField("<TiltAvailable>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            capability.SetValue(Game.Controls, true);
            Game.Controls.Mode = FishingControlMode.Tilt; Until(RunState.Diving);
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Game);
            var tilt = (TiltSteering)typeof(DragInput).GetField("_tilt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
            tilt.BeginCalibration(); for (int i = 0; i < 25; i++) tilt.Sample(Vector3.down, .02f, 1);
            for (int i = 0; i < 25; i++) tilt.Sample(new Vector3(.2f, -.98f, 0), .02f, 1);
            float start = Game.Hook.transform.position.x; Steps(10);
            Assert.That(Game.Hook.transform.position.x - start, Is.GreaterThan(.5f));
            float stopped = Game.Hook.transform.position.x; tilt.Sample(Vector3.down, .02f, 1); Steps(10);
            Assert.That(Game.Hook.transform.position.x, Is.EqualTo(stopped).Within(.0001f));
            for (int i = 0; i < 25; i++) tilt.Sample(new Vector3(.8f, -.6f, 0), .02f, 1);
            Steps(100);
            var config = (FishingConfig)typeof(GameBootstrap).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Game);
            Assert.That(Game.Hook.transform.position.x, Is.EqualTo(config.halfWidth - Game.Hook.Radius).Within(.001f));
            for (int i = 0; i < 25; i++) tilt.Sample(new Vector3(-.8f, -.6f, 0), .02f, 1);
            Steps(100);
            Assert.That(Game.Hook.transform.position.x, Is.EqualTo(-config.halfWidth + Game.Hook.Radius).Within(.001f));
            tilt.Sample(Vector3.down, .02f, 1); float left = Game.Hook.transform.position.x; Steps(10);
            Assert.That(Game.Hook.transform.position.x, Is.EqualTo(left).Within(.0001f));
            yield return null;
        }
        /// <summary>Stationary hold raises the actual bar, dragging releases it, and a short tap survives a physics-step gap.</summary>
        [UnityTest]
        public IEnumerator UnzonedGesturesRaiseAndReleaseActualBarAndShortTapsAreNotLost()
        {
            Until(RunState.Ascending);
            AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(1);
            Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(1));
            Game.Run.Reeling.GrantGrace(10);
            var router = new FishingTouchRouter(); var area = new Rect(0, 0, 720, 1602);
            var origin = new Vector2(50, 100);
            router.BeginFrame(area, true, true, 0, 12); router.Process(1, origin, TouchPhase.Began, false); router.EndFrame();
            float initial = Game.Run.Reeling.BarPosition;
            for (int i = 1; i <= 30; i++)
            {
                router.BeginFrame(area, true, true, i * .02f, 12);
                router.Process(1, origin, TouchPhase.Stationary, false); router.EndFrame();
                Game.SetReelingInput(router.Reeling); Steps(1);
            }
            float raised = Game.Run.Reeling.BarPosition; Assert.That(raised, Is.GreaterThan(initial + .05f));
            for (int i = 31; i <= 110; i++)
            {
                router.BeginFrame(area, true, true, i * .02f, 12);
                router.Process(1, origin + Vector2.right * 80, TouchPhase.Moved, false); router.EndFrame();
                Assert.That(router.Steering, Is.True); Assert.That(router.Reeling, Is.False);
                Game.SetReelingInput(router.Reeling); Steps(1);
            }
            Assert.That(Game.Run.Reeling.BarPosition, Is.LessThan(raised - .05f));
            float released = Game.Run.Reeling.BarPosition; router.Reset();
            router.BeginFrame(area, true, true, 3, 12); router.Process(2, origin, TouchPhase.Began, false); router.EndFrame();
            router.BeginFrame(area, true, true, 3.05f, 12); router.Process(2, origin, TouchPhase.Ended, false); router.EndFrame();
            Game.SetReelingInput(router.Reeling);
            router.BeginFrame(area, true, true, 3.06f, 12); router.EndFrame(); Game.SetReelingInput(router.Reeling);
            Steps(1);
            Assert.That(Game.Run.Reeling.BarPosition, Is.GreaterThan(released + .00001f));
            yield return null;
        }

        /// <summary>Sensor sampling for calibration never enables steering in countdown, surfacing or result.</summary>
        [UnityTest]
        public IEnumerator TiltDoesNotMoveTheHookInNoninteractivePhases()
        {
            typeof(FishingControlSettings).GetField("<TiltAvailable>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(Game.Controls, true);
            Game.Controls.Mode = FishingControlMode.Tilt; Game.BeginRun(); Game.Spawner.Clear();
            var input = (DragInput)typeof(GameBootstrap).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Game);
            var tilt = (TiltSteering)typeof(DragInput).GetField("_tilt", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
            for (int i = 0; i < 25; i++) tilt.Sample(Vector3.down, .02f, 1);
            for (int i = 0; i < 25; i++) tilt.Sample(new Vector3(.3f, -.95f, 0), .02f, 1);
            float x = Game.Hook.transform.position.x; Steps(30);
            Assert.That(Game.Run.State, Is.EqualTo(RunState.Countdown));
            Assert.That(Game.Hook.transform.position.x, Is.EqualTo(x).Within(.0001f));
            Until(RunState.Surfacing);
            for (int i = 0; i < 25; i++) tilt.Sample(new Vector3(.3f, -.95f, 0), .02f, 1);
            x = Game.Hook.transform.position.x; Steps(10);
            Assert.That(Game.Hook.transform.position.x, Is.EqualTo(x).Within(.0001f));
            Until(RunState.Result);
            for (int i = 0; i < 25; i++) tilt.Sample(new Vector3(.3f, -.95f, 0), .02f, 1);
            Steps(10); Assert.That(Game.Hook.transform.position.x, Is.EqualTo(x).Within(.0001f));
            yield return null;
        }

        /// <summary>Both modes remain usable within real portrait safe areas, with readable settings and no controls outside the sheet.</summary>
        [UnityTest]
        public IEnumerator SettingsBothModesFitPortraitSafeAreasWithoutClippedText()
        {
            Game.Ui.ShowMenu(); Click("Menu settings");
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.Ui.ApplySafeArea(new Rect(12, 28, 426, height - 92));
                foreach (var mode in new[] { FishingControlMode.Drag, FishingControlMode.Tilt })
                {
                    Game.Controls.Mode = mode; Canvas.ForceUpdateCanvases();
                    var root = GameObject.Find("Feedback settings"); var safe = Game.Ui.SafeArea;
                    var corners = new Vector3[4]; var safeCorners = new Vector3[4]; safe.GetWorldCorners(safeCorners);
                    foreach (var button in root.GetComponentsInChildren<Button>())
                    {
                        ((RectTransform)button.transform).GetWorldCorners(corners);
                        foreach (var p in corners)
                        {
                            Assert.That(p.x, Is.InRange(safeCorners[0].x - 1, safeCorners[2].x + 1), button.name);
                            Assert.That(p.y, Is.InRange(safeCorners[0].y - 1, safeCorners[2].y + 1), button.name);
                        }
                    }
                    foreach (var text in root.GetComponentsInChildren<Text>())
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + " clipped");
                }
            }
            yield return null;
        }
    }
}
