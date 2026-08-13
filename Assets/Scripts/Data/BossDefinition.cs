namespace TypingMe.Data
{
    /// <summary>
    /// The boss for one level, derived deterministically from the level number so it is identical
    /// every time that level is played.
    /// </summary>
    public sealed class BossDefinition
    {
        public BossDefinition(int levelNumber, Season season, int levelInSeason, BossRank rank,
            char sigil, float maxHealth, BossRankProfile profile)
        {
            LevelNumber = levelNumber;
            Season = season;
            LevelInSeason = levelInSeason;
            Rank = rank;
            Sigil = sigil;
            MaxHealth = maxHealth;
            Profile = profile;
        }

        public int LevelNumber { get; }
        public Season Season { get; }
        public int LevelInSeason { get; }
        public BossRank Rank { get; }

        /// <summary>The letter shown on the boss. Words containing it deal bonus damage.</summary>
        public char Sigil { get; }

        public float MaxHealth { get; }
        public BossRankProfile Profile { get; }

        public bool IsSeasonBoss => Rank == BossRank.S;

        /// <summary>e.g. "SPRING · 11".</summary>
        public string SeasonLabel => $"{SeasonCatalog.DisplayName(Season)} · {LevelInSeason:00}";
    }
}
