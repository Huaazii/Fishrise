using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Bounded, collider-free water bubbles and suspended specks on the shared presentation clock.</summary>
    public sealed class AmbientWaterDetails : MonoBehaviour
    {
        [SerializeField] private Sprite[] bubbles;
        [SerializeField] private Sprite speck;
        [SerializeField] private Material spriteMaterial;
        [SerializeField] private float surfaceY = .35f, bottomY = -103;
        private const int BubbleCount = 18, SpeckCount = 12;
        private sealed class Particle
        {
            internal SpriteRenderer Renderer;
            internal Vector2 Origin;
            internal float Age, Lifetime, Speed, Phase, Size;
        }
        private Particle[] _particles;
        private System.Random _random;
        private Camera _camera;
        /// <summary>Fixed scene-owned visual pool, independent of fish population and physics.</summary>
        public int ParticleCount => _particles?.Length ?? 0;
        /// <summary>Whether the imported images and URP 2D material are saved in the environment.</summary>
        public bool HasBindings => bubbles != null && bubbles.Length >= 2 && bubbles[0] != null &&
            bubbles[1] != null && speck != null && spriteMaterial != null;

        /// <summary>Assigns imported art and existing water boundaries without changing gameplay geometry.</summary>
        public void Bind(Sprite[] images, Sprite dust, Material material, float surface, float bottom)
        { bubbles = images; speck = dust; spriteMaterial = material; surfaceY = surface; bottomY = bottom; }

        private void Awake()
        {
            if (!HasBindings) { enabled = false; return; }
            _camera = Camera.main; _particles = new Particle[BubbleCount + SpeckCount];
            for (int i = 0; i < _particles.Length; i++)
            {
                var child = new GameObject(i < BubbleCount ? "Ambient bubble " + i : "Suspended speck " + i);
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = spriteMaterial;
                renderer.sprite = i < BubbleCount ? bubbles[i % bubbles.Length] : speck;
                renderer.sortingOrder = i < BubbleCount ? -20 : -24;
                _particles[i] = new Particle { Renderer = renderer };
            }
            ResetMotion();
        }

        /// <summary>Restarts decorative randomness locally; never consumes the fish population's random stream.</summary>
        public void ResetMotion()
        {
            if (_particles == null) return;
            _random = new System.Random(7143);
            for (int i = 0; i < _particles.Length; i++) Spawn(i, true);
        }

        /// <summary>Advances world-space motion and recycles distant sprites after the camera leaves them behind.</summary>
        public void Tick(float dt)
        {
            if (!isActiveAndEnabled || dt <= 0 || _particles == null || _camera == null) return;
            ViewBand(out float low, out float high);
            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i]; p.Age += dt;
                float y = p.Origin.y + p.Age * p.Speed;
                if (p.Age >= p.Lifetime || y > high + 1 || y < low - 1 || y > surfaceY - .2f)
                { Spawn(i, false); continue; }
                Draw(i);
            }
        }

        private void ViewBand(out float low, out float high)
        {
            float centre = _camera != null ? _camera.transform.position.y : 0;
            float extent = _camera != null ? _camera.orthographicSize : 12;
            low = Mathf.Max(bottomY + .3f, centre - extent - .5f);
            high = Mathf.Min(surfaceY - .25f, centre + extent + .5f);
            if (high < low) high = low;
        }
        private float Between(float min, float max) => Mathf.Lerp(min, max, (float)_random.NextDouble());
        private void Spawn(int i, bool prewarm)
        {
            var p = _particles[i]; bool bubble = i < BubbleCount;
            ViewBand(out float low, out float high);
            // Most air seeps along the walls; sparse free bubbles leave the fish-head route readable.
            float x = bubble && i % 4 != 0 ? (i % 2 == 0 ? -1 : 1) * Between(3.15f, 4.35f) : Between(-4.1f, 4.1f);
            p.Lifetime = bubble ? Between(6, 12) : Between(12, 22);
            p.Speed = bubble ? Between(.3f, .65f) : Between(.025f, .08f);
            p.Size = bubble ? Between(.055f, .16f) : Between(.015f, .035f);
            p.Phase = Between(0, Mathf.PI * 2); p.Age = prewarm ? p.Lifetime * Between(.15f, .6f) : 0;
            p.Origin = new Vector2(x, Between(low, high) - p.Age * p.Speed);
            float width = p.Renderer.sprite.bounds.size.x;
            p.Renderer.transform.localScale = Vector3.one * (p.Size / width);
            Draw(i);
        }
        private void Draw(int i)
        {
            var p = _particles[i]; bool bubble = i < BubbleCount;
            float y = p.Origin.y + p.Age * p.Speed;
            float x = p.Origin.x + Mathf.Sin(p.Phase + p.Age * (bubble ? 1.15f : .4f)) * (bubble ? .09f : .15f);
            p.Renderer.transform.position = new Vector3(x, y, 0);
            float fade = Mathf.Min(Mathf.Clamp01(p.Age / .8f), Mathf.Clamp01((p.Lifetime - p.Age) / 1.2f));
            fade *= Mathf.Clamp01((surfaceY - .15f - y) / .5f) * Mathf.Clamp01((y - bottomY) / .5f);
            p.Renderer.color = bubble ? new Color(.53f, .84f, .94f, fade * .3f) : new Color(.64f, .88f, .94f, fade * .15f);
        }
    }
}
