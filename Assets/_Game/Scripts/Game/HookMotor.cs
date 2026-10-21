using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Moves the main hook and reports trigger candidates without making rule decisions.</summary>
    public sealed class HookMotor : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer lamp;
        [SerializeField] private SpriteRenderer shield;
        [SerializeField] private CircleCollider2D head;
        [SerializeField] private LineRenderer rope;
        [SerializeField] private Transform ropeAnchor;
        [SerializeField] private Transform[] hangPoints;
        [SerializeField] private SpriteRenderer[] bubbles;
        private float _bubbleTravel;
        private Transform _ropeOrigin, _ropeGuide;
        private ContactDetector _detector;
        /// <summary>Hook detection radius in world units.</summary>
        public float Radius => head.radius;
        /// <summary>Shape reused by the common single-hook detector.</summary>
        public CircleCollider2D Head => head;
        /// <summary>Shared existing line material for branch tethers and the sonar ring.</summary>
        public Material LineMaterial => rope.sharedMaterial;
        /// <summary>Sets prefab art/detection bindings.</summary>
        public void Bind(SpriteRenderer lampRenderer, SpriteRenderer shieldRenderer, CircleCollider2D collider, LineRenderer line = null)
        { lamp = lampRenderer; shield = shieldRenderer; head = collider; rope = line; }
        /// <summary>Binds the art-specific rope, capture slots and decorative bubble trail.</summary>
        public void BindPresentation(Transform lineAnchor, Transform[] anchors, SpriteRenderer[] bubbleRenderers)
        { ropeAnchor = lineAnchor; hangPoints = anchors; bubbles = bubbleRenderers; }
        /// <summary>Returns a slot in hook-local space, reusing slots if future capacities exceed their count.</summary>
        public Vector3 HangPosition(int index) => hangPoints != null && hangPoints.Length > 0 ?
            transform.InverseTransformPoint(hangPoints[index % hangPoints.Length].position) : new Vector3(0, -.1f, 0);
        /// <summary>Resets visual time when a fresh run reuses the main hook.</summary>
        public void ResetPresentation() { _bubbleTravel = 0; MoveBubbles(0); }
        /// <summary>Connects callbacks to the current run adapter.</summary>
        public void SetDetector(ContactDetector detector) { _detector = detector; }
        /// <summary>Connects the instantiated hook to this scene's rod tip without a cross-scene prefab reference.</summary>
        public void SetRopeOrigin(Transform origin, Transform guide = null)
        { _ropeOrigin = origin; _ropeGuide = guide; UpdateRope(); }
        /// <summary>Moves explicitly before manual physics simulation.</summary>
        public void Move(float targetX, float depth, float units, float speed, float halfWidth, float dt)
        {
            transform.position = new Vector3(Mathf.MoveTowards(transform.position.x,
                Mathf.Clamp(targetX, -halfWidth + Radius, halfWidth - Radius), speed * dt), -depth * units, 0);
            UpdateRope();
            MoveBubbles(dt);
        }
        /// <summary>Applies a visual state without maintaining a separate capture counter.</summary>
        public void Show(Color lampColor, bool protectionActive)
        {
            lamp.color = lampColor; shield.enabled = protectionActive;
            if (bubbles != null) foreach (var bubble in bubbles) bubble.enabled = protectionActive;
            UpdateRope();
        }
        private void UpdateRope()
        {
            if (rope == null) return;
            Vector3 end = ropeAnchor != null ? ropeAnchor.position : transform.position + Vector3.up * .2f;
            bool throughHole = _ropeGuide != null && end.y < _ropeGuide.position.y;
            rope.positionCount = throughHole ? 3 : 2;
            rope.SetPosition(0, _ropeOrigin != null ? _ropeOrigin.position : new Vector3(0, 1.4f, 0));
            if (throughHole) rope.SetPosition(1, _ropeGuide.position);
            rope.SetPosition(rope.positionCount - 1, end);
        }
        private void MoveBubbles(float dt)
        {
            _bubbleTravel = Mathf.Repeat(_bubbleTravel + dt * .5f, .9f);
            if (bubbles == null) return;
            for (int i = 0; i < bubbles.Length; i++)
                bubbles[i].transform.localPosition = new Vector3(i % 2 == 0 ? -.38f : .38f,
                    .85f + Mathf.Repeat(_bubbleTravel + i * .3f, .9f), 0);
        }
        private void OnTriggerEnter2D(Collider2D other) { _detector?.Report(other); }
        private void OnTriggerStay2D(Collider2D other) { _detector?.Report(other); }
    }
}
