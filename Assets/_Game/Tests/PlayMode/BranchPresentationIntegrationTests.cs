using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Fishing.Core;
using Fishing.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Fishing.Tests
{
    /// <summary>Actual pooled tether geometry, pause/reset, and metal/nylon responses to the scene lights.</summary>
    public sealed class BranchPresentationIntegrationTests : AbilityTestScene
    {
        private static LineRenderer Line(BranchHookView branch) => branch.GetComponent<LineRenderer>();
        private static SpriteRenderer Art(BranchHookView branch) => branch.transform.Find("Branch art").GetComponent<SpriteRenderer>();
        private static Vector3[] Points(BranchHookView branch)
        { var points=new Vector3[Line(branch).positionCount]; Line(branch).GetPositions(points); return points; }
        private static float Bow(BranchHookView branch)
        {
            var points=Points(branch); Vector3 start=points[0],direction=points[points.Length-1]-start; float bow=0;
            foreach(var point in points)
            {
                float t=Mathf.Clamp01(Vector3.Dot(point-start,direction)/direction.sqrMagnitude);
                bow=Mathf.Max(bow,Vector3.Distance(point,start+direction*t));
                Assert.That(float.IsNaN(point.x)||float.IsInfinity(point.x)||float.IsNaN(point.y)||float.IsInfinity(point.y),Is.False);
            }
            return bow;
        }
        /// <summary>Checks entire hook/fish silhouettes and every tether point through the ice, including full-speed and coarse steps.</summary>
        [UnityTest]
        public IEnumerator LoadedBranchesFoldThroughTheOpeningBeforeSpreadingAboveTheIce()
        {
            var puffer=UnityEditor.AssetDatabase.LoadAssetAtPath<FishDefinition>("Assets/_Game/Configs/Fish_puffer.asset");
            var input=(DragInput)typeof(GameBootstrap).GetField("_input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
            int routes=0, samples=0;
            foreach(float x in new[]{-3.9f,0,3.9f}) foreach(bool full in new[]{false,true}) foreach(float dt in new[]{.02f,.08f})
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending);
                for(int i=0;i<(full?10:1);i++) AtHead(Blue,Game.Hook.transform.position+Vector3.up*.13f);
                Steps(1); Game.DeployBranches(); Steps(20);
                foreach(var branch in Game.AbilityView.Branches)
                    for(int i=0;i<3;i++) AtHead(i==1?Blue:puffer,branch.transform.position+Vector3.up*.325f);
                Steps(1); Assert.That(Game.Run.BranchCaptureCount,Is.EqualTo(6));
                var records=Game.Run.Captures.ToArray(); input.Reset(x);
                int guard=0;
                while(Game.Run.State==RunState.Ascending && guard++<6000)
                {
                    Game.AdvanceGameplay(dt);
                    if(Game.Run.State!=RunState.Ascending) break;
                    foreach(var branch in Game.AbilityView.Branches) Check(Art(branch).bounds,"Ascending hook");
                    foreach(var record in records.Where(r=>r.HookId>0))
                        if(Game.Spawner.TryGetOwned(record.FishInstanceId,out var fish)) Check(fish.Body.bounds,"Ascending catch");
                }
                Assert.That(Game.Run.State,Is.EqualTo(RunState.Surfacing));
                Assert.That(Game.Hook.transform.position.x,Is.EqualTo(x).Within(.02f),"The gameplay main hook must retain its route.");
                Assert.That(Game.AbilityView.Branches.All(b=>b.gameObject.activeSelf),Is.True,"Branches must visibly leave the water before being pooled.");
                var emerged=new bool[Game.AbilityView.Branches.Count];
                guard=0;
                while(Game.Run.State==RunState.Surfacing && guard++<200)
                {
                    for(int branchIndex=0;branchIndex<Game.AbilityView.Branches.Count;branchIndex++)
                    {
                        var branch=Game.AbilityView.Branches[branchIndex];
                        Assert.That(branch.GetComponent<CircleCollider2D>().enabled,Is.False);
                        foreach(var front in Game.Ui.Surface.AirHook.GetComponentsInChildren<SpriteRenderer>())
                        {
                            var line=Line(branch);
                            int backLayer=SortingLayer.GetLayerValueFromID(line.sortingLayerID),frontLayer=SortingLayer.GetLayerValueFromID(front.sortingLayerID);
                            Assert.That(backLayer<frontLayer || backLayer==frontLayer && line.sortingOrder<front.sortingOrder,Is.True,
                                "The returning branch tether must stay behind the main hook's opaque metal.");
                        }
                        Check(Art(branch).bounds,"Airborne branch");
                        emerged[branchIndex]|=Art(branch).bounds.min.y>=1.02f;
                        foreach(var point in Points(branch))
                            if(point.y> -1.4f && point.y<.98f) Assert.That(Mathf.Abs(point.x),Is.LessThan(.74f),"Tether crossing");
                    }
                    foreach(var renderer in Game.GetComponentsInChildren<MeshRenderer>())
                        if(renderer.name.StartsWith("Airborne fish ") && renderer.gameObject.activeSelf) Check(renderer.bounds,"Airborne catch");
                    if(x==0 && !full && dt==.02f && guard==10)
                    {
                        var pause=typeof(GameBootstrap).GetMethod("OnApplicationPause",BindingFlags.Instance|BindingFlags.NonPublic);
                        var positions=Game.AbilityView.Branches.Select(b=>b.transform.position).ToArray();
                        var lines=Game.AbilityView.Branches.Select(Points).ToArray();
                        pause.Invoke(Game,new object[]{true}); Steps(20);
                        CollectionAssert.AreEqual(positions,Game.AbilityView.Branches.Select(b=>b.transform.position).ToArray());
                        for(int i=0;i<lines.Length;i++) CollectionAssert.AreEqual(lines[i],Points(Game.AbilityView.Branches[i]));
                        pause.Invoke(Game,new object[]{false});
                    }
                    Game.AdvanceGameplay(dt); samples++;
                }
                Assert.That(Game.Run.State,Is.EqualTo(RunState.Result));
                CollectionAssert.AreEqual(records,Game.Run.Captures);
                Assert.That(emerged.All(visible=>visible),Is.True,"Both branches must fully emerge before pooling.");
                Assert.That(Game.AbilityView.Branches.All(b=>!b.gameObject.activeSelf),Is.True);
                routes++;
            }
            File.WriteAllText("Docs/Verification/branch-exit-routes-20261025.json","{\"routes\":"+routes+",\"surfaceSamples\":"+samples+",\"branchCatchesPerRun\":6}");
            yield return null;

            void Check(Bounds bounds,string label)
            {
                if(bounds.max.y<=-1.4f || bounds.min.y>=1.02f) return;
                Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(-.74f),label+" left side "+bounds);
                Assert.That(bounds.max.x,Is.LessThanOrEqualTo(.74f),label+" right side "+bounds);
            }
        }
        /// <summary>Checks full-speed folding together with the actual camera, Android reeling, portrait ratios and late full-load changes.</summary>
        [UnityTest]
        public IEnumerator AcceleratedCameraKeepsFoldingBranchesVisibleAcrossPortraits()
        {
            var camera=Camera.main; var rows=new System.Collections.Generic.List<string>();
            var input=(DragInput)typeof(GameBootstrap).GetField("_input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
            var puffer=UnityEditor.AssetDatabase.LoadAssetAtPath<FishDefinition>("Assets/_Game/Configs/Fish_puffer.asset");
            using(new ReelingComparisonScope(true,false))
            foreach(int height in new[]{800,975,1050,2670}) foreach(float x in new[]{-3.9f,0,3.9f}) foreach(bool late in new[]{false,true})
            {
                camera.aspect=height==2670 ? 1200f/2670 : 450f/height;
                float dt=height==1050 ? .08f : .02f;
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending);
                for(int i=0;i<(late?9:10);i++) AtHead(Blue,Game.Hook.transform.position+Vector3.up*.13f);
                Steps(1); Game.DeployBranches(); Steps(20);
                foreach(var branch in Game.AbilityView.Branches)
                    for(int i=0;i<3;i++) AtHead(i==1?Blue:puffer,branch.transform.position+Vector3.up*.325f);
                Steps(1); Assert.That(Game.Run.BranchCaptureCount,Is.EqualTo(6));
                Game.Run.Reeling.GrantGrace(100); input.Reset(x); Game.CameraRig.Follow(Game.Run,0,true); Game.SceneView.FollowCamera(camera);
                double foldStart=-1,foldEnd=-1; float baseline=-1,maxZoomRate=0; int guard=0;
                while(Game.Run.State==RunState.Ascending && guard++<6000)
                {
                    if(late && !Game.Run.IsFull && Game.Run.Depth<=9)
                        AtHead(Blue,Game.Hook.Head.transform.TransformPoint(Game.Hook.Head.offset)+Vector3.up*.1f);
                    float width=camera.orthographicSize*camera.aspect;
                    Game.AdvanceGameplay(dt); Game.CameraRig.Follow(Game.Run,dt); Game.SceneView.FollowCamera(camera);
                    Assert.That(Game.AbilityView.Branches.All(b=>b.gameObject.activeSelf),Is.True,"A late full-load HUD refresh must not pool underwater branches.");
                    float score=0;
                    for(int i=0;i<Game.AbilityView.Branches.Count;i++)
                        score+=Mathf.Abs(Game.AbilityView.Branches[i].transform.position.x-(i==0 ? -.12f : .12f));
                    if(Game.Run.Depth<35 && baseline<0) baseline=score;
                    if(baseline>0 && score<baseline*.96f && foldStart<0) foldStart=Game.Run.GameTime;
                    if(foldStart>=0 && score<.20f && foldEnd<0) foldEnd=Game.Run.GameTime;
                    if(Game.Run.Depth<30) maxZoomRate=Mathf.Max(maxZoomRate,Mathf.Abs(camera.orthographicSize*camera.aspect-width)/dt);
                    if(Game.Run.Depth<7)
                    {
                        foreach(var branch in Game.AbilityView.Branches)
                        {
                            var bounds=Art(branch).bounds; var a=camera.WorldToViewportPoint(bounds.min); var b=camera.WorldToViewportPoint(bounds.max);
                            Assert.That(a.x,Is.GreaterThan(.015f)); Assert.That(b.x,Is.LessThan(.985f));
                            Assert.That(a.y,Is.GreaterThan(.015f),"Accelerated view h="+height+" x="+x+" late="+late+
                                " depth="+Game.Run.Depth+" hookY="+Game.Hook.transform.position.y+" branchMinY="+bounds.min.y+
                                " cameraY="+camera.transform.position.y+" size="+camera.orthographicSize+" state="+Game.Run.State);
                            Assert.That(b.y,Is.LessThan(.9f));
                        }
                    }
                }
                Assert.That(Game.Run.State,Is.EqualTo(RunState.Surfacing));
                Assert.That(Game.Run.IsFull,Is.True,"Late full scenario h="+height+" x="+x+" late="+late+" main="+Game.Run.MainCaptureCount);
                Assert.That(foldStart,Is.GreaterThanOrEqualTo(0)); Assert.That(foldEnd-foldStart,Is.GreaterThan(.35),"Folding needs readable time even during the sprint.");
                Assert.That(maxZoomRate,Is.LessThan(3f));
                var records=Game.Run.Captures.ToArray(); var position=camera.transform.position; float size=camera.orthographicSize;
                for(int i=0;i<30;i++)
                {
                    Game.AdvanceGameplay(.02f); Game.CameraRig.Follow(Game.Run,.02f); Game.SceneView.FollowCamera(camera);
                    Assert.That(Vector3.Distance(position,camera.transform.position),Is.LessThan(.02f));
                    Assert.That(camera.orthographicSize,Is.EqualTo(size).Within(.001f));
                }
                CollectionAssert.AreEqual(records,Game.Run.Captures);
                rows.Add("{\"height\":"+height+",\"x\":"+x.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"late\":"+(late?"true":"false")+
                    ",\"foldSeconds\":"+(foldEnd-foldStart).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"maxZoomRate\":"+maxZoomRate.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
            }
            camera.ResetAspect();
            foreach(string side in new[]{"Left","Right"})
            {
                var relief=Game.SceneView.transform.Find("Surface/"+side+" ice ledges");
                Assert.That(relief.GetComponentsInChildren<Collider2D>(),Is.Empty);
                var bounds=relief.GetComponent<MeshRenderer>().bounds;
                Assert.That(side=="Left" ? -bounds.max.x : bounds.min.x,Is.GreaterThanOrEqualTo(.79f));
            }
            File.WriteAllText("Docs/Verification/fast-camera-branch-routes-20261025.json","{\"paths\":"+rows.Count+",\"rows\":["+string.Join(",",rows)+"]}");
            yield return null;
        }
        [UnityTest]
        public IEnumerator TethersSagWithPinnedEndsAndOnlyTheArtTiltsWhilePauseFreezesGeometry()
        {
            Until(RunState.Ascending); Game.DeployBranches(); Steps(30);
            var input=(DragInput)typeof(GameBootstrap).GetField("_input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Game);
            input.Reset(3.4f); Steps(8);
            foreach(var branch in Game.AbilityView.Branches)
            {
                Assert.That(Line(branch).positionCount,Is.EqualTo(13)); Assert.That(Bow(branch),Is.InRange(.025f,.241f));
                var points=Points(branch);
                Assert.That(Vector3.Distance(points[0],Game.Hook.transform.position+Vector3.up*.12f),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(points[points.Length-1],branch.transform.position+Vector3.up*.12f),Is.LessThan(.001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,Art(branch).transform.localEulerAngles.z)),Is.InRange(.1f,7.01f));
                Assert.That(branch.transform.rotation,Is.EqualTo(Quaternion.identity));
                Assert.That(branch.GetComponent<CircleCollider2D>().offset,Is.EqualTo(Vector2.zero));
            }
            Game.PauseRun(); var frozen=Game.AbilityView.Branches.Select(Points).ToArray();
            var rotations=Game.AbilityView.Branches.Select(b=>Art(b).transform.localRotation).ToArray();
            Steps(100); yield return null;
            for(int i=0;i<2;i++)
            { CollectionAssert.AreEqual(frozen[i],Points(Game.AbilityView.Branches[i])); Assert.That(Art(Game.AbilityView.Branches[i]).transform.localRotation,Is.EqualTo(rotations[i])); }
            Game.ResumeRun(); input.Reset(Game.Hook.transform.position.x); Steps(60);
            foreach(var branch in Game.AbilityView.Branches)
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(0,Art(branch).transform.localEulerAngles.z)),Is.LessThan(.1f));
        }
        [UnityTest]
        public IEnumerator TwentyRestartsReuseMaterialsAndRenderersAndResetAllBranchMotion()
        {
            var views=Game.AbilityView.Branches; var line=Line(views[0]); var metal=Art(views[0]).sharedMaterial;
            int[] identities=views.Select(b=>Line(b).GetInstanceID()).ToArray(); Vector3[][] reference=null;
            Assert.That(line.sharedMaterial,Is.SameAs(Game.Hook.RopeMaterial));
            Assert.That(metal.shader.name,Is.EqualTo("Fishing/Hook Metal")); Assert.That(metal.shader.isSupported,Is.True);
            Assert.That(Game.AbilityView.Wave.sharedMaterial,Is.SameAs(Game.Hook.LineMaterial));
            for(int run=0;run<20;run++)
            {
                Game.BeginRun(); Game.Spawner.Clear(); Until(RunState.Ascending); Game.DeployBranches();
                foreach(var branch in views)
                {
                    Assert.That(Art(branch).transform.localRotation,Is.EqualTo(Quaternion.identity));
                    Assert.That(Points(branch).All(p=>p==Points(branch)[0]),Is.True,"Deployment must erase the previous run's tether.");
                }
                Steps(30); var current=views.Select(Points).ToArray();
                if(reference==null) reference=current; else for(int i=0;i<2;i++) CollectionAssert.AreEqual(reference[i],current[i]);
                CollectionAssert.AreEqual(identities,views.Select(b=>Line(b).GetInstanceID()));
                foreach(var branch in views)
                {
                    Assert.That(Art(branch).sharedMaterial,Is.SameAs(metal)); Assert.That(Line(branch).sharedMaterial,Is.SameAs(line.sharedMaterial));
                    Assert.That(Line(branch).startWidth,Is.EqualTo(.025f)); Assert.That(Art(branch).color,Is.EqualTo(Color.white));
                }
            }
            yield return null;
        }
        private static int Difference(Texture2D a,Texture2D b,bool left)
        {
            var x=a.GetPixels32(); var y=b.GetPixels32(); int count=0;
            for(int i=0;i<x.Length;i++)
                if((i%a.width<a.width/2)==left && Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>6) count++;
            return count;
        }
        [UnityTest]
        public IEnumerator ActualLanternMovesMetalHighlightsAndLightsTethersWhileDeepHooksStayVisible()
        {
            Until(RunState.Ascending); Game.DeployBranches(); Steps(30); Game.Ui.Canvas.enabled=false;
            foreach(var renderer in Game.SceneView.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled=false;
            foreach(var renderer in Game.Hook.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
            var camera=Camera.main; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.02f,.06f,.1f);
            camera.orthographicSize=4; camera.transform.position=new Vector3(0,0,-10);
            var lamp=Game.SceneView.transform.Find("Surface/Lighting/Lantern Warmth").GetComponent<Light2D>();
            var original=lamp.transform.position; var images=new System.Collections.Generic.List<Texture2D>();
            Texture2D Capture(string name)
            {
                var image=ScreenCapture.CaptureScreenshotAsTexture(); images.Add(image);
                File.WriteAllBytes("Docs/Verification/branch-lighting-"+name+"-20261023.png",image.EncodeToPNG()); return image;
            }
            void Position(float depth)
            {
                for(int index=0;index<2;index++)
                {
                    var branch=Game.AbilityView.Branches[index]; var origin=new Vector3(0,-depth,0); branch.Deploy(origin);
                    for(int i=0;i<40;i++) branch.Tick(origin,new Vector2(index==0 ? -1.8f:1.8f,.25f),4.5f,.25f,.08f,true,.02f);
                }
            }
            try
            {
                Position(0); foreach(var b in Game.AbilityView.Branches) Line(b).enabled=false;
                lamp.enabled=false; yield return null; yield return new WaitForEndOfFrame(); var off=Capture("metal-off");
                lamp.enabled=true; yield return null; yield return new WaitForEndOfFrame(); var on=Capture("metal-on");
                int right=Difference(off,on,false),left=Difference(off,on,true); Assert.That(right,Is.GreaterThan(20)); Assert.That(right,Is.GreaterThan(left));
                lamp.transform.position=new Vector3(-original.x,original.y,original.z);
                yield return null; yield return new WaitForEndOfFrame(); var moved=Capture("metal-moved");
                int movedLeft=Difference(off,moved,true); Assert.That(movedLeft,Is.GreaterThan(20)); Assert.That(movedLeft,Is.GreaterThan(Difference(off,moved,false)));
                foreach(var b in Game.AbilityView.Branches) { Art(b).enabled=false; Line(b).enabled=true; }
                lamp.transform.position=original; lamp.enabled=false;
                yield return null; yield return new WaitForEndOfFrame(); var tetherOff=Capture("tether-off");
                lamp.enabled=true; yield return null; yield return new WaitForEndOfFrame(); var tetherOn=Capture("tether-on");
                int tether=Difference(tetherOff,tetherOn,true)+Difference(tetherOff,tetherOn,false); Assert.That(tether,Is.GreaterThan(8));
                Position(75); camera.transform.position=new Vector3(0,-75,-10);
                foreach(var b in Game.AbilityView.Branches) { Art(b).enabled=true; Line(b).enabled=false; }
                yield return null; yield return new WaitForEndOfFrame(); var deep=Capture("metal-deep");
                int visible=deep.GetPixels32().Count(p=>p.r>15 && p.g>25 && p.b>35); Assert.That(visible,Is.GreaterThan(60));
                File.WriteAllText("Docs/Verification/branch-lighting-pixels-20261023.json","{\"metalLampRightPixels\":"+right+",\"metalLampLeftPixels\":"+left+",\"metalMovedLeftPixels\":"+movedLeft+",\"tetherLampPixels\":"+tether+",\"deepMetalPixels\":"+visible+"}");
            }
            finally { lamp.transform.position=original; lamp.enabled=true; foreach(var image in images) Object.Destroy(image); }
        }
    }
}
