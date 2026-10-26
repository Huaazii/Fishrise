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
        [SerializeField] private Material effectLineMaterial;
        [SerializeField] private Material branchMetalMaterial;
        [SerializeField] private Transform ropeAnchor;
        [SerializeField] private Transform[] hangPoints;
        [SerializeField] private SpriteRenderer[] bubbles;
        private float _bubbleTravel;
        private Transform _ropeOrigin, _ropeGuide;
        private ContactDetector _detector;
        private FishingLineView _lineView;
        private SurfaceLighting _lineLighting;
        private Transform _surfaceVisual;
        private SpriteRenderer[] _surfaceSources, _surfaceCopies;
        private bool[] _surfaceEnabled;
        private Vector3 _surfaceAnchor;
        /// <summary>Hook detection radius in world units.</summary>
        public float Radius => head.radius;
        /// <summary>Shape reused by the common single-hook detector.</summary>
        public CircleCollider2D Head => head;
        /// <summary>Shared existing line material for branch tethers and the sonar ring.</summary>
        public Material LineMaterial => effectLineMaterial != null ? effectLineMaterial : rope.sharedMaterial;
        /// <summary>Shared nylon shading for the main line and branch tethers.</summary>
        public Material RopeMaterial => rope.sharedMaterial;
        /// <summary>Shared metal shading for branch art, separate from sonar and treasure effects.</summary>
        public Material BranchMetalMaterial => branchMetalMaterial;
        /// <summary>Binds the reusable branch material without modifying the hook collider or art.</summary>
        public void BindBranchMetalMaterial(Material material) { branchMetalMaterial = material; }
        /// <summary>Keeps sonar, treasures and branch tethers on their original effect material.</summary>
        public void BindEffectLineMaterial(Material material) { effectLineMaterial = material; }
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
        public void ResetPresentation() { _bubbleTravel = 0; MoveBubbles(0); _lineView?.Reset(); UpdateRope(); }
        /// <summary>Builds a collider-free copy once; the authoritative hook remains at its gameplay position.</summary>
        public Transform CreateSurfaceVisual(Transform parent)
        {
            _surfaceVisual = new GameObject("Airborne hook").transform; _surfaceVisual.SetParent(parent, false);
            var sources = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var source in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (source == shield || (bubbles != null && System.Array.IndexOf(bubbles, source) >= 0)) continue;
                sources.Add(source);
            }
            _surfaceSources = sources.ToArray(); _surfaceCopies = new SpriteRenderer[sources.Count]; _surfaceEnabled = new bool[sources.Count];
            _surfaceAnchor = transform.InverseTransformPoint(ropeAnchor != null ? ropeAnchor.position : transform.position + Vector3.up * .2f);
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i]; var copy = new GameObject(source.name).AddComponent<SpriteRenderer>();
                copy.transform.SetParent(_surfaceVisual, false); copy.sprite = source.sprite; copy.color = source.color;
                copy.enabled = source.enabled;
                copy.transform.localPosition = transform.InverseTransformPoint(source.transform.position);
                copy.transform.localRotation = Quaternion.Inverse(transform.rotation) * source.transform.rotation;
                copy.transform.localScale = source.transform.lossyScale;
                copy.sharedMaterial = source != lamp && branchMetalMaterial != null ? branchMetalMaterial : source.sharedMaterial;
                copy.sortingLayerID = source.sortingLayerID; copy.sortingOrder = source.sortingOrder;
                _surfaceCopies[i] = copy;
            }
            _surfaceVisual.gameObject.SetActive(false); return _surfaceVisual;
        }
        /// <summary>Returns the visual rope attachment offset at a proposed world rotation.</summary>
        public Vector3 SurfaceLineOffset(float angle) => Quaternion.Euler(0, 0, angle) *
            Vector3.Scale(_surfaceAnchor, _surfaceVisual.lossyScale);
        /// <summary>Displays only the air hook and reconnects the existing rope to its moving top anchor.</summary>
        public void PresentSurface(Vector3 position, float angle)
        {
            if (!_surfaceVisual.gameObject.activeSelf)
            {
                for (int i = 0; i < _surfaceSources.Length; i++)
                {
                    _surfaceEnabled[i] = _surfaceSources[i].enabled;
                    _surfaceCopies[i].enabled = _surfaceEnabled[i];
                    _surfaceCopies[i].color = _surfaceSources[i].color; _surfaceSources[i].enabled = false;
                }
                _surfaceVisual.gameObject.SetActive(true);
            }
            _surfaceVisual.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
            foreach (var copy in _surfaceCopies) _lineLighting?.ApplyLighting(copy);
            UpdateRope();
        }
        /// <summary>Restores the original art after skip, settlement or restart without moving any collider.</summary>
        public void EndSurface()
        {
            if (_surfaceVisual == null || !_surfaceVisual.gameObject.activeSelf) return;
            _surfaceVisual.gameObject.SetActive(false);
            for (int i = 0; i < _surfaceSources.Length; i++) _surfaceSources[i].enabled = _surfaceEnabled[i];
            _lineView?.Reset(); UpdateRope();
        }
        /// <summary>Connects callbacks to the current run adapter.</summary>
        public void SetDetector(ContactDetector detector) { _detector = detector; }
        /// <summary>Connects the instantiated hook to this scene's rod tip without a cross-scene prefab reference.</summary>
        public void SetRopeOrigin(Transform origin, Transform guide = null, SurfaceLighting lighting = null)
        { _ropeOrigin = origin; _ropeGuide = guide; _lineLighting = lighting; _lineView?.Reset(); UpdateRope(); }
        /// <summary>Moves explicitly before manual physics simulation.</summary>
        public void Move(float targetX, float depth, float units, float speed, float halfWidth, float dt)
        {
            transform.position = new Vector3(Mathf.MoveTowards(transform.position.x,
                Mathf.Clamp(targetX, -halfWidth + Radius, halfWidth - Radius), speed * dt), -depth * units, 0);
            UpdateRope(dt);
            MoveBubbles(dt);
        }
        /// <summary>Applies a visual state without maintaining a separate capture counter.</summary>
        public void Show(Color lampColor, bool protectionActive)
        {
            lamp.color = lampColor; shield.enabled = protectionActive;
            if (bubbles != null) foreach (var bubble in bubbles) bubble.enabled = protectionActive;
            UpdateRope();
        }
        private void UpdateRope(float dt = 0)
        {
            if (rope == null) return;
            Vector3 end = _surfaceVisual != null && _surfaceVisual.gameObject.activeSelf ? _surfaceVisual.TransformPoint(_surfaceAnchor) :
                ropeAnchor != null ? ropeAnchor.position : transform.position + Vector3.up * .2f;
            bool throughHole = _ropeGuide != null && end.y < _ropeGuide.position.y;
            if (_lineView == null) _lineView = new FishingLineView(rope);
            _lineView.Draw(_ropeOrigin != null ? _ropeOrigin.position : new Vector3(0,1.4f,0),
                _ropeGuide != null ? _ropeGuide.position : end,end,throughHole,dt);
            _lineLighting?.ApplyLighting(rope);
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
