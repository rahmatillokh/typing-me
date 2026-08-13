using TypingMe.Data;
using TypingMe.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.Fx
{
    /// <summary>
    /// Declarative theming: tag any Image / RawImage / TMP text with the palette role it should take,
    /// and it recolours itself whenever the theme changes (§7). Keeps ThemeManager free of scene wiring.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public sealed class ThemedGraphic : MonoBehaviour
    {
        [SerializeField] private ThemeRole role = ThemeRole.AccentPrimary;

        [Tooltip("Scales the role colour's alpha, for subtle fills that share an accent.")]
        [SerializeField, Range(0f, 1f)] private float alphaMultiplier = 1f;

        private Graphic _graphic;

        public ThemeRole Role
        {
            get => role;
            set { role = value; Apply(ThemeManager.ActiveTheme); }
        }

        public float AlphaMultiplier
        {
            get => alphaMultiplier;
            set { alphaMultiplier = Mathf.Clamp01(value); Apply(ThemeManager.ActiveTheme); }
        }

        private void OnEnable()
        {
            _graphic = GetComponent<Graphic>();
            ThemeManager.ThemeChanged += Apply;
            Apply(ThemeManager.ActiveTheme);
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= Apply;

        private void Apply(ThemeSO theme)
        {
            if (theme == null) return;
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            if (_graphic == null) return;

            Color colour = theme.Resolve(role);
            colour.a *= alphaMultiplier;
            _graphic.color = colour;
        }
    }
}
