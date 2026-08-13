using System.Collections.Generic;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// The stylised QWERTY bar docked at the bottom (§6). Built from code so the scene stays light.
    /// </summary>
    /// <remarks>
    /// The layout is QWERTY by assumption (§11) and is a *display* only — input is layout-agnostic and
    /// arrives as characters via <see cref="TypingMe.Gameplay.InputRouter"/>. A key the player's layout
    /// doesn't have simply never lights up.
    /// </remarks>
    public sealed class KeyboardVisualUI : MonoBehaviour
    {
        private static readonly string[] QwertyRows = { "qwertyuiop", "asdfghjkl", "zxcvbnm" };

        [SerializeField] private KeyboardKeyView keyPrefab;
        [SerializeField] private RectTransform rowsParent;

        [Header("Metrics (reference px)")]
        [SerializeField] private float keySize = 74f;
        [SerializeField] private float keySpacing = 9f;
        [SerializeField] private float rowSpacing = 9f;

        [Tooltip("Left indent per row, giving the staggered QWERTY look.")]
        [SerializeField] private float[] rowIndents = { 0f, 30f, 76f };

        private readonly Dictionary<char, KeyboardKeyView> _keys = new Dictionary<char, KeyboardKeyView>();
        private char _highlighted;

        private void Awake() => Build();

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += ApplyTheme;
            ApplyTheme(ThemeManager.ActiveTheme);
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= ApplyTheme;

        private void Build()
        {
            if (_keys.Count > 0) return;
            if (keyPrefab == null || rowsParent == null)
            {
                Debug.LogWarning("[KeyboardVisual] Missing key prefab or rows parent; keyboard not built.");
                return;
            }

            VerticalLayoutGroup column = rowsParent.GetComponent<VerticalLayoutGroup>();
            if (column == null) column = rowsParent.gameObject.AddComponent<VerticalLayoutGroup>();

            column.spacing = rowSpacing;
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;

            for (int r = 0; r < QwertyRows.Length; r++)
            {
                var row = new GameObject($"Row{r + 1}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                row.transform.SetParent(rowsParent, false);

                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = keySpacing;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                int indent = Mathf.RoundToInt(r < rowIndents.Length ? rowIndents[r] : 0f);
                layout.padding = new RectOffset(indent, 0, 0, 0);

                foreach (char c in QwertyRows[r])
                {
                    KeyboardKeyView key = Instantiate(keyPrefab, row.transform);
                    key.name = $"Key_{char.ToUpperInvariant(c)}";

                    LayoutElement element = key.GetComponent<LayoutElement>();
                    if (element == null) element = key.gameObject.AddComponent<LayoutElement>();
                    element.preferredWidth = keySize;
                    element.preferredHeight = keySize;

                    key.Bind(c, ThemeManager.ActiveTheme);
                    _keys[c] = key;
                }
            }
        }

        private void ApplyTheme(ThemeSO theme)
        {
            foreach (KeyboardKeyView key in _keys.Values)
                if (key != null) key.ApplyTheme(theme);
        }

        /// <summary>Marks the key the locked word needs next. Pass '\0' to clear.</summary>
        public void HighlightNext(char c)
        {
            char next = char.ToLowerInvariant(c);
            if (next == _highlighted) return;

            if (_highlighted != '\0' && _keys.TryGetValue(_highlighted, out KeyboardKeyView previous))
                if (previous != null) previous.SetNextTarget(false);

            _highlighted = next >= 'a' && next <= 'z' ? next : '\0';

            if (_highlighted != '\0' && _keys.TryGetValue(_highlighted, out KeyboardKeyView current))
                if (current != null) current.SetNextTarget(true);
        }

        public void ClearHighlight() => HighlightNext('\0');

        public void FlashCorrect(char c)
        {
            if (_keys.TryGetValue(char.ToLowerInvariant(c), out KeyboardKeyView key) && key != null)
                key.FlashCorrect();
        }

        public void FlashWrong(char c)
        {
            if (_keys.TryGetValue(char.ToLowerInvariant(c), out KeyboardKeyView key) && key != null)
                key.FlashWrong();
        }

    }
}
