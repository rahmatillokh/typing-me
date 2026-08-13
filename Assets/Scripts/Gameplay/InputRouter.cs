using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// Turns raw character events into hits on a single locked word (§9).
    /// </summary>
    /// <remarks>
    /// Uses <c>Keyboard.onTextInput</c> rather than key codes so the mapping follows the player's OS
    /// layout — an AZERTY player types the letters they see, and the on-screen QWERTY graphic stays a
    /// purely visual reference (§6, §11).
    ///
    /// The lock is a guess the player can overrule by typing. With "taste" and "trip" on screen,
    /// 't' locks the lower of the two — say "taste" — but a player going for "trip" keeps typing
    /// 'r'. Instead of flashing a wrong key, the router looks for another live word that starts
    /// with everything typed so far plus the new letter ("tr…") and re-routes the lock there,
    /// carrying the progress across. Only when no such continuation exists is it a wrong key.
    /// </remarks>
    public sealed class InputRouter : MonoBehaviour
    {
        [SerializeField] private WordSpawner spawner;

        private Keyboard _subscribed;

        /// <summary>Gate for typing; the level runner opens it only while the run is live.</summary>
        public bool AcceptInput { get; set; }

        /// <summary>The word the player is locked onto, or null when nothing is locked.</summary>
        public WordController Target { get; private set; }

        /// <summary>(character, word it landed on)</summary>
        public event Action<char, WordController> CorrectKey;

        /// <summary>A keystroke that matched nothing. Feedback only — never a miss (§11).</summary>
        public event Action<char> WrongKey;

        /// <summary>Raised when the lock moves, so the keyboard visual can re-highlight.</summary>
        public event Action<WordController> TargetChanged;

        public void Bind(WordSpawner wordSpawner) => spawner = wordSpawner;

        private void OnEnable() => Subscribe(Keyboard.current);

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            // Re-bind whenever Keyboard.current changes identity. This replaces reacting to specific
            // onDeviceChange cases: any case not in that list — a device swapped on focus change, a
            // re-plug reported as something unexpected — left the handler bound to a dead keyboard
            // and typing silently stopped working with nothing to show for it. Comparing the
            // reference every frame costs nothing and cannot miss a case.
            if (!ReferenceEquals(_subscribed, Keyboard.current))
            {
                Unsubscribe();
                Subscribe(Keyboard.current);
            }

            // The target can die between keystrokes (cleared by its last letter, or missed).
            if (Target != null && !Target.IsAlive) SetTarget(null);
        }

        private void Subscribe(Keyboard keyboard)
        {
            if (keyboard == null || ReferenceEquals(keyboard, _subscribed)) return;

            _subscribed = keyboard;
            _subscribed.onTextInput += HandleTextInput;
        }

        private void Unsubscribe()
        {
            if (_subscribed == null) return;

            _subscribed.onTextInput -= HandleTextInput;
            _subscribed = null;
        }

        private void HandleTextInput(char typed) => SubmitCharacter(typed);

        /// <summary>
        /// Routes one typed character. Physical keys arrive here via <c>onTextInput</c>; it is public so
        /// the loop can also be driven without a real keyboard — by automated tests today, and by an
        /// on-screen or accessibility input source later.
        /// </summary>
        public void SubmitCharacter(char typed)
        {
            if (!AcceptInput || spawner == null) return;

            char c = char.ToLowerInvariant(typed);
            if (c < 'a' || c > 'z') return; // words are a-z only, so ignore space/enter/punctuation

            if (Target != null && !Target.IsAlive) SetTarget(null);

            if (Target != null)
            {
                if (Target.TryConsume(c))
                {
                    RaiseCorrect(c, Target);
                    return;
                }

                // The lock was only ever a guess between words sharing a prefix. If another live
                // word continues everything typed so far with this letter, the player meant that
                // word — move the lock and the progress there rather than flashing a wrong key.
                WordController rerouted = FindPrefixContinuation(Target, c);
                if (rerouted == null)
                {
                    WrongKey?.Invoke(c);
                    return;
                }

                WordController abandoned = Target;
                int carried = abandoned.MatchedLength;

                abandoned.ResetProgress();
                rerouted.AdoptProgress(carried);

                SetTarget(rerouted);
                rerouted.TryConsume(c);
                RaiseCorrect(c, rerouted);
                return;
            }

            WordController acquired = FindLowestMatching(c);
            if (acquired == null)
            {
                WrongKey?.Invoke(c);
                return;
            }

            SetTarget(acquired);
            acquired.TryConsume(c);
            RaiseCorrect(c, acquired);
        }

        /// <summary>Lowest word (closest to the bottom line) whose next unmatched letter is <paramref name="c"/>.</summary>
        private WordController FindLowestMatching(char c)
        {
            IReadOnlyList<WordController> active = spawner.Active;

            WordController best = null;
            float lowestY = float.MaxValue;

            for (int i = 0; i < active.Count; i++)
            {
                WordController word = active[i];
                if (word == null || !word.IsAlive) continue;
                if (word.NextChar != c) continue;

                float y = word.CurrentY;
                if (y >= lowestY) continue;

                lowestY = y;
                best = word;
            }

            return best;
        }

        /// <summary>
        /// The lowest live word, other than the locked one, that starts with the locked word's
        /// matched prefix and continues it with <paramref name="c"/> — i.e. the word the player
        /// was actually typing. Ties resolve like acquisition: closest to the bottom line wins.
        /// </summary>
        private WordController FindPrefixContinuation(WordController locked, char c)
        {
            IReadOnlyList<WordController> active = spawner.Active;

            int prefixLength = locked.MatchedLength;
            string lockedWord = locked.Word;

            WordController best = null;
            float lowestY = float.MaxValue;

            for (int i = 0; i < active.Count; i++)
            {
                WordController word = active[i];
                if (word == null || !word.IsAlive || ReferenceEquals(word, locked)) continue;

                string candidate = word.Word;
                if (candidate.Length <= prefixLength) continue;
                if (candidate[prefixLength] != c) continue;
                if (string.CompareOrdinal(candidate, 0, lockedWord, 0, prefixLength) != 0) continue;

                float y = word.CurrentY;
                if (y >= lowestY) continue;

                lowestY = y;
                best = word;
            }

            return best;
        }

        private void RaiseCorrect(char c, WordController word)
        {
            CorrectKey?.Invoke(c, word);

            // Clearing the word releases the lock immediately so the next keystroke can acquire freely.
            if (word != null && !word.IsAlive) SetTarget(null);
        }

        private void SetTarget(WordController word)
        {
            if (ReferenceEquals(Target, word)) return;

            if (Target != null) Target.SetTargeted(false);
            Target = word;
            if (Target != null) Target.SetTargeted(true);

            TargetChanged?.Invoke(Target);
        }

        /// <summary>Drops the lock, e.g. when a level ends.</summary>
        public void ResetTarget() => SetTarget(null);
    }
}
