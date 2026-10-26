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
            Steps(1); Game.DeployBranches(); Steps(20);
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
            Assert.That(GameObject.Find("Skip surfacing"), Is.Null);
            Assert.That(GameObject.Find("Surfacing show").GetComponentsInChildren<UnityEngine.UI.Button>(true), Is.Empty);
            Assert.That(Game.Spawner.Spawned.Where(f => f.IsCaptured).All(f => !f.gameObject.activeSelf), Is.True);
            Assert.That(Game.Ui.Surface.WorldFishCount, Is.EqualTo(16), "Fish must remain visible during the hook's rise.");
            var amounts = GameObject.Find("Surfacing show").GetComponentsInChildren<Text>(true).Where(t => t.name == "Surface gold").ToArray();
            Assert.That(amounts.Length, Is.EqualTo(16));
            foreach (var amount in amounts)
            {
                Assert.That(System.Text.RegularExpressions.Regex.IsMatch(amount.text, @"^\+\d+$"), Is.True,
                    "Every source should show only its score during the surface celebration.");
                Assert.That(amount.color, Is.EqualTo(amounts[0].color));
                var coin = amount.transform.parent.Find("Surface coin").GetComponent<UnityEngine.UI.Image>();
                Assert.That(coin.sprite, Is.Not.Null); Assert.That(coin.raycastTarget, Is.False); Assert.That(coin.preserveAspect, Is.True);
                Assert.That(coin.rectTransform.anchoredPosition.x + coin.rectTransform.rect.width * .5f,
                    Is.LessThan(amount.rectTransform.anchoredPosition.x - amount.rectTransform.rect.width * .5f));
                Assert.That(coin.GetComponentInParent<CanvasGroup>(true), Is.SameAs(amount.transform.parent.GetComponent<CanvasGroup>()), "The icon must share movement, visibility and fading with its score.");
            }
            Assert.That(Game.Ui.Surface.VisibleRewardCount, Is.Zero, "Rewards must not cover the initial water break.");
            Steps(32); Assert.That(Game.Ui.Surface.LaunchedCount, Is.GreaterThan(0));
            Assert.That(Game.Ui.Surface.LaunchedBranchCount, Is.Zero);
            Steps(15); Assert.That(Game.Ui.Surface.LaunchedBranchCount, Is.GreaterThan(0), "Branch releases follow the main sequence by 0.3 seconds.");
            Until(RunState.Result); Steps(40); yield return null;
            Assert.That(Game.Ui.Surface.LaunchedCount, Is.EqualTo(16)); Assert.That(Game.Ui.Surface.Elapsed, Is.LessThanOrEqualTo(2.5f));
            Assert.That(Game.Ui.Surface.LandedCount, Is.EqualTo(16));
            Assert.That(Game.Ui.Surface.ActiveCount, Is.Zero); Assert.That(Game.Ui.SpeedLinesVisible, Is.False);
            Assert.That(Game.Run.Captures, Is.EqualTo(records)); Assert.That(Game.Run.Gold, Is.EqualTo(gold)); Assert.That(Game.Run.Shells, Is.EqualTo(shells));
            Assert.That(Game.Ui.ResultRows.Sum(r => r.TotalCount), Is.EqualTo(16));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Capture), Is.EqualTo(16));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Dive), Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Full), Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Surface), Is.EqualTo(1));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Result), Is.EqualTo(1));
        }
        /// <summary>Checks collider-free rise, rope continuity, recoil and background freeze against unchanged gameplay state.</summary>
        [UnityTest]
        public IEnumerator AirHookRisesWithConnectedRopeWhileGameplayHookAndRewardsStayUnchanged()
        {
            Sixteen(); Until(RunState.Surfacing);
            var surface = Game.Ui.Surface; var physical = Game.Hook.transform.position;
            int gold = Game.Run.Gold; var initial = surface.AirHook.position;
            Steps(20);
            Assert.That(surface.AirHook.position.y - initial.y, Is.GreaterThan(2));
            Assert.That(Game.Hook.transform.position, Is.EqualTo(physical)); Assert.That(Game.Run.Depth, Is.Zero);
            Assert.That(Game.Run.Gold, Is.EqualTo(gold)); Assert.That(surface.VisibleRewardCount, Is.Zero);
            Assert.That(surface.AirHook.GetComponentsInChildren<Collider2D>(), Is.Empty);
            Assert.That(surface.AirHook.GetComponentsInChildren<SpriteRenderer>().First(r => r.name == "Stem").enabled,
                Is.False, "The retired placeholder shaft must remain hidden on the visual copy.");
            var line = Game.Hook.GetComponentInChildren<LineRenderer>();
            Assert.That(line.GetPosition(line.positionCount - 1).y, Is.GreaterThan(surface.AirHook.position.y + .8f));
            Assert.That(GameObject.Find("Airborne fish 0").GetComponent<MeshRenderer>().sortingLayerName, Is.EqualTo("Surface Lit"));
            var first = surface.AirHook.rotation; Steps(6); var second = surface.AirHook.rotation;
            Assert.That(Quaternion.Angle(first, second), Is.GreaterThan(.2f));
            var position = surface.AirHook.position; var rotation = surface.AirHook.rotation; var time = surface.Elapsed;
            var background = typeof(GameBootstrap).GetMethod("OnApplicationPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            background.Invoke(Game, new object[] { true }); Steps(30); yield return null;
            Assert.That(surface.AirHook.position, Is.EqualTo(position)); Assert.That(surface.AirHook.rotation, Is.EqualTo(rotation));
            Assert.That(surface.Elapsed, Is.EqualTo(time)); background.Invoke(Game, new object[] { false });
            Until(RunState.Result); Assert.That(surface.ActiveCount, Is.Zero); Assert.That(surface.AirHook.gameObject.activeSelf, Is.False);
        }
        /// <summary>Exercises every catchable silhouette and bounded reward overlap at three portrait ratios.</summary>
        [UnityTest]
        public IEnumerator MixedSpeciesStayInPortraitFrameAndEveryRewardFinishesBeforeSettlement()
        {
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending);
                var species = Resources.FindObjectsOfTypeAll<FishDefinition>().Where(f => f.isCatchable).GroupBy(f => f.speciesId).Select(g => g.First()).ToArray();
                foreach (var definition in species) AtHead(definition, Game.Hook.transform.position + Vector3.up * .13f);
                Steps(1); Assert.That(Game.Run.TotalCaptureCount, Is.EqualTo(species.Length)); Until(RunState.Surfacing);
                Game.CameraRig.Follow(Game.Run, 0, true); var camera = Camera.main;
                int maxLabels = 0; float maxY = 0;
                while (Game.Run.State == RunState.Surfacing)
                {
                    Steps(1); Game.CameraRig.Follow(Game.Run, .02f);
                    maxLabels = Mathf.Max(maxLabels, Game.Ui.Surface.VisibleRewardCount);
                    foreach (var fish in Game.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Airborne fish")))
                    {
                        var lo = camera.WorldToViewportPoint(fish.bounds.min); var hi = camera.WorldToViewportPoint(fish.bounds.max);
                        Assert.That(lo.x, Is.GreaterThan(0)); Assert.That(hi.x, Is.LessThan(1));
                        maxY = Mathf.Max(maxY, hi.y); Assert.That(hi.y, Is.LessThan(.91f), "Fish must stay below the heading.");
                    }
                }
                Assert.That(maxY, Is.GreaterThan(.65f), "The catch must clearly rise above the ice.");
                Assert.That(maxLabels, Is.InRange(1, 3)); Assert.That(Game.Ui.Surface.LandedCount, Is.EqualTo(species.Length));
                Assert.That(Game.Ui.Surface.ActiveCount, Is.Zero);
            }
        }
        /// <summary>Isolates the flying fish and measures actual Light2D response in rendered pixels.</summary>
        [UnityTest]
        public IEnumerator AirborneFishRespondsToTheRealLanternLight()
        {
            Sixteen(); Until(RunState.Surfacing); Steps(40); Game.CameraRig.Follow(Game.Run, 0, true);
            var fish = GameObject.Find("Airborne fish 0").GetComponent<MeshRenderer>();
            var lamp = Game.SceneView.GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>().First(l => l.name == "Lantern Warmth");
            var lampPosition = lamp.transform.position; var renderers = Object.FindObjectsOfType<Renderer>();
            var enabled = renderers.Select(r => r.enabled).ToArray(); var camera = Camera.main;
            var clear = camera.clearFlags; var background = camera.backgroundColor;
            Texture2D off = null, on = null;
            try
            {
                Game.Ui.Canvas.enabled = false;
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = renderers[i] == fish;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                lamp.transform.position = fish.bounds.center + new Vector3(.3f, .2f, 0);
                lamp.enabled = false; yield return null; yield return new WaitForEndOfFrame(); off = ScreenCapture.CaptureScreenshotAsTexture();
                lamp.enabled = true; yield return null; yield return new WaitForEndOfFrame(); on = ScreenCapture.CaptureScreenshotAsTexture();
                var a = off.GetPixels32(); var b = on.GetPixels32(); int changed = 0;
                for (int i = 0; i < a.Length; i++)
                    if (a[i].r + a[i].g + a[i].b > 30 && Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 6) changed++;
                Assert.That(changed, Is.GreaterThan(20), "A lamp toggle must change the isolated airborne fish pixels.");
                System.IO.File.WriteAllText("Docs/Verification/fishrise-camera-timing-light-pixels-20261024.json", "{\"isolatedFishChangedPixels\":" + changed + "}");
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                lamp.transform.position = lampPosition; lamp.enabled = true; camera.clearFlags = clear; camera.backgroundColor = background; Game.Ui.Canvas.enabled = true;
                if (off != null) Object.Destroy(off); if (on != null) Object.Destroy(on);
            }
        }
        /// <summary>Checks close framing, rod deformation, background freeze and exact restart restoration.</summary>
        [UnityTest]
        public IEnumerator CloseSurfaceShotBendsTheRodAndRestoresTheOriginalSceneOnRestart()
        {
            var camera = Camera.main; var actor = Game.SceneView.Fisherman.transform;
            var position = actor.localPosition; var scale = actor.localScale; var tip = Game.SceneView.RodTip.localPosition;
            var clouds = Game.SceneView.transform.Find("Surface/Clouds"); var cloudPosition = clouds.localPosition;
            var rodMesh = Game.SceneView.SurfaceRod.GetComponent<MeshFilter>().sharedMesh;
            Sixteen(); Until(RunState.Surfacing); Steps(12);
            Assert.That(Vector3.Distance(Game.SceneView.RodTip.localPosition, tip), Is.GreaterThan(.1f));
            var block = new MaterialPropertyBlock(); Game.SceneView.SurfaceRod.GetPropertyBlock(block);
            var bend = block.GetVector("_Deform"); Assert.That(Mathf.Abs(bend.y), Is.GreaterThan(.1f));
            var callback = typeof(GameBootstrap).GetMethod("OnApplicationPause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            callback.Invoke(Game, new object[] { true }); Steps(30); yield return null;
            Game.SceneView.SurfaceRod.GetPropertyBlock(block); Assert.That(block.GetVector("_Deform"), Is.EqualTo(bend));
            callback.Invoke(Game, new object[] { false }); Steps(16); Game.CameraRig.Follow(Game.Run, 0, true);
            Assert.That(camera.orthographicSize * camera.aspect, Is.EqualTo(4.7f).Within(.005f));
            Assert.That(actor.localScale.x, Is.EqualTo(scale.x * 1.45f).Within(.001f));
            Assert.That(Game.SceneView.Fisherman.enabled, Is.False); Assert.That(Game.SceneView.SurfaceRod.enabled, Is.True);
            Until(RunState.Result); Game.CameraRig.Follow(Game.Run, 0, true);
            Assert.That(camera.orthographicSize * camera.aspect, Is.EqualTo(4.7f).Within(.005f));
            Game.BeginRun(); Game.CameraRig.Follow(Game.Run, 0, true);
            Assert.That(camera.orthographicSize * camera.aspect, Is.EqualTo(6).Within(.005f));
            Assert.That(actor.localPosition, Is.EqualTo(position)); Assert.That(actor.localScale, Is.EqualTo(scale));
            Assert.That(Game.SceneView.RodTip.localPosition, Is.EqualTo(tip)); Assert.That(clouds.localPosition, Is.EqualTo(cloudPosition));
            Assert.That(Game.SceneView.SurfaceActor.enabled, Is.True); Assert.That(Game.SceneView.SurfaceRod.enabled, Is.False);
            Assert.That(Game.SceneView.SurfaceRod.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(rodMesh));
        }
        /// <summary>Measures silhouette movement between two shader phases while the fish transform stays fixed.</summary>
        [UnityTest]
        public IEnumerator FishTailDeformationChangesRenderedSilhouetteWithoutTransformMotion()
        {
            Sixteen(); Until(RunState.Surfacing); Steps(40); Game.CameraRig.Follow(Game.Run, 0, true);
            var fish = GameObject.Find("Airborne fish 0").GetComponent<MeshRenderer>();
            var renderers = Object.FindObjectsOfType<Renderer>(); var enabled = renderers.Select(r => r.enabled).ToArray();
            var camera = Camera.main; var clear = camera.clearFlags; var background = camera.backgroundColor;
            var original = new MaterialPropertyBlock(); fish.GetPropertyBlock(original);
            var block = new MaterialPropertyBlock(); fish.GetPropertyBlock(block); var parameters = block.GetVector("_Deform");
            var position = fish.transform.position; var rotation = fish.transform.rotation; Texture2D a = null, b = null;
            try
            {
                Game.Ui.Canvas.enabled = false;
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = renderers[i] == fish;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                parameters.z = 0; block.SetVector("_Deform", parameters); fish.SetPropertyBlock(block);
                yield return null; yield return new WaitForEndOfFrame(); a = ScreenCapture.CaptureScreenshotAsTexture();
                parameters.z = Mathf.PI; block.SetVector("_Deform", parameters); fish.SetPropertyBlock(block);
                yield return null; yield return new WaitForEndOfFrame(); b = ScreenCapture.CaptureScreenshotAsTexture();
                var first = a.GetPixels32(); var second = b.GetPixels32(); int changed = 0;
                for (int i = 0; i < first.Length; i++)
                    if ((first[i].r + first[i].g + first[i].b > 30) != (second[i].r + second[i].g + second[i].b > 30)) changed++;
                Assert.That(changed, Is.GreaterThan(10), "Tail bending must change the silhouette, not only shader parameters.");
                Assert.That(fish.transform.position, Is.EqualTo(position)); Assert.That(fish.transform.rotation, Is.EqualTo(rotation));
                System.IO.File.WriteAllText("Docs/Verification/fishrise-camera-timing-deformation-pixels-20261024.json", "{\"silhouetteChangedPixels\":" + changed + "}");
            }
            finally
            {
                fish.SetPropertyBlock(original);
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = enabled[i];
                camera.clearFlags = clear; camera.backgroundColor = background; Game.Ui.Canvas.enabled = true;
                if (a != null) Object.Destroy(a); if (b != null) Object.Destroy(b);
            }
        }
        /// <summary>Verifies visual recentering and water-break location without moving either extreme gameplay hook.</summary>
        [UnityTest]
        public IEnumerator ExtremeHorizontalReturnsGuideTheVisualAndSplashThroughTheOpening()
        {
            foreach (float x in new[] { -3.9f, 3.9f })
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending);
                AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(1);
                var input = (DragInput)typeof(GameBootstrap).GetField("_input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Game);
                input.Reset(x); Until(RunState.Surfacing);
                float physicalX = Game.Hook.transform.position.x; Assert.That(physicalX, Is.EqualTo(x).Within(.01f));
                Steps(10);
                Assert.That(Mathf.Abs(Game.Ui.Surface.AirHook.position.x), Is.LessThanOrEqualTo(.81f));
                Assert.That(Game.Hook.transform.position.x, Is.EqualTo(physicalX));
                var drops = Game.GetComponentsInChildren<LineRenderer>().Where(r => r.name.StartsWith("Surface water drop") && r.enabled).ToArray();
                Assert.That(drops.Length, Is.GreaterThan(0));
                foreach (var drop in drops) Assert.That(Mathf.Abs(drop.GetPosition(0).x), Is.LessThan(1.5f), "Side-wall returns must not splash through the ice.");
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator SkipAtStartMiddleAndEndClearsScheduledViewsExactlyOnce()
        {
            foreach (int steps in new[] { 0, 35, 112 })
            {
                Game.BeginRun(); Game.Spawner.Clear(); Sixteen(); Until(RunState.Surfacing); Steps(steps);
                var records = Game.Run.Captures.ToArray(); Game.SkipSurfacing(); Game.SkipSurfacing(); Steps(150);
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
        /// <summary>Real held contacts flash three times without replay, and bottom-zone entry cancels the remaining tail.</summary>
        [UnityTest]
        public IEnumerator ConsumedProtectionBlinksThreeTimesWithoutRetriggeringOnContinuousContactAndHidesAtBottom()
        {
            Until(RunState.Diving);
            var shield = Game.Hook.transform.Find("Protection").GetComponent<SpriteRenderer>();
            Assert.That(shield.enabled, Is.True);
            var fish = AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f);
            Steps(1);
            Assert.That(Game.Run.Protection, Is.EqualTo(1)); Assert.That(shield.enabled, Is.False);
            int brightTransitions = 0; bool previous = shield.enabled;
            for (int step = 0; step < 75; step++)
            {
                Align(fish, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f);
                Steps(1);
                if (!previous && shield.enabled) brightTransitions++;
                previous = shield.enabled;
                Assert.That(Game.Run.Protection, Is.EqualTo(1), "A held contact must not spend protection again after immunity expires.");
                Assert.That(Game.Hook.Head.enabled, Is.True, "Flashing must not disable the actual hook collider.");
                if (step >= 37) Assert.That(shield.enabled, Is.True, "The remaining protection must return to a steady bubble.");
            }
            Assert.That(brightTransitions, Is.EqualTo(3)); Assert.That(fish.IsCaptured, Is.False);
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Protection), Is.EqualTo(1));
            Game.Spawner.Clear();
            while (Game.Run.Depth < 95) Steps(1);
            AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f); Steps(1);
            Assert.That(Game.Run.Protection, Is.Zero);
            while (Game.Run.Depth < Game.Run.Rules.BottomZoneDepth) Steps(1);
            Assert.That(shield.enabled, Is.False, "Entering the bottom zone must hide even an unfinished hit flash.");
            yield return null;
        }

        /// <summary>The last charge remains readable, freezes on pause and cannot flash again in a new run.</summary>
        [UnityTest]
        public IEnumerator LastProtectionStillFlashesWhilePauseFreezesItAndRestartClearsIt()
        {
            Until(RunState.Diving);
            var shield = Game.Hook.transform.Find("Protection").GetComponent<SpriteRenderer>();
            AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f); Steps(1);
            Game.Spawner.Clear(); Steps(60);
            AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f); Steps(1);
            Assert.That(Game.Run.Protection, Is.Zero); Assert.That(shield.enabled, Is.False);
            Game.Spawner.Clear(); Steps(7);
            Assert.That(shield.enabled, Is.True, "The spent last bubble must have a visible on phase before disappearing.");
            Game.PauseRun(); double time = Game.Run.GameTime; Steps(100);
            Assert.That(Game.Run.GameTime, Is.EqualTo(time)); Assert.That(shield.enabled, Is.True);
            Game.ResumeRun(); Steps(30);
            Assert.That(Game.Run.Protection, Is.Zero); Assert.That(shield.enabled, Is.False);
            Assert.That(Game.Hook.transform.Find("Bubbles").GetComponentsInChildren<SpriteRenderer>().All(r => !r.enabled), Is.True);
            Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Diving);
            for (int step = 0; step < 120; step++)
            {
                Steps(1);
                Assert.That(shield.enabled, Is.True, "A new run must never inherit the previous run's flash deadline.");
            }
            Assert.That(Game.Run.Protection, Is.EqualTo(2));
            Assert.That(Game.Feedback.EventCount(GameFeedback.Cue.Protection), Is.Zero);
            yield return null;
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
        public IEnumerator ThreePortraitRatiosKeepTheSurfaceHeadingInsideSafeAreaAndFinishAutomatically()
        {
            foreach (int height in new[] { 800, 975, 1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.Ui.ApplySafeArea(new Rect(15, 28, 420, height - 64)); Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Surfacing);
                Canvas.ForceUpdateCanvases(); var heading = GameObject.Find("Surface heading").GetComponent<RectTransform>();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Game.Ui.SafeArea, heading);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(Game.Ui.SafeArea.rect.xMin)); Assert.That(bounds.max.x, Is.LessThanOrEqualTo(Game.Ui.SafeArea.rect.xMax));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(Game.Ui.SafeArea.rect.yMin)); Assert.That(bounds.max.y, Is.LessThanOrEqualTo(Game.Ui.SafeArea.rect.yMax));
                Assert.That(heading.GetComponentInChildren<Text>().cachedTextGenerator.characterCountVisible, Is.GreaterThan(0));
                Assert.That(GameObject.Find("Skip surfacing"), Is.Null); Until(RunState.Result);
                Assert.That(Game.Ui.ResultRows.Count, Is.Zero); Assert.That(Game.Run.Gold, Is.Zero);
            }
        }
        /// <summary>Checks the entire tethered rise at centre and both side returns against the actual bent rod tip.</summary>
        [UnityTest]
        public IEnumerator AirHookRemainsBelowTheBentRodTipAndRopeEndpointsStayConnected()
        {
            float smallestGap = float.MaxValue;
            foreach (float x in new[] { 0f, -3.9f, 3.9f })
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending);
                AtHead(Blue, Game.Hook.transform.position + Vector3.up * .13f); Steps(1);
                var input = (DragInput)typeof(GameBootstrap).GetField("_input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(Game);
                input.Reset(x); Until(RunState.Surfacing);
                var physical = Game.Hook.transform.position; var rope = Game.Hook.GetComponentInChildren<LineRenderer>();
                while (Game.Run.State == RunState.Surfacing)
                {
                    var visual = Game.Ui.Surface.AirHook;
                    var attachment = visual.position + Game.Hook.SurfaceLineOffset(visual.eulerAngles.z);
                    float gap = Game.SceneView.RodTip.position.y - attachment.y; smallestGap = Mathf.Min(smallestGap, gap);
                    Assert.That(gap, Is.GreaterThanOrEqualTo(.449f), "The hook attachment must never rise above the bent rod tip.");
                    Assert.That(Vector3.Distance(rope.GetPosition(0), Game.SceneView.RodTip.position), Is.LessThan(.001f));
                    Assert.That(Vector3.Distance(rope.GetPosition(rope.positionCount - 1), attachment), Is.LessThan(.001f));
                    Assert.That(Game.Hook.transform.position, Is.EqualTo(physical)); Steps(1);
                }
            }
            System.IO.File.WriteAllText("Docs/Verification/fishrise-camera-timing-clearance-20261024.json",
                "{\"minimumAnchorGap\":" + smallestGap.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"returnPaths\":3}");
            yield return null;
        }
    
        /// <summary>Captures actual rendered game camera frames during real fish-head collisions and verifies both pulses and final depletion.</summary>
        [UnityTest]
        public IEnumerator ProtectionFlashVisualSamplingCapturesActualGameFramesAndVerifiesBlinkSequence()
        {
            Resolution(800);
            Until(RunState.Diving);
            var shield = Game.Hook.transform.Find("Protection").GetComponent<SpriteRenderer>();
            var bubbles = Game.Hook.transform.Find("Bubbles").GetComponentsInChildren<SpriteRenderer>();
            var camera = Camera.main;
            var directory = "Docs/Verification";
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            void Capture(string fileName)
            {
                var rt = RenderTexture.GetTemporary(450, 800, 24);
                var prevRt = camera.targetTexture;
                var prevActive = RenderTexture.active;
                try
                {
                    camera.targetTexture = rt;
                    camera.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(450, 800, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 450, 800), 0, 0);
                    tex.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, fileName), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                finally
                {
                    camera.targetTexture = prevRt;
                    RenderTexture.active = prevActive;
                    RenderTexture.ReleaseTemporary(rt);
                }
            }

            var samples = new System.Collections.Generic.List<string>();
            void Record(string phase, string file)
            {
                Capture(file);
                samples.Add($"{{\"phase\":\"{phase}\",\"gameTime\":{Game.Run.GameTime.ToString(culture)},\"depth\":{Game.Run.Depth.ToString(culture)},\"protection\":{Game.Run.Protection},\"shieldVisible\":{shield.enabled.ToString().ToLower()},\"bubblesVisible\":{bubbles.All(b => b.enabled).ToString().ToLower()},\"file\":\"{file}\"}}");
            }

            // 1. Initial diving state with full protection (2 charges)
            Assert.That(Game.Run.Protection, Is.EqualTo(2));
            Assert.That(shield.enabled, Is.True);
            Record("01-InitialSteadyProtection", "protection-flash-01-initial-steady.png");

            // 2. Real fish-head collision (First hit -> Protection reduced to 1)
            AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f);
            Steps(1);
            Assert.That(Game.Run.Protection, Is.EqualTo(1));
            Assert.That(shield.enabled, Is.False, "Pulse 1 OFF phase starts immediately after spending a charge.");
            Record("02-FirstHit-Pulse1-Off", "protection-flash-02-first-hit-pulse1-off.png");

            // Pulse 1 ON (around 0.16s after hit)
            Steps(7);
            Assert.That(shield.enabled, Is.True, "Pulse 1 ON phase must be visible.");
            Record("03-FirstHit-Pulse1-On", "protection-flash-03-first-hit-pulse1-on.png");

            // Pulse 2 OFF (around 0.28s after hit)
            Steps(6);
            Assert.That(shield.enabled, Is.False, "Pulse 2 OFF phase.");
            Record("04-FirstHit-Pulse2-Off", "protection-flash-04-first-hit-pulse2-off.png");

            // Pulse 2 ON (around 0.40s after hit)
            Steps(6);
            Assert.That(shield.enabled, Is.True, "Pulse 2 ON phase.");
            Record("05-FirstHit-Pulse2-On", "protection-flash-05-first-hit-pulse2-on.png");

            // Pulse 3 OFF (around 0.52s after hit)
            Steps(6);
            Assert.That(shield.enabled, Is.False, "Pulse 3 OFF phase.");
            Record("06-FirstHit-Pulse3-Off", "protection-flash-06-first-hit-pulse3-off.png");

            // Pulse 3 ON (around 0.64s after hit)
            Steps(6);
            Assert.That(shield.enabled, Is.True, "Pulse 3 ON phase.");
            Record("07-FirstHit-Pulse3-On", "protection-flash-07-first-hit-pulse3-on.png");

            // Advance past flash duration (0.72s). Remaining 1 protection charge restores steady bubble.
            Steps(8);
            Assert.That(Game.Run.Protection, Is.EqualTo(1));
            Assert.That(shield.enabled, Is.True, "Remaining protection charge must return to steady bubble after 3 pulses.");
            Record("08-FirstHit-FlashCompleted-Steady", "protection-flash-08-first-hit-steady.png");

            // 3. Second real fish-head collision (Second hit -> Protection reduced to 0)
            Game.Spawner.Clear();
            Steps(30); // Clear any lingering immunity
            AtHead(Blue, Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset) + Vector3.down * .12f);
            Steps(1);
            Assert.That(Game.Run.Protection, Is.Zero);
            Assert.That(shield.enabled, Is.False, "Last protection charge spent: pulse 1 OFF starts.");
            Record("09-LastHit-Pulse1-Off", "protection-flash-09-last-hit-pulse1-off.png");

            // Last charge pulse 1 ON
            Steps(7);
            Assert.That(shield.enabled, Is.True, "Last protection charge must still show pulse 1 ON phase.");
            Record("10-LastHit-Pulse1-On", "protection-flash-10-last-hit-pulse1-on.png");

            // Last charge pulse 2 OFF
            Steps(6);
            Assert.That(shield.enabled, Is.False, "Last protection pulse 2 OFF phase.");
            Record("11-LastHit-Pulse2-Off", "protection-flash-11-last-hit-pulse2-off.png");

            // Last charge pulse 2 ON
            Steps(6);
            Assert.That(shield.enabled, Is.True, "Last protection pulse 2 ON phase.");
            Record("12-LastHit-Pulse2-On", "protection-flash-12-last-hit-pulse2-on.png");

            // Last charge pulse 3 OFF
            Steps(6);
            Assert.That(shield.enabled, Is.False, "Last protection pulse 3 OFF phase.");
            Record("13-LastHit-Pulse3-Off", "protection-flash-13-last-hit-pulse3-off.png");

            // Last charge pulse 3 ON
            Steps(6);
            Assert.That(shield.enabled, Is.True, "Last protection pulse 3 ON phase.");
            Record("14-LastHit-Pulse3-On", "protection-flash-14-last-hit-pulse3-on.png");

            // Advance past flash duration (0.72s). Protection is 0, so bubble must disappear completely!
            Steps(8);
            Assert.That(Game.Run.Protection, Is.Zero);
            Assert.That(shield.enabled, Is.False, "Spent protection bubble must disappear completely after flashing 3 times.");
            Assert.That(bubbles.All(b => !b.enabled), Is.True, "Hook bubbles must be disabled when protection is depleted.");
            Record("15-LastHit-FlashCompleted-Depleted", "protection-flash-15-last-hit-depleted.png");

            var summary = $"{{\"runId\":\"{Game.Run.RunId}\",\"totalSamples\":{samples.Count},\"samples\":[{string.Join(",", samples)}]}}";
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "protection-flash-sampling-20261024.json"), summary);
            yield return null;
        }
    }
}
 