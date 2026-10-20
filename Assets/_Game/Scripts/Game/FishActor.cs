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
        private void Awake() { _placeholder = body.sprite; }
        /// <summary>Identity is reassigned on every spawn, including reuse of the same GameObject.</summary>
        public int InstanceId { get; private set; }
        /// <summary>Resource definition stays on the Unity side.</summary>
        public FishDefinition Definition { get; private set; }
        /// <summary>Whether this spawn has already been captured.</summary>
        public bool IsCaptured { get; private set; }
        /// <summary>Fish-head detection shape.</summary>
        public CircleCollider2D Head => head;
        /// <summary>Sets serialized prefab bindings during editor preparation.</summary>
        public void Bind(SpriteRenderer bodyRenderer, SpriteRenderer tailRenderer, CircleCollider2D headCollider, SpriteRenderer eyeRenderer = null)
        { body = bodyRenderer; tail = tailRenderer; head = headCollider; eye = eyeRenderer; }
        /// <summary>Resets all state when a pooled view becomes a fresh spawn.</summary>
        public void Spawn(int id, FishDefinition definition, Vector2 position, float direction, float halfWidth)
        {
            InstanceId = id; Definition = definition; IsCaptured = false;
            _direction = direction; _halfWidth = halfWidth;
            transform.SetParent(null); transform.position = position; transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(definition.size * direction, definition.size, 1);
            body.sprite = definition.sprite != null ? definition.sprite : _placeholder;
            body.transform.localScale = definition.sprite != null ? Vector3.one : new Vector3(.95f, .55f, 1);
            body.color = definition.sprite != null ? Color.white : definition.color;
            tail.enabled = definition.sprite == null;
            if (eye != null) eye.enabled = definition.sprite == null;
            tail.color = definition.color * new Color(.75f, .75f, .75f, 1);
            body.sortingOrder = 2; tail.sortingOrder = 1;
            head.transform.localPosition = definition.headPosition; head.offset = Vector2.zero; head.radius = definition.headRadius;
            head.enabled = definition.isCatchable; gameObject.name = "Fish_" + id + "_" + definition.speciesId;
            gameObject.SetActive(true);
        }
        /// <summary>Moves on the explicit paused-aware game clock.</summary>
        public void Tick(float dt)
        {
            if (IsCaptured) return;
            var p = transform.position;
            p.x += _direction * Definition.swimSpeed * dt;
            float bound = _halfWidth - Definition.size * .6f;
            if (Mathf.Abs(p.x) > bound)
            { p.x = Mathf.Clamp(p.x, -bound, bound); _direction *= -1; }
            transform.position = p;
            transform.localScale = new Vector3(Definition.size * _direction, Definition.size, 1);
        }
        /// <summary>Disables gameplay detection immediately and attaches to a visual hang point.</summary>
        public void Attach(Transform hook, int index, Vector3 hangPosition)
        {
            IsCaptured = true; head.enabled = false; transform.SetParent(hook, false);
            transform.localRotation = Quaternion.Euler(0, 0, 70 + index % 5 * 10);
            transform.localScale = new Vector3(.65f * Definition.size, .65f * Definition.size, 1);
            // Place the mouth on the slot; different silhouettes must not hang from their body centres.
            transform.localPosition = hangPosition - transform.localRotation *
                Vector3.Scale(Definition.mouthPosition, transform.localScale);
            body.sortingOrder = 8 + index; tail.sortingOrder = 7 + index;
        }
        /// <summary>Hides and detaches this view without destroying its reusable components.</summary>
        public void Despawn(Transform pool)
        { head.enabled = false; transform.SetParent(pool); gameObject.SetActive(false); }
    }
}
