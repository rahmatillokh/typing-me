using System;
using System.Collections.Generic;
using TypingMe.Core;
using TypingMe.Data;
using UnityEngine;

namespace TypingMe.UI
{
    /// <summary>
    /// Owns the active palette (§7). The theme is not a player choice: each season carries its own
    /// palette, and the only way to change theme is to beat the season and move into the next one.
    /// Lives on the persistent services object so the theme survives scene changes.
    /// </summary>
    public sealed class ThemeManager : MonoBehaviour
    {
        [Tooltip("One theme per season, indexed by (int)Season: Spring, Summer, Autumn, Winter.")]
        [SerializeField] private List<ThemeSO> seasonThemes = new List<ThemeSO>();

        public static ThemeManager Instance { get; private set; }

        /// <summary>
        /// Static so <see cref="TypingMe.Fx.ThemedGraphic"/> can subscribe in OnEnable without caring
        /// whether the manager has awoken yet.
        /// </summary>
        public static event Action<ThemeSO> ThemeChanged;

        public static ThemeSO ActiveTheme { get; private set; }

        /// <summary>The season whose palette is currently applied.</summary>
        public static Season ActiveSeason { get; private set; } = Season.Spring;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ApplyProgressSeason();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public ThemeSO ThemeFor(Season season)
        {
            int index = (int)season;
            if (index >= 0 && index < seasonThemes.Count && seasonThemes[index] != null)
                return seasonThemes[index];

            return seasonThemes.Count > 0 ? seasonThemes[0] : null;
        }

        /// <summary>
        /// Applies a season's palette and announces it. This is the single switch point: the moment
        /// a season boss falls, calling this with the next season recolours every live scene at once.
        /// </summary>
        public void ApplySeason(Season season)
        {
            ThemeSO theme = ThemeFor(season);
            if (theme == null)
            {
                Debug.LogWarning($"[ThemeManager] No season themes assigned; cannot apply {season}.");
                return;
            }

            ActiveSeason = season;
            ActiveTheme = theme;
            ThemeChanged?.Invoke(theme);
        }

        /// <summary>The palette for the season a given level belongs to.</summary>
        public void ApplyLevelSeason(int levelNumber) => ApplySeason(SeasonCatalog.SeasonOf(levelNumber));

        /// <summary>The palette for where the campaign currently stands — used by menus and boot.</summary>
        public void ApplyProgressSeason() => ApplyLevelSeason(SaveSystem.Data.lastPlayedLevel);

        /// <summary>Re-broadcasts the active theme; used by scenes that load after the theme was set.</summary>
        public static void Republish()
        {
            if (ActiveTheme != null) ThemeChanged?.Invoke(ActiveTheme);
        }
    }
}
