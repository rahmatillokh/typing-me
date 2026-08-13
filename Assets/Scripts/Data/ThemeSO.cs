using UnityEngine;

namespace TypingMe.Data
{
    /// <summary>Which colour on the active theme a themed graphic should take (§7 Theme System).</summary>
    public enum ThemeRole
    {
        BackgroundTop,
        BackgroundBottom,
        AccentPrimary,
        AccentSecondary,
        AccentAlert,
        Grid,
        KeyboardGlow,
        TextPrimary,
        TextMuted,
        Panel
    }

    /// <summary>
    /// One season's cyberpunk palette. Applied across scenes by <see cref="TypingMe.UI.ThemeManager"/>,
    /// which swaps it the moment the campaign crosses into the next season.
    /// </summary>
    [CreateAssetMenu(menuName = "Typing Me/Theme", fileName = "Theme")]
    public sealed class ThemeSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, used only for asset naming and debugging.")]
        public string themeId = "spring";

        public string displayName = "Spring";

        [Header("Background")]
        [Tooltip("Optional. When null the background is the vertical gradient below.")]
        public Sprite backgroundSprite;

        public Color backgroundTop = new Color32(0x0A, 0x0E, 0x17, 0xFF);
        public Color backgroundBottom = new Color32(0x0D, 0x02, 0x21, 0xFF);
        public Color grid = new Color32(0x00, 0xFF, 0xF2, 0x14);

        [Header("Accents (§8)")]
        public Color accentPrimary = new Color32(0x00, 0xFF, 0xF2, 0xFF);
        public Color accentSecondary = new Color32(0xFF, 0x2F, 0xD0, 0xFF);
        public Color accentAlert = new Color32(0xFF, 0x38, 0x60, 0xFF);
        public Color keyboardGlow = new Color32(0x00, 0xFF, 0xF2, 0xFF);

        [Header("Text & panels")]
        public Color textPrimary = new Color32(0xE8, 0xFA, 0xFF, 0xFF);
        public Color textMuted = new Color32(0x7A, 0x8C, 0xA6, 0xFF);
        public Color panel = new Color32(0x12, 0x18, 0x28, 0xD8);

        public Color Resolve(ThemeRole role) => role switch
        {
            ThemeRole.BackgroundTop => backgroundTop,
            ThemeRole.BackgroundBottom => backgroundBottom,
            ThemeRole.AccentPrimary => accentPrimary,
            ThemeRole.AccentSecondary => accentSecondary,
            ThemeRole.AccentAlert => accentAlert,
            ThemeRole.Grid => grid,
            ThemeRole.KeyboardGlow => keyboardGlow,
            ThemeRole.TextPrimary => textPrimary,
            ThemeRole.TextMuted => textMuted,
            ThemeRole.Panel => panel,
            _ => Color.white
        };
    }
}
