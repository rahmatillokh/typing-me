using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.Fx
{
    public enum GeneratedPattern
    {
        /// <summary>Faint circuit-like grid that drifts behind the play area (§8).</summary>
        Grid,

        /// <summary>Horizontal CRT scanlines (§8).</summary>
        Scanlines
    }

    /// <summary>
    /// Generates a tiling pattern texture and scrolls its UVs. Covers both the moving grid background
    /// and the scanline overlay without any authored art or shaders.
    /// </summary>
    /// <remarks>
    /// Colour comes from a sibling <see cref="ThemedGraphic"/>; the texture itself is white-on-transparent.
    /// </remarks>
    [RequireComponent(typeof(RawImage))]
    public sealed class ScrollingTexture : MonoBehaviour
    {
        [SerializeField] private GeneratedPattern pattern = GeneratedPattern.Grid;

        [Tooltip("How many times the pattern repeats across the graphic.")]
        [SerializeField] private Vector2 tiling = new Vector2(12f, 7f);

        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.01f, -0.02f);

        [Header("Pattern")]
        [SerializeField] private int cellPixels = 32;
        [SerializeField] private int lineThickness = 1;

        private RawImage _image;
        private Texture2D _texture;
        private Vector2 _offset;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _texture = Build();
            _image.texture = _texture;
            _image.uvRect = new Rect(0f, 0f, tiling.x, tiling.y);
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            _offset += scrollSpeed * Time.unscaledDeltaTime;

            // Keep the offset bounded so precision doesn't drift over a long session.
            _offset.x -= Mathf.Floor(_offset.x);
            _offset.y -= Mathf.Floor(_offset.y);

            _image.uvRect = new Rect(_offset.x, _offset.y, tiling.x, tiling.y);
        }

        private Texture2D Build()
        {
            int size = Mathf.Max(4, cellPixels);
            int height = pattern == GeneratedPattern.Scanlines ? Mathf.Max(2, cellPixels) : size;

            var texture = new Texture2D(size, height, TextureFormat.RGBA32, false)
            {
                name = $"Generated_{pattern}",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[size * height];
            int thickness = Mathf.Clamp(lineThickness, 1, size / 2);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool lit = pattern == GeneratedPattern.Scanlines
                        ? y < thickness
                        : x < thickness || y < thickness;

                    pixels[y * size + x] = lit
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
