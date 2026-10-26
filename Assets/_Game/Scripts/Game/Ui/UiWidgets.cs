using System;
using UnityEngine;
using UnityEngine.UI;

namespace Fishing.Game
{
    /// <summary>Shared UGUI construction and restrained ice-water palette for the game's views.</summary>
    internal sealed class UiWidgets
    {
        internal static readonly Color White = new Color(.91f, .97f, 1);
        internal static readonly Color Muted = new Color(.53f, .71f, .78f);
        internal static readonly Color Accent = new Color(.26f, .86f, .9f);
        internal static readonly Color Full = new Color(1, .39f, .27f);
        internal readonly GameUiAssets Art;
        private readonly Action<GameFeedback.Cue> _sound;
        internal UiWidgets(GameUiAssets art, Action<GameFeedback.Cue> sound = null) { Art = art; _sound = sound; }
        internal void Sound(GameFeedback.Cue cue) { _sound?.Invoke(cue); }
        internal static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size; rect.anchoredPosition = position; }
        internal static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }
        internal GameObject Overlay(string name, Transform parent, float opacity)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.015f, .06f, .105f, opacity); image.raycastTarget = opacity > 0;
            return rect.gameObject;
        }
        internal RectTransform Panel(string name, Transform parent, Color color, bool blocks = false)
        {
            var rect = Rect(name, parent); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Art.panel; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = blocks;
            return rect;
        }
        internal RectTransform Frame(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            var fill = Panel("Card background", rect, new Color(.025f, .10f, .16f, .98f), true);
            Stretch(fill, 30, 30, 30, 30);
            var border = Picture("Ice border", rect, Art.frame, Vector2.zero, Vector2.one);
            Stretch(border.rectTransform); border.type = Image.Type.Sliced; border.preserveAspect = false;
            border.pixelsPerUnitMultiplier = 2.5f; return rect;
        }
        internal Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size,
            int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            var rect = Rect(name, parent); Place(rect, Vector2.one * .5f, position, size);
            var label = rect.gameObject.AddComponent<Text>(); label.font = Art.font; label.text = text;
            label.fontSize = fontSize; label.color = color ?? White; label.alignment = alignment;
            label.raycastTarget = false; label.supportRichText = false; label.lineSpacing = 1.12f;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        internal Text AnchoredLabel(string name, Transform parent, string text, Vector2 anchor, Vector2 position,
            Vector2 size, int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            var label = Label(name, parent, text, position, size, fontSize, alignment, color);
            Place(label.rectTransform, anchor, position, size); return label;
        }
        internal Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var rect = Rect(name, parent); Place(rect, Vector2.one * .5f, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        internal Button Button(string name, Transform parent, string text, Vector2 position, Vector2 size, Action clicked, bool primary = true)
        {
            var root = Panel(name, parent, Color.clear, true); Place(root, Vector2.one * .5f, position, size);
            root.GetComponent<Image>().sprite = null;
            var lower = Rect("Button ice base", root);
            var baseGraphic = lower.gameObject.AddComponent<IceButtonGraphic>(); baseGraphic.BaseOnly = true; baseGraphic.raycastTarget = false;
            var surface = Rect("Button surface", root); surface.offsetMin = surface.offsetMax = new Vector2(0, 7);
            var face = Rect("Button face", surface);
            var graphic = face.gameObject.AddComponent<IceButtonGraphic>(); graphic.Primary = primary;
            graphic.color = graphic.FaceColor; graphic.raycastTarget = false;
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
            var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(.97f, 1, 1); colors.pressedColor = new Color(.82f, .92f, .98f);
            colors.disabledColor = new Color(.5f, .62f, .7f); colors.fadeDuration = .08f; button.colors = colors;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            button.onClick.AddListener(() => { Sound(GameFeedback.Cue.Ui); clicked(); });
            var label = Label("Label", surface, text, Vector2.zero, size - new Vector2(28, 12), primary ? 30 : 27);
            label.fontStyle = FontStyle.Bold; Stretch(label.rectTransform, 14, 6, 14, 6);
            root.gameObject.AddComponent<IceButtonFeedback>().Bind(button, surface, primary);
            return button;
        }
        internal static void ButtonText(Button button, string text) { button.GetComponentInChildren<Text>().text = text; }
    }
}
