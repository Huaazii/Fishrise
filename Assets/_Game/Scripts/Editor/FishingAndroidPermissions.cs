using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Fishing.Editor
{
    /// <summary>Declares the normal Android permission required by the game's 20 ms haptic cues.</summary>
    public sealed class FishingAndroidPermissions : IPostGenerateGradleAndroidProject
    {
        /// <summary>Applies after Unity generates the library manifest.</summary>
        public int callbackOrder => 10;
        /// <summary>Adds only VIBRATE; no activity or launcher manifest is replaced.</summary>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = new XmlDocument(); document.Load(manifest);
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (XmlNode child in document.DocumentElement.ChildNodes)
                if (child.Name == "uses-permission" && child.Attributes?["name", android]?.Value == "android.permission.VIBRATE") return;
            var permission = document.CreateElement("uses-permission");
            permission.SetAttribute("name", android, "android.permission.VIBRATE");
            document.DocumentElement.AppendChild(permission); document.Save(manifest);
        }
    }
}
