namespace Fishing.Game
{
    /// <summary>Explicit gameplay delta source; UI continues using unscaled engine time.</summary>
    public sealed class GameClock
    {
        /// <summary>Whether gameplay is frozen.</summary>
        public bool IsPaused { get; set; }
        /// <summary>Returns zero while paused without changing Unity's global time scale.</summary>
        public float Delta(float unscaledDelta) => IsPaused ? 0 : unscaledDelta;
    }
}
