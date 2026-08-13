using System;
using System.Collections;
using UnityEngine;

namespace TypingMe.Fx
{
    /// <summary>
    /// Small coroutine tween helpers covering the motion called for in §8 (punch-scale on clear,
    /// shake on miss, eased UI transitions).
    /// </summary>
    /// <remarks>
    /// §2 suggests DOTween. This project has no external tween dependency, so these stand in and keep
    /// the scaffold building offline. If DOTween is added later, the call sites are all inside Fx/,
    /// UI/ and Gameplay/WordController — swapping is mechanical.
    /// All of these run on unscaled time so effects still play while the game is paused.
    /// </remarks>
    public static class Juice
    {
        public static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

        public static float EaseInQuad(float x) => x * x;

        public static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        public static IEnumerator PunchScale(Transform target, float strength = 0.35f, float duration = 0.26f)
        {
            if (target == null) yield break;

            Vector3 baseScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Rise fast, settle slow.
                float pulse = Mathf.Sin(t * Mathf.PI) * (1f - t) * strength;
                if (target == null) yield break;
                target.localScale = baseScale * (1f + pulse);

                yield return null;
            }

            if (target != null) target.localScale = baseScale;
        }

        public static IEnumerator ScaleTo(Transform target, Vector3 from, Vector3 to, float duration,
            Func<float, float> ease = null)
        {
            if (target == null) yield break;

            ease ??= EaseOutBack;
            float elapsed = 0f;
            target.localScale = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (target == null) yield break;
                target.localScale = Vector3.LerpUnclamped(from, to, ease(t));
                yield return null;
            }

            if (target != null) target.localScale = to;
        }

        /// <summary>Positional shake that returns the transform to where it started.</summary>
        public static IEnumerator ShakeAnchored(RectTransform target, float strength = 16f, float duration = 0.3f)
        {
            if (target == null) yield break;

            Vector2 origin = target.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float damping = 1f - Mathf.Clamp01(elapsed / duration);

                if (target == null) yield break;
                target.anchoredPosition = origin + new Vector2(
                    UnityEngine.Random.Range(-strength, strength) * damping,
                    UnityEngine.Random.Range(-strength, strength) * damping);

                yield return null;
            }

            if (target != null) target.anchoredPosition = origin;
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            group.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (group == null) yield break;
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            if (group != null) group.alpha = to;
        }

        /// <summary>Cross-fades a graphic colour, e.g. the red alert flash on a miss.</summary>
        public static IEnumerator FlashColor(UnityEngine.UI.Graphic graphic, Color flash, Color restore, float duration)
        {
            if (graphic == null) yield break;

            graphic.color = flash;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (graphic == null) yield break;
                graphic.color = Color.Lerp(flash, restore, EaseOutCubic(Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            if (graphic != null) graphic.color = restore;
        }
    }
}
