using System.Collections;
using System.IO;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Checks actual surface illumination, fixed face normals and local ground contact in saved scenes.</summary>
    public sealed class SurfaceLightingIntegrationTests : AbilityTestScene
    {
        private Transform Surface => Game.SceneView.transform.Find("Surface");
        private Light2D Lamp => Surface.Find("Lighting/Lantern Warmth").GetComponent<Light2D>();
        private SpriteRenderer Left => Surface.Find("Ice Left").GetComponent<SpriteRenderer>();
        private SpriteRenderer Right => Surface.Find("Ice Right").GetComponent<SpriteRenderer>();

        /// <summary>Checks isolation from the water/hook, collider-free art and material reuse across twenty restarts.</summary>
        [UnityTest]
        public IEnumerator SavedSurfaceLightingStaysLocalAndDoesNotAllocateMaterialsOnRestart()
        {
            var lighting = Game.SceneView.GetComponent<SurfaceLighting>(); Assert.That(lighting.HasBindings,Is.True);
            var material = Left.sharedMaterial;
            Assert.That(material.shader.name,Is.EqualTo("Fishing/Surface Ice Lit")); Assert.That(material.shader.isSupported,Is.True);
            Assert.That(Right.sharedMaterial,Is.SameAs(material));
            Assert.That(Game.SceneView.GetComponentsInChildren<Collider2D>().Length,Is.Zero);
            Assert.That(Game.SceneView.GetComponentsInChildren<Light2D>().Length,Is.EqualTo(4));
            Assert.That(Lamp.normalMapQuality,Is.EqualTo(Light2D.NormalMapQuality.Accurate));
            Assert.That(Left.sortingLayerName,Is.EqualTo("Surface Lit"));
            Assert.That(Game.Hook.LineMaterial.shader.name,Is.EqualTo("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            for (int i=0; i<20; i++) { Game.BeginRun(); lighting.ApplyLighting(); Assert.That(Left.sharedMaterial,Is.SameAs(material)); }
            Assert.That(Game.SceneView.GetComponentsInChildren<Light2D>().Length,Is.EqualTo(4));
            yield return null;
        }

        /// <summary>Uses real frames to prove warm illumination follows the lamp and leaves the underwater route unchanged.</summary>
        [UnityTest]
        public IEnumerator LanternOnOffAndPositionChangeTheActualNearbyIcePixels()
        {
            FreezeDecoration();
            Vector3 origin = Lamp.transform.position;
            Texture2D off=null,on=null,moved=null;
            try
            {
                Lamp.enabled=false; yield return null; yield return new WaitForEndOfFrame(); off=ScreenCapture.CaptureScreenshotAsTexture();
                Lamp.enabled=true; yield return null; yield return new WaitForEndOfFrame(); on=ScreenCapture.CaptureScreenshotAsTexture();
                int nearby=Changed(off,on,new Vector2(.25f,-.4f),new Vector2(4.2f,1.2f));
                Assert.That(nearby,Is.GreaterThan(40),"The light must change actual nearby surface pixels.");
                Vector3 gain=Gain(off,on,new Vector2(.25f,-.4f),new Vector2(4.2f,1.2f));
                Assert.That(gain.x,Is.GreaterThan(gain.z*1.15f),"The nearby light must have a warmer colour than the sky.");
                Assert.That(Changed(off,on,new Vector2(-3,-8),new Vector2(3,-4)),Is.Zero,"Surface light must not recolour the water.");
                Lamp.transform.position=new Vector3(-2.35f,origin.y,origin.z);
                yield return null; yield return new WaitForEndOfFrame(); moved=ScreenCapture.CaptureScreenshotAsTexture();
                Assert.That(Changed(on,moved,new Vector2(-4.2f,-.4f),new Vector2(-.25f,1.2f)),Is.GreaterThan(30),"Moving the light must move the lit region.");
                File.WriteAllBytes("Docs/Verification/surface-lighting-lamp-off-20261023.png",off.EncodeToPNG());
                File.WriteAllBytes("Docs/Verification/surface-lighting-lamp-on-20261023.png",on.EncodeToPNG());
                File.WriteAllBytes("Docs/Verification/surface-lighting-lamp-moved-20261023.png",moved.EncodeToPNG());
                File.WriteAllText("Docs/Verification/surface-lighting-lamp-pixels-20261023.json",
                    "{\"nearbyChangedPixels\":"+nearby+",\"redGain\":"+(int)gain.x+",\"blueGain\":"+(int)gain.z+",\"underwaterChangedPixels\":0}");
            }
            finally
            {
                Lamp.transform.position=origin; Lamp.enabled=true;
                if(off!=null) Object.Destroy(off); if(on!=null) Object.Destroy(on); if(moved!=null) Object.Destroy(moved);
            }
        }

        /// <summary>Disables highlights to isolate true Light2D normal response, then compares soft contact shadows.</summary>
        [UnityTest]
        public IEnumerator FixedNormalsAndGroundContactsAffectRenderedIceWithoutMovingTheArtwork()
        {
            FreezeDecoration(); Lamp.enabled=false;
            var original=Left.sharedMaterial; var material=new Material(original); Left.sharedMaterial=Right.sharedMaterial=material;
            var ground=Surface.Find("Rear snow shelf").GetComponent<MeshRenderer>();
            var groundOriginal=ground.sharedMaterial; var groundMaterial=new Material(groundOriginal); ground.sharedMaterial=groundMaterial;
            groundMaterial.SetFloat("_ContactStrength",0);
            material.SetFloat("_IceGloss",0); material.SetFloat("_SnowGloss",0); material.SetFloat("_ContactStrength",0);
            Texture2D flat=null,normal=null,contacts=null;
            try
            {
                material.SetFloat("_NormalStrength",0); yield return null; yield return new WaitForEndOfFrame(); flat=ScreenCapture.CaptureScreenshotAsTexture();
                material.SetFloat("_NormalStrength",.65f); yield return null; yield return new WaitForEndOfFrame(); normal=ScreenCapture.CaptureScreenshotAsTexture();
                int normalPixels=Changed(flat,normal,new Vector2(-6,-1.7f),new Vector2(6,.98f));
                Assert.That(normalPixels,Is.GreaterThan(40),"With custom highlights disabled, Light2D must still respond to the fixed face normals.");
                material.SetFloat("_ContactStrength",.24f); groundMaterial.SetFloat("_ContactStrength",.24f);
                yield return null; yield return new WaitForEndOfFrame(); contacts=ScreenCapture.CaptureScreenshotAsTexture();
                foreach (string name in new[] { "Fisherman","Stand","Barrel","Weather Pole" })
                {
                    var point=(Vector2)Surface.Find(name).position;
                    Assert.That(Changed(normal,contacts,point+new Vector2(-1,-.3f),point+new Vector2(1,.15f)),Is.GreaterThan(3),name+" must meet the ice with a visible soft contact.");
                }
                Assert.That(Changed(flat,contacts,new Vector2(-3,-8),new Vector2(3,-4)),Is.Zero);
                File.WriteAllText("Docs/Verification/surface-lighting-normal-pixels-20261023.json","{\"normalChangedPixels\":"+normalPixels+",\"customHighlightsEnabled\":false,\"underwaterChangedPixels\":0}");
            }
            finally
            {
                Left.sharedMaterial=Right.sharedMaterial=original; Object.Destroy(material); Lamp.enabled=true;
                ground.sharedMaterial=groundOriginal; Object.Destroy(groundMaterial);
                if(flat!=null) Object.Destroy(flat); if(normal!=null) Object.Destroy(normal); if(contacts!=null) Object.Destroy(contacts);
            }
        }

        private void FreezeDecoration()
        {
            Game.Ui.Canvas.enabled=false;
            Game.SceneView.GetComponentInChildren<AmbientWaterDetails>().gameObject.SetActive(false);
            Game.SceneView.GetComponent<UnderwaterAtmosphere>().ResetAnimation();
        }

        /// <summary>Checks real sole, stool, stand and barrel contacts, and proves distant mountain pixels cannot show through their snow support.</summary>
        [UnityTest]
        public IEnumerator SurfaceFeetHaveVisibleContactsAndSnowClosesTheMountainGap()
        {
            Until(Fishing.Core.RunState.Surfacing); Steps(40); Game.CameraRig.Follow(Game.Run,0,true); FreezeDecoration();
            var ground=Surface.Find("Rear snow shelf").GetComponent<MeshRenderer>();
            var mountain=Surface.Find("Mountains").GetComponent<SpriteRenderer>();
            var position=mountain.transform.localPosition; var scale=mountain.transform.localScale;
            Assert.That(mountain.sharedMaterial.shader.name,Is.EqualTo("Fishing/Distant Mountains"));
            Assert.That(mountain.sharedMaterial.shader.isSupported,Is.True);
            Assert.That(ground.bounds.max.y,Is.EqualTo(.98f).Within(.001f));
            Assert.That(ground.bounds.min.y,Is.EqualTo(.84f).Within(.001f));
            Assert.That(ground.sortingOrder,Is.GreaterThan(Left.sortingOrder));
            var receivers=new Renderer[]{Left,Right,ground}; var saved=new MaterialPropertyBlock[receivers.Length];
            var block=new MaterialPropertyBlock(); var points=new System.Collections.Generic.List<Vector4>();
            Texture2D off=null,on=null,noMountain=null;
            try
            {
                for(int i=0;i<receivers.Length;i++)
                { saved[i]=new MaterialPropertyBlock(); receivers[i].GetPropertyBlock(saved[i]); receivers[i].GetPropertyBlock(block); block.SetFloat("_ContactStrength",0); receivers[i].SetPropertyBlock(block); }
                yield return null; yield return new WaitForEndOfFrame(); off=ScreenCapture.CaptureScreenshotAsTexture();
                foreach(var receiver in receivers)
                { receiver.GetPropertyBlock(block); block.SetFloat("_ContactStrength",.24f); receiver.SetPropertyBlock(block); }
                yield return null; yield return new WaitForEndOfFrame(); on=ScreenCapture.CaptureScreenshotAsTexture();
                ground.GetPropertyBlock(block); block.GetVectorArray("_GroundContacts",points); Assert.That(points.Count,Is.EqualTo(12));
                var changes=new System.Collections.Generic.List<string>();
                foreach(int index in new[]{4,6,8,11})
                {
                    var contact=points[index]; var centre=new Vector2(contact.x,contact.y);
                    int count=Changed(off,on,centre-new Vector2(contact.z,.10f),centre+new Vector2(contact.z,.10f));
                    Assert.That(count,Is.GreaterThan(5),"Visible support contact "+index);
                    changes.Add("{\"contact\":"+index+",\"changedPixels\":"+count+"}");
                }
                mountain.enabled=false; yield return null; yield return new WaitForEndOfFrame(); noMountain=ScreenCapture.CaptureScreenshotAsTexture();
                // The rounded lip opens to x=±.89 at the top; only the solid support bands may occlude mountains.
                int leak=Changed(on,noMountain,new Vector2(-4.3f,.72f),new Vector2(-1f,.97f))+
                    Changed(on,noMountain,new Vector2(1f,.72f),new Vector2(4.3f,.97f));
                Assert.That(leak,Is.Zero,
                    "The support snow must be opaque: mountain pixels may not leak under the feet or through the rear rim.");
                File.WriteAllBytes("Docs/Verification/grounding-contacts-off-20261025.png",off.EncodeToPNG());
                File.WriteAllBytes("Docs/Verification/grounding-contacts-on-20261025.png",on.EncodeToPNG());
                File.WriteAllText("Docs/Verification/grounding-contact-pixels-20261025.json","{\"samples\":["+string.Join(",",changes)+"],\"mountainLeakPixels\":0}");
                Assert.That(mountain.transform.localPosition,Is.EqualTo(position)); Assert.That(mountain.transform.localScale,Is.EqualTo(scale));
            }
            finally
            {
                mountain.enabled=true;
                for(int i=0;i<receivers.Length;i++) receivers[i].SetPropertyBlock(saved[i]);
                if(off!=null) Object.Destroy(off); if(on!=null) Object.Destroy(on); if(noMountain!=null) Object.Destroy(noMountain);
            }
        }

        private static RectInt Pixels(Texture2D image,Vector2 from,Vector2 to)
        {
            Vector3 a=Camera.main.WorldToScreenPoint(from),b=Camera.main.WorldToScreenPoint(to);
            int x=Mathf.Clamp(Mathf.FloorToInt(a.x),0,image.width),y=Mathf.Clamp(Mathf.FloorToInt(a.y),0,image.height);
            return new RectInt(x,y,Mathf.Clamp(Mathf.CeilToInt(b.x),x,image.width)-x,Mathf.Clamp(Mathf.CeilToInt(b.y),y,image.height)-y);
        }
        private static int Changed(Texture2D first,Texture2D second,Vector2 from,Vector2 to)
        {
            var a=first.GetPixels32(); var b=second.GetPixels32(); var bounds=Pixels(first,from,to); int changed=0;
            for(int y=bounds.yMin;y<bounds.yMax;y++) for(int x=bounds.xMin;x<bounds.xMax;x++)
            { int p=y*first.width+x; if(Mathf.Abs(a[p].r-b[p].r)+Mathf.Abs(a[p].g-b[p].g)+Mathf.Abs(a[p].b-b[p].b)>6) changed++; }
            return changed;
        }
        private static Vector3 Gain(Texture2D first,Texture2D second,Vector2 from,Vector2 to)
        {
            var a=first.GetPixels32(); var b=second.GetPixels32(); var bounds=Pixels(first,from,to); var gain=Vector3.zero;
            for(int y=bounds.yMin;y<bounds.yMax;y++) for(int x=bounds.xMin;x<bounds.xMax;x++)
            { int p=y*first.width+x; gain+=new Vector3(b[p].r-a[p].r,b[p].g-a[p].g,b[p].b-a[p].b); }
            return gain;
        }
    }
}
