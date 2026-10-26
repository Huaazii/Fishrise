using System;
using System.Reflection;
using UnityEditor;

namespace Fishing.Editor
{
    /// <summary>Editor-only preview configuration; the player keeps its independent portrait setting.</summary>
    public static class FishingPreview
    {
        /// <summary>Selects a true 450x800 Game View so screenshots do not stretch a landscape render.</summary>
        [MenuItem("Fishing/Portrait Game View")]
        public static string Portrait() => Resolution(450, 800);
        /// <summary>Selects the Xiaomi 15's native portrait resolution as the primary UI acceptance profile.</summary>
        [MenuItem("Fishing/Xiaomi 15 Portrait")]
        public static string Xiaomi15Portrait() => SelectResolution(1200, 2670, "Xiaomi 15 Portrait");
        /// <summary>Selects an actual fixed-resolution Game View for portrait layout acceptance.</summary>
        public static string Resolution(int targetWidth, int targetHeight) => SelectResolution(targetWidth, targetHeight, null);
        private static string SelectResolution(int targetWidth, int targetHeight, string profileName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", flags).GetValue(null);
            var groupType = sizesType.GetProperty("currentGroupType", flags).GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { groupType });
            var groupClass = group.GetType();
            int count = (int)groupClass.GetMethod("GetTotalCount", flags).Invoke(group, null);
            int selected = -1;
            var sizeClass = assembly.GetType("UnityEditor.GameViewSize");
            for (int i = 0; i < count; i++)
            {
                var size = groupClass.GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
                int width = (int)sizeClass.GetProperty("width", flags).GetValue(size);
                int height = (int)sizeClass.GetProperty("height", flags).GetValue(size);
                string name = (string)sizeClass.GetProperty("baseText", flags).GetValue(size);
                if (width == targetWidth && height == targetHeight && (profileName == null || name == profileName)) { selected = i; break; }
            }
            if (selected < 0)
            {
                var kind = assembly.GetType("UnityEditor.GameViewSizeType");
                var constructor = sizeClass.GetConstructor(flags, null, new[] { kind, typeof(int), typeof(int), typeof(string) }, null);
                var size = constructor.Invoke(new[] { Enum.Parse(kind, "FixedResolution"), (object)targetWidth, targetHeight, profileName ?? "Fishing " + targetWidth + "x" + targetHeight });
                groupClass.GetMethod("AddCustomSize", flags).Invoke(group, new[] { size }); selected = count;
            }
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, selected);
            view.Focus(); view.Repaint();
            return "Game View: " + targetWidth + "x" + targetHeight + ", group " + groupType;
        }
    }
}
