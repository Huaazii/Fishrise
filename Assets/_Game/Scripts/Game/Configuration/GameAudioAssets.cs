using System;
using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Scene-shared audio references and mix levels, separate from authoritative fishing rules.</summary>
    [CreateAssetMenu(menuName = "Fishing/Audio Assets")]
    public sealed class GameAudioAssets : ScriptableObject
    {
        /// <summary>Variants rotate without consuming the gameplay random stream.</summary>
        [Serializable]
        public sealed class Sound
        {
            public GameFeedback.Cue cue;
            public AudioClip[] clips;
            [Range(0, 1)] public float volume = .35f;
            [Range(0, 1)] public float interval = .07f;
        }
        [Tooltip("Menu, tutorial, catalog and post-surface theme.")]
        public AudioClip music;
        public AudioClip underwaterMusic, wind, underwater, reel;
        [Range(0, 1)] public float underwaterMusicVolume = .18f;
        [Range(.3f, 4)] public float musicTransitionSeconds = 1.6f;
        [Range(.5f, 5)] public float musicLoopBlendSeconds = 2.4f;
        [Range(0, 3)] public float menuMusicEndPadding = 1f;
        [Range(0, 1)] public float musicVolume = .14f, windVolume = .12f, underwaterVolume = .11f, reelVolume = .07f;
        public Sound[] sounds;
    }
}
