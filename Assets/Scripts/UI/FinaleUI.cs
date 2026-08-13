using System.Collections;
using System.Collections.Generic;
using TMPro;
using TypingMe.Audio;
using TypingMe.Core;
using TypingMe.Data;
using TypingMe.Fx;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// The campaign finale, shown once the last level of Winter falls: the player is named the
    /// hero, told the game has no level left worthy of them, and offered a fresh start.
    /// </summary>
    /// <remarks>
    /// The tribute is revealed word by word with the game's own key tick — the campaign ends the
    /// way it was played, one typed word at a time. The five defeated creatures line up beneath it
    /// as trophies. "Start a new journey" wipes progress behind the same two-tap confirm the
    /// Settings reset uses, then drops the player straight into Spring 01.
    /// </remarks>
    public sealed class FinaleUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image dim;
        [SerializeField] private Image flash;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text heroLabel;
        [SerializeField] private TMP_Text tributeLabel;
        [SerializeField] private TMP_Text noLevelsLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private RectTransform trophyRoot;
        [SerializeField] private Button newJourneyButton;
        [SerializeField] private TMP_Text newJourneyLabel;
        [SerializeField] private Button homeButton;

        [Tooltip("One creature per rank, indexed by (int)BossRank — the defeated, lined up.")]
        [SerializeField] private Sprite[] rankSprites = new Sprite[5];

        [Tooltip("Seconds between tribute words. The reveal is the ceremony; don't rush it.")]
        [SerializeField] private float wordCadence = 0.16f;

        private const string Tribute =
            "EIGHTY BOSSES FELL. FOUR SEASONS TURNED. FROM THE FIRST SLIME OF SPRING TO THE LAST " +
            "DRAGON OF WINTER, EVERY WORD YOU TYPED BECAME A BLADE. THE KEYBOARD REMEMBERS ITS HERO.";

        private const string NoLevels = "NO LEVEL WE HAVE LEFT IS A MATCH FOR YOU.";

        private const string NewJourneyIdleText = "START A NEW JOURNEY";
        private const string NewJourneyConfirmText = "TAP AGAIN TO CONFIRM";

        private readonly List<RectTransform> _trophies = new List<RectTransform>();

        private Coroutine _sequence;
        private Coroutine _confirmTimeout;
        private bool _awaitingConfirm;
        private float _bobTime;

        private void Awake()
        {
            if (newJourneyButton != null) newJourneyButton.onClick.AddListener(HandleNewJourneyPressed);
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);
        }

        public void Hide()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            CancelConfirm();
            gameObject.SetActive(false);
        }

        /// <summary>Plays the finale. <paramref name="finalScore"/> is the winning run's score.</summary>
        public void Show(int finalScore)
        {
            gameObject.SetActive(true);

            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = StartCoroutine(ShowRoutine(finalScore));
        }

        private IEnumerator ShowRoutine(int finalScore)
        {
            Color gold = HudUI.RankColour(BossRank.S);
            ThemeSO theme = ThemeManager.ActiveTheme;

            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            if (dim != null) dim.color = new Color(0f, 0f, 0f, 0.9f);

            if (titleLabel != null)
            {
                titleLabel.text = "ALL SEASONS CONQUERED";
                titleLabel.color = theme != null ? theme.accentPrimary : Color.white;
            }

            if (heroLabel != null)
            {
                heroLabel.text = "YOU ARE THE HERO";
                heroLabel.color = gold;
                heroLabel.transform.localScale = Vector3.zero;
            }

            // Both lines start hidden and are typed out below, word by word.
            if (tributeLabel != null)
            {
                tributeLabel.text = Tribute;
                tributeLabel.maxVisibleWords = 0;
                if (theme != null) tributeLabel.color = theme.textPrimary;
            }

            if (noLevelsLabel != null)
            {
                noLevelsLabel.text = NoLevels;
                noLevelsLabel.maxVisibleWords = 0;
                if (theme != null) noLevelsLabel.color = theme.textMuted;
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = $"FINAL RUN  {finalScore:N0}   ·   BEST  {SaveSystem.Data.bestScore:N0}";
                if (theme != null) scoreLabel.color = theme.textMuted;
            }

            if (newJourneyLabel != null) newJourneyLabel.text = NewJourneyIdleText;

            BuildTrophies();

            if (titleLabel != null)
                StartCoroutine(Juice.ScaleTo(titleLabel.transform, Vector3.one * 1.3f, Vector3.one,
                    0.45f, Juice.EaseOutBack));

            yield return Juice.Fade(group, 0f, 1f, 0.35f);
            yield return WaitUnscaled(0.25f);

            // The coronation beat: the hero line lands with a gold flash.
            if (heroLabel != null)
                StartCoroutine(Juice.ScaleTo(heroLabel.transform, Vector3.zero, Vector3.one,
                    0.5f, Juice.EaseOutBack));

            if (flash != null)
                StartCoroutine(Juice.FlashColor(flash,
                    new Color(gold.r, gold.g, gold.b, 0.4f), Color.clear, 0.6f));

            if (AudioManager.Instance != null) AudioManager.Instance.PlayClear();

            yield return WaitUnscaled(0.55f);

            // The trophies rise one by one — the campaign's cast taking its bow.
            for (int i = 0; i < _trophies.Count; i++)
            {
                StartCoroutine(Juice.ScaleTo(_trophies[i], Vector3.zero, Vector3.one,
                    0.35f, Juice.EaseOutBack));
                yield return WaitUnscaled(0.12f);
            }

            // The tribute types itself out, one word per tick, with the game's own key sound.
            yield return RevealWords(tributeLabel);
            yield return WaitUnscaled(0.3f);
            yield return RevealWords(noLevelsLabel);

            _sequence = null;
        }

        private IEnumerator RevealWords(TMP_Text label)
        {
            if (label == null) yield break;

            int words = label.text.Split(' ').Length;

            for (int shown = 1; shown <= words; shown++)
            {
                label.maxVisibleWords = shown;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayKey();

                yield return WaitUnscaled(Mathf.Max(0.02f, wordCadence));
            }

            // TMP counts words its own way; make sure nothing stays hidden on a count mismatch.
            label.maxVisibleWords = int.MaxValue;
        }

        /// <summary>The five defeated creatures, lined up in rank order under the tribute.</summary>
        private void BuildTrophies()
        {
            for (int i = 0; i < _trophies.Count; i++)
                if (_trophies[i] != null) Destroy(_trophies[i].gameObject);

            _trophies.Clear();

            if (trophyRoot == null || rankSprites == null) return;

            const float spacing = 132f;
            float start = -spacing * (rankSprites.Length - 1) * 0.5f;

            for (int i = 0; i < rankSprites.Length; i++)
            {
                if (rankSprites[i] == null) continue;

                var go = new GameObject($"Trophy{(BossRank)i}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(trophyRoot, false);

                var image = go.GetComponent<Image>();
                image.sprite = rankSprites[i];
                image.type = Image.Type.Simple;
                image.raycastTarget = false;

                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(92f, 92f);
                rect.anchoredPosition = new Vector2(start + i * spacing, 0f);
                rect.localScale = Vector3.zero;

                _trophies.Add(rect);
            }
        }

        private void Update()
        {
            if (_trophies.Count == 0) return;

            // A gentle idle bob, phase-offset per creature so the row ripples.
            _bobTime += Time.unscaledDeltaTime;

            for (int i = 0; i < _trophies.Count; i++)
            {
                if (_trophies[i] == null) continue;

                Vector2 position = _trophies[i].anchoredPosition;
                position.y = Mathf.Sin(_bobTime * 1.6f + i * 0.7f) * 6f;
                _trophies[i].anchoredPosition = position;
            }
        }

        #region Buttons

        /// <summary>Two-step confirm — a full campaign shouldn't vanish on one stray tap.</summary>
        private void HandleNewJourneyPressed()
        {
            if (!_awaitingConfirm)
            {
                _awaitingConfirm = true;
                if (newJourneyLabel != null) newJourneyLabel.text = NewJourneyConfirmText;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

                _confirmTimeout = StartCoroutine(ConfirmTimeout());
                return;
            }

            CancelConfirm();

            // The new journey: progress wiped, palette back to Spring, straight into level one.
            SaveSystem.ResetProgress();
            if (ThemeManager.Instance != null) ThemeManager.Instance.ApplyProgressSeason();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (GameManager.Instance != null) GameManager.Instance.PlayLevel(1);
        }

        private IEnumerator ConfirmTimeout()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            CancelConfirm();
        }

        private void CancelConfirm()
        {
            if (_confirmTimeout != null)
            {
                StopCoroutine(_confirmTimeout);
                _confirmTimeout = null;
            }

            _awaitingConfirm = false;
            if (newJourneyLabel != null) newJourneyLabel.text = NewJourneyIdleText;
        }

        private void GoHome()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            if (GameManager.Instance != null) GameManager.Instance.GoHome();
        }

        #endregion

        /// <summary>Unscaled wait — the game is frozen behind this panel.</summary>
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }
    }
}
