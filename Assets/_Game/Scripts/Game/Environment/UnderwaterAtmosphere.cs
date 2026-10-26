using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Scene-owned water flow and ice caustics driven by the existing presentation clock.</summary>
    public sealed class UnderwaterAtmosphere : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer water;
        [SerializeField] private SpriteRenderer[] walls;
        [SerializeField] private Texture2D[] causticFrames;
        [SerializeField, Min(1)] private float framesPerSecond = 8;
        private Material _waterMaterial, _wallMaterial;
        private AmbientWaterDetails _details;
        private int _frame = -1;
        private static readonly int WaterTime = Shader.PropertyToID("_WaterTime");
        private static readonly int CausticA = Shader.PropertyToID("_CausticA");
        private static readonly int CausticB = Shader.PropertyToID("_CausticB");
        private static readonly int CausticBlend = Shader.PropertyToID("_CausticBlend");
        /// <summary>Animation phase, frozen by the same pause gate as camera presentation.</summary>
        public float AnimationTime { get; private set; }
        /// <summary>Whether the saved environment has the water, ice walls and complete caustic sequence.</summary>
        public bool HasBindings => water != null && walls != null && walls.Length > 0 &&
            causticFrames != null && causticFrames.Length == 32;

        /// <summary>Binds only decorative renderers and imported frames while preparing the prefab.</summary>
        public void Bind(SpriteRenderer background, SpriteRenderer[] iceWalls, Texture2D[] frames)
        { water = background; walls = iceWalls; causticFrames = frames; }

        private void Awake()
        {
            _details = GetComponentInChildren<AmbientWaterDetails>();
            if (!HasBindings) { enabled = false; return; }
            // Each environment owns two instances; neither shared assets nor per-wall materials are animated.
            _waterMaterial = new Material(water.sharedMaterial) { name = "Water atmosphere (runtime)" };
            _wallMaterial = new Material(walls[0].sharedMaterial) { name = "Ice caustics (runtime)" };
            water.sharedMaterial = _waterMaterial;
            foreach (var wall in walls) wall.sharedMaterial = _wallMaterial;
            ResetAnimation();
        }

        /// <summary>Returns flow and the frame crossfade to their original phase on restart.</summary>
        public void ResetAnimation()
        { AnimationTime = 0; _frame = -1; ApplyPhase(); _details?.ResetMotion(); }

        /// <summary>Advances cosmetic motion only; the caller skips this while paused or backgrounded.</summary>
        public void Tick(float dt)
        {
            if (!enabled || dt <= 0 || _waterMaterial == null) return;
            AnimationTime += dt; ApplyPhase(); _details?.Tick(dt);
        }

        private void ApplyPhase()
        {
            if (_waterMaterial == null) return;
            _waterMaterial.SetFloat(WaterTime, AnimationTime);
            _wallMaterial.SetFloat(WaterTime, AnimationTime);
            float position = AnimationTime * framesPerSecond;
            int frame = Mathf.FloorToInt(position) % causticFrames.Length;
            if (frame != _frame)
            {
                _frame = frame;
                _wallMaterial.SetTexture(CausticA, causticFrames[frame]);
                _wallMaterial.SetTexture(CausticB, causticFrames[(frame + 1) % causticFrames.Length]);
            }
            _wallMaterial.SetFloat(CausticBlend, position - Mathf.Floor(position));
        }

        private void OnDestroy()
        { if (_waterMaterial != null) Destroy(_waterMaterial); if (_wallMaterial != null) Destroy(_wallMaterial); }
    }
}
