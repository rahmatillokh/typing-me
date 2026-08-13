using UnityEngine;

namespace TypingMe.Data
{
    /// <summary>The four seasons the campaign is divided into. Play starts in Spring.</summary>
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    /// <summary>
    /// Boss difficulty rank. D is the mildest, A the hardest of the ordinary levels;
    /// S is reserved for the season boss on level 20.
    /// </summary>
    public enum BossRank
    {
        D = 0,
        C = 1,
        B = 2,
        A = 3,
        S = 4
    }

    /// <summary>
    /// Maps a global level number onto the campaign structure: 4 seasons of 20 levels
    /// (19 ordinary + a season boss), 80 levels in total.
    /// </summary>
    /// <remarks>
    /// A global level number stays the single source of truth everywhere else — save data,
    /// level generation and the boss generator all key off it — so nothing needs to store
    /// a season/level pair.
    /// </remarks>
    public static class SeasonCatalog
    {
        public const int LevelsPerSeason = 20;
        public const int SeasonCount = 4;
        public const int TotalLevels = LevelsPerSeason * SeasonCount;

        /// <summary>The level within its season that is the season boss.</summary>
        public const int SeasonBossLevel = LevelsPerSeason;

        public static int Clamp(int levelNumber) => Mathf.Clamp(levelNumber, 1, TotalLevels);

        public static Season SeasonOf(int levelNumber) =>
            (Season)Mathf.Clamp((Clamp(levelNumber) - 1) / LevelsPerSeason, 0, SeasonCount - 1);

        /// <summary>1-based position inside the season (1..20).</summary>
        public static int LevelInSeason(int levelNumber) => (Clamp(levelNumber) - 1) % LevelsPerSeason + 1;

        public static bool IsSeasonBoss(int levelNumber) => LevelInSeason(levelNumber) == SeasonBossLevel;

        public static bool IsFinalLevel(int levelNumber) => Clamp(levelNumber) == TotalLevels;

        /// <summary>Global level number for the first level of a season.</summary>
        public static int FirstLevelOf(Season season) => (int)season * LevelsPerSeason + 1;

        public static int GlobalLevel(Season season, int levelInSeason) =>
            (int)season * LevelsPerSeason + Mathf.Clamp(levelInSeason, 1, LevelsPerSeason);

        /// <summary>
        /// Rank for a level. Ranks restart at D every season and climb to A over the 19 ordinary
        /// levels, so each season replays the full difficulty arc; level 20 alone is S.
        /// </summary>
        public static BossRank RankFor(int levelNumber)
        {
            int inSeason = LevelInSeason(levelNumber);
            if (inSeason == SeasonBossLevel) return BossRank.S;

            // 1-5 D, 6-10 C, 11-15 B, 16-19 A.
            return (BossRank)Mathf.Clamp((inSeason - 1) / 5, 0, (int)BossRank.A);
        }

        /// <summary>The season that follows, clamped at Winter — there is nothing after it.</summary>
        public static Season NextSeason(Season season) =>
            (Season)Mathf.Min((int)season + 1, SeasonCount - 1);

        public static string DisplayName(Season season) => season switch
        {
            Season.Spring => "SPRING",
            Season.Summer => "SUMMER",
            Season.Autumn => "AUTUMN",
            Season.Winter => "WINTER",
            _ => "SPRING"
        };
    }
}
