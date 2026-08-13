using System.Collections.Generic;
using TypingMe.Data;
using UnityEngine;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// Builds a level from its number alone (§5), so difficulty scales smoothly and any level can be
    /// reproduced or resumed without storing it.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Arbitrary odd multiplier — spreads consecutive level numbers across the RNG space.</summary>
        private const int SeedMultiplier = 7919;

        public static LevelData Generate(int levelNumber, WordBankSO bank, LevelTuningSO tuning)
        {
            if (bank == null) throw new System.ArgumentNullException(nameof(bank));
            if (tuning == null) throw new System.ArgumentNullException(nameof(tuning));

            levelNumber = SeasonCatalog.Clamp(levelNumber);
            bank.EnsureLoaded();

            var rng = new System.Random(unchecked(levelNumber * SeedMultiplier));
            tuning.WeightsFor(levelNumber, out float easy, out float medium, out float hard);

            int target = Mathf.Max(1, tuning.wordsPerLevel);

            // The queue runs well past the nominal count. The boss's health ends the level, not a word
            // counter, so a run that misses words or ignores the sigil bonus simply needs more of them.
            int queueSize = tuning.QueueSizeFor();

            var words = new List<string>(queueSize);
            var used = new HashSet<string>();

            for (int i = 0; i < queueSize; i++)
            {
                WordTier tier = PickTier(rng, easy, medium, hard);

                // Retry a handful of times for variety, then accept a repeat rather than loop forever
                // on a small pool.
                string word = bank.GetRandomWord(tier, rng);
                for (int attempt = 0; attempt < 12 && !used.Add(word); attempt++)
                    word = bank.GetRandomWord(tier, rng);

                words.Add(word);
            }

            return new LevelData(
                levelNumber,
                words,
                target,
                tuning.FallSpeedFor(levelNumber),
                tuning.SpawnIntervalFor(levelNumber));
        }

        private static WordTier PickTier(System.Random rng, float easy, float medium, float hard)
        {
            double roll = rng.NextDouble();

            if (roll < easy) return WordTier.Easy;
            if (roll < easy + medium) return WordTier.Medium;
            return WordTier.Hard;
        }
    }
}
