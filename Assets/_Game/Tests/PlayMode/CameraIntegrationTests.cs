using System.Collections;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Saved-scene camera bounds, buffered reversal and accelerated retrieval feedback.</summary>
    public sealed class CameraIntegrationTests : AbilityTestScene
    {
        private bool _hadHaptics;
        private int _haptics;
        /// <summary>Preserves the user's stored haptic setting before exercising both values.</summary>
        [UnitySetUp]
        public IEnumerator RememberSettings()
        { _hadHaptics = PlayerPrefs.HasKey(GameFeedback.HapticsKey); _haptics = PlayerPrefs.GetInt(GameFeedback.HapticsKey); yield break; }
        /// <summary>Restores the exact preference key, including its original absence.</summary>
        [UnityTearDown]
        public IEnumerator RestoreSettings()
        {
            if (_hadHaptics) PlayerPrefs.SetInt(GameFeedback.HapticsKey, _haptics); else PlayerPrefs.DeleteKey(GameFeedback.HapticsKey);
            PlayerPrefs.Save(); yield break;
        }
        private void Frame(float dt = .02f)
        { Game.AdvanceGameplay(dt); Game.CameraRig.Follow(Game.Run, dt); }
        private void FramesUntil(RunState state)
        { for (int i = 0; i < 6000 && Game.Run.State != state; i++) Frame(); Assert.That(Game.Run.State, Is.EqualTo(state)); }
        private void FillMain()
        { for (int i = 0; i < 10; i++) AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Frame(); }

        /// <summary>The closer portrait view makes the actor prominent, retains both foreground ice faces and fills the sky, then restores the backdrop.</summary>
        [UnityTest]
        public IEnumerator SurfaceViewKeepsActorProminentAndBothIceFacesVisibleAcrossPortraits()
        {
            var camera = Camera.main;
            var clouds = Game.SceneView.transform.Find("Surface/Clouds").GetComponent<SpriteRenderer>();
            var sky = Game.SceneView.transform.Find("Surface/Sky").GetComponent<SpriteRenderer>();
            var position = clouds.transform.localPosition; var scale = clouds.transform.localScale;
            var samples = new System.Collections.Generic.List<string>();
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            foreach (float aspect in new[] { 450f / 800, 450f / 975, 450f / 1050, 1200f / 2670 })
            {
                camera.aspect = aspect; Game.BeginRun(); Game.Spawner.Clear(); FramesUntil(RunState.Surfacing); Frame();
                float waterline = camera.WorldToViewportPoint(new Vector3(0, .98f, 0)).y;
                Assert.That(waterline, Is.EqualTo(.38f).Within(.005f));
                float leftWidth = 0, rightWidth = 0, iceHeight = 1;
                foreach (string name in new[] { "Ice Left", "Ice Right" })
                {
                    var wall = Game.SceneView.transform.Find("Surface/" + name).GetComponent<SpriteRenderer>();
                    var lo = camera.WorldToViewportPoint(wall.bounds.min); var hi = camera.WorldToViewportPoint(wall.bounds.max);
                    float width = Mathf.Max(0, Mathf.Min(1, hi.x) - Mathf.Max(0, lo.x));
                    iceHeight = Mathf.Min(iceHeight, Mathf.Min(1, hi.y) - Mathf.Max(0, lo.y));
                    if (wall.bounds.center.x < 0) leftWidth = Mathf.Max(leftWidth, width);
                    else rightWidth = Mathf.Max(rightWidth, width);
                }
                Assert.That(leftWidth, Is.GreaterThan(.40f), "The left foreground ice face must frame the opening.");
                Assert.That(rightWidth, Is.GreaterThan(.40f), "The right foreground ice face must frame the opening.");
                Assert.That(iceHeight, Is.GreaterThan(.10f), "The closer view must preserve visible ice thickness.");
                var actor = Game.SceneView.Fisherman;
                var actorBounds = actor.sprite.bounds;
                var actorLo = camera.WorldToViewportPoint(actor.transform.TransformPoint(actorBounds.min));
                var actorHi = camera.WorldToViewportPoint(actor.transform.TransformPoint(actorBounds.max));
                Assert.That(actorHi.x - actorLo.x, Is.GreaterThan(.30f), "The fisherman must read as the foreground subject.");
                Assert.That(actorHi.y - actorLo.y, Is.GreaterThan(.155f));
                Assert.That(actorLo.x, Is.GreaterThan(.05f)); Assert.That(actorHi.x, Is.LessThan(.5f));
                Assert.That(Game.SceneView.transform.Find("Surface/Surface upper clouds"), Is.Null, "Do not duplicate the same photographic cloud silhouette.");
                Assert.That(sky.sharedMaterial.shader.name, Is.EqualTo("Fishing/Overcast Sky"));
                Assert.That(sky.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(sky.bounds.max.y, Is.GreaterThan(camera.transform.position.y + camera.orthographicSize));
                Assert.That(clouds.color.a, Is.LessThan(.2f), "The old cloud photo should remain only a subtle low cloud band.");
                samples.Add("{\"aspect\":" + aspect.ToString(culture) + ",\"waterline\":" + waterline.ToString(culture) +
                    ",\"leftIceWidth\":" + leftWidth.ToString(culture) + ",\"rightIceWidth\":" + rightWidth.ToString(culture) +
                    ",\"iceHeight\":" + iceHeight.ToString(culture) + ",\"actorWidth\":" + (actorHi.x - actorLo.x).ToString(culture) +
                    ",\"actorHeight\":" + (actorHi.y - actorLo.y).ToString(culture) + "}");
                FramesUntil(RunState.Result); Game.BeginRun();
                Assert.That(clouds.transform.localPosition, Is.EqualTo(position)); Assert.That(clouds.transform.localScale, Is.EqualTo(scale));
            }
            System.IO.File.WriteAllText("Docs/Verification/scene-coherence-geometry-20261025.json", "{\"samples\":[" + string.Join(",", samples) + "]}");
            camera.ResetAspect(); yield return null;
        }

        /// <summary>The rendered viewport must stay above the actual seabed for every supported portrait ratio.</summary>
        [UnityTest]
        public IEnumerator ThreePortraitRatiosNeverRevealBelowTheSavedSeabed()
        {
            var camera = Camera.main;
            float seabed = Game.SceneView.transform.Find("Underwater/Seabed").GetComponent<SpriteRenderer>().bounds.max.y;
            foreach (int height in new[] { 800, 975, 1050 })
            foreach (bool fast in new[] { false, true })
            {
                camera.aspect = 450f / height; Game.BeginRun(); Game.Spawner.Clear();
                for (int i = 0; i < 6000 && Game.Run.State != RunState.Surfacing; i++)
                {
                    if (fast && Game.Run.State == RunState.Ascending && !Game.Run.IsFull) FillMain();
                    Frame();
                    Assert.That(camera.transform.position.y - camera.orthographicSize, Is.GreaterThanOrEqualTo(seabed - .001f),
                        $"450x{height}, {Game.Run.State}, depth={Game.Run.Depth}");
                }
                Assert.That(Game.Run.State, Is.EqualTo(RunState.Surfacing));
                Assert.That(camera.orthographicSize * camera.aspect, Is.EqualTo(4.7f).Within(.001f));
                for (int i = 0; i < 60; i++) Game.CameraRig.Follow(Game.Run, .02f);
                Assert.That(Game.CameraRig.ShakeOffset, Is.EqualTo(Vector2.zero));
            }
            camera.ResetAspect(); yield return null;
        }
        /// <summary>Actual slow reeling and fast full retrieval prepare the close view before water, keeping the lift readable.</summary>
        [UnityTest]
        public IEnumerator CloseShotPreparesDuringAscentAndStaysStillThroughoutTheHookLift()
        {
            var camera = Camera.main; float maxZoomStep = 0, maxLiftMotion = 0; int paths = 0;
            foreach (bool reeling in new[] { false, true })
            using (new ReelingComparisonScope(reeling, false))
            foreach (int height in new[] { 800, 1050 })
            foreach (float returnX in new[] { 0f, -3.9f, 3.9f })
            {
                camera.aspect = 450f / height; Game.BeginRun(); Game.Spawner.Clear(); FramesUntil(RunState.Ascending); FillMain();
                Game.Run.Reeling.GrantGrace(100);
                var input = (DragInput)typeof(GameBootstrap).GetField("_input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Game);
                input.Reset(returnX);
                double approachStart = -1; bool paused = false, settled = false;
                while (Game.Run.State == RunState.Ascending)
                {
                    float width = camera.orthographicSize * camera.aspect; Frame();
                    float framing = Game.CameraRig.SurfaceFraming(Game.Run);
                    if (framing <= 0 || Game.Run.State != RunState.Ascending) continue;
                    if (approachStart < 0) approachStart = Game.Run.GameTime;
                    maxZoomStep = Mathf.Max(maxZoomStep, Mathf.Abs(width - camera.orthographicSize * camera.aspect));
                    Assert.That(camera.WorldToViewportPoint(Game.Hook.transform.position).y, Is.GreaterThanOrEqualTo(.119f));
                    Assert.That(camera.WorldToViewportPoint(Game.Hook.transform.position).x, Is.InRange(.05f, .95f));
                    if (!paused && framing > .2f && framing < .8f)
                    {
                        var position = camera.transform.position; float size = camera.orthographicSize;
                        var actor = Game.SceneView.Fisherman.transform.localScale;
                        Game.PauseRun(); for (int i = 0; i < 15; i++) Frame();
                        Assert.That(camera.transform.position, Is.EqualTo(position)); Assert.That(camera.orthographicSize, Is.EqualTo(size));
                        Assert.That(Game.SceneView.Fisherman.transform.localScale, Is.EqualTo(actor));
                        Game.ResumeRun(); paused = true;
                    }
                    if (Game.Run.Depth <= 1.5f)
                    {
                        settled = true;
                        Assert.That(camera.orthographicSize * camera.aspect, Is.EqualTo(4.7f).Within(.001f));
                    }
                }
                Assert.That(Game.Run.State, Is.EqualTo(RunState.Surfacing)); Assert.That(paused && settled, Is.True);
                Assert.That(Game.Run.GameTime - approachStart, Is.GreaterThan(1.3), "The zoom must precede the lift, not complete in its old 0.35 second window.");
                var closePosition = camera.transform.position; float closeSize = camera.orthographicSize;
                for (int i = 0; i < 30; i++)
                {
                    Frame(); maxLiftMotion = Mathf.Max(maxLiftMotion, Vector3.Distance(camera.transform.position, closePosition));
                    Assert.That(camera.orthographicSize, Is.EqualTo(closeSize).Within(.001f));
                }
                Assert.That(Game.Ui.Surface.AirHook.position.y, Is.GreaterThan(1));
                FramesUntil(RunState.Result); Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(10)); paths++;
            }
            Assert.That(maxZoomStep, Is.LessThan(.08f)); Assert.That(maxLiftMotion, Is.LessThan(.02f));
            System.IO.File.WriteAllText("Docs/Verification/scene-coherence-motion-20261025.json",
                "{\"paths\":" + paths + ",\"maxHalfWidthStep\":" + maxZoomStep.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"maxCameraMotionDuringLift\":" + maxLiftMotion.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            camera.ResetAspect(); yield return null;
        }

        /// <summary>A late tenth capture changes ascent speed without changing the depth-to-framing mapping.</summary>
        [UnityTest]
        public IEnumerator FillingNearSurfaceDoesNotJumpThePreparedCamera()
        {
            using (new ReelingComparisonScope(true, false))
            foreach (float fillDepth in new[] { 40f, 8f, 2f })
            {
                Game.BeginRun(); Game.Spawner.Clear(); FramesUntil(RunState.Ascending);
                for (int i = 0; i < 9; i++) AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f);
                Frame(); Game.Run.Reeling.GrantGrace(100);
                while (Game.Run.Depth > fillDepth) Frame();
                Assert.That(Game.Run.MainCaptureCount, Is.EqualTo(9));
                float width = Camera.main.orthographicSize * Camera.main.aspect;
                float framing = Game.CameraRig.SurfaceFraming(Game.Run);
                AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Frame();
                Assert.That(Game.Run.IsFull, Is.True);
                Assert.That(Mathf.Abs(Camera.main.orthographicSize * Camera.main.aspect - width), Is.LessThan(.08f));
                Assert.That(Mathf.Abs(Game.CameraRig.SurfaceFraming(Game.Run) - framing), Is.LessThan(.05f));
                FramesUntil(RunState.Result); Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(10));
            }
            yield return null;
        }

        /// <summary>Touching bottom and reversing must preserve position/velocity continuity; pause and restart reset correctly.</summary>
        [UnityTest]
        public IEnumerator BottomReversalIsBufferedAndPauseAndRestartDoNotLeaveMotion()
        {
            FramesUntil(RunState.BottomHold); var camera = Camera.main;
            Vector3 bottom = camera.transform.position; FramesUntil(RunState.Ascending);
            Assert.That(Vector3.Distance(camera.transform.position, bottom), Is.LessThan(.001f));
            Game.CameraRig.Follow(Game.Run, 0); Assert.That(camera.transform.position, Is.EqualTo(bottom));
            float lastSpeed = 0;
            for (int i = 0; i < 100; i++)
            {
                float previous = camera.transform.position.y; Frame();
                float speed = (camera.transform.position.y - previous) / .02f;
                Assert.That(speed, Is.InRange(-.001f, 10f));
                Assert.That(Mathf.Abs(speed - lastSpeed), Is.LessThan(1.5f), "Camera velocity must ease out of the bottom hold.");
                lastSpeed = speed;
            }
            Assert.That(camera.transform.position.y, Is.GreaterThan(bottom.y + 5));
            var paused = camera.transform.position; Game.PauseRun();
            for (int i = 0; i < 30; i++) Frame();
            Assert.That(camera.transform.position, Is.EqualTo(paused)); Game.ResumeRun();
            Game.BeginRun(); Game.Spawner.Clear();
            Assert.That(Game.CameraRig.ShakeOffset, Is.EqualTo(Vector2.zero));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0, -camera.orthographicSize * .4f, -10)));
            yield return null;
        }

        /// <summary>Full-load acceleration adds one haptic pulse, bounded shake, and no extra feedback on pause/resume or restart.</summary>
        [UnityTest]
        public IEnumerator FastAscentAddsOnePulseAndShakeFreezesWithPauseAndResetsPerRun()
        {
            if (!Game.Feedback.HapticsEnabled) Game.Feedback.ToggleHaptics();
            for (int run = 0; run < 3; run++)
            {
                if (run > 0) { Game.BeginRun(); Game.Spawner.Clear(); }
                FramesUntil(RunState.Ascending); Assert.That(Game.Feedback.HapticCommands, Is.Zero);
                FillMain(); Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(11), "Ten captures and one acceleration pulse.");
                float maximum = 0;
                for (int i = 0; i < 30; i++)
                {
                    Frame(); Vector2 offset = Game.CameraRig.ShakeOffset; maximum = Mathf.Max(maximum, offset.magnitude);
                    Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(.0351f));
                    Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(.0159f));
                    Assert.That(Camera.main.transform.position.y - Camera.main.orthographicSize, Is.GreaterThanOrEqualTo(Game.SceneView.SeabedY - .001f));
                }
                Assert.That(maximum, Is.GreaterThan(.01f)); Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(11));
                var position = Camera.main.transform.position; var shake = Game.CameraRig.ShakeOffset; Game.PauseRun();
                for (int i = 0; i < 30; i++) Frame();
                Assert.That(Camera.main.transform.position, Is.EqualTo(position)); Assert.That(Game.CameraRig.ShakeOffset, Is.EqualTo(shake));
                Game.ResumeRun(); Frame(); Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(11));
            }
            Game.BeginRun(); Assert.That(Game.Feedback.HapticCommands, Is.Zero);
            Assert.That(Game.CameraRig.ShakeOffset, Is.EqualTo(Vector2.zero)); yield return null;
        }

        /// <summary>Filling at the bottom delays the acceleration pulse until ascent, while the haptic switch suppresses every pulse.</summary>
        [UnityTest]
        public IEnumerator BottomFullWaitsForAscentAndDisabledHapticsStaySilent()
        {
            foreach (bool enabled in new[] { true, false })
            {
                if (Game.Feedback.HapticsEnabled != enabled) Game.Feedback.ToggleHaptics();
                Game.BeginRun(); Game.Spawner.Clear(); FramesUntil(RunState.BottomHold); FillMain();
                Assert.That(Game.Run.State, Is.EqualTo(RunState.BottomHold)); Assert.That(Game.Run.IsFull, Is.True);
                Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(enabled ? 10 : 0));
                Assert.That(Game.CameraRig.ShakeOffset, Is.EqualTo(Vector2.zero));
                FramesUntil(RunState.Ascending); Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(enabled ? 11 : 0));
                for (int i = 0; i < 30; i++) Frame();
                Assert.That(Game.Feedback.HapticCommands, Is.EqualTo(enabled ? 11 : 0));
            }
            yield return null;
        }
    }
}
