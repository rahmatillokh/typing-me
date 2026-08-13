using TMPro;
using TypingMe.Audio;
using TypingMe.Core;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>Home tab: Continue, the level grid, and the season the campaign stands in (§7).</summary>
    public sealed class HomeUI : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private TMP_Text bestScoreLabel;
        [SerializeField] private TMP_Text seasonLabel;
        [SerializeField] private LevelSelectUI levelSelect;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        private void OnEnable()
        {
            ThemeManager.ThemeChanged += HandleThemeChanged;
            SaveSystem.Changed += HandleSaveChanged;
            Refresh();
        }

        private void OnDisable()
        {
            ThemeManager.ThemeChanged -= HandleThemeChanged;
            SaveSystem.Changed -= HandleSaveChanged;
        }

        private void HandleThemeChanged(ThemeSO theme) => Refresh();

        private void HandleSaveChanged(SaveData data) => Refresh();

        public void Refresh()
        {
            SaveData data = SaveSystem.Data;

            if (continueLabel != null)
            {
                Season season = SeasonCatalog.SeasonOf(data.lastPlayedLevel);
                int inSeason = SeasonCatalog.LevelInSeason(data.lastPlayedLevel);
                continueLabel.text = $"CONTINUE  ·  {SeasonCatalog.DisplayName(season)} {inSeason:00}";
            }

            if (bestScoreLabel != null) bestScoreLabel.text = $"BEST  {data.bestScore:N0}";

            if (seasonLabel != null)
            {
                seasonLabel.text = $"{SeasonCatalog.DisplayName(ThemeManager.ActiveSeason)} SEASON";

                ThemeSO theme = ThemeManager.ActiveTheme;
                if (theme != null) seasonLabel.color = theme.accentSecondary;
            }

            if (levelSelect != null) levelSelect.Rebuild();
        }

        private void Continue()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (GameManager.Instance != null) GameManager.Instance.ContinueRun();
        }
    }
}
