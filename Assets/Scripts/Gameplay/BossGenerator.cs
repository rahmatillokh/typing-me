using System.Collections.Generic;
using TypingMe.Data;
using UnityEngine;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// Derives a level's boss from the level number and its generated word queue. Deterministic,
    /// so replaying a level always faces the same boss with the same sigil and health.
    /// </summary>
    public static class BossGenerator
    {
        private const int SeedMultiplier = 6151;

        public static BossDefinition Generate(int levelNumber, LevelData level, BossTuningSO tuning)
        {
            if (level == null) throw new System.ArgumentNullException(nameof(level));
            if (tuning == null) throw new System.ArgumentNullException(nameof(tuning));

            levelNumber = SeasonCatalog.Clamp(levelNumber);

            BossRank rank = SeasonCatalog.RankFor(levelNumber);
            BossRankProfile profile = tuning.ProfileFor(rank);

            var rng = new System.Random(unchecked(levelNumber * SeedMultiplier));

            char sigil = PickSigil(level, tuning.sigilTargetCoverage, rng);
            float health = ComputeHealth(level, tuning, profile);

            return new BossDefinition(
                levelNumber,
                SeasonCatalog.SeasonOf(levelNumber),
                SeasonCatalog.LevelInSeason(levelNumber),
                rank,
                sigil,
                health,
                profile);
        }

        /// <summary>
        /// Picks the letter whose share of the level's words is closest to the target coverage.
        /// </summary>
        /// <remarks>
        /// Chasing the sigil should be a real decision: pick 'e' and almost every word qualifies so
        /// the bonus is free; pick 'z' and it never fires. Selecting for a middling coverage keeps
        /// it worth aiming for.
        /// </remarks>
        private static char PickSigil(LevelData level, float targetCoverage, System.Random rng)
        {
            IReadOnlyList<string> words = level.Words;
            int considered = Mathf.Min(words.Count, Mathf.Max(1, level.TargetClears));
            if (considered == 0) return 'a';

            var containing = new int[26];

            for (int i = 0; i < considered; i++)
            {
                // Count each letter once per word: coverage is "words containing it", not total uses.
                var seen = new bool[26];
                string word = words[i];

                for (int c = 0; c < word.Length; c++)
                {
                    int index = word[c] - 'a';
                    if (index < 0 || index >= 26 || seen[index]) continue;

                    seen[index] = true;
                    containing[index]++;
                }
            }

            char best = 'a';
            float bestDistance = float.MaxValue;

            // Walk from a rotating offset so ties don't always resolve to the earliest letter.
            int offset = rng.Next(26);

            for (int step = 0; step < 26; step++)
            {
                int index = (step + offset) % 26;
                if (containing[index] == 0) continue;

                float coverage = containing[index] / (float)considered;
                float distance = Mathf.Abs(coverage - targetCoverage);

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = (char)('a' + index);
            }

            return best;
        }

        private static float ComputeHealth(LevelData level, BossTuningSO tuning, BossRankProfile profile)
        {
            int considered = Mathf.Min(level.Words.Count, Mathf.Max(1, level.TargetClears));

            int totalLetters = 0;
            for (int i = 0; i < considered; i++) totalLetters += level.Words[i].Length;

            float health = totalLetters * tuning.healthFromWordLength * profile.healthMultiplier;
            return Mathf.Max(1f, Mathf.Round(health));
        }
    }
}
