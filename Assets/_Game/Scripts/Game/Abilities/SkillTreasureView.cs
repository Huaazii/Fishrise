using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>A pooled skill capsule with existing art, small glints and no physics or input ownership.</summary>
    public sealed class SkillTreasureView : MonoBehaviour
    {
        /// <summary>Maximum cosmetic displacement, also reserved by spawn clearance checks.</summary>
        public const float BobAmplitude = .09f;
        private bool _previewReady = true;
        private SpriteRenderer _halo, _star;
        private readonly SpriteRenderer[] _icons = new SpriteRenderer[2];
        private readonly LineRenderer[] _rings = new LineRenderer[2], _glints = new LineRenderer[3];
        private Sprite _sonar, _hook;
        private Vector3 _origin;
        private Color _tint;
        private float _size, _time, _burstAge;
        /// <summary>Run-local pickup identity, independent of fish instance IDs.</summary>
        public int Id { get; private set; }
        /// <summary>Skill represented by the capsule.</summary>
        public SkillTreasureKind Kind { get; private set; }
        /// <summary>Stable placement before cosmetic bobbing.</summary>
        public Vector3 SpawnPosition => _origin;
        /// <summary>Visual and swept-pickup centre in world space.</summary>
        public Vector3 Position => transform.position;
        /// <summary>Centre before this cosmetic step, for relative swept detection.</summary>
        public Vector3 PreviousPosition { get; private set; }
        /// <summary>Whether the owning model already committed this pickup.</summary>
        public bool Collected { get; private set; }

        /// <summary>Builds one reusable capsule from existing sprites and shared materials.</summary>
        public void Initialize(GameUiAssets art, Material spriteMaterial, Material lineMaterial)
        {
            _sonar = art.bubble; _hook = art.hook;
            _halo = Sprite("Capsule halo", art.bubble, spriteMaterial, 6);
            _star = Sprite("Treasure glimmer", art.star, spriteMaterial, 7);
            for (int i = 0; i < 2; i++)
            {
                _icons[i] = Sprite("Skill symbol " + i, art.hook, spriteMaterial, 9);
                _rings[i] = Line("Sonar ring " + i, lineMaterial, true, 48);
            }
            for (int i = 0; i < _glints.Length; i++) _glints[i] = Line("Treasure glint " + i, lineMaterial, false, 5);
        }
        private SpriteRenderer Sprite(string name, Sprite sprite, Material material, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = material; renderer.sortingOrder = order; return renderer;
        }
        private LineRenderer Line(string name, Material material, bool loop, int count)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = false;
            line.sortingOrder = 10; line.loop = loop; line.positionCount = count;
            line.startWidth = line.endWidth = loop ? .018f : .022f; return line;
        }
        private static void Size(SpriteRenderer renderer, float width)
        { renderer.transform.localScale = Vector3.one * (width / renderer.sprite.bounds.size.x); }

        /// <summary>Returns the view to an uncollected state at a new run's sampled position.</summary>
        public void ResetView(int id, SkillTreasureKind kind, Vector3 origin, float size)
        {
            Id = id; Kind = kind; _origin = origin; _size = size; _time = 0; _burstAge = 0; Collected = false; _previewReady = true;
            _tint = kind == SkillTreasureKind.Sonar ? new Color(.38f, .94f, 1) : new Color(1, .82f, .35f);
            _icons[0].sprite = kind == SkillTreasureKind.Sonar ? _sonar : _hook;
            _icons[1].sprite = _hook;
            transform.position = PreviousPosition = origin; transform.localScale = Vector3.one;
            Draw(); gameObject.SetActive(false);
        }
        /// <summary>Starts a short visual burst only after successful acquisition.</summary>
        public void Collect() { Collected = true; _burstAge = 0; }
        /// <summary>Advances visual motion only on the paused-aware gameplay clock.</summary>
        public void Tick(float dt, bool visible, bool previewReady = true)
        {
            _previewReady = previewReady;
            PreviousPosition = transform.position; _time += dt;
            if (Collected) _burstAge += dt;
            transform.position = _origin + Vector3.up * (BobAmplitude * Mathf.Sin(_time * 1.7f + Id) + (Collected ? _burstAge * .5f : 0));
            Draw(); gameObject.SetActive(visible && (!Collected || _burstAge < .4f));
        }
        private void Draw()
        {
            float fade = Collected ? Mathf.Clamp01(1 - _burstAge / .4f) : 1;
            if (!_previewReady) fade = 0;
            float pulse = .5f + .5f * Mathf.Sin(_time * 2.6f + Id * 1.7f);
            float scale = _size * (Collected ? 1 + _burstAge : 1);
            Size(_halo, scale * 1.25f); _halo.color = new Color(_tint.r, _tint.g, _tint.b, (.48f + pulse * .15f) * fade);
            Size(_star, scale * .77f); _star.color = new Color(_tint.r, _tint.g, _tint.b, (.15f + pulse * .13f) * fade);
            _star.transform.localRotation = Quaternion.Euler(0, 0, _time * 12);
            for (int i = 0; i < 2; i++)
            {
                bool sonar = Kind == SkillTreasureKind.Sonar;
                _icons[i].enabled = !sonar || i == 0;
                Size(_icons[i], scale * (sonar ? .52f : .34f));
                _icons[i].transform.localPosition = sonar ? Vector3.zero : new Vector3((i == 0 ? -.15f : .15f) * scale, -.02f * scale, 0);
                _icons[i].color = new Color(1, 1, 1, fade);
                var ring = _rings[i]; ring.enabled = sonar;
                ring.startColor = ring.endColor = new Color(.7f, 1, 1, fade * (.85f - i * .2f));
                for (int p = 0; p < ring.positionCount; p++)
                { float a = p * Mathf.PI * 2 / ring.positionCount; ring.SetPosition(p, new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * scale * (.2f + i * .12f)); }
            }
            for (int i = 0; i < _glints.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / 3 + _time * .18f;
                var line = _glints[i]; line.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * scale * .61f;
                float twinkle = Mathf.Pow(Mathf.Max(0, Mathf.Sin(_time * 3.2f + i * 2 + Id)), 3);
                float length = scale * (.025f + twinkle * .08f);
                line.startColor = line.endColor = new Color(_tint.r, _tint.g, _tint.b, fade * twinkle);
                line.SetPosition(0, Vector3.left * length); line.SetPosition(1, Vector3.right * length);
                line.SetPosition(2, Vector3.zero); line.SetPosition(3, Vector3.up * length); line.SetPosition(4, Vector3.down * length);
            }
        }
    }
}
