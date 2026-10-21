using UnityEngine;

namespace Fishing.Game
{
    /// <summary>Shared font and existing art used by the Chinese UI; contains no gameplay rules.</summary>
    [CreateAssetMenu(menuName = "Fishing/UI Assets")]
    public sealed class GameUiAssets : ScriptableObject
    {
        public Font font;
        public Sprite panel;
        public Sprite coin, shell, star, protection, depth, hook, bubble, frame, rod;
        public Sprite coral, blueFish, purpleFish, silverFish, spottedFish, orangeConch, greyConch, rocks, barrel;
    }
}
