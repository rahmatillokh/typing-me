using System.Collections;
using TMPro;
using TypingMe.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>One key on the stylised bar (§6). Purely visual — it never reads input.</summary>
    public sealed class KeyboardKeyView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image glow;
        [SerializeField] private TMP_Text label;

        private Color _idleBackground = new Color(1f, 1f, 1f, 0.06f);
        private Color _idleLabel = Color.white;
        private Color _glowColour = Color.cyan;
        private Color _alertColour = Color.red;

        private Coroutine _flash;
        private bool _isNextTarget;

        public char Key { get; private set; }

        public void Bind(char key, ThemeSO theme)
        {
            Key = key;
            if (label != null) label.text = key.ToString().ToUpperInvariant();
            ApplyTheme(theme);
        }

        public void ApplyTheme(ThemeSO theme)
        {
            if (theme == null) return;

            _glowColour = theme.keyboardGlow;
            _alertColour = theme.accentAlert;
            _idleLabel = theme.textMuted;
            _idleBackground = new Color(theme.accentPrimary.r, theme.accentPrimary.g, theme.accentPrimary.b, 0.07f);

            if (_flash == null) ResetVisuals();
        }

        /// <summary>Highlights the key the targeted word needs next (§6).</summary>
        public void SetNextTarget(bool isNext)
        {
            _isNextTarget = isNext;
            if (_flash == null) ResetVisuals();
        }


        public void FlashCorrect() => Flash(_glowColour, 0.22f);

        public void FlashWrong() => Flash(_alertColour, 0.26f);

        private void Flash(Color colour, float duration)
        {
            if (!isActiveAndEnabled) return;

            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine(colour, duration));
        }

        private IEnumerator FlashRoutine(Color colour, float duration)
        {
            if (glow != null)
            {
                glow.enabled = true;
                glow.color = colour;
            }

            if (label != null) label.color = Color.white;
            if (background != null) background.color = new Color(colour.r, colour.g, colour.b, 0.45f);

            transform.localScale = Vector3.one * 1.12f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                transform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Fx.Juice.EaseOutCubic(t));

                if (glow != null)
                {
                    Color fading = colour;
                    fading.a = Mathf.Lerp(1f, 0f, t);
                    glow.color = fading;
                }

                if (background != null)
                    background.color = Color.Lerp(new Color(colour.r, colour.g, colour.b, 0.45f), TargetBackground(), t);

                yield return null;
            }

            _flash = null;
            ResetVisuals();
        }

        private Color TargetBackground() =>
            _isNextTarget
                ? new Color(_glowColour.r, _glowColour.g, _glowColour.b, 0.3f)
                : _idleBackground;

        private void ResetVisuals()
        {
            transform.localScale = Vector3.one;


            if (background != null) background.color = TargetBackground();
            if (label != null) label.color = _isNextTarget ? Color.white : _idleLabel;

            if (glow == null) return;

            if (_isNextTarget)
            {
                glow.enabled = true;
                Color soft = _glowColour;
                soft.a = 0.55f;
                glow.color = soft;
            }
            else
            {
                glow.enabled = false;
            }
        }
    }
}
