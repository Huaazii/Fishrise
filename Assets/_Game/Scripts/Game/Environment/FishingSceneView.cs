using Fishing.Core;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Scene-only art state: aligned fisherman poses, rod grip and distant decoration.</summary>
    public sealed class FishingSceneView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer fisherman;
        [SerializeField] private Sprite idlePose, pullPose;
        [SerializeField] private Transform rod, rodTip, lineGuide;
        [SerializeField] private Vector2 idleGrip = new Vector2(.5f, 1.12f), pullGrip = new Vector2(.25f, 1.8f);
        [SerializeField] private Transform[] distantFish;
        [SerializeField] private Material deformMaterial;
        [SerializeField] private Material skyAtmosphereMaterial, submergedIceMaterial, waterSurfaceMaterial;
        [SerializeField] private Material snowGroundMaterial, distantMountainMaterial;
        [SerializeField] private Sprite submergedIceTexture;
        private Vector3[] _distantOrigins;
        private UnderwaterAtmosphere _atmosphere;
        private DeformedSprite _actorMesh, _rodMesh, _standMesh;
        private SpriteRenderer _standArt;
        private SpriteRenderer _rodArt, _sky, _clouds, _mountains, _barrel;
        private Transform _stand, _lantern;
        private Transform[] _weather;
        private Vector3[] _weatherPositions, _weatherScales;
        private Vector3 _barrelScale, _standPosition, _standScale, _lanternPosition, _lanternScale, _lampPosition;
        private UnityEngine.Rendering.Universal.Light2D _lamp;
        private float _lampRadius;
        private float _standTopOffset, _lanternFootOffset;
        private SurfaceScenery _scenery;
        private Sprite _rodSprite;
        private SurfaceLighting _lighting;
        private Vector3 _actorPosition, _actorScale, _cloudPosition, _cloudScale, _barrelPosition, _tipPosition;
        private Vector3 _mountainPosition, _mountainScale;
        private Color _skyColor, _cloudColor, _mountainColor;
        private bool _surfaceStyled;
        /// <summary>Shared shader for the flying fish, rooted torso and flexible rod.</summary>
        public Material DeformationMaterial => deformMaterial;
        /// <summary>Actual curved rod renderer for frame and cleanup checks.</summary>
        public MeshRenderer SurfaceRod => _rodMesh?.Renderer;
        /// <summary>Grounded character mesh used in both ordinary play and the surface sequence.</summary>
        public MeshRenderer SurfaceActor => _actorMesh?.Renderer;
        /// <summary>Rod endpoint used by the main hook's world-space rope.</summary>
        public Transform RodTip => rodTip;
        /// <summary>Fixed point inside the ice opening, keeping the underwater line out of the ice.</summary>
        public Transform LineGuide => lineGuide;
        /// <summary>Renderer sharing one foot pivot across both pose sprites.</summary>
        public SpriteRenderer Fisherman => fisherman;
        /// <summary>Top of the existing barrel, used only as the decorative fish landing point.</summary>
        public Vector3 CatchPoint
        {
            get { var barrel = fisherman.transform.parent.Find("Barrel").GetComponent<SpriteRenderer>();
                return new Vector3(barrel.bounds.center.x, barrel.bounds.max.y - .08f, 0); }
        }
        /// <summary>Landing point of the barrel in the closer surface composition.</summary>
        public Vector3 SurfaceCatchPoint => CatchPoint;
        /// <summary>World-space top edge of the existing seabed art, used to bound the entire camera viewport.</summary>
        public float SeabedY => transform.Find("Underwater/Seabed").GetComponent<SpriteRenderer>().bounds.max.y;
        /// <summary>Whether the saved scene has both poses, rod references and the shared deformation material.</summary>
        public bool HasBindings => fisherman != null && idlePose != null && pullPose != null && rod != null && rodTip != null && lineGuide != null && deformMaterial != null &&
            skyAtmosphereMaterial != null && submergedIceMaterial != null && waterSurfaceMaterial != null && submergedIceTexture != null &&
            snowGroundMaterial != null && distantMountainMaterial != null;

        /// <summary>Sets art references while preparing the saved environment.</summary>
        public void Bind(SpriteRenderer actor, Sprite idle, Sprite pull, Transform rodTransform, Transform tip, Transform guide, Transform[] backgroundFish)
        { fisherman = actor; idlePose = idle; pullPose = pull; rod = rodTransform; rodTip = tip; lineGuide = guide; distantFish = backgroundFish; }

        private void Awake()
        {
            _atmosphere = GetComponent<UnderwaterAtmosphere>();
            _lighting = GetComponent<SurfaceLighting>();
            _actorPosition = fisherman.transform.localPosition; _actorScale = fisherman.transform.localScale;
            _rodArt = rod.GetComponent<SpriteRenderer>(); _rodSprite = _rodArt.sprite; _tipPosition = rodTip.localPosition;
            var surface = fisherman.transform.parent;
            _sky = surface.Find("Sky").GetComponent<SpriteRenderer>(); _clouds = surface.Find("Clouds").GetComponent<SpriteRenderer>();
            _mountains = surface.Find("Mountains").GetComponent<SpriteRenderer>(); _barrel = surface.Find("Barrel").GetComponent<SpriteRenderer>();
            _skyColor = _sky.color; _cloudColor = _clouds.color; _mountainColor = _mountains.color;
            _mountainPosition = _mountains.transform.localPosition; _mountainScale = _mountains.transform.localScale;
            _mountains.sharedMaterial = distantMountainMaterial;
            _cloudPosition = _clouds.transform.localPosition; _cloudScale = _clouds.transform.localScale; _barrelPosition = _barrel.transform.localPosition;
            _barrelScale = _barrel.transform.localScale;
            _stand = surface.Find("Stand"); _lantern = surface.Find("Lantern");
            _standPosition = _stand.localPosition; _standScale = _stand.localScale;
            _lanternPosition = _lantern.localPosition; _lanternScale = _lantern.localScale;
            var standBounds = _stand.GetComponent<SpriteRenderer>().sprite.bounds;
            // The support plane is the centre of the painted tabletop, below its rear corner's bounding-box maximum.
            _standTopOffset = (standBounds.min.y + standBounds.size.y * .62f) * _standScale.y;
            _lanternFootOffset = _lantern.GetComponent<SpriteRenderer>().sprite.bounds.min.y * _lanternScale.y;
            _actorPosition.y = .98f; _barrelPosition.y = .98f; _standPosition.y = .98f;
            _lanternPosition = _standPosition + Vector3.up * (_standTopOffset - _lanternFootOffset - .04f);
            fisherman.transform.localPosition = _actorPosition; _barrel.transform.localPosition = _barrelPosition;
            _stand.localPosition = _standPosition; _lantern.localPosition = _lanternPosition;
            _weather = new[] { surface.Find("Weather Pole"), surface.Find("Anemometer"), surface.Find("Windsock") };
            _weatherPositions = new Vector3[3]; _weatherScales = new Vector3[3];
            for (int i = 0; i < 3; i++) { _weatherPositions[i] = _weather[i].localPosition; _weatherScales[i] = _weather[i].localScale; }
            float weatherLift = .98f - _weatherPositions[0].y;
            for (int i = 0; i < 3; i++) { _weatherPositions[i].y += weatherLift; _weather[i].localPosition = _weatherPositions[i]; }
            _lamp = surface.Find("Lighting/Lantern Warmth").GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            _lampPosition = _lamp.transform.localPosition; _lampRadius = _lamp.pointLightOuterRadius;
            _lampPosition = _lanternPosition + Vector3.up * .42f; _lamp.transform.localPosition = _lampPosition;
            _scenery = new SurfaceScenery(transform, _sky, skyAtmosphereMaterial, submergedIceMaterial,
                waterSurfaceMaterial, snowGroundMaterial, submergedIceTexture, _lighting);
            if (deformMaterial != null)
            {
                _actorMesh = new DeformedSprite("Surface fisherman mesh", fisherman.transform, deformMaterial, fisherman.sortingOrder);
                _actorMesh.SetSprite(idlePose); ActorSupport(); _actorMesh.SetPose(1,0,0,1,Color.white); fisherman.enabled = false;
                _rodMesh = new DeformedSprite("Surface flexible rod", rod, deformMaterial, _rodArt.sortingOrder);
                _rodMesh.SetSprite(_rodSprite); _rodMesh.Renderer.enabled = false;
                _standArt = _stand.GetComponent<SpriteRenderer>();
                _standMesh = new DeformedSprite("Grounded stand mesh", _stand, deformMaterial, _standArt.sortingOrder);
                _standMesh.SetSprite(_standArt.sprite); _standMesh.SetPose(3,0,0,1,Color.white); _standArt.enabled = false;
            }
            _distantOrigins = new Vector3[distantFish.Length];
            for (int i = 0; i < distantFish.Length; i++) _distantOrigins[i] = distantFish[i].localPosition;
        }

        /// <summary>Changes only the upper-body image and rod grip, retaining the common foot transform.</summary>
        public void ApplyState(RunState state)
        {
            if (state == RunState.Surfacing || (state == RunState.Result && _surfaceStyled)) return;
            ResetSurface();
            bool pulling = state == RunState.Ascending || state == RunState.Surfacing || state == RunState.Result;
            fisherman.sprite = pulling ? pullPose : idlePose;
            if (_actorMesh != null)
            {
                _actorMesh.SetSprite(fisherman.sprite); ActorSupport(); _actorMesh.SetPose(1,0,0,1,Color.white);
                _actorMesh.Renderer.enabled = true; fisherman.enabled = false;
            }
            else fisherman.enabled = true; // Saved-environment preparation also applies the pose outside Play mode.
            rod.localPosition = pulling ? pullGrip : idleGrip;
            rod.localRotation = Quaternion.Euler(0, 0, pulling ? -5 : -30);
        }

        /// <summary>Prepares the same foreground and backdrop composition gradually before the hook breaks water.</summary>
        public void FrameSurface(float blend)
        {
            if (blend <= 0) { ResetSurface(); return; }
            _surfaceStyled = true;
            // Keep the near boot on the flat support outside the widened, rounded opening.
            fisherman.transform.localPosition = _actorPosition;
            fisherman.transform.localScale = _actorScale * Mathf.Lerp(1, 1.45f, blend);
            _barrel.transform.localPosition = Vector3.Lerp(_barrelPosition, new Vector3(3.15f, .98f, _barrelPosition.z), blend);
            _barrel.transform.localScale = _barrelScale * Mathf.Lerp(1, 1.65f, blend);
            var standTarget = new Vector3(1.65f, .98f, _standPosition.z);
            var lanternTarget = standTarget + Vector3.up * (_standTopOffset * 1.5f - _lanternFootOffset * 1.35f - .04f);
            _stand.localPosition = Vector3.Lerp(_standPosition, standTarget, blend); _stand.localScale = _standScale * Mathf.Lerp(1, 1.5f, blend);
            _lantern.localPosition = Vector3.Lerp(_lanternPosition, lanternTarget, blend); _lantern.localScale = _lanternScale * Mathf.Lerp(1, 1.35f, blend);
            for (int i = 0; i < _weather.Length; i++)
            {
                var target = new Vector3(3.55f, .98f, _weatherPositions[0].z) + (_weatherPositions[i] - _weatherPositions[0]) * 1.9f;
                _weather[i].localPosition = Vector3.Lerp(_weatherPositions[i], target, blend);
                // A taller mast needs a longer shaft, rather than a fisherman-sized windsock.
                float size = i == 0 ? 1.9f : i == 1 ? 1.7f : 1.1f;
                _weather[i].localScale = _weatherScales[i] * Mathf.Lerp(1, size, blend);
            }
            _lamp.transform.localPosition = Vector3.Lerp(_lampPosition, lanternTarget + Vector3.up * .57f, blend);
            _lamp.pointLightOuterRadius = Mathf.Lerp(_lampRadius, 3.6f, blend);
            _sky.color = Color.Lerp(_skyColor, Color.white, blend);
            _clouds.color = Color.Lerp(_cloudColor, new Color(.48f, .57f, .65f, .10f), blend);
            _mountains.color = Color.Lerp(_mountainColor, Color.white, blend);
            _mountains.transform.localPosition = Vector3.Lerp(_mountainPosition,
                new Vector3(_mountainPosition.x, .92f, _mountainPosition.z), blend);
            _mountains.transform.localScale = Vector3.Scale(_mountainScale, new Vector3(1, Mathf.Lerp(1, .82f, blend), 1));
            _clouds.transform.localPosition = _cloudPosition + Vector3.up * (.5f * blend);
            _clouds.transform.localScale = _cloudScale;
        }
        /// <summary>Adds one pull and damped rod recoil after the surface composition has settled.</summary>
        public void PresentSurface(float elapsed, float duration)
        {
            float t = elapsed * 2.3f / Mathf.Max(.01f, duration);
            FrameSurface(1);
            float load = Mathf.Sin(Mathf.Clamp01(t / .45f) * Mathf.PI);
            float after = Mathf.Max(0, t - .45f), recoil = Mathf.Sin(after * 18) * Mathf.Exp(-after * 4);
            float bodyBend = -.32f * load + .13f * Mathf.Sin(after * 10) * Mathf.Exp(-after * 4);
            float rodBend = -.6f * load + .22f * recoil;
            fisherman.enabled = _rodArt.enabled = false;
            _actorMesh.Renderer.enabled = _rodMesh.Renderer.enabled = true;
            _actorMesh.SetPose(1, bodyBend, 0, 1, Color.white); _rodMesh.SetPose(2, rodBend, 0, 1, Color.white);
            rod.localPosition = _actorMesh.BendPoint(pullGrip, 1, bodyBend);
            rod.localRotation = Quaternion.Euler(0, 0, -5 + 18 * load - 8 * recoil);
            rodTip.localPosition = _rodMesh.BendPoint(_tipPosition, 2, rodBend);
            _lighting?.ApplyLighting(_actorMesh.Renderer); _lighting?.ApplyLighting(_rodMesh.Renderer);
        }
        /// <summary>Restores original art and backdrop exactly for a new run or menu.</summary>
        public void ResetSurface()
        {
            if (!_surfaceStyled) return; _surfaceStyled = false;
            fisherman.transform.localPosition = _actorPosition; fisherman.transform.localScale = _actorScale;
            fisherman.enabled = false; _rodArt.enabled = true;
            _actorMesh.Renderer.enabled = true; _actorMesh.SetPose(1,0,0,1,Color.white); _rodMesh.Renderer.enabled = false;
            _barrel.transform.localPosition = _barrelPosition; _clouds.transform.localPosition = _cloudPosition; _clouds.transform.localScale = _cloudScale;
            _barrel.transform.localScale = _barrelScale;
            _stand.localPosition = _standPosition; _stand.localScale = _standScale;
            _lantern.localPosition = _lanternPosition; _lantern.localScale = _lanternScale;
            for (int i = 0; i < _weather.Length; i++) { _weather[i].localPosition = _weatherPositions[i]; _weather[i].localScale = _weatherScales[i]; }
            _lamp.transform.localPosition = _lampPosition; _lamp.pointLightOuterRadius = _lampRadius;
            _sky.color = _skyColor; _clouds.color = _cloudColor; _mountains.color = _mountainColor; rodTip.localPosition = _tipPosition;
            _mountains.transform.localPosition = _mountainPosition; _mountains.transform.localScale = _mountainScale;
        }
        private void OnDestroy() { _actorMesh?.Dispose(); _rodMesh?.Dispose(); _standMesh?.Dispose(); _scenery?.Dispose(); }

        private void ActorSupport()
        {
            // Measured opaque lower contours, in the imported sprite's local units, rather than its transparent rectangular bounds.
            _actorMesh.SetSupportProfile(fisherman.sprite == pullPose ? new Vector4(.155f,.081f,.0065f,.049f) : new Vector4(.132f,.059f,.0065f,.059f));
        }

        /// <summary>Offsets distant fish with the camera while preserving their original layout for restarts.</summary>
        public void FollowCamera(Camera camera)
        {
            _scenery.Tick(_atmosphere != null ? _atmosphere.AnimationTime : 0);
            _lighting.ApplyLighting(_actorMesh.Renderer); _lighting.ApplyLighting(_standMesh.Renderer);
            for (int i = 0; i < _distantOrigins.Length; i++)
            {
                float phase = (_atmosphere != null ? _atmosphere.AnimationTime : 0) * (.12f + i * .009f);
                var drift = new Vector3(Mathf.Sin(phase + i * 1.7f) - Mathf.Sin(i * 1.7f),
                    (Mathf.Sin(phase * .8f + i) - Mathf.Sin(i)) * .12f, 0);
                distantFish[i].localPosition = _distantOrigins[i] + drift + Vector3.up * (camera.transform.position.y * .12f);
            }
        }
    }
}
