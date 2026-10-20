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
        private Vector3[] _distantOrigins;
        /// <summary>Rod endpoint used by the main hook's world-space rope.</summary>
        public Transform RodTip => rodTip;
        /// <summary>Fixed point inside the ice opening, keeping the underwater line out of the ice.</summary>
        public Transform LineGuide => lineGuide;
        /// <summary>Renderer sharing one foot pivot across both pose sprites.</summary>
        public SpriteRenderer Fisherman => fisherman;
        /// <summary>Whether the saved scene has both poses and its rod references.</summary>
        public bool HasBindings => fisherman != null && idlePose != null && pullPose != null && rod != null && rodTip != null && lineGuide != null;

        /// <summary>Sets art references while preparing the saved environment.</summary>
        public void Bind(SpriteRenderer actor, Sprite idle, Sprite pull, Transform rodTransform, Transform tip, Transform guide, Transform[] backgroundFish)
        { fisherman = actor; idlePose = idle; pullPose = pull; rod = rodTransform; rodTip = tip; lineGuide = guide; distantFish = backgroundFish; }

        private void Awake()
        {
            _distantOrigins = new Vector3[distantFish.Length];
            for (int i = 0; i < distantFish.Length; i++) _distantOrigins[i] = distantFish[i].localPosition;
        }

        /// <summary>Changes only the upper-body image and rod grip, retaining the common foot transform.</summary>
        public void ApplyState(RunState state)
        {
            bool pulling = state == RunState.Ascending || state == RunState.Surfacing || state == RunState.Result;
            fisherman.sprite = pulling ? pullPose : idlePose;
            rod.localPosition = pulling ? pullGrip : idleGrip;
            rod.localRotation = Quaternion.Euler(0, 0, pulling ? -5 : -30);
        }

        /// <summary>Offsets distant fish with the camera while preserving their original layout for restarts.</summary>
        public void FollowCamera(Camera camera)
        {
            for (int i = 0; i < _distantOrigins.Length; i++)
                distantFish[i].localPosition = _distantOrigins[i] + Vector3.up * (camera.transform.position.y * .12f);
        }
    }
}
