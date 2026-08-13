using System.Collections.Generic;
using TMPro;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// In-run HUD: season/level, boss rank, boss health, remaining misses and score.
    /// </summary>
    /// <remarks>
    /// The centre bar tracks boss health rather than a word counter — the boss ending the level is
    /// the win condition, so a separate "07 / 20" would just be a second, redundant progress bar.
    /// </remarks>
    public sealed class HudUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private Image rankFrame;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private Image healthFill;
        [SerializeField] private RectTransform missPipContainer;
        [SerializeField] private Image missPipPrefab;

        private readonly List<Image> _pips = new List<Image>();

        public void SetLevelLabel(string text)
        {
            if (levelLabel != null) levelLabel.text = text;
        }

        public void SetRank(BossRank rank)
        {
            if (rankLabel != null) rankLabel.text = rank.ToString();

            Color colour = RankColour(rank);
            if (rankLabel != null) rankLabel.color = Color.white;

            if (rankFrame == null) return;

            colour.a = 0.85f;
            rankFrame.color = colour;
        }

        /// <summary>
        /// Each rank's colour is its creature's palette — slime green, thorn cyan, witch violet,
        /// golem pink, dragon gold — so the HUD badge, level grid, sigil ring and rank-up screen
        /// all match the boss art they refer to.
        /// </summary>
        public static Color RankColour(BossRank rank) => rank switch
        {
            BossRank.D => new Color32(0x3F, 0xAE, 0x62, 0xFF),
            BossRank.C => new Color32(0x35, 0xB8, 0xC8, 0xFF),
            BossRank.B => new Color32(0x82, 0x57, 0xDE, 0xFF),
            BossRank.A => new Color32(0xFF, 0x5C, 0x9E, 0xFF),
            BossRank.S => new Color32(0xF2, 0xB4, 0x37, 0xFF),
            _ => Color.grey
        };

        public void SetBossHealth(float current, float max)
        {
            int shown = Mathf.CeilToInt(Mathf.Max(0f, current));

            if (healthLabel != null) healthLabel.text = $"HP {shown} / {Mathf.CeilToInt(max)}";
            if (healthFill != null) healthFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        public void SetScore(int score)
        {
            if (scoreLabel != null) scoreLabel.text = score.ToString("N0");
        }

        /// <summary>Rebuilds the miss pips; call once per level with the level's miss allowance.</summary>
        public void SetupMisses(int maxMisses)
        {
            if (missPipContainer == null || missPipPrefab == null) return;

            for (int i = 0; i < _pips.Count; i++)
                if (_pips[i] != null) Destroy(_pips[i].gameObject);

            _pips.Clear();

            for (int i = 0; i < maxMisses; i++)
            {
                Image pip = Instantiate(missPipPrefab, missPipContainer);
                pip.gameObject.SetActive(true);
                pip.name = $"MissPip{i + 1}";
                _pips.Add(pip);
            }

            SetMisses(0, maxMisses);
        }

        public void SetMisses(int misses, int maxMisses)
        {
            ThemeSO theme = ThemeManager.ActiveTheme;

            Color spent = theme != null ? theme.accentAlert : Color.red;
            Color remaining = theme != null ? theme.textMuted : Color.grey;
            remaining.a = 0.35f;

            for (int i = 0; i < _pips.Count; i++)
            {
                if (_pips[i] == null) continue;
                _pips[i].color = i < misses ? spent : remaining;
            }
        }
    }
}
