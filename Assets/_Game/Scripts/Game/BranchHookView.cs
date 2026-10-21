using UnityEngine;

namespace Fishing.Game
{
    /// <summary>One pooled branch hook with its own detector, hang slots and tether.</summary>
    public sealed class BranchHookView : MonoBehaviour
    {
        private LineRenderer _line;
        private CircleCollider2D _head;
        private Vector3 _velocity;
        private float _age;
        /// <summary>Stable source hook ID within the run.</summary>
        public int HookId { get; private set; }
        /// <summary>Shared contact adapter including swept detection.</summary>
        public ContactDetector Detector { get; private set; }
        /// <summary>Start of this fixed step's swept path.</summary>
        public Vector2 Previous { get; private set; }
        /// <summary>Detection radius in world units.</summary>
        public float Radius => _head.radius;
        /// <summary>Creates reusable visual and physical components from existing art.</summary>
        public void Initialize(int id, Sprite sprite, Material material, float radius, int layerMask)
        {
            HookId = id; gameObject.layer = LayerMask.NameToLayer("FishingBranchHook");
            var art = new GameObject("Branch art"); art.transform.SetParent(transform, false);
            var renderer = art.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 6; renderer.color = new Color(.45f, 1, 1);
            art.transform.localScale = Vector3.one * .55f;
            _head = gameObject.AddComponent<CircleCollider2D>(); _head.radius = radius; _head.isTrigger = true;
            var body = gameObject.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true;
            _line = gameObject.AddComponent<LineRenderer>(); _line.sharedMaterial = material; _line.positionCount = 2;
            _line.startWidth = _line.endWidth = .025f; _line.sortingOrder = 5;
            _line.startColor = _line.endColor = new Color(.4f, .95f, 1, .65f);
            Detector = new ContactDetector(_head, id, layerMask); gameObject.SetActive(false);
        }
        /// <summary>Reuses this hook from the main hook's current origin.</summary>
        public void Deploy(Vector3 origin)
        { transform.position = origin; Previous = origin; _velocity = Vector3.zero; _age = 0; _head.enabled = true; gameObject.SetActive(true); }
        /// <summary>Moves with a fan-out envelope and damped follow, clamped inside the playable water.</summary>
        public void Tick(Vector3 origin, Vector2 offset, float halfWidth, float deploySeconds, float smoothing, bool eligible, float dt)
        {
            Previous = transform.position; Detector.Begin(); _age += dt;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_age / deploySeconds));
            var target = origin + (Vector3)(offset * t);
            target.x = Mathf.Clamp(target.x, -halfWidth + Radius, halfWidth - Radius);
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, smoothing, Mathf.Infinity, dt);
            var bounded = transform.position; bounded.x = Mathf.Clamp(bounded.x, -halfWidth + Radius, halfWidth - Radius); transform.position = bounded;
            _head.enabled = eligible; _line.SetPosition(0, origin + Vector3.up * .12f); _line.SetPosition(1, transform.position + Vector3.up * .12f);
        }
        /// <summary>Returns a small hook-local mouth anchor without maintaining a capture counter.</summary>
        public Vector3 HangPosition(int index) => new Vector3((index % 3 - 1) * .22f, -.18f - index / 3 * .2f, 0);
        /// <summary>Returns this persistent view to its inactive pool.</summary>
        public void ResetView() { _head.enabled = false; Detector.Begin(); gameObject.SetActive(false); }
        private void OnTriggerEnter2D(Collider2D other) { Detector?.Report(other); }
        private void OnTriggerStay2D(Collider2D other) { Detector?.Report(other); }
    }
}
