using System.Collections.Generic;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Scene-owned shallow ice geometry and water optics, sharing existing textures and the pause-aware atmosphere clock.</summary>
    public sealed class SurfaceScenery : System.IDisposable
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<MeshRenderer> _ice = new List<MeshRenderer>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly SpriteRenderer _sky, _wall;
        private readonly MeshRenderer _water;
        private readonly MeshRenderer _surfaceWater;
        private readonly SpriteRenderer _waterSource;
        private readonly SurfaceLighting _lighting;
        private readonly Texture _texture;
        private float _time = float.NaN;
        private static readonly int WaterTime = Shader.PropertyToID("_WaterTime"), MainTexture = Shader.PropertyToID("_MainTex"),
            CausticA = Shader.PropertyToID("_CausticA"), CausticB = Shader.PropertyToID("_CausticB"), CausticBlend = Shader.PropertyToID("_CausticBlend");

        /// <summary>Builds collider-free foreground supports once; all renderers retain serialized shared materials.</summary>
        public SurfaceScenery(Transform parent, SpriteRenderer sky, Material skyMaterial, Material iceMaterial,
            Material waterMaterial, Material groundMaterial, Sprite texture, SurfaceLighting lighting)
        {
            _sky = sky; _sky.sharedMaterial = skyMaterial; _texture = texture.texture; _lighting = lighting;
            _wall = parent.Find("Underwater/Walls").GetChild(0).GetComponent<SpriteRenderer>();
            var root = new GameObject("Shallow ice scenery").transform; root.SetParent(parent, false);
            var ground = Renderer(parent.Find("Surface"), "Rear snow shelf", groundMaterial, "Surface Lit", 26);
            var groundMesh = Mesh("Level profile snow caps");
            groundMesh.vertices = new[] { new Vector3(-7,.84f,0),new Vector3(-.6f,.84f,0),new Vector3(-7,.98f,0),new Vector3(-.6f,.98f,0),
                new Vector3(.6f,.84f,0),new Vector3(7,.84f,0),new Vector3(.6f,.98f,0),new Vector3(7,.98f,0) };
            groundMesh.uv = new[] { new Vector2(0,0),new Vector2(.45f,0),new Vector2(0,1),new Vector2(.45f,1),
                new Vector2(.55f,0),new Vector2(1,0),new Vector2(.55f,1),new Vector2(1,1) };
            groundMesh.triangles = new[] {0,2,1,1,2,3,4,6,5,5,6,7}; groundMesh.RecalculateBounds();
            ground.GetComponent<MeshFilter>().sharedMesh = groundMesh; _lighting.AttachGround(ground);
            _waterSource = parent.Find("Underwater/Water").GetComponent<SpriteRenderer>();
            _surfaceWater = Renderer(parent.Find("Surface"), "Profile surface water", _waterSource.sharedMaterial, "Default", -39);
            var fill = Mesh("Water below profile opening");
            fill.vertices = new[] {new Vector3(-7,.35f,0),new Vector3(7,.35f,0),new Vector3(-7,.86f,0),new Vector3(7,.86f,0)};
            fill.uv = new[] {Vector2.zero,Vector2.right,Vector2.up,Vector2.one}; fill.triangles = new[] {0,2,1,1,2,3}; fill.RecalculateBounds();
            _surfaceWater.GetComponent<MeshFilter>().sharedMesh = fill;
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                _ice.Add(Body(root, iceMaterial, sign, true)); _ice.Add(Body(root, iceMaterial, sign, false));
                _ice.Add(Icicles(root, iceMaterial, sign));
                _ice.Add(Ledges(parent.Find("Surface"),iceMaterial,sign));
            }
            _water = Renderer(root, "Shallow water refraction", waterMaterial, "Underwater Ice", 10);
            var mesh = Mesh("Shallow water band");
            mesh.vertices = new[] { new Vector3(-7, .86f, 0), new Vector3(7, .86f, 0), new Vector3(-7, -5.6f, 0), new Vector3(7, -5.6f, 0) };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 }; mesh.RecalculateBounds();
            _water.GetComponent<MeshFilter>().sharedMesh = mesh;
            Tick(0);
        }

        private MeshRenderer Body(Transform parent, Material material, float side, bool distant)
        {
            var renderer = Renderer(parent, (side < 0 ? "Left" : "Right") + (distant ? " submerged back face" : " submerged ice face"), material, "Default", distant ? -20 : -18);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var normals = new List<Vector3>();
            var colors = new List<Color>(); var indices = new List<int>();
            const int columns = 8, rows = 4;
            float[] bottoms = { -2.2f, -3.25f, -3.8f, -3.55f, -4.3f, -4.05f, -4.65f, -4.35f, -4.8f };
            for (int x = 0; x < columns; x++)
            {
                float facet = Mathf.Sin(x * 1.71f + (side < 0 ? .4f : 2.1f));
                for (int y = 0; y < rows; y++)
                {
                    int start = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float u = (float)(x + corner % 2) / columns, v = (float)(y + (corner >= 2 ? 1 : 0)) / rows;
                        float bottom = bottoms[x + corner % 2] + (side > 0 ? .25f * Mathf.Sin((x + corner % 2) * 1.4f) : 0);
                        float px = Mathf.Lerp(.85f, 6.8f, u) + (distant ? .45f : 0) - .08f * Mathf.Sin(v * 4 + x);
                        vertices.Add(new Vector3(side * px, Mathf.Lerp(.23f, bottom, v) - (distant ? .65f : 0), 0));
                        uv.Add(new Vector2(.045f + u * .22f + (side > 0 ? .02f : 0), .12f + (1 - v) * .75f));
                        normals.Add(new Vector3(side * facet * .6f, .14f + .15f * Mathf.Cos(x), 1).normalized);
                        colors.Add(new Color(distant ? .58f : 1, distant ? .76f : 1, 1, distant ? .5f : .94f));
                    }
                    indices.AddRange(new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 });
                }
            }
            Assign(renderer, vertices, uv, normals, colors, indices); return renderer;
        }

        private MeshRenderer Ledges(Transform parent,Material material,float side)
        {
            var renderer=Renderer(parent,(side<0 ? "Left" : "Right")+" ice ledges",material,"Surface Lit",26);
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var normals=new List<Vector3>();
            var colors=new List<Color>(); var indices=new List<int>();
            int count=side<0 ? 2 : 3;
            for(int i=0;i<count;i++)
            {
                float top=side<0 ? (i==0 ? .18f : -.91f) : (i==0 ? -.13f : i==1 ? -.40f : -1.20f);
                float inner=.80f+i%2*.035f, outer=inner+.27f+i*.045f;
                float thickness=.10f+i%2*.035f;
                var a=new Vector3(side*inner,top-.06f,0); var b=new Vector3(side*outer,top+.05f,0);
                var c=new Vector3(side*(inner+.07f),top-thickness,0); var d=new Vector3(side*outer,top-thickness-.08f,0);
                for(int face=0;face<3;face++)
                {
                    int start=vertices.Count;
                    if(face==0) vertices.AddRange(new[]{a,b,a+new Vector3(side*.08f,.035f,0),b+new Vector3(-side*.04f,.06f,0)});
                    else if(face==1) vertices.AddRange(new[]{c,d,a,b});
                    else vertices.AddRange(new[]{c+Vector3.right*side*.09f,d-Vector3.up*.07f,c,d});
                    var normal=face==0 ? new Vector3(-side*.15f,.78f,.62f) :
                        face==1 ? new Vector3(-side*.5f,.06f,.86f) : new Vector3(-side*.3f,-.65f,.7f);
                    var tint=face==0 ? new Color(.75f,.84f,.89f,.90f) : face==1 ? new Color(.50f,.64f,.73f,.92f) : new Color(.34f,.47f,.59f,.92f);
                    for(int corner=0;corner<4;corner++)
                    { uv.Add(new Vector2(.055f+corner%2*.19f,.35f+(corner/2)*.13f+i*.09f)); normals.Add(normal.normalized); colors.Add(tint); }
                    indices.AddRange(new[]{start,start+1,start+2,start+2,start+1,start+3});
                }
            }
            Assign(renderer,vertices,uv,normals,colors,indices); return renderer;
        }

        private MeshRenderer Icicles(Transform parent, Material material, float side)
        {
            var renderer = Renderer(parent, (side < 0 ? "Left" : "Right") + " hanging ice columns", material, "Default", -17);
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var normals = new List<Vector3>();
            var colors = new List<Color>(); var indices = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                float x = side * (1.05f + i * 1.43f + (side > 0 ? .2f : 0));
                float top = -1.05f - i * .68f, bottom = -4.1f - i * .5f + (side > 0 ? .25f : 0);
                float width = i == 1 ? .66f : .42f;
                for (int facet = 0; facet < 2; facet++)
                {
                    int start = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float u = (facet + corner % 2) * .5f; bool lower = corner >= 2;
                        vertices.Add(new Vector3(x + (u - .5f) * (lower ? .13f : width) + (lower ? side * .16f : 0), lower ? bottom : top, 0));
                        uv.Add(new Vector2(.045f + u * .22f, lower ? .18f : .85f));
                        normals.Add(new Vector3(facet == 0 ? -.65f : .65f, .12f, 1).normalized);
                        colors.Add(new Color(.77f, .93f, 1, lower ? .65f : .9f));
                    }
                    indices.AddRange(new[] { start, start + 1, start + 2, start + 2, start + 1, start + 3 });
                }
            }
            Assign(renderer, vertices, uv, normals, colors, indices); return renderer;
        }

        private Mesh Mesh(string name) { var mesh = new Mesh { name = name }; _meshes.Add(mesh); return mesh; }
        private void Assign(MeshRenderer renderer, List<Vector3> vertices, List<Vector2> uv, List<Vector3> normals, List<Color> colors, List<int> indices)
        {
            var mesh = Mesh(renderer.name); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetNormals(normals);
            mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        private static MeshRenderer Renderer(Transform parent, string name, Material material, string layer, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingLayerName = layer; renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; return renderer;
        }

        /// <summary>Updates existing property blocks; repeated calls at the same paused phase do no work.</summary>
        public void Tick(float time)
        {
            if (_time.Equals(time)) return; _time = time;
            if (_surfaceWater.sharedMaterial != _waterSource.sharedMaterial) _surfaceWater.sharedMaterial = _waterSource.sharedMaterial;
            _sky.GetPropertyBlock(_block); _block.SetFloat(WaterTime, time); _sky.SetPropertyBlock(_block);
            _water.GetPropertyBlock(_block); _block.SetFloat(WaterTime, time); _water.SetPropertyBlock(_block);
            var caustics = _wall.sharedMaterial;
            var frameA = caustics.GetTexture(CausticA); var frameB = caustics.GetTexture(CausticB); float blend = caustics.GetFloat(CausticBlend);
            foreach (var receiver in _ice)
            {
                receiver.GetPropertyBlock(_block); _block.SetFloat(WaterTime, time); _block.SetTexture(MainTexture, _texture);
                _block.SetTexture(CausticA, frameA); _block.SetTexture(CausticB, frameB);
                _block.SetFloat(CausticBlend, blend); receiver.SetPropertyBlock(_block);
                _lighting.ApplyLighting(receiver);
            }
        }

        /// <summary>Releases only owned meshes; shared material assets and textures belong to the project.</summary>
        public void Dispose() { foreach (var mesh in _meshes) if (mesh != null) Object.Destroy(mesh); }
    }
}
