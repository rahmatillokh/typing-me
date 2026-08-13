using System;
using System.Collections.Generic;
using UnityEngine;

namespace TypingMe.Data
{
    /// <summary>How <see cref="WordBankSO"/> decides which tier a word belongs to.</summary>
    public enum WordRankingMode
    {
        /// <summary>Line number in the source list is the frequency rank (list must be sorted most-frequent first).</summary>
        FrequencyRank = 0,

        /// <summary>Source list is unordered; tier by word length only. Use this for lists like dwyl/english-words.</summary>
        LengthOnly = 1
    }

    /// <summary>
    /// Parses a bundled word list into tiered, in-memory pools (§4).
    /// Nothing in gameplay reads the text asset directly, so swapping the source list is a one-field change.
    /// </summary>
    [CreateAssetMenu(menuName = "Typing Me/Word Bank", fileName = "WordBank")]
    public sealed class WordBankSO : ScriptableObject
    {
        [Serializable]
        public sealed class TierRule
        {
            public WordTier tier;

            [Tooltip("1-based inclusive frequency rank (line number). Ignored in LengthOnly mode.")]
            public int minRank = 1;

            [Tooltip("Inclusive. Use int.MaxValue for 'to the end of the list'.")]
            public int maxRank = 1000;

            [Tooltip("Inclusive word length bounds.")]
            public int minLength = 3;

            public int maxLength = 5;
        }

        [Header("Source")]
        [Tooltip("One word per line. In FrequencyRank mode it must be sorted most-frequent first.")]
        [SerializeField] private TextAsset sourceList;

        [SerializeField] private WordRankingMode rankingMode = WordRankingMode.FrequencyRank;

        [Header("Tiers (§4)")]
        [SerializeField]
        private TierRule[] tiers =
        {
            new TierRule { tier = WordTier.Easy,   minRank = 1,    maxRank = 1000,          minLength = 3, maxLength = 5 },
            new TierRule { tier = WordTier.Medium, minRank = 1001, maxRank = 5000,          minLength = 5, maxLength = 7 },
            new TierRule { tier = WordTier.Hard,   minRank = 5001, maxRank = int.MaxValue,  minLength = 7, maxLength = 24 }
        };

        /// <summary>Used only when <see cref="sourceList"/> is missing, so a broken asset never hard-stops the game.</summary>
        private static readonly string[] FallbackWords =
        {
            "neon", "grid", "byte", "code", "data", "link", "node", "sync", "wave", "zero",
            "cipher", "matrix", "vector", "signal", "packet", "kernel", "daemon", "socket",
            "protocol", "bandwidth", "encryption", "mainframe", "firewall", "singularity"
        };

        [NonSerialized] private Dictionary<WordTier, List<string>> _byTier;

        public WordRankingMode RankingMode => rankingMode;
        public bool IsLoaded => _byTier != null;

        /// <summary>Parses the source list once. Safe to call every frame; it early-outs after the first build.</summary>
        public void EnsureLoaded()
        {
            if (_byTier != null) return;
            Reload();
        }

        /// <summary>Forces a re-parse. Call after changing the source asset at edit time.</summary>
        public void Reload()
        {
            _byTier = new Dictionary<WordTier, List<string>>();
            foreach (WordTier tier in Enum.GetValues(typeof(WordTier)))
                _byTier[tier] = new List<string>();

            List<string> words = ReadSourceWords();

            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i];
                int rank = i + 1;

                for (int r = 0; r < tiers.Length; r++)
                {
                    if (Matches(tiers[r], rank, word.Length))
                        _byTier[tiers[r].tier].Add(word);
                }
            }

            BackfillEmptyTiers(words);
        }

        private bool Matches(TierRule rule, int rank, int length)
        {
            if (length < rule.minLength || length > rule.maxLength) return false;
            if (rankingMode == WordRankingMode.LengthOnly) return true;
            return rank >= rule.minRank && rank <= rule.maxRank;
        }

        /// <summary>
        /// A tier with no words would starve the spawner, so borrow from the whole corpus (length-filtered)
        /// rather than letting <see cref="GetRandomWord"/> fail.
        /// </summary>
        private void BackfillEmptyTiers(List<string> allWords)
        {
            for (int r = 0; r < tiers.Length; r++)
            {
                TierRule rule = tiers[r];
                List<string> pool = _byTier[rule.tier];
                if (pool.Count > 0) continue;

                foreach (string word in allWords)
                {
                    if (word.Length >= rule.minLength && word.Length <= rule.maxLength)
                        pool.Add(word);
                }

                if (pool.Count == 0)
                {
                    Debug.LogWarning($"[WordBank] Tier {rule.tier} is empty even after backfill; using fallback words.");
                    pool.AddRange(FallbackWords);
                }
            }
        }

        private List<string> ReadSourceWords()
        {
            var result = new List<string>(16384);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (sourceList == null)
            {
                Debug.LogWarning("[WordBank] No source list assigned; falling back to the built-in word set.");
                result.AddRange(FallbackWords);
                return result;
            }

            string text = sourceList.text;
            int start = 0;

            for (int i = 0; i <= text.Length; i++)
            {
                if (i != text.Length && text[i] != '\n' && text[i] != '\r') continue;

                if (i > start)
                {
                    string candidate = Normalize(text, start, i - start);
                    if (candidate != null && seen.Add(candidate))
                        result.Add(candidate);
                }

                start = i + 1;
            }

            return result;
        }

        /// <summary>Lowercases and rejects anything that is not a run of a-z, so typing stays layout-simple.</summary>
        private static string Normalize(string source, int start, int length)
        {
            if (length < 2 || length > 24) return null;

            Span<char> buffer = stackalloc char[length];
            int count = 0;

            for (int i = 0; i < length; i++)
            {
                char c = char.ToLowerInvariant(source[start + i]);
                if (c < 'a' || c > 'z') return null;
                buffer[count++] = c;
            }

            return count < 2 ? null : new string(buffer[..count]);
        }

        public int CountFor(WordTier tier)
        {
            EnsureLoaded();
            return _byTier[tier].Count;
        }

        public IReadOnlyList<string> GetPool(WordTier tier)
        {
            EnsureLoaded();
            return _byTier[tier];
        }

        /// <summary>Random word from a tier. Falls back to a neighbouring tier if this one is somehow empty.</summary>
        public string GetRandomWord(WordTier tier, System.Random rng)
        {
            EnsureLoaded();

            List<string> pool = _byTier[tier];
            if (pool.Count == 0)
            {
                foreach (WordTier other in Enum.GetValues(typeof(WordTier)))
                {
                    if (_byTier[other].Count <= 0) continue;
                    pool = _byTier[other];
                    break;
                }
            }

            return pool.Count == 0
                ? FallbackWords[rng.Next(FallbackWords.Length)]
                : pool[rng.Next(pool.Count)];
        }
    }
}
