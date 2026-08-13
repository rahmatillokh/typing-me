using System.Collections.Generic;
using TypingMe.Data;
using TypingMe.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.Fx
{
    /// <summary>
    /// The season made visible: a layer of drifting motes between the background and the play
    /// content — blossom petals in Spring, rising embers in Summer, tumbling leaves in Autumn,
    /// snow in Winter. Rebuilt whenever the theme (and so the season) changes.
    /// </summary>
    /// <remarks>
    /// Pooled uGUI images for the same reason as <see cref="ShardBurst"/>: a ParticleSystem doesn't
    /// sort cleanly against a Screen Space – Camera canvas, and a couple dozen images sharing the
    /// canvas batch cost effectively nothing. Runs on unscaled time so the weather keeps moving
    /// through pause and end-of-run panels.
    /// </remarks>
    public sealed class SeasonalAmbientFx : MonoBehaviour
    {
        [SerializeField] private Sprite softSprite;
        [SerializeField] private Sprite circleSprite;

        [Tooltip("Motes kept alive at once. The pool is built once and retinted per season.")]
        [SerializeField] private int count = 26;

        /// <summary>Everything one mote needs to fly and wrap on its own.</summary>
        private struct Mote
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public float FallSpeed;      // Positive falls, negative rises (Summer embers).
            public float SwayAmplitude;
            public float SwayRate;
            public float SwayPhase;
            public float SpinSpeed;
            public float Alpha;
        }

        private readonly List<Mote> _motes = new List<Mote>();

        private RectTransform _self;
        private Season _builtSeason = (Season)(-1);
        private float _time;

        private void Awake() => _self = (RectTransform)transform;

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += HandleThemeChanged;
            Rebuild();
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= HandleThemeChanged;

        private void HandleThemeChanged(ThemeSO theme) => Rebuild();

        /// <summary>Retints and re-rolls the pool for the active season.</summary>
        private void Rebuild()
        {
            ThemeSO theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            Season season = ThemeManager.ActiveSeason;
            _builtSeason = season;

            EnsurePool();

            for (int i = 0; i < _motes.Count; i++)
            {
                Mote mote = _motes[i];
                RollMote(ref mote, season, theme, scatterVertically: true);
                _motes[i] = mote;
            }
        }

        private void EnsurePool()
        {
            while (_motes.Count < count)
            {
                var go = new GameObject($"Mote{_motes.Count}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;

                _motes.Add(new Mote { Rect = (RectTransform)go.transform, Image = image });
            }
        }

        /// <summary>
        /// Gives one mote a fresh body and flight plan for the season. Speeds are in reference px/s.
        /// </summary>
        private void RollMote(ref Mote mote, Season season, ThemeSO theme, bool scatterVertically)
        {
            float width = PlayWidth();
            float height = PlayHeight();

            float x = Random.Range(-width * 0.5f, width * 0.5f);
            float y = scatterVertically
                ? Random.Range(-height * 0.5f, height * 0.5f)
                : StartY(season, height);

            switch (season)
            {
                case Season.Spring: // Blossom petals: small, pink, lazy fall with a visible sway.
                    mote.Image.sprite = circleSprite;
                    mote.Rect.sizeDelta = Vector2.one * Random.Range(9f, 17f);
                    mote.FallSpeed = Random.Range(38f, 68f);
                    mote.SwayAmplitude = Random.Range(26f, 60f);
                    mote.SwayRate = Random.Range(0.5f, 1.1f);
                    mote.SpinSpeed = Random.Range(-40f, 40f);
                    mote.Alpha = Random.Range(0.20f, 0.40f);
                    mote.Image.color = Tint(theme.accentSecondary, mote.Alpha);
                    break;

                case Season.Summer: // Heat embers: tiny, gold, rising instead of falling.
                    mote.Image.sprite = softSprite;
                    mote.Rect.sizeDelta = Vector2.one * Random.Range(6f, 13f);
                    mote.FallSpeed = -Random.Range(28f, 62f);
                    mote.SwayAmplitude = Random.Range(10f, 26f);
                    mote.SwayRate = Random.Range(0.8f, 1.6f);
                    mote.SpinSpeed = 0f;
                    mote.Alpha = Random.Range(0.18f, 0.36f);
                    mote.Image.color = Tint(theme.accentPrimary, mote.Alpha);
                    break;

                case Season.Autumn: // Leaves: the biggest motes, tumbling hard in the wind.
                    mote.Image.sprite = circleSprite;
                    mote.Rect.sizeDelta = new Vector2(Random.Range(13f, 24f), Random.Range(9f, 16f));
                    mote.FallSpeed = Random.Range(64f, 110f);
                    mote.SwayAmplitude = Random.Range(50f, 110f);
                    mote.SwayRate = Random.Range(0.7f, 1.4f);
                    mote.SpinSpeed = Random.Range(-160f, 160f);
                    mote.Alpha = Random.Range(0.22f, 0.42f);
                    mote.Image.color = Tint(Color.Lerp(theme.accentPrimary, theme.accentSecondary,
                        Random.value), mote.Alpha);
                    break;

                default: // Winter snow: soft flakes, the slowest fall of the year.
                    mote.Image.sprite = softSprite;
                    mote.Rect.sizeDelta = Vector2.one * Random.Range(8f, 19f);
                    mote.FallSpeed = Random.Range(24f, 50f);
                    mote.SwayAmplitude = Random.Range(18f, 42f);
                    mote.SwayRate = Random.Range(0.35f, 0.8f);
                    mote.SpinSpeed = 0f;
                    mote.Alpha = Random.Range(0.24f, 0.45f);
                    mote.Image.color = Tint(Color.Lerp(theme.accentPrimary, Color.white, 0.55f), mote.Alpha);
                    break;
            }

            mote.SwayPhase = Random.Range(0f, Mathf.PI * 2f);
            mote.Position = new Vector2(x, y);
            mote.Rect.anchoredPosition = mote.Position;
        }

        private void Update()
        {
            if (_motes.Count == 0 || ThemeManager.ActiveTheme == null) return;

            // Weather is scenery: it keeps drifting while the game is paused, like the grid scroll.
            float dt = Time.unscaledDeltaTime;
            _time += dt;

            float height = PlayHeight();
            float halfHeight = height * 0.5f + 40f;

            for (int i = 0; i < _motes.Count; i++)
            {
                Mote mote = _motes[i];
                if (mote.Rect == null) continue;

                mote.Position.y -= mote.FallSpeed * dt;
                float sway = Mathf.Sin(_time * mote.SwayRate * Mathf.PI * 2f + mote.SwayPhase)
                             * mote.SwayAmplitude * dt;
                mote.Position.x += sway;

                if (mote.SpinSpeed != 0f)
                    mote.Rect.Rotate(0f, 0f, mote.SpinSpeed * dt);

                // Off the top or bottom → rejoin on the opposite edge with a fresh roll.
                if (mote.Position.y < -halfHeight || mote.Position.y > halfHeight)
                {
                    RollMote(ref mote, _builtSeason, ThemeManager.ActiveTheme, scatterVertically: false);
                }
                else
                {
                    mote.Rect.anchoredPosition = mote.Position;
                }

                _motes[i] = mote;
            }
        }

        /// <summary>Fresh motes enter from the edge they drift away from.</summary>
        private static float StartY(Season season, float height) =>
            season == Season.Summer ? -height * 0.5f - 30f : height * 0.5f + 30f;

        private float PlayWidth() => _self != null && _self.rect.width > 1f ? _self.rect.width : 1920f;
        private float PlayHeight() => _self != null && _self.rect.height > 1f ? _self.rect.height : 1080f;

        private static Color Tint(Color colour, float alpha) =>
            new Color(colour.r, colour.g, colour.b, alpha);
    }
}
