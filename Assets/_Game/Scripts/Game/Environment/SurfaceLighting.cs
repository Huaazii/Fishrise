using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Fishing.Game
{
    /// <summary>Shares the actual surface lights and contact anchors with the ice's local highlights.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SurfaceLighting : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] ice;
        [SerializeField] private Light2D sky, lantern;
        [SerializeField] private Transform[] contactAnchors;
        [SerializeField] private Vector2[] contactRadii;
        private MaterialPropertyBlock _block;
        private readonly Vector4[] _contacts = new Vector4[12];
        private Renderer _ground;
        private static readonly int SkyPosition = Shader.PropertyToID("_SkyPosition");
        private static readonly int SkyColor = Shader.PropertyToID("_SkyColor");
        private static readonly int LampPosition = Shader.PropertyToID("_LampPosition");
        private static readonly int LampColor = Shader.PropertyToID("_LampColor");
        private static readonly int ContactShadows = Shader.PropertyToID("_GroundContacts");
        /// <summary>Whether the two ice faces, two local lights and four support anchors are connected.</summary>
        public bool HasBindings => ice != null && ice.Length == 2 && ice[0] != null && ice[1] != null &&
            sky != null && lantern != null && contactAnchors != null && contactAnchors.Length == 4 &&
            contactRadii != null && contactRadii.Length == 4;

        /// <summary>Binds only presentation objects in the shared environment prefab.</summary>
        public void Bind(SpriteRenderer[] receivers, Light2D skyLight, Light2D lanternLight,
            Transform[] anchors, Vector2[] radii)
        { ice = receivers; sky = skyLight; lantern = lanternLight; contactAnchors = anchors; contactRadii = radii; ApplyLighting(); }

        private void OnEnable() { ApplyLighting(); }
        private void LateUpdate() { ApplyLighting(); }

        /// <summary>Receives the same contact footprints on the continuous snow behind the existing foreground ice.</summary>
        public void AttachGround(Renderer receiver) { _ground = receiver; ApplyLighting(); }

        /// <summary>Updates highlights from light position, colour, intensity and enabled state without cloning materials.</summary>
        public void ApplyLighting()
        {
            if (!HasBindings) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            for (int i = 0; i < contactAnchors.Length; i++)
            {
                Vector3 point = contactAnchors[i].position;
                var scale = contactAnchors[i].localScale;
                _contacts[i] = new Vector4(point.x, point.y - .04f,
                    contactRadii[i].x * Mathf.Abs(scale.x), contactRadii[i].y * Mathf.Abs(scale.y));
            }
            // Authored support points on the shared artwork: two soles, two stool feet, three table feet and the barrel base.
            Contact(4, contactAnchors[0], new Vector2(.16f, 0), new Vector2(.30f, .055f));
            Contact(5, contactAnchors[0], new Vector2(.73f, 0), new Vector2(.31f, .05f));
            Contact(6, contactAnchors[0], new Vector2(-.40f, 0), new Vector2(.13f, .045f));
            Contact(7, contactAnchors[0], new Vector2(-.73f, 0), new Vector2(.12f, .045f));
            Contact(8, contactAnchors[1], new Vector2(.12f, 0), new Vector2(.14f, .045f));
            Contact(9, contactAnchors[1], new Vector2(-.50f, 0), new Vector2(.12f, .04f));
            Contact(10, contactAnchors[1], new Vector2(.50f, 0), new Vector2(.12f, .04f));
            Contact(11, contactAnchors[2], new Vector2(0, -.01f), new Vector2(.43f, .055f));
            foreach (var receiver in ice)
                ApplyContacts(receiver);
            if (_ground != null) ApplyContacts(_ground);
        }

        private void Contact(int index, Transform anchor, Vector2 offset, Vector2 radius)
        {
            Vector3 point = anchor.TransformPoint(offset); var scale = anchor.lossyScale;
            _contacts[index] = new Vector4(point.x, point.y, radius.x * Mathf.Abs(scale.x), radius.y * Mathf.Abs(scale.y));
        }
        private void ApplyContacts(Renderer receiver)
        { ApplyLighting(receiver); receiver.GetPropertyBlock(_block); _block.SetVectorArray(ContactShadows, _contacts); receiver.SetPropertyBlock(_block); }

        /// <summary>Shares the same real sky and lantern parameters with the line while preserving its sorting layer.</summary>
        public void ApplyLighting(Renderer receiver)
        {
            if (!HasBindings || receiver == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            receiver.GetPropertyBlock(_block);
            _block.SetVector(SkyPosition,LightPosition(sky)); _block.SetColor(SkyColor,LightColor(sky));
            _block.SetVector(LampPosition,LightPosition(lantern)); _block.SetColor(LampColor,LightColor(lantern));
            receiver.SetPropertyBlock(_block);
        }

        private static Vector4 LightPosition(Light2D light) => new Vector4(light.transform.position.x,
            light.transform.position.y, light.normalMapDistance, light.pointLightOuterRadius);
        private static Color LightColor(Light2D light) => light.color * (light.isActiveAndEnabled ? light.intensity : 0);
    }
}
