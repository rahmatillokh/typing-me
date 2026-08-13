using System;
using System.Collections;
using TMPro;
using TypingMe.Fx;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>Shared panel for Game Over and Level Complete (§3 steps 8–9).</summary>
    public sealed class GameEndPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;
        [SerializeField] private Button tertiaryButton;
        [SerializeField] private TMP_Text tertiaryLabel;
        [SerializeField] private RectTransform card;

        public void Hide()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the panel. The third button is optional — the buttons sit in a vertical layout, so
        /// hiding it simply collapses the row rather than leaving a gap.
        /// </summary>
        public void Show(string title, string subtitle,
            string primaryText, Action onPrimary,
            string secondaryText, Action onSecondary,
            string tertiaryText = null, Action onTertiary = null)
        {
            gameObject.SetActive(true);

            if (titleLabel != null) titleLabel.text = title;
            if (subtitleLabel != null) subtitleLabel.text = subtitle;

            Wire(primaryButton, primaryLabel, primaryText, onPrimary);
            Wire(secondaryButton, secondaryLabel, secondaryText, onSecondary);
            Wire(tertiaryButton, tertiaryLabel, tertiaryText, onTertiary);

            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            if (card != null) StartCoroutine(Juice.ScaleTo(card, Vector3.one * 0.85f, Vector3.one, 0.3f, Juice.EaseOutBack));
            yield return Juice.Fade(group, 0f, 1f, 0.25f);
        }

        private static void Wire(Button button, TMP_Text label, string text, Action callback)
        {
            if (button == null) return;

            bool visible = !string.IsNullOrEmpty(text) && callback != null;
            button.gameObject.SetActive(visible);
            if (!visible) return;

            if (label != null) label.text = text;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback());
        }
    }
}
