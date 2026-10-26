using System.Collections;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Saved 2D water rendering, world anchoring and pause/restart material lifetime acceptance.</summary>
    public sealed class UnderwaterIntegrationTests : AbilityTestScene
    {
        /// <summary>Checks shared ice materials and original collider-free decoration in the saved prefab.</summary>
        [UnityTest]
        public IEnumerator SavedWaterUsesSupportedShadersAndOneSharedIceMaterial()
        {
            var view = Game.SceneView;
            var atmosphere = view.GetComponent<UnderwaterAtmosphere>();
            Assert.That(atmosphere.HasBindings, Is.True);
            var water = view.transform.Find("Underwater/Water").GetComponent<SpriteRenderer>();
            var walls = view.transform.Find("Underwater/Walls").GetComponentsInChildren<SpriteRenderer>();
            foreach (string name in new[] { "Left Backing", "Right Backing" })
                Assert.That(view.transform.Find("Underwater/"+name).GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(water.sharedMaterial.shader.name, Is.EqualTo("Fishing/Water Depth"));
            Assert.That(water.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(water.sharedMaterial.GetTexture("_FlowTex"), Is.Not.Null);
            Assert.That(water.sharedMaterial.GetFloat("_DepthRange"), Is.EqualTo(100).Within(.001f));
            Assert.That(walls[0].sharedMaterial.shader.name, Is.EqualTo("Fishing/Ice Caustics"));
            Assert.That(walls[0].sharedMaterial.shader.isSupported, Is.True);
            foreach (var wall in walls)
            {
                Assert.That(wall.sharedMaterial, Is.SameAs(walls[0].sharedMaterial));
                Assert.That(wall.sharedMaterial.GetTexture("_CausticA"), Is.Not.Null);
                Assert.That(wall.sharedMaterial.GetTexture("_FlowTex"), Is.Not.Null);
                Assert.That(wall.sortingLayerName, Is.EqualTo("Underwater Ice"));
                Assert.That(wall.sortingOrder, Is.GreaterThan(water.sortingOrder));
            }
            Assert.That(view.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            yield return null;
        }

        /// <summary>Checks saved asymmetric silhouettes, scale variety and the unchanged central swim corridor.</summary>
        [UnityTest]
        public IEnumerator IceBreaksTheRepeatedLadderWithoutEnteringThePlayableRoute()
        {
            var root = Game.SceneView.transform.Find("Underwater/Walls");
            float shortest = float.MaxValue, tallest = 0;
            int asymmetric = 0, recessed = 0, visible = 0;
            for (int i = 0; i < 27; i++)
            {
                var left = root.Find("Left " + i.ToString("00")).GetComponent<SpriteRenderer>();
                var right = root.Find("Right " + i.ToString("00")).GetComponent<SpriteRenderer>();
                Assert.That(left.bounds.max.x, Is.LessThanOrEqualTo(-4.719f));
                Assert.That(right.bounds.min.x, Is.GreaterThanOrEqualTo(4.719f));
                if (Mathf.Abs(left.bounds.max.x + right.bounds.min.x) > .15f) asymmetric++;
                foreach (var wall in new[] { left, right })
                {
                    shortest = Mathf.Min(shortest, wall.bounds.size.y); tallest = Mathf.Max(tallest, wall.bounds.size.y);
                    float inner = wall == left ? -wall.bounds.max.x : wall.bounds.min.x;
                    if (inner > 6) recessed++;
                    if (inner < 5.2f) visible++;
                }
            }
            Assert.That(tallest-shortest, Is.GreaterThan(2));
            Assert.That(asymmetric, Is.GreaterThan(15));
            Assert.That(recessed, Is.GreaterThan(8)); Assert.That(visible, Is.GreaterThan(8));
            yield return null;
        }

        /// <summary>Checks imported silhouette roles, proportional sizing and inward-facing right-hand variants.</summary>
        [UnityTest]
        public IEnumerator ThreeNewIceShapesHaveSparseAccentsAndPreserveTheirProportionsOnBothSides()
        {
            var walls = Game.SceneView.transform.Find("Underwater/Walls");
            foreach (string side in new[] { "Left ", "Right " })
            {
                int bases = 0, broad = 0, shoulders = 0;
                for (int i = 0; i < 27; i++)
                {
                    var wall = walls.Find(side + i.ToString("00")).GetComponent<SpriteRenderer>();
                    Assert.That(wall.flipX, Is.EqualTo(side == "Right "));
                    Assert.That(wall.transform.localScale.x, Is.EqualTo(wall.transform.localScale.y).Within(.0001f));
                    switch (wall.sprite.name)
                    {
                        case "IceWall_A_Base_Left": bases++; Assert.That(wall.sortingOrder, Is.EqualTo(6)); break;
                        case "IceWall_B_Outcrop_Left": broad++; Assert.That(wall.sortingOrder, Is.EqualTo(7)); break;
                        case "IceWall_C_Shoulder_Left": shoulders++; Assert.That(wall.sortingOrder, Is.EqualTo(7)); break;
                        default: Assert.Fail("Unexpected ice source: " + wall.sprite.name); break;
                    }
                }
                Assert.That(bases, Is.EqualTo(18));
                Assert.That(broad + shoulders, Is.EqualTo(9));
                Assert.That(broad, Is.GreaterThan(0)); Assert.That(shoulders, Is.GreaterThan(0));
            }
            yield return null;
        }

        /// <summary>Uses a striped background to prove actual refraction and isolates animated reflection from caustics.</summary>
        [UnityTest]
        public IEnumerator RenderedIceRefractsTheBackgroundAndMovesReflectionsOnlyInsideItsSilhouette()
        {
            Game.Ui.Canvas.enabled = false;
            Game.SceneView.transform.Find("Underwater/Distant Fish").gameObject.SetActive(false);
            // This comparison isolates ice optics; intentional moving bubbles must not alter its control region.
            Game.SceneView.GetComponentInChildren<AmbientWaterDetails>().gameObject.SetActive(false);
            var walls = Game.SceneView.transform.Find("Underwater/Walls");
            foreach (var wall in walls.GetComponentsInChildren<SpriteRenderer>()) wall.enabled = wall.name == "Left 00";
            var ice = walls.Find("Left 00").GetComponent<SpriteRenderer>().sharedMaterial;
            var atmosphere = Game.SceneView.GetComponent<UnderwaterAtmosphere>(); atmosphere.ResetAnimation();
            Camera.main.transform.position = new Vector3(0,-6,-10);
            var texture = new Texture2D(256,256,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear };
            var colors = new Color[256*256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                colors[y*256+x] = ((x/8+y/8)%2 == 0) ? new Color(.12f,.45f,.8f) : new Color(.65f,.8f,.9f);
            texture.SetPixels(colors); texture.Apply();
            var sprite = Sprite.Create(texture,new Rect(0,0,256,256),Vector2.one*.5f,32);
            var pattern = new GameObject("Refraction acceptance pattern");
            var background = pattern.AddComponent<SpriteRenderer>(); background.sprite = sprite; background.sortingOrder = -20;
            pattern.transform.position = new Vector3(0,-6,0); pattern.transform.localScale = new Vector3(2,3,1);
            ice.SetFloat("_CausticStrength",0); ice.SetFloat("_TextureDistortion",0); ice.SetFloat("_ReflectionStrength",0);
            try
            {
                ice.SetFloat("_RefractionStrength",0); yield return new WaitForEndOfFrame();
                var straight = ScreenCapture.CaptureScreenshotAsTexture();
                ice.SetFloat("_RefractionStrength",.006f); yield return new WaitForEndOfFrame();
                var bent = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    var capture = Shader.GetGlobalTexture("_CameraSortingLayerTexture");
                    Assert.That(capture, Is.Not.Null);
                    Assert.That(capture.width, Is.EqualTo(Screen.width/2).Within(1));
                    Assert.That(capture.height, Is.EqualTo(Screen.height/2).Within(1));
                    Assert.That(ChangedPixels(straight,bent,0,.15f), Is.GreaterThan(40), "Background must actually bend inside ice.");
                    Assert.That(ChangedPixels(straight,bent,.35f,.65f), Is.Zero, "Refraction must leave the central route alone.");
                }
                finally { Object.Destroy(straight); Object.Destroy(bent); }
                ice.SetFloat("_Transmission",0); ice.SetFloat("_ReflectionStrength",.24f);
                yield return new WaitForEndOfFrame(); var first = ScreenCapture.CaptureScreenshotAsTexture();
                atmosphere.Tick(2.4f); yield return new WaitForEndOfFrame(); var second = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Assert.That(ChangedPixels(first,second,0,.15f), Is.GreaterThan(20), "Reflected highlights must move independently of caustics.");
                    Assert.That(ChangedPixels(first,second,.35f,.65f), Is.Zero);
                }
                finally { Object.Destroy(first); Object.Destroy(second); }
            }
            finally { Object.Destroy(pattern); Object.Destroy(sprite); Object.Destroy(texture); }
        }

        private static int ChangedPixels(Texture2D first, Texture2D second, float fromX, float toX)
        {
            var a = first.GetPixels32(); var b = second.GetPixels32(); int count = 0;
            for (int y = 0; y < first.height; y++)
                for (int x = Mathf.CeilToInt(first.width*fromX); x < first.width*toX; x++)
                {
                    int p = y*first.width+x;
                    if (Mathf.Abs(a[p].r-b[p].r)+Mathf.Abs(a[p].g-b[p].g)+Mathf.Abs(a[p].b-b[p].b) > 5) count++;
                }
            return count;
        }

        /// <summary>Samples actual render pixels for depth attenuation and a stationary world point across camera motion.</summary>
        [UnityTest]
        public IEnumerator RenderedWaterDarkensWithDepthAndDoesNotMoveItsGradientWithTheCamera()
        {
            Game.Ui.Canvas.enabled = false;
            Game.SceneView.transform.Find("Underwater/Distant Fish").gameObject.SetActive(false);
            var camera = Camera.main;
            var samples = new Color[3];
            float[] depths = { 4, 40, 85 };
            for (int i = 0; i < depths.Length; i++)
            {
                var point = new Vector3(0, -depths[i], 0);
                camera.transform.position = new Vector3(0, point.y, -10);
                yield return new WaitForEndOfFrame();
                samples[i] = Pixel(camera, point);
            }
            Assert.That(samples[0].grayscale, Is.GreaterThan(samples[1].grayscale + .035f));
            Assert.That(samples[1].grayscale, Is.GreaterThan(samples[2].grayscale + .025f));
            var fixedPoint = new Vector3(-.8f, -32, 0);
            camera.transform.position = new Vector3(0, -28, -10);
            yield return new WaitForEndOfFrame(); var first = Pixel(camera, fixedPoint);
            camera.transform.position = new Vector3(0, -35, -10);
            yield return new WaitForEndOfFrame(); var second = Pixel(camera, fixedPoint);
            Assert.That(Mathf.Abs(first.r-second.r)+Mathf.Abs(first.g-second.g)+Mathf.Abs(first.b-second.b), Is.LessThan(.025f));
        }

        /// <summary>Runs real presentation frames, freezes pause and destroys owned materials when the scene unloads.</summary>
        [UnityTest]
        public IEnumerator FlowAndCausticsFreezeOnPauseResetOnRestartAndReleaseTheirMaterials()
        {
            Until(RunState.Diving);
            var atmosphere = Game.SceneView.GetComponent<UnderwaterAtmosphere>();
            var water = Game.SceneView.transform.Find("Underwater/Water").GetComponent<SpriteRenderer>().sharedMaterial;
            var ice = Game.SceneView.transform.Find("Underwater/Walls").GetComponentInChildren<SpriteRenderer>().sharedMaterial;
            Game.enabled = true;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(atmosphere.AnimationTime, Is.GreaterThan(0));
            Game.PauseRun(); float phase = atmosphere.AnimationTime;
            float blend = ice.GetFloat("_CausticBlend");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(atmosphere.AnimationTime, Is.EqualTo(phase));
            Assert.That(water.GetFloat("_WaterTime"), Is.EqualTo(phase));
            Assert.That(ice.GetFloat("_CausticBlend"), Is.EqualTo(blend));
            Game.ResumeRun(); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(atmosphere.AnimationTime, Is.GreaterThan(phase));
            Game.BeginRun(); Assert.That(atmosphere.AnimationTime, Is.Zero);
            Assert.That(water.GetFloat("_WaterTime"), Is.Zero);
            Assert.That(ice.GetTexture("_CausticA").name, Is.EqualTo("caust00"));
            SceneManager.LoadScene("Menu"); yield return null; yield return null;
            Assert.That(water == null && ice == null, Is.True, "Runtime materials must leave with their environment.");
        }

        /// <summary>Measures both old backing boundaries at three portrait ratios without ice obscuring the water.</summary>
        [UnityTest]
        public IEnumerator WaterStaysContinuousAcrossBothOldSideStripsAtThreePortraitRatios()
        {
            Game.Ui.Canvas.enabled = false;
            Game.SceneView.transform.Find("Underwater/Walls").gameObject.SetActive(false);
            Game.SceneView.transform.Find("Underwater/Distant Fish").gameObject.SetActive(false);
            var camera = Camera.main;
            foreach (int height in new[] { 800,975,1050 })
            {
                Resolution(height); yield return null; yield return null;
                Game.CameraRig.Follow(null,0,true);
                foreach (float depth in new[] { 8f,45f,85f })
                {
                    camera.transform.position = new Vector3(0,-depth,-10);
                    yield return new WaitForEndOfFrame(); var image = ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        foreach (float side in new[] { -1f,1f })
                        {
                            var inner = Pixel(image,camera,new Vector3(side*4.66f,-depth,0));
                            var outer = Pixel(image,camera,new Vector3(side*4.74f,-depth,0));
                            float difference = Mathf.Abs(inner.r-outer.r)+Mathf.Abs(inner.g-outer.g)+Mathf.Abs(inner.b-outer.b);
                            Assert.That(difference,Is.LessThan(.035f),"Visible water step at former backing edge");
                        }
                    }
                    finally { Object.Destroy(image); }
                }
            }
        }

        private static Color Pixel(Camera camera, Vector3 world)
        {
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            var color = Pixel(image,camera,world);
            Object.Destroy(image); return color;
        }
        private static Color Pixel(Texture2D image, Camera camera, Vector3 world)
        {
            var point = camera.WorldToScreenPoint(world);
            return image.GetPixel(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
        }
    }
}
