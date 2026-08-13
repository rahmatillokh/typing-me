using System;
using System.Collections.Generic;
using TMPro;
using TypingMe.Audio;
using TypingMe.Core;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// The level grid on Home: four season tabs, each showing that season's 20 levels
    /// (19 ordinary plus the season boss).
    /// </summary>
    /// <remarks>
    /// Tabs rather than one 80-tile list: seasons are the campaign's structure, and a season that
    /// hasn't been reached should read as locked rather than as more scrolling.
    /// </remarks>
    public sealed class LevelSelectUI : MonoBehaviour
    {
        [Serializable]
        public sealed class SeasonTab
        {
            public Button button;
            public TMP_Text label;
            public Image frame;
            public GameObject lockedBadge;
        }

        [SerializeField] private SeasonTab[] seasonTabs = new SeasonTab[SeasonCatalog.SeasonCount];
        [SerializeField] private RectTransform grid;
        [SerializeField] private LevelButton buttonPrefab;

        private readonly List<LevelButton> _buttons = new List<LevelButton>();

        public Season CurrentSeason { get; private set; } = Season.Spring;

        private void Awake()
        {
            for (int i = 0; i < seasonTabs.Length; i++)
            {
                SeasonTab tab = seasonTabs[i];
                if (tab?.button == null) continue;

                var season = (Season)i;
                tab.button.onClick.AddListener(() => SelectSeason(season));
            }
        }

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += HandleThemeChanged;

            // Open on the season the player is actually in.
            int level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;
            CurrentSeason = SeasonCatalog.SeasonOf(level);

            Rebuild();
        }

        private void OnDisable() => ThemeManager.ThemeChanged -= HandleThemeChanged;

        private void HandleThemeChanged(ThemeSO theme) => Rebuild();

        public void SelectSeason(Season season)
        {
            if (!IsSeasonUnlocked(season)) return;

            CurrentSeason = season;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            Rebuild();
        }

        private static bool IsSeasonUnlocked(Season season)
        {
            int unlocked = GameManager.Instance != null ? GameManager.Instance.HighestUnlockedLevel : 1;
            return SeasonCatalog.FirstLevelOf(season) <= unlocked;
        }

        public void Rebuild()
        {
            RebuildTabs();
            RebuildGrid();
        }

        private void RebuildTabs()
        {
            ThemeSO theme = ThemeManager.ActiveTheme;

            for (int i = 0; i < seasonTabs.Length; i++)
            {
                SeasonTab tab = seasonTabs[i];
                if (tab == null) continue;

                var season = (Season)i;
                bool unlocked = IsSeasonUnlocked(season);
                bool active = season == CurrentSeason;

                if (tab.label != null)
                {
                    tab.label.text = SeasonCatalog.DisplayName(season);

                    if (theme != null)
                    {
                        Color colour = !unlocked ? theme.textMuted
                            : active ? theme.accentPrimary
                            : theme.textPrimary;
                        colour.a = unlocked ? 1f : 0.4f;
                        tab.label.color = colour;
                    }
                }

                if (tab.frame != null && theme != null)
                {
                    Color frameColour = active ? theme.accentPrimary : theme.textMuted;
                    frameColour.a = active ? 0.22f : 0.08f;
                    tab.frame.color = frameColour;
                }

                if (tab.lockedBadge != null) tab.lockedBadge.SetActive(!unlocked);
                if (tab.button != null) tab.button.interactable = unlocked;
            }
        }

        private void RebuildGrid()
        {
            if (grid == null || buttonPrefab == null) return;

            int unlocked = GameManager.Instance != null ? GameManager.Instance.HighestUnlockedLevel : 1;
            int current = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;

            EnsureCapacity(SeasonCatalog.LevelsPerSeason);

            for (int i = 0; i < _buttons.Count; i++)
            {
                bool visible = i < SeasonCatalog.LevelsPerSeason;
                _buttons[i].gameObject.SetActive(visible);
                if (!visible) continue;

                int globalLevel = SeasonCatalog.GlobalLevel(CurrentSeason, i + 1);
                _buttons[i].Bind(globalLevel, globalLevel <= unlocked, globalLevel == current, Select);
            }
        }

        private void EnsureCapacity(int count)
        {
            while (_buttons.Count < count)
            {
                LevelButton button = Instantiate(buttonPrefab, grid);
                button.name = $"Level{_buttons.Count + 1:00}";
                _buttons.Add(button);
            }
        }

        private void Select(int levelNumber)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (GameManager.Instance != null) GameManager.Instance.PlayLevel(levelNumber);
        }
    }
}
