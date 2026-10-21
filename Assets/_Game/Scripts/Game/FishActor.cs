using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Presentation and horizontal motion for one pooled fish spawn.</summary>
    public sealed class FishActor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer tail;
        [SerializeField] private CircleCollider2D head;
        [SerializeField] private SpriteRenderer eye;
        private float _direction;
        private float _halfWidth;
        private Sprite _placeholder;
        private float _scaredRemaining, _escapeMultiplier;
        private int _headLayer;
        private Vector3 _hangPosition;
        private float _hangAngle, _animationAge;
        /// <summary>Dedicated layer used by sonar and branch-only small-fish queries.</summary>
        public const string SmallFishLayer = "FishingSmallFish";
        private void Awake() { _placeholder = body.sprite; _headLayer = head.gameObject.layer; }
        /// <summary>Identity is reassigned on every spawn, including reuse of the same GameObject.</summary>
        public int InstanceId { get; private set; }
        /// <summary>Resource definition stays on the Unity side.</summary>
        public FishDefinition Definition { get; private set; }
        /// <summary>Whether this spawn has already been captured.</summary>
        public bool IsCaptured { get; private set; }
        /// <summary>Whether this swimming fish is temporarily escaping with detection disabled.</summary>
        public bool IsScared => _scaredRemaining > 0;
        /// <summary>Remaining escape time, advanced only by the gameplay clock.</summary>
        public float ScaredRemaining => _scaredRemaining;
        /// <summary>Fish-head detection shape.</summary>
        public CircleCollider2D Head => head;
        /// <summary>Sampled speed shared by members of a school for this spawn.</summary>
        public float SwimSpeed { get; private set; }
        /// <summary>World-space centre limits that keep the entire school outside its passage.</summary>
        public Vector2 SwimRange { get; private set; }
        /// <summary>Sets serialized prefab bindings during editor preparation.</summary>
        public void Bind(SpriteRenderer bodyRenderer, SpriteRenderer tailRenderer, CircleCollider2D headCollider, SpriteRenderer eyeRenderer = null)
        { body = bodyRenderer; tail = tailRenderer; head = headCollider; eye = eyeRenderer; }
        /// <summary>Resets all state when a pooled view becomes a fresh spawn.</summary>
        public void Spawn(int id, FishDefinition definition, Vector2 position, float direction, float halfWidth,
            float swimSpeed = -1, Vector2? swimRange = null)
        {
            InstanceId = id; Definition = definition; IsCaptured = false; _scaredRemaining = 0; _escapeMultiplier = 1;
            _direction = direction; _halfWidth = halfWidth; _animationAge = 0;
            SwimSpeed = swimSpeed < 0 ? definition.swimSpeed : swimSpeed;
            float bound = _halfWidth - definition.size * .6f;
            SwimRange = swimRange ?? new Vector2(-bound, bound);
            transform.SetParent(null); transform.position = position; transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(definition.size * direction, definition.size, 1);
            body.sprite = definition.sprite != null ? definition.sprite : _placeholder;
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = definition.sprite != null ? Vector3.one : new Vector3(.95f, .55f, 1);
            body.color = definition.sprite != null ? Color.white : definition.color;
            tail.enabled = definition.sprite == null;
            if (eye != null) eye.enabled = definition.sprite == null;
            tail.color = definition.color * new Color(.75f, .75f, .75f, 1);
            body.sortingOrder = 2; tail.sortingOrder = 1;
            head.transform.localPosition = definition.headPosition; head.offset = Vector2.zero; head.radius = definition.headRadius;
            head.gameObject.layer = definition.isSmall ? LayerMask.NameToLayer(SmallFishLayer) : _headLayer;
            head.enabled = definition.isCatchable; gameObject.name = "Fish_" + id + "_" + definition.speciesId;
            gameObject.SetActive(true);
        }
        /// <summary>Moves on the explicit paused-aware game clock.</summary>
        public void Tick(float dt)
        {
            _animationAge += dt;
            if (IsCaptured) { AnimateHang(); return; }
            // Whole-image art has no separate fin; a small visual flex leaves head detection unchanged.
            float flex = Mathf.Sin(_animationAge * (6 + SwimSpeed) + InstanceId);
            body.transform.localRotation = Quaternion.Euler(0, 0, flex * 1.4f);
            if (Definition.sprite != null) body.transform.localScale = new Vector3(1, 1 + flex * .025f, 1);
            var p = transform.position;
            if (IsScared)
            {
                p.x = Mathf.Clamp(p.x + _direction * SwimSpeed * _escapeMultiplier * dt, SwimRange.x, SwimRange.y);
                _scaredRemaining = Mathf.Max(0, _scaredRemaining - dt);
                if (!IsScared)
                { head.enabled = Definition.isCatchable; body.color = Definition.sprite != null ? Color.white : Definition.color; }
                transform.position = p;
                transform.localScale = new Vector3(Definition.size * _direction, Definition.size, 1); return;
            }
            p.x += _direction * SwimSpeed * dt;
            if (p.x < SwimRange.x || p.x > SwimRange.y)
            { p.x = Mathf.Clamp(p.x, SwimRange.x, SwimRange.y); _direction *= -1; }
            transform.position = p;
            transform.localScale = new Vector3(Definition.size * _direction, Definition.size, 1);
        }
        /// <summary>Escapes across the water bounds; recovery keeps the displaced position instead of snapping to the old school lane.</summary>
        public void Scare(float direction, float duration, float speedMultiplier)
        {
            if (IsCaptured || !Definition.isSmall || !Definition.isCatchable) return;
            _direction = direction; _scaredRemaining = duration; _escapeMultiplier = speedMultiplier; head.enabled = false;
            float extent = Mathf.Max(Definition.sprite != null ? Definition.sprite.bounds.extents.x : .625f,
                Mathf.Abs(Definition.headPosition.x) + Definition.headRadius) * Definition.size;
            float bound = Mathf.Max(0, _halfWidth - extent - .05f);
            SwimRange = new Vector2(-bound, bound); body.color = new Color(.5f, 1, 1, .72f);
        }
        /// <summary>Disables gameplay detection immediately and attaches to a visual hang point.</summary>
        public void Attach(Transform hook, int index, Vector3 hangPosition)
        {
            IsCaptured = true; _scaredRemaining = 0; head.enabled = false; transform.SetParent(hook, false);
            _hangPosition = hangPosition; _hangAngle = 70 + index % 5 * 10; _animationAge = 0;
            body.transform.localRotation = Quaternion.identity; body.transform.localScale = Vector3.one;
            AnimateHang();
            body.sortingOrder = 8 + index; tail.sortingOrder = 7 + index;
        }
        private void AnimateHang()
        {
            transform.localRotation = Quaternion.Euler(0, 0, _hangAngle + Mathf.Sin(_animationAge * 3.5f + InstanceId) * 5);
            float pop = 1 + .23f * Mathf.Sin(Mathf.Clamp01(_animationAge / .22f) * Mathf.PI);
            transform.localScale = Vector3.one * (.65f * Definition.size * pop);
            // Place the mouth on the slot; different silhouettes must not hang from their body centres.
            transform.localPosition = _hangPosition - transform.localRotation *
                Vector3.Scale(Definition.mouthPosition, transform.localScale);
        }
        /// <summary>Hides and detaches this view without destroying its reusable components.</summary>
        public void Despawn(Transform pool)
        { _scaredRemaining = 0; head.enabled = false; transform.SetParent(pool); gameObject.SetActive(false); }
    }
}
