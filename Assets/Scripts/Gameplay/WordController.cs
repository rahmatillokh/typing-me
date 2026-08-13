using System;
using System.Collections;
using System.Text;
using TMPro;
using TypingMe.Data;
using TypingMe.Fx;
using UnityEngine;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// One falling word. Owns its own matched-length state and per-letter highlighting (§9);
    /// the router only ever hands it a character and asks whether it matched.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class WordController : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup group;

        private readonly StringBuilder _builder = new StringBuilder(96);

        private RectTransform _rect;
        private float _fallSpeed;
        private float _bottomY;
        private float _startY;
        private AnimationCurve _easing;
        private bool _resolving;

        private string _typedHex = "00FFF2";
        private string _nextHex = "FF2FD0";
        private string _restHex = "E8FAFF";
        private Color _alertColor = new Color32(0xFF, 0x38, 0x60, 0xFF);

        public string Word { get; private set; } = string.Empty;
        public int MatchedLength { get; private set; }
        public bool IsTargeted { get; private set; }

        /// <summary>Scales the fall speed. Driven by the boss's Speed Surge attack.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>How fast the throw's sideways momentum bleeds off; ~95% spent in 0.7s.</summary>
        private const float DriftDecay = 4.5f;

        /// <summary>Sideways velocity from being thrown, decaying to zero.</summary>
        private float _drift;
        private float _driftLimit;

        // Season wind (§ seasons): a bounded sine displacement layered over the fall, so Autumn
        // words gust sideways while Spring words drop clean. Tracked as last-offset so only the
        // *delta* is applied each frame and the word never strays more than the amplitude.
        private float _swayAmplitude;
        private float _swayRate;
        private float _swayPhase;
        private float _swayTime;
        private float _swayLastOffset;

        /// <summary>
        /// True while the boss's Word Veil attack hides this word's unread letters.
        /// Matching is unaffected — the word stays typeable, it just can't be read ahead.
        /// </summary>
        public bool IsVeiled { get; private set; }

        /// <summary>False once the word has been cleared or missed — it is then playing its exit animation.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>
        /// Where the spawner intends this word to settle horizontally. A thrown word is still
        /// travelling for a moment, so the spawner separates new arrivals against this rather
        /// than against positions that are about to change.
        /// </summary>
        public float PlannedX { get; set; }

        public bool IsComplete => MatchedLength >= Word.Length;

        /// <summary>The next letter the player must type, or '\0' when the word is finished.</summary>
        public char NextChar => MatchedLength < Word.Length ? Word[MatchedLength] : '\0';

        /// <summary>Vertical position; smaller means closer to the bottom line (used by the targeting rule).</summary>
        public float CurrentY => _rect != null ? _rect.anchoredPosition.y : 0f;

        /// <summary>Where the word currently sits, so effects can be spawned on it.</summary>
        public Vector2 AnchoredPosition => _rect != null ? _rect.anchoredPosition : Vector2.zero;

        /// <summary>
        /// Laid-out width of the plain word, used by the spawner to keep long words on screen.
        /// Measured from <see cref="Word"/> rather than the rich-text string so the colour tags
        /// can't skew the result.
        /// </summary>
        public float MeasuredWidth => label != null ? label.GetPreferredValues(Word).x : 0f;

        public event Action<WordController> Cleared;
        public event Action<WordController> Missed;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            if (label == null) label = GetComponentInChildren<TMP_Text>();
            if (group == null) group = GetComponent<CanvasGroup>();
        }

        public void Initialise(string word, float fallSpeed, Vector2 spawnPosition, float bottomY,
            AnimationCurve easing, ThemeSO theme)
        {
            if (_rect == null) _rect = (RectTransform)transform;

            Word = string.IsNullOrEmpty(word) ? "null" : word;
            MatchedLength = 0;
            IsTargeted = false;
            IsAlive = true;
            _resolving = false;

            _fallSpeed = Mathf.Max(1f, fallSpeed);
            _bottomY = bottomY;
            _startY = spawnPosition.y;
            _easing = easing;

            _rect.anchoredPosition = spawnPosition;
            PlannedX = spawnPosition.x;
            if (group != null) group.alpha = 1f;

            _swayAmplitude = 0f;
            _swayTime = 0f;
            _swayLastOffset = 0f;

            ApplyTheme(theme);
            Render();

            StartCoroutine(Juice.ScaleTo(transform, Vector3.one * 0.75f, Vector3.one, 0.22f, Juice.EaseOutBack));
        }

        public void ApplyTheme(ThemeSO theme)
        {
            if (theme == null) return;

            _typedHex = ColorUtility.ToHtmlStringRGB(theme.accentPrimary);
            _nextHex = ColorUtility.ToHtmlStringRGB(theme.accentSecondary);
            _restHex = ColorUtility.ToHtmlStringRGB(theme.textPrimary);
            _alertColor = theme.accentAlert;

            if (IsAlive) Render();
        }

        private void Update()
        {
            if (!IsAlive || _resolving) return;

            float travelled = Mathf.Max(1f, _startY - _bottomY);
            float progress = Mathf.Clamp01((_startY - _rect.anchoredPosition.y) / travelled);
            float multiplier = _easing?.Evaluate(progress) ?? 1f;

            Vector2 position = _rect.anchoredPosition;
            position.y -= _fallSpeed * multiplier * Mathf.Max(0.1f, SpeedMultiplier) * Time.deltaTime;

            if (Mathf.Abs(_drift) > 1f)
            {
                position.x += _drift * Time.deltaTime;
                position.x = Mathf.Clamp(position.x, -_driftLimit, _driftLimit);

                _drift *= Mathf.Exp(-DriftDecay * Time.deltaTime);
            }

            if (_swayAmplitude > 0f)
            {
                _swayTime += Time.deltaTime;

                float offset = Mathf.Sin(_swayTime * _swayRate * Mathf.PI * 2f + _swayPhase) * _swayAmplitude;
                position.x += offset - _swayLastOffset;
                _swayLastOffset = offset;

                if (_driftLimit > 0f) position.x = Mathf.Clamp(position.x, -_driftLimit, _driftLimit);
            }

            _rect.anchoredPosition = position;

            if (position.y <= _bottomY) ResolveAsMissed();
        }

        /// <summary>
        /// Consumes <paramref name="typed"/> if it is this word's next letter.
        /// Comparison is case-insensitive (§4); the word bank stores lowercase.
        /// </summary>
        public bool TryConsume(char typed)
        {
            if (!IsAlive || IsComplete) return false;
            if (char.ToLowerInvariant(typed) != Word[MatchedLength]) return false;

            MatchedLength++;
            Render();

            if (IsComplete) ResolveAsCleared();
            return true;
        }

        public void SetTargeted(bool targeted)
        {
            if (IsTargeted == targeted) return;

            IsTargeted = targeted;
            if (IsAlive) Render();
        }

        /// <summary>
        /// Takes over an already-typed prefix when the router re-routes a lock onto this word —
        /// the letters were typed once on the abandoned word and must not need typing again.
        /// </summary>
        public void AdoptProgress(int matchedLength)
        {
            if (!IsAlive || IsComplete) return;

            MatchedLength = Mathf.Clamp(matchedLength, 0, Word.Length);
            Render();
        }

        /// <summary>
        /// Hands back progress when the router re-routes its lock away: the typed letters now
        /// belong to the new word, so this one must read — and match — as untouched again.
        /// </summary>
        public void ResetProgress()
        {
            if (MatchedLength == 0) return;

            MatchedLength = 0;
            if (IsAlive) Render();
        }

        public void SetVeiled(bool veiled)
        {
            if (IsVeiled == veiled) return;

            IsVeiled = veiled;
            if (IsAlive) Render();
        }

        public void ResetSpeedMultiplier() => SpeedMultiplier = 1f;

        /// <summary>
        /// Throws the word sideways so words the boss launches fan out instead of dropping in a
        /// column. The drift decays away, leaving the vertical fall the tuning is built on.
        /// </summary>
        /// <param name="horizontalDistance">
        /// How far the word should end up from where it spawned, in reference px. Taking a distance
        /// rather than a velocity lets the spawner aim words at specific, non-overlapping columns.
        /// </param>
        /// <param name="limit">Half-width the word must stay inside.</param>
        public void ThrowBy(float horizontalDistance, float limit)
        {
            // Drift is v·e^(-k·t), which integrates to v/k — so the velocity for a given distance
            // is simply distance × k.
            _drift = horizontalDistance * DriftDecay;
            _driftLimit = Mathf.Max(0f, limit);
        }

        /// <summary>
        /// The season's wind on this word: a sine displacement of up to
        /// <paramref name="amplitude"/> px at <paramref name="rate"/> Hz. Zero amplitude is a
        /// clean vertical fall.
        /// </summary>
        /// <param name="limit">Half-width the word must stay inside while swaying.</param>
        public void SetSway(float amplitude, float rate, float phase, float limit)
        {
            _swayAmplitude = Mathf.Max(0f, amplitude);
            _swayRate = Mathf.Max(0f, rate);
            _swayPhase = phase;
            _swayTime = 0f;
            _swayLastOffset = 0f;

            if (limit > 0f) _driftLimit = Mathf.Max(_driftLimit, limit);
        }

        private void Render()
        {
            if (label == null) return;

            _builder.Clear();

            if (MatchedLength > 0)
            {
                _builder.Append("<color=#").Append(_typedHex).Append('>')
                        .Append(Word, 0, MatchedLength)
                        .Append("</color>");
            }

            int index = MatchedLength;

            // The targeted word calls out its next letter, so the player can see the lock (§3, §6).
            if (IsTargeted && index < Word.Length)
            {
                _builder.Append("<color=#").Append(_nextHex).Append("><b>")
                        .Append(Word[index])
                        .Append("</b></color>");
                index++;
            }

            if (index < Word.Length)
            {
                _builder.Append("<color=#").Append(_restHex).Append('>');

                if (IsVeiled)
                {
                    // ASCII on purpose: a block glyph would be missing from the mono font and render
                    // as a tofu box. Length is preserved so the word still reads as the same shape.
                    _builder.Append('#', Word.Length - index);
                }
                else
                {
                    _builder.Append(Word, index, Word.Length - index);
                }

                _builder.Append("</color>");
            }

            label.text = _builder.ToString();
        }

        private void ResolveAsCleared()
        {
            if (_resolving) return;

            _resolving = true;
            IsAlive = false;
            Cleared?.Invoke(this);
            StartCoroutine(ClearRoutine());
        }

        private void ResolveAsMissed()
        {
            if (_resolving) return;

            _resolving = true;
            IsAlive = false;
            Missed?.Invoke(this);
            StartCoroutine(MissRoutine());
        }

        private IEnumerator ClearRoutine()
        {
            StartCoroutine(Juice.PunchScale(transform, 0.5f, 0.24f));
            yield return Juice.Fade(group, 1f, 0f, 0.24f);
            Destroy(gameObject);
        }

        private IEnumerator MissRoutine()
        {
            if (label != null) label.color = _alertColor;
            if (label != null) label.text = Word;

            StartCoroutine(Juice.ShakeAnchored(_rect, 10f, 0.2f));
            yield return Juice.Fade(group, 1f, 0f, 0.2f);
            Destroy(gameObject);
        }
    }
}
