using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Presentation and horizontal motion for one pooled fish spawn.</summary>
    public sealed class FishActor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private CircleCollider2D head;
        [SerializeField] private Transform mouth;
        [SerializeField] private FishDefinition definition;
        private float _direction;
        private float _halfWidth;
        private float _scaredRemaining, _escapeMultiplier;
        private Vector3 _hangPosition;
        private float _hangAngle, _animationAge;
        /// <summary>Dedicated small-fish layer retained for sonar; branches now query all fish heads.</summary>
        public const string SmallFishLayer = "FishingSmallFish";
        /// <summary>Identity is reassigned on every spawn, including reuse of the same GameObject.</summary>
        public int InstanceId { get; private set; }
        /// <summary>Current run's visual school, or zero for an individually spawned fish; never a capture identity.</summary>
        public int SchoolId { get; internal set; }
        /// <summary>Resource definition stays on the Unity side.</summary>
        public FishDefinition Definition => definition;
        /// <summary>Whether this spawn has already been captured.</summary>
        public bool IsCaptured { get; private set; }
        /// <summary>Whether this lost catch is leaving permanently without becoming catchable again.</summary>
        public bool IsEscaping { get; private set; }
        /// <summary>Whether this swimming fish is temporarily escaping with detection disabled.</summary>
        public bool IsScared => _scaredRemaining > 0;
        /// <summary>Remaining escape time, advanced only by the gameplay clock.</summary>
        public float ScaredRemaining => _scaredRemaining;
        /// <summary>Fish-head detection shape.</summary>
        public CircleCollider2D Head => head;
        /// <summary>Authored head reach in world units, including its offset and child scale, for clear swimming lanes.</summary>
        public float HeadHorizontalExtent => Mathf.Abs(head.transform.TransformPoint(head.offset).x - transform.position.x) +
            head.radius * Mathf.Max(Mathf.Abs(head.transform.lossyScale.x), Mathf.Abs(head.transform.lossyScale.y));
        /// <summary>Serialized mouth anchor used by the hanging animation.</summary>
        public Transform Mouth => mouth;
        /// <summary>Authored body renderer reused by the collider-free surfacing copy.</summary>
        public SpriteRenderer Body => body;
        /// <summary>Sampled speed shared by members of a school for this spawn.</summary>
        public float SwimSpeed { get; private set; }
        /// <summary>World-space centre limits that keep the entire school outside its passage.</summary>
        public Vector2 SwimRange { get; private set; }
        /// <summary>Sets serialized prefab bindings during editor preparation.</summary>
        public void Bind(FishDefinition species, SpriteRenderer bodyRenderer, CircleCollider2D headCollider, Transform mouthAnchor)
        { definition = species; body = bodyRenderer; head = headCollider; mouth = mouthAnchor; ApplyDefinition(); }
        /// <summary>Restores SO appearance and mouth anchors while retaining the prefab's authored collision geometry.</summary>
        public void ApplyDefinition()
        {
            transform.localScale = new Vector3(definition.size, definition.size, 1);
            body.sprite = definition.sprite; body.color = Color.white; body.sortingOrder = 2;
            body.transform.localRotation = Quaternion.identity; body.transform.localScale = Vector3.one;
            // Position, rotation, scale, offset and radius belong to the hand-tuned prefab, not the SO seed values.
            head.gameObject.layer = definition.isSmall ? LayerMask.NameToLayer(SmallFishLayer) : gameObject.layer;
            head.enabled = definition.isCatchable; mouth.localPosition = definition.mouthPosition;
        }
        /// <summary>Resets all state when a pooled view becomes a fresh spawn.</summary>
        public void Spawn(int id, FishDefinition definition, Vector2 position, float direction, float halfWidth,
            float swimSpeed = -1, Vector2? swimRange = null)
        {
            if (definition != this.definition) throw new System.InvalidOperationException("Fish prefab belongs to a different species.");
            InstanceId = id; IsCaptured = IsEscaping = false; _scaredRemaining = 0; _escapeMultiplier = 1;
            _direction = direction; _halfWidth = halfWidth; _animationAge = 0;
            SwimSpeed = swimSpeed < 0 ? definition.swimSpeed : swimSpeed;
            float bound = _halfWidth - definition.size * .6f;
            SwimRange = swimRange ?? new Vector2(-bound, bound);
            transform.SetParent(null); transform.position = position; transform.localRotation = Quaternion.identity;
            SchoolId = 0; ApplyDefinition();
            transform.localScale = new Vector3(definition.size * direction, definition.size, 1);
            gameObject.name = "Fish_" + id + "_" + definition.speciesId;
            gameObject.SetActive(true);
        }
        /// <summary>Moves on the explicit paused-aware game clock.</summary>
        public void Tick(float dt)
        {
            _animationAge += dt;
            if (IsEscaping)
            {
                transform.position += new Vector3(_direction * 5, .7f, 0) * dt;
                body.color = new Color(1, 1, 1, Mathf.Clamp01(1 - _animationAge / .7f));
                if (_animationAge >= .7f) gameObject.SetActive(false);
                return;
            }
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
            float extent = Mathf.Max((Definition.sprite != null ? Definition.sprite.bounds.extents.x : .625f) * Definition.size,
                HeadHorizontalExtent);
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
            body.sortingOrder = 8 + index;
        }
        /// <summary>Compacts a retained catch's slot without restarting its capture animation.</summary>
        public void Rehang(int index, Vector3 hangPosition)
        {
            _hangPosition = hangPosition; _hangAngle = 70 + index % 5 * 10;
            body.sortingOrder = 8 + index; AnimateHang();
        }
        /// <summary>Detaches a lost catch and fades it on the gameplay clock; its head stays disabled.</summary>
        public void Escape(Transform pool)
        {
            IsCaptured = false; IsEscaping = true; _scaredRemaining = 0; _animationAge = 0; head.enabled = false;
            _direction = transform.position.x < 0 ? -1 : 1;
            transform.SetParent(pool, true); transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(Definition.size * _direction, Definition.size, 1);
        }
        private void AnimateHang()
        {
            transform.localRotation = Quaternion.Euler(0, 0, _hangAngle + Mathf.Sin(_animationAge * 3.5f + InstanceId) * 5);
            float pop = 1 + .23f * Mathf.Sin(Mathf.Clamp01(_animationAge / .22f) * Mathf.PI);
            transform.localScale = Vector3.one * (.65f * Definition.size * pop);
            // Place the mouth on the slot; different silhouettes must not hang from their body centres.
            transform.localPosition = _hangPosition - transform.localRotation *
                Vector3.Scale(mouth.localPosition, transform.localScale);
        }
        /// <summary>Hides and detaches this view without destroying its reusable components.</summary>
        public void Despawn(Transform pool)
        { IsEscaping = false; _scaredRemaining = 0; head.enabled = false; transform.SetParent(pool); gameObject.SetActive(false); }
    }
}
