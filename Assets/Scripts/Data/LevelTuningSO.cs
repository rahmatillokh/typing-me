using UnityEngine;

namespace TypingMe.Data
{
    /// <summary>
    /// Every balance constant in one asset, so playtesting never requires a recompile.
    /// The shipped values are starting points, not final balance.
    /// </summary>
    /// <remarks>
    /// Speed and spawn cadence ramp over the 20 levels *within* a season and restart each season,
    /// mirroring the D→A→S rank arc — but each season multiplies from a higher floor, so the
    /// campaign still gets harder level by level end to end. Word difficulty keys off the global
    /// level number instead, so vocabulary only ever climbs.
    /// </remarks>
    [CreateAssetMenu(menuName = "Typing Me/Level Tuning", fileName = "LevelTuning")]
    public sealed class LevelTuningSO : ScriptableObject
    {
        [Header("Level shape (§3)")]
        [Tooltip("Nominal word count a level is balanced around; boss health is derived from it.")]
        public int wordsPerLevel = 20;

        [Tooltip("Missed words allowed before the run ends.")]
        public int maxMisses = 3;

        [Tooltip("Spare words queued beyond the nominal count, since the boss — not a word " +
                 "counter — ends the level and a sloppy run needs more of them.")]
        public int spareWords = 16;

        [Header("Fall speed — reference px/s")]
        [Tooltip("Scaled ×0.788 when the boss took the top band and the fall shortened from 680 to " +
                 "536 px, so fall duration — what this curve was actually tuned against — held steady.")]
        public float baseSpeed = 59f;

        [Tooltip("Added per level within a season. Softened on request so each rank band lands a " +
                 "little easier than its first tuning.")]
        public float speedStep = 2.8f;

        public float maxSpeed = 190f;

        [Header("Spawn interval — seconds")]
        public float baseInterval = 3.0f;

        [Tooltip("Softened alongside speedStep.")]
        public float intervalStep = 0.062f;

        public float minInterval = 0.85f;

        [Header("Season scaling")]
        [Tooltip("Fall speed multiplier per season (Spring, Summer, Autumn, Winter).")]
        public float[] seasonSpeedScale = { 1.00f, 1.15f, 1.30f, 1.45f };

        [Tooltip("Spawn interval multiplier per season — lower is faster.")]
        public float[] seasonIntervalScale = { 1.00f, 0.93f, 0.87f, 0.82f };

        [Header("Season boss (level 20)")]
        public float bossSpeedSpike = 1.12f;
        public float bossIntervalSpike = 0.88f;

        [Header("Tier weights (§5) — driven by the global level number")]
        [Tooltip("easy = max(easyStart - level*easyStep, easyMin)")]
        public float easyStart = 70f;
        public float easyStep = 3f;
        public float easyMin = 10f;

        [Tooltip("hard = min(level*hardStep, hardMax); medium takes the remainder.")]
        public float hardStep = 2f;
        public float hardMax = 60f;

        [Header("Motion")]
        [Tooltip("Speed multiplier over the word's fall progress (0 = spawn, 1 = bottom line). " +
                 "Averages ~1 so the tuning above stays meaningful; gives the non-linear drift called for in §8.")]
        public AnimationCurve fallEasing = new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(1f, 1.15f));

        /// <summary>Total words queued for a level.</summary>
        public int QueueSizeFor() => Mathf.Max(1, wordsPerLevel) + Mathf.Max(0, maxMisses) + Mathf.Max(0, spareWords);

        public float FallSpeedFor(int levelNumber)
        {
            int inSeason = SeasonCatalog.LevelInSeason(levelNumber);
            float speed = baseSpeed + inSeason * speedStep;

            speed *= Scale(seasonSpeedScale, SeasonCatalog.SeasonOf(levelNumber));
            if (SeasonCatalog.IsSeasonBoss(levelNumber)) speed *= bossSpeedSpike;

            return Mathf.Min(speed, maxSpeed);
        }

        public float SpawnIntervalFor(int levelNumber)
        {
            int inSeason = SeasonCatalog.LevelInSeason(levelNumber);
            float interval = baseInterval - inSeason * intervalStep;

            interval *= Scale(seasonIntervalScale, SeasonCatalog.SeasonOf(levelNumber));
            if (SeasonCatalog.IsSeasonBoss(levelNumber)) interval *= bossIntervalSpike;

            return Mathf.Max(minInterval, interval);
        }

        private static float Scale(float[] table, Season season)
        {
            int index = (int)season;
            return table != null && index >= 0 && index < table.Length ? table[index] : 1f;
        }

        /// <summary>Tier weights for a level, normalised so they always sum to 1.</summary>
        public void WeightsFor(int levelNumber, out float easy, out float medium, out float hard)
        {
            easy = Mathf.Max(easyStart - levelNumber * easyStep, easyMin);
            hard = Mathf.Min(levelNumber * hardStep, hardMax);
            medium = Mathf.Max(0f, 100f - easy - hard);

            float total = easy + medium + hard;
            if (total <= 0f)
            {
                easy = 1f;
                medium = hard = 0f;
                return;
            }

            easy /= total;
            medium /= total;
            hard /= total;
        }
    }
}
