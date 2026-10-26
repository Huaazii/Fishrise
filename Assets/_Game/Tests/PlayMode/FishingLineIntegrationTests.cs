using System.Collections;
using System.IO;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Actual line endpoints, damped lateral response, pause/reset and shared-light pixel acceptance.</summary>
    public sealed class FishingLineIntegrationTests : AbilityTestScene
    {
        private LineRenderer Line => Game.Hook.transform.Find("Rope").GetComponent<LineRenderer>();
        private DragInput Input => (DragInput)typeof(GameBootstrap).GetField("_input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
        private Vector3[] Points() { var points=new Vector3[Line.positionCount]; Line.GetPositions(points); return points; }
        private float Bow()
        {
            Vector3 start=Vector3.zero,end=Line.GetPosition(Line.positionCount-1),direction=Vector3.zero;
            float bow=0; bool below=false;
            foreach(var point in Points())
            {
                if(!below && Mathf.Abs(point.y-Game.SceneView.LineGuide.position.y)<.001f) { start=point; direction=end-start; below=true; }
                if(below) { float t=Mathf.Clamp01(Vector3.Dot(point-start,direction)/direction.sqrMagnitude); bow=Mathf.Max(bow,Vector3.Distance(point,start+direction*t)); }
            }
            return bow;
        }
        [UnityTest]
        public IEnumerator HoleCrossingMovesNaturallyAndStaysSmoothAtShallowAndDeepHorizontalLimits()
        {
            Until(RunState.Diving);
            float worst=0,centerCrossing=0;
            float shallowDepth=Game.Hook.transform.Find("Rope Anchor").position.y-Game.Hook.transform.position.y-
                Game.SceneView.LineGuide.position.y+.05f;
            foreach(float depth in new[]{shallowDepth,2f,10f,95f})
            foreach(float x in new[]{0f,-4.2f,4.2f})
            {
                for(int step=0;step<80;step++) Game.Hook.Move(x,depth,1,13.5f,4.5f,.02f);
                var points=Points(); int join=-1;
                for(int i=1;i<points.Length-1;i++) if(Mathf.Abs(points[i].y-Game.SceneView.LineGuide.position.y)<.001f) { join=i; break; }
                Assert.That(join,Is.GreaterThan(0));
                Assert.That(Mathf.Abs(points[join].x-Game.SceneView.LineGuide.position.x),Is.LessThanOrEqualTo(.351f));
                float angle=Vector3.Angle(points[join]-points[join-1],points[join+1]-points[join]);
                worst=Mathf.Max(worst,angle); Assert.That(angle,Is.LessThan(6),"A visible corner must not return at the hole crossing.");
                Assert.That(Vector3.Distance(points[0],Game.SceneView.RodTip.position),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(points[points.Length-1],Game.Hook.transform.Find("Rope Anchor").position),Is.LessThan(.001f));
                for(int i=1;i<points.Length;i++) Assert.That(points[i].y,Is.LessThanOrEqualTo(points[i-1].y+.002f),"The shallow curve must not double back vertically.");
                if(depth==2 && x==0) centerCrossing=points[join].x;
            }
            Assert.That(Mathf.Abs(centerCrossing),Is.GreaterThan(.08f),"A centred hook must not force the line through the old fixed centre.");
            File.WriteAllText("Docs/Verification/line-continuity-geometry-20261023.json","{\"cases\":12,\"worstJoinAngle\":"+worst.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"centerCrossingX\":"+centerCrossing.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
            yield return null;
        }
        [UnityTest]
        public IEnumerator LateralMotionBendsTheLineDampsAfterStoppingAndPauseKeepsEveryPointFixed()
        {
            Until(RunState.Diving); Steps(100); Input.Reset(3.4f); Steps(13);
            Assert.That(Line.positionCount,Is.GreaterThan(10));
            float moving=Bow(); Assert.That(moving,Is.GreaterThan(.025f)); Assert.That(moving,Is.LessThan(.46f));
            Assert.That(Vector3.Distance(Line.GetPosition(0),Game.SceneView.RodTip.position),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(Line.GetPosition(Line.positionCount-1),Game.Hook.transform.Find("Rope Anchor").position),Is.LessThan(.001f));
            foreach(var point in Points()) Assert.That(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y),Is.False);
            Game.PauseRun(); var paused=Points(); Steps(80); CollectionAssert.AreEqual(paused,Points()); Game.ResumeRun();
            Input.Reset(Game.Hook.transform.position.x); Steps(50);
            Assert.That(Bow(),Is.LessThan(moving*.8f+.01f),"Water damping should settle the lateral lag after the hook stops moving sideways.");
            yield return null;
        }
        [UnityTest]
        public IEnumerator TwentyRestartsReuseTheRendererMaterialAndResetTheSameMotion()
        {
            int renderer=Line.GetInstanceID(); var material=Line.sharedMaterial; Vector3[] reference=null;
            Assert.That(material.shader.name,Is.EqualTo("Fishing/Fishing Line")); Assert.That(material.shader.isSupported,Is.True);
            Assert.That(Game.AbilityView.Wave.sharedMaterial,Is.SameAs(Game.Hook.LineMaterial)); Assert.That(material,Is.Not.SameAs(Game.Hook.LineMaterial));
            for(int run=0;run<20;run++)
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Diving); Input.Reset(2.8f); Steps(20);
                Assert.That(Line.GetInstanceID(),Is.EqualTo(renderer)); Assert.That(Line.sharedMaterial,Is.SameAs(material));
                if(reference==null) reference=Points(); else CollectionAssert.AreEqual(reference,Points());
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator RenderedLineRespondsToTheActualLanternAndRemainsVisibleInDeepWater()
        {
            Game.Ui.Canvas.enabled=false;
            foreach(var renderer in Game.SceneView.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled=false;
            foreach(var renderer in Game.Hook.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled=false;
            var camera=Camera.main; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.02f,.06f,.1f);
            camera.orthographicSize=4; camera.transform.position=new Vector3(0,0,-10);
            var lamp=Game.SceneView.transform.Find("Surface/Lighting/Lantern Warmth").GetComponent<Light2D>();
            Texture2D off=null,on=null,deep=null;
            try
            {
                lamp.enabled=false; Game.Hook.Show(Color.white,false); yield return null; yield return new WaitForEndOfFrame(); off=ScreenCapture.CaptureScreenshotAsTexture();
                lamp.enabled=true; Game.Hook.Show(Color.white,false); yield return null; yield return new WaitForEndOfFrame(); on=ScreenCapture.CaptureScreenshotAsTexture();
                var a=off.GetPixels32(); var b=on.GetPixels32(); int changed=0;
                for(int i=0;i<a.Length;i++) if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>6) changed++;
                Assert.That(changed,Is.GreaterThan(8),"Real light toggling must change the line's own rendered pixels.");
                camera.transform.position=new Vector3(0,-45,-10); camera.orthographicSize=10;
                Game.Hook.Move(0,60,1,20,4.5f,.02f); Game.Hook.Show(Color.white,false);
                yield return null; yield return new WaitForEndOfFrame(); deep=ScreenCapture.CaptureScreenshotAsTexture();
                var pixels=deep.GetPixels32(); int rows=0;
                for(int y=0;y<deep.height;y++)
                {
                    bool visible=false;
                    for(int x=deep.width/2-12;x<deep.width/2+12;x++)
                    { var p=pixels[y*deep.width+x]; if(p.g>30 && p.b>40) { visible=true; break; } }
                    if(visible) rows++;
                }
                Assert.That(rows,Is.GreaterThan(deep.height*.85f),"The deep-water line must remain visible down the actual portrait image.");
                File.WriteAllText("Docs/Verification/line-lighting-pixels-20261023.json","{\"lampChangedPixels\":"+changed+",\"visibleDeepRows\":"+rows+",\"height\":"+deep.height+"}");
            }
            finally { lamp.enabled=true; if(off!=null) Object.Destroy(off); if(on!=null) Object.Destroy(on); if(deep!=null) Object.Destroy(deep); }
        }
    }
}
