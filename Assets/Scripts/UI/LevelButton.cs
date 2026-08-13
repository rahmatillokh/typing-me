using System;
using TMPro;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>One node in the level grid: its number within the season and its boss rank.</summary>
    public sealed class LevelButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private Image frame;
        [SerializeField] private GameObject lockedBadge;

        public int LevelNumber { get; private set; }
        public bool IsUnlocked { get; private set; }

        public void Bind(int globalLevelNumber, bool unlocked, bool isCurrent, Action<int> onSelected)
        {
            LevelNumber = globalLevelNumber;
            IsUnlocked = unlocked;

            BossRank rank = SeasonCatalog.RankFor(globalLevelNumber);
            int inSeason = SeasonCatalog.LevelInSeason(globalLevelNumber);

            if (numberLabel != null) numberLabel.text = inSeason.ToString("00");
            if (rankLabel != null) rankLabel.text = unlocked ? rank.ToString() : "?";
            if (lockedBadge != null) lockedBadge.SetActive(!unlocked);

            if (button != null)
            {
                button.interactable = unlocked;
                button.onClick.RemoveAllListeners();
                if (unlocked) button.onClick.AddListener(() => onSelected?.Invoke(globalLevelNumber));
            }

            Repaint(rank, isCurrent);
        }

        private void Repaint(BossRank rank, bool isCurrent)
        {
            ThemeSO theme = ThemeManager.ActiveTheme;
            if (theme == null) return;

            // The season boss reads differently from the 19 ordinary levels at a glance.
            Color accent = rank == BossRank.S
                ? theme.accentSecondary
                : isCurrent ? theme.accentSecondary : theme.accentPrimary;

            if (frame != null)
            {
                Color frameColour = IsUnlocked ? accent : theme.textMuted;
                frameColour.a = IsUnlocked ? (rank == BossRank.S ? 0.85f : 0.5f) : 0.28f;
                frame.color = frameColour;
            }

            if (numberLabel != null)
            {
                Color textColour = IsUnlocked ? theme.textPrimary : theme.textMuted;
                textColour.a = IsUnlocked ? 1f : 0.5f;
                numberLabel.color = textColour;
            }

            if (rankLabel == null) return;

            Color rankColour = IsUnlocked ? HudUI.RankColour(rank) : theme.textMuted;
            rankColour.a = IsUnlocked ? 1f : 0.4f;
            rankLabel.color = rankColour;
        }
    }
}
