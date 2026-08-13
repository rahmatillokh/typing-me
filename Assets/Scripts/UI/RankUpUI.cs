using System;
using System.Collections;
using TMPro;
using TypingMe.Audio;
using TypingMe.Data;
using TypingMe.Fx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>
    /// The moment between ranks: a full-screen interstitial shown when the player beats the last
    /// boss of a rank — "RANK D CLEARED", a line of fire, and a live teaser of the next rank's
    /// actual boss patrolling the screen. Beating a season boss upgrades it into the season
    /// handover: the whole game recolours to the next season's palette mid-animation.
    /// </summary>
    /// <remarks>
    /// The teaser is not an illustration — it is a real <see cref="BossUI"/> bound to the next
    /// level's generated <see cref="BossDefinition"/>, so the archetype, colour and sigil letter
    /// the player is shown are exactly what they will fight next.
    /// </remarks>
    public sealed class RankUpUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image dim;
        [SerializeField] private Image flash;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text motivationLabel;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private RectTransform previewRoot;
        [SerializeField] private BossUI previewBoss;
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;

        [Tooltip("Seconds before any-key/continue can dismiss, so the level's last keystroke " +
                 "cannot skip the moment unseen.")]
        [SerializeField] private float inputGrace = 0.7f;

        private Action _onDone;
        private Coroutine _sequence;
        private float _shownAt;
        private bool _armed;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(Dismiss);
        }

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
        /// Plays the interstitial for a beaten rank. <paramref name="next"/> is the boss of the
        /// first level of the next rank; <paramref name="onDone"/> runs after dismissal — the
        /// caller uses it to bring up the ordinary level-complete panel.
        /// </summary>
        public void Show(BossDefinition cleared, BossDefinition next, Action onDone)
        {
            if (cleared == null || next == null)
            {
                onDone?.Invoke();
                return;
            }

            _onDone = onDone;
            gameObject.SetActive(true);

            if (_sequence != null) StopCoroutine(_sequence);
            _sequence = StartCoroutine(ShowRoutine(cleared, next));
        }

        private IEnumerator ShowRoutine(BossDefinition cleared, BossDefinition next)
        {
            bool seasonHandover = cleared.IsSeasonBoss;

            _shownAt = Time.unscaledTime;
            _armed = true;

            // Opening beat wears the season being left; the handover recolours it live below.
            ApplyPalette(cleared, next, seasonHandover, revealed: false);

            if (group != null)
            {
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            if (previewRoot != null) previewRoot.localScale = Vector3.zero;
            if (previewBoss != null) previewBoss.Bind(next);

            if (titleLabel != null)
                StartCoroutine(Juice.ScaleTo(titleLabel.transform, Vector3.one * 1.35f, Vector3.one,
                    0.4f, Juice.EaseOutBack));

            yield return Juice.Fade(group, 0f, 1f, 0.25f);

            if (seasonHandover)
            {
                yield return WaitUnscaled(0.35f);

                // The handover itself: one flash, and every live scene — this overlay included —
                // snaps to the next season's palette through ThemeManager.ThemeChanged.
                if (ThemeManager.Instance != null) ThemeManager.Instance.ApplySeason(next.Season);
                ApplyPalette(cleared, next, true, revealed: true);

                if (AudioManager.Instance != null) AudioManager.Instance.PlayClear();
                if (flash != null)
                    StartCoroutine(Juice.FlashColor(flash, new Color(1f, 1f, 1f, 0.55f), Color.clear, 0.5f));
                if (titleLabel != null)
                    StartCoroutine(Juice.PunchScale(titleLabel.transform, 0.3f, 0.3f));
            }

            yield return WaitUnscaled(0.2f);

            if (previewRoot != null)
                yield return Juice.ScaleTo(previewRoot, Vector3.zero, Vector3.one, 0.45f, Juice.EaseOutBack);

            _sequence = null;
        }

        /// <summary>All the text and colour for the two beats of the sequence.</summary>
        private void ApplyPalette(BossDefinition cleared, BossDefinition next, bool seasonHandover,
            bool revealed)
        {
            ThemeSO theme = ThemeManager.ActiveTheme;

            if (titleLabel != null)
            {
                if (seasonHandover)
                {
                    titleLabel.text = revealed
                        ? $"{SeasonCatalog.DisplayName(next.Season)} RISES"
                        : $"{SeasonCatalog.DisplayName(cleared.Season)} CONQUERED";
                    titleLabel.color = theme != null ? theme.accentPrimary : Color.white;
                }
                else
                {
                    titleLabel.text = $"RANK {cleared.Rank} CLEARED";
                    titleLabel.color = HudUI.RankColour(cleared.Rank);
                }
            }

            if (motivationLabel != null)
            {
                motivationLabel.text = MotivationFor(cleared);
                motivationLabel.color = theme != null ? theme.textPrimary : Color.white;
            }

            if (nextLabel != null)
            {
                string callsign = $"RANK {next.Rank} — {BossUI.ArchetypeName(next.Rank)}";
                nextLabel.text = seasonHandover
                    ? $"{SeasonCatalog.DisplayName(next.Season)} OPENS WITH {callsign}"
                    : $"NEXT · {callsign}";
                nextLabel.color = HudUI.RankColour(next.Rank);
            }

            if (continueLabel != null && theme != null) continueLabel.color = theme.textMuted;
            if (dim != null) dim.color = new Color(0f, 0f, 0f, 0.82f);
        }

        private static string MotivationFor(BossDefinition cleared) => cleared.Rank switch
        {
            BossRank.D => "THE SLIME IS A PUDDLE. SPEED IS A HABIT NOW.",
            BossRank.C => "THE THORN IS DULLED. YOUR RHYTHM SHOWS.",
            BossRank.B => "THE WITCH IS GROUNDED. FINGERS LIKE LIGHTNING.",
            BossRank.A => "THE GOLEM IS RUBBLE. THE DRAGON IS WATCHING.",
            _ => "THE DRAGON HAS FALLEN. A NEW WORLD OPENS."
        };

        private void Update()
        {
            if (!_armed || Time.unscaledTime - _shownAt < inputGrace) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) Dismiss();
        }

        private void Dismiss()
        {
            if (!_armed || Time.unscaledTime - _shownAt < inputGrace) return;

            _armed = false;

            if (_sequence != null)
            {
                StopCoroutine(_sequence);
                _sequence = null;
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();
            StartCoroutine(DismissRoutine());
        }

        private IEnumerator DismissRoutine()
        {
            yield return Juice.Fade(group, group != null ? group.alpha : 1f, 0f, 0.2f);

            Hide();

            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        /// <summary>Unscaled wait — the game may be frozen behind this panel.</summary>
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until) yield return null;
        }
    }
}
