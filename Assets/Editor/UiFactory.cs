using TMPro;
using TypingMe.Data;
using TypingMe.Fx;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.EditorTools
{
    /// <summary>Terse builders for the uGUI hierarchies the bootstrap generates.</summary>
    internal static class UiFactory
    {
        /// <summary>Rounded rect with a sliced border — the workhorse for panels, keys and buttons.</summary>
        public static Sprite RoundedSprite =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        public static Sprite CircleSprite =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        public static Sprite SoftSprite =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        public static RectTransform AddRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Stretches to fill the parent.</summary>
        public static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f,
            float bottom = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Anchors to one edge/corner with an explicit size.</summary>
        public static RectTransform Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform StretchHorizontal(RectTransform rect, float height, float yFromEdge,
            bool fromTop, float sideMargin = 0f)
        {
            rect.anchorMin = new Vector2(0f, fromTop ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, fromTop ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, fromTop ? 1f : 0f);
            rect.offsetMin = new Vector2(sideMargin, fromTop ? -yFromEdge - height : yFromEdge);
            rect.offsetMax = new Vector2(-sideMargin, fromTop ? -yFromEdge : yFromEdge + height);
            return rect;
        }

        public static Image AddImage(string name, Transform parent, Sprite sprite, Color colour,
            bool raycastTarget = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = raycastTarget;
            if (sprite != null) image.type = Image.Type.Sliced;

            return image;
        }

        public static RawImage AddRawImage(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);

            var raw = go.GetComponent<RawImage>();
            raw.color = colour;
            raw.raycastTarget = false;
            return raw;
        }

        public static TextMeshProUGUI AddText(string name, Transform parent, string content,
            TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = colour;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            return text;
        }

        /// <summary>Button with a rounded frame and a centred label; returns the button.</summary>
        public static Button AddButton(string name, Transform parent, string label, TMP_FontAsset font,
            float fontSize, Color frameColour, Color textColour, out TextMeshProUGUI labelText,
            out Image frame)
        {
            frame = AddImage(name, parent, RoundedSprite, frameColour, raycastTarget: true);

            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;

            ColorBlock colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colours.disabledColor = new Color(1f, 1f, 1f, 0.25f);
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            labelText = AddText($"{name}Label", frame.transform, label, font, fontSize,
                TextAlignmentOptions.Center, textColour);
            Stretch((RectTransform)labelText.transform);

            return button;
        }

        /// <summary>Tags a graphic so it follows a palette role (§7).</summary>
        public static T Themed<T>(T graphic, ThemeRole role, float alphaMultiplier = 1f) where T : Graphic
        {
            var themed = graphic.gameObject.AddComponent<ThemedGraphic>();

            using (var wiring = new Wiring(themed))
            {
                wiring.Enum("role", (int)role)
                      .Float("alphaMultiplier", alphaMultiplier);
            }

            return graphic;
        }

        public static CanvasGroup AddCanvasGroup(GameObject target, float alpha = 1f,
            bool interactable = true, bool blocksRaycasts = true)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();

            group.alpha = alpha;
            group.interactable = interactable;
            group.blocksRaycasts = blocksRaycasts;
            return group;
        }
    }
}
