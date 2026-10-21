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
        internal UiWidgets(GameUiAssets art) { Art = art; }
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
            var rect = Panel(name, parent, Color.white, true); Place(rect, Vector2.one * .5f, position, size);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = primary ? new Color(.10f, .52f, .63f) : new Color(.10f, .23f, .30f);
            colors.highlightedColor = new Color(.20f, .64f, .74f); colors.pressedColor = new Color(.07f, .35f, .45f);
            colors.disabledColor = new Color(.12f, .21f, .26f, .7f); colors.fadeDuration = .08f; button.colors = colors;
            button.onClick.AddListener(() => clicked());
            Label("Label", rect, text, Vector2.zero, size - new Vector2(16, 8), 28);
            return button;
        }
        internal static void ButtonText(Button button, string text) { button.GetComponentInChildren<Text>().text = text; }
    }
}
