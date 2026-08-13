using System;
using UnityEngine;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// Counts missed words. Per §11 a "mistake" is a word reaching the bottom line uncleared —
    /// a stray wrong keystroke is feedback only, never a miss.
    /// </summary>
    public sealed class MistakeTracker : MonoBehaviour
    {
        [SerializeField] private int maxMisses = 3;

        public int Misses { get; private set; }
        public int MaxMisses => maxMisses;
        public int Remaining => Mathf.Max(0, maxMisses - Misses);
        public bool IsExhausted => Misses >= maxMisses;

        /// <summary>(misses, maxMisses)</summary>
        public event Action<int, int> Changed;

        /// <summary>Raised once, on the miss that ends the run.</summary>
        public event Action Exhausted;

        public void ResetFor(int newMaxMisses)
        {
            maxMisses = Mathf.Max(1, newMaxMisses);
            Misses = 0;
            Changed?.Invoke(Misses, maxMisses);
        }

        public void RegisterMiss()
        {
            if (IsExhausted) return;

            Misses++;
            Changed?.Invoke(Misses, maxMisses);

            if (IsExhausted) Exhausted?.Invoke();
        }
    }
}
