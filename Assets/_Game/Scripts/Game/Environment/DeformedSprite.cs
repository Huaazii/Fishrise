using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Scene-owned textured grid with shader deformation; never modifies the source Sprite or texture.</summary>
    public sealed class DeformedSprite : System.IDisposable
    {
        private readonly Mesh _mesh;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private Sprite _source;
        private Vector4 _bounds;
        private Bounds _sourceBounds;
        private Vector4 _supportProfile = new Vector4(.155f,.081f,.0065f,.049f);
        private static readonly int SupportProfile = Shader.PropertyToID("_SupportProfile");
        /// <summary>Renderer participating in the existing URP 2D lighting and sorting.</summary>
        public MeshRenderer Renderer { get; }
        /// <summary>Visual transform, separate from all physics.</summary>
        public Transform Transform => Renderer.transform;
        /// <summary>Last deformation parameters, useful for pause and reset checks.</summary>
        public Vector4 Parameters { get; private set; }
        /// <summary>Creates one reusable visual mesh with a shared material.</summary>
        public DeformedSprite(string name, Transform parent, Material material, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            _mesh = new Mesh { name = name + " grid" }; go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            Renderer = go.AddComponent<MeshRenderer>(); Renderer.sharedMaterial = material;
            Renderer.sortingLayerName = "Surface Lit"; Renderer.sortingOrder = order;
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; Renderer.receiveShadows = false;
        }
        /// <summary>Rebuilds only when the source art changes, retaining exact UVs, pivot and native scale.</summary>
        public void SetSprite(Sprite source)
        {
            if (_source == source) return; _source = source;
            const int columns = 20, rows = 10;
            var vertices = new Vector3[(columns + 1) * (rows + 1)]; var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length]; var indices = new int[columns * rows * 6];
            var bounds = source.bounds; _sourceBounds = bounds; var rect = source.textureRect;
            _bounds = new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                int i = y * (columns + 1) + x; float u = (float)x / columns, v = (float)y / rows;
                vertices[i] = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, u), Mathf.Lerp(bounds.min.y, bounds.max.y, v), 0);
                uv[i] = new Vector2((rect.x + rect.width * u) / source.texture.width, (rect.y + rect.height * v) / source.texture.height);
                colors[i] = Color.white;
                if (x == columns || y == rows) continue;
                int p = (y * columns + x) * 6;
                indices[p] = i; indices[p + 1] = i + columns + 1; indices[p + 2] = i + 1;
                indices[p + 3] = i + 1; indices[p + 4] = i + columns + 1; indices[p + 5] = i + columns + 2;
            }
            _mesh.Clear(); _mesh.vertices = vertices; _mesh.uv = uv; _mesh.colors = colors; _mesh.triangles = indices;
            _mesh.bounds = bounds;
            Renderer.GetPropertyBlock(_block); _block.SetTexture("_MainTex", source.texture); _block.SetVector("_SpriteBounds", _bounds);
            Renderer.SetPropertyBlock(_block);
        }
        /// <summary>Sets fish (0), grounded actor (1), rod (2), or grounded stand (3) deformation without rebuilding geometry.</summary>
        public void SetPose(int kind, float amplitude, float phase, float head, Color tint)
        {
            Parameters = new Vector4(kind, amplitude, phase, head);
            // Include the shader's maximum displacement, without a character-sized margin on a tiny fish.
            var bounds = _sourceBounds; float bend = Mathf.Abs(amplitude);
            bounds.Expand(new Vector3(kind == 1 ? bend * 2 : 0, kind == 1 ? bend * .24f : bend * 2, .02f));
            if (kind == 3) bounds.Expand(new Vector3(0,.55f,0));
            _mesh.bounds = bounds;
            Renderer.GetPropertyBlock(_block); _block.SetVector("_Deform", Parameters); _block.SetColor("_Tint", tint);
            Renderer.SetPropertyBlock(_block);
        }
        /// <summary>Sets the measured lower contour heights at the character's four support columns.</summary>
        internal void SetSupportProfile(Vector4 profile)
        { _supportProfile = profile; Renderer.GetPropertyBlock(_block); _block.SetVector(SupportProfile,profile); Renderer.SetPropertyBlock(_block); }
        /// <summary>Matches the shader's rooted bending at an authored hand or rod-tip anchor.</summary>
        public Vector3 BendPoint(Vector3 point, int kind, float amplitude)
        {
            float y = Mathf.Clamp01((point.y - _bounds.y) / Mathf.Max(.001f, _bounds.w));
            if (kind == 1) { point.x += amplitude * y * y; point.y -= Mathf.Abs(amplitude) * y * .12f; }
            else { float x = Mathf.Clamp01(point.x / Mathf.Max(.001f, _bounds.x + _bounds.z)); point.y += amplitude * x * x; }
            float u = Mathf.Clamp01((point.x - _bounds.x) / Mathf.Max(.001f,_bounds.z));
            if (kind == 1)
            {
                float floor = u < .30f ? Mathf.Lerp(_supportProfile.x,_supportProfile.y,Mathf.InverseLerp(.135f,.30f,u)) :
                    u < .53f ? Mathf.Lerp(_supportProfile.y,_supportProfile.z,Mathf.InverseLerp(.30f,.53f,u)) : Mathf.Lerp(_supportProfile.z,_supportProfile.w,Mathf.InverseLerp(.53f,.85f,u));
                point.y -= floor * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.45f,y)));
            }
            else if (kind == 3)
            {
                float floor = u < .60f ? Mathf.Lerp(.255f,0,Mathf.InverseLerp(.05f,.60f,u)) : Mathf.Lerp(0,.255f,Mathf.InverseLerp(.60f,.95f,u));
                point.y -= floor * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.78f,y)));
            }
            return point;
        }
        /// <summary>Releases only the owned mesh; scene teardown owns the renderer GameObject.</summary>
        public void Dispose() { if (_mesh != null) Object.Destroy(_mesh); }
    }
}
