using TypingMe.Data;
using TypingMe.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.Fx
{
    /// <summary>
    /// Vertical near-black gradient behind everything (§8). Generates its own texture from the active
    /// theme, so a palette swap needs no art.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class GradientBackground : MonoBehaviour
    {
        private const int Resolution = 128;

        private RawImage _image;
        private Texture2D _texture;

        private void Awake()
        {
            _image = GetComponent<RawImage>();

            _texture = new Texture2D(1, Resolution, TextureFormat.RGBA32, false)
            {
                name = "GradientBackground",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            _image.texture = _texture;

            // The gradient lives in the texture; tinting it again would double-darken.
            _image.color = Color.white;
        }

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += Apply;
            Apply(ThemeManager.ActiveTheme);
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= Apply;

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private void Apply(ThemeSO theme)
        {
            if (theme == null || _texture == null) return;

            if (theme.backgroundSprite != null)
            {
                _image.texture = theme.backgroundSprite.texture;
                return;
            }

            _image.texture = _texture;

            for (int y = 0; y < Resolution; y++)
            {
                float t = y / (float)(Resolution - 1);
                _texture.SetPixel(0, y, Color.Lerp(theme.backgroundBottom, theme.backgroundTop, t));
            }

            _texture.Apply();
        }
    }
}
