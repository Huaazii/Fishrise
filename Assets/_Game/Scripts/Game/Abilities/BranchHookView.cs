using UnityEngine;

namespace Fishing.Game
{
    /// <summary>One pooled branch hook with its own detector, hang slots and tether.</summary>
    public sealed class BranchHookView : MonoBehaviour
    {
        private LineRenderer _line;
        private CircleCollider2D _head;
        private SpriteRenderer _art;
        private SurfaceLighting _lighting;
        private readonly Vector3[] _points = new Vector3[13];
        private readonly System.Collections.Generic.List<SpriteRenderer> _retrievalArt = new System.Collections.Generic.List<SpriteRenderer>(4);
        private Vector2 _bow, _bowVelocity;
        private Vector3 _velocity;
        private float _age, _tilt, _tiltVelocity;
        /// <summary>Stable source hook ID within the run.</summary>
        public int HookId { get; private set; }
        /// <summary>Shared contact adapter including swept detection.</summary>
        public ContactDetector Detector { get; private set; }
        /// <summary>Start of this fixed step's swept path.</summary>
        public Vector2 Previous { get; private set; }
        /// <summary>Detection radius in world units.</summary>
        public float Radius => _head.radius;
        /// <summary>Current complete hook and attached-fish envelope for the accelerated camera's lower framing limit.</summary>
        public Bounds RetrievalBounds
        {
            get
            {
                var bounds=_art.bounds; GetComponentsInChildren(false,_retrievalArt);
                foreach(var renderer in _retrievalArt) if(renderer.enabled) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
        }
        /// <summary>Creates reusable visual and physical components from existing art.</summary>
        public void Initialize(int id, Sprite sprite, Material material, float radius, int layerMask,
            Material metal = null, SurfaceLighting lighting = null)
        {
            _lighting = lighting;
            HookId = id; gameObject.layer = LayerMask.NameToLayer("FishingBranchHook");
            var art = new GameObject("Branch art"); art.transform.SetParent(transform, false);
            _art = art.AddComponent<SpriteRenderer>(); _art.sprite = sprite; _art.sortingOrder = 6;
            if (metal != null) _art.sharedMaterial = metal;
            art.transform.localScale = Vector3.one * .55f;
            _head = gameObject.AddComponent<CircleCollider2D>(); _head.radius = radius; _head.isTrigger = true;
            var body = gameObject.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true;
            _line = gameObject.AddComponent<LineRenderer>(); _line.sharedMaterial = material; _line.positionCount = _points.Length;
            _line.startWidth = _line.endWidth = .025f; _line.sortingOrder = 5;
            _line.startColor = _line.endColor = Color.white; _line.generateLightingData = true;
            _line.numCapVertices = _line.numCornerVertices = 2;
            Detector = new ContactDetector(_head, id, layerMask); gameObject.SetActive(false);
        }
        /// <summary>Reuses this hook from the main hook's current origin.</summary>
        public void Deploy(Vector3 origin)
        {
            transform.position = origin; Previous = origin; _velocity = Vector3.zero; _age = 0;
            ResetPresentation(); DrawTether(origin); _head.enabled = true; gameObject.SetActive(true);
        }
        /// <summary>Moves with a fan-out envelope and damped follow, clamped inside the playable water.</summary>
        public void Tick(Vector3 origin, Vector2 offset, float halfWidth, float deploySeconds, float smoothing, bool eligible, float dt, float retrieval = 0)
        {
            Previous = transform.position; Detector.Begin(); _age += dt;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_age / deploySeconds));
            var target = origin + (Vector3)(offset * t);
            target.x = Mathf.Lerp(target.x, Mathf.Sign(offset.x) * .12f, retrieval);
            target.x = Mathf.Clamp(target.x, -halfWidth + Radius, halfWidth - Radius);
            transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, smoothing, Mathf.Infinity, dt);
            var bounded = transform.position; bounded.x = Mathf.Clamp(bounded.x, -halfWidth + Radius, halfWidth - Radius);
            // Remove follow lag before the ice band, including accelerated/coarse fixed steps.
            if(retrieval>=1) bounded.x = Mathf.Clamp(bounded.x, -.12f, .12f);
            bounded.y = Mathf.Lerp(bounded.y,target.y,retrieval);
            transform.position = bounded;
            _head.enabled = eligible;
            AdvancePresentation(origin, dt); DrawTether(origin);
        }
        /// <summary>Reuses the visible branch during water exit with its detector disabled; captures stay in the run model.</summary>
        public void PresentSurface(Vector3 origin, Vector3 position, float angle)
        {
            _head.enabled = false; gameObject.SetActive(true); transform.position = position;
            _art.transform.localRotation = Quaternion.Euler(0,0,angle);
            var bounds = _art.bounds;
            if (bounds.min.y < 1.02f)
            {
                float shift = Mathf.Clamp(0,-.70f-bounds.min.x,.70f-bounds.max.x);
                transform.position += Vector3.right * shift;
            }
            _art.sortingLayerName = _art.bounds.min.y >= 1.02f ? "Surface Lit" : "Default";
            // The airborne main hook remains on Default/order 20; its metal must occlude the rear tethers.
            _line.sortingLayerName = "Default";
            _bow = new Vector2(0,-.035f); DrawTether(origin);
            _lighting?.ApplyLighting(_art); _lighting?.ApplyLighting(_line);
        }
        private void AdvancePresentation(Vector3 origin, float dt)
        {
            if (dt <= 0) return;
            float length = Vector3.Distance(origin, transform.position);
            // More lag and slack during fan-out; the settled tether stays taut on retrieval.
            float slack = Mathf.Lerp(.14f, .055f, Mathf.Clamp01(_age / .6f));
            Vector2 target = new Vector2(-_velocity.x * .008f, -length * slack);
            float limit = Mathf.Min(.24f, length * .12f);
            _bow = Vector2.SmoothDamp(_bow, Vector2.ClampMagnitude(target, limit), ref _bowVelocity, .16f, Mathf.Infinity, dt);
            _bow = Vector2.ClampMagnitude(_bow, limit);
            _tilt = Mathf.SmoothDampAngle(_tilt, Mathf.Clamp(-_velocity.x * 1.5f, -7, 7), ref _tiltVelocity, .14f, Mathf.Infinity, dt);
            // Only the art tilts: detection, fish hang slots and authoritative movement keep their original geometry.
            _art.transform.localRotation = Quaternion.Euler(0, 0, _tilt);
        }
        private void DrawTether(Vector3 origin)
        {
            Vector3 start = origin + Vector3.up * .12f, end = transform.position + Vector3.up * .12f;
            for (int i = 0; i < _points.Length; i++)
            {
                float t = (float)i / (_points.Length - 1);
                _points[i] = Vector3.Lerp(start, end, t) + (Vector3)_bow * (4 * t * (1 - t));
            }
            _line.SetPositions(_points);
        }
        private void LateUpdate()
        {
            // Real lights may change while paused; this never advances geometry or the gameplay clock.
            _lighting?.ApplyLighting(_line); _lighting?.ApplyLighting(_art);
        }
        private void ResetPresentation()
        {
            _bow = _bowVelocity = Vector2.zero; _tilt = _tiltVelocity = 0;
            _art.transform.localRotation = Quaternion.identity;
            _art.sortingLayerName = _line.sortingLayerName = "Default";
        }
        /// <summary>Returns a small hook-local mouth anchor without maintaining a capture counter.</summary>
        public Vector3 HangPosition(int index) => new Vector3((index % 3 - 1) * .22f, -.18f - index / 3 * .2f, 0);
        /// <summary>Returns this persistent view to its inactive pool.</summary>
        public void ResetView() { _head.enabled = false; Detector.Begin(); ResetPresentation(); gameObject.SetActive(false); }
        private void OnTriggerEnter2D(Collider2D other) { Detector?.Report(other); }
        private void OnTriggerStay2D(Collider2D other) { Detector?.Report(other); }
    }
}
