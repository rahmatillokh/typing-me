using System;
using UnityEngine;

namespace TypingMe.Data
{
    /// <summary>How aggressive a boss of a given rank is. One entry per <see cref="BossRank"/>.</summary>
    [Serializable]
    public sealed class BossRankProfile
    {
        public BossRank rank;

        [Header("Cadence")]
        [Tooltip("Seconds between attacks.")]
        public float attackInterval = 20f;

        [Tooltip("Grace period before the first attack, so a level never opens with one.")]
        public float firstAttackDelay = 11f;

        [Tooltip("Seconds the incoming attack is telegraphed before it lands.")]
        public float telegraph = 1.4f;

        [Tooltip("Fire two different attacks at once.")]
        public bool doubleAttacks;

        [Header("Word burst")]
        public int burstWords = 2;

        [Header("Speed surge")]
        public float surgeMultiplier = 1.3f;
        public float surgeDuration = 3.5f;

        [Header("Word veil")]
        public int veilWords = 2;
        public float veilDuration = 2.5f;

        [Header("Health")]
        [Tooltip("Scales the HP derived from the level's word queue.")]
        public float healthMultiplier = 1f;

        [Tooltip("Attacks this rank is allowed to use.")]
        public BossAttack[] attacks = { BossAttack.WordBurst };
    }

    /// <summary>
    /// Per-rank boss balance, kept in an asset so it can be retuned without a recompile.
    /// The shipped values are a starting point.
    /// </summary>
    [CreateAssetMenu(menuName = "Typing Me/Boss Tuning", fileName = "BossTuning")]
    public sealed class BossTuningSO : ScriptableObject
    {
        [Tooltip("HP is the level's total word length times this, before the rank multiplier. " +
                 "Above 1 so a level still runs about 20 words when the sigil bonus lands normally.")]
        public float healthFromWordLength = 1.2f;

        [Header("Sigil")]
        [Tooltip("Words containing the boss's sigil letter deal this much extra damage.")]
        public float sigilDamageMultiplier = 1.5f;

        [Tooltip("The sigil is chosen so roughly this share of the level's words contain it — " +
                 "common enough to chase, rare enough to matter.")]
        [Range(0.15f, 0.8f)] public float sigilTargetCoverage = 0.45f;

        [Header("Season boss")]
        [Tooltip("Below this share of HP the season boss enrages.")]
        [Range(0.05f, 0.6f)] public float enrageThreshold = 0.3f;

        [Tooltip("Attack interval is scaled by this while enraged.")]
        public float enrageIntervalScale = 0.6f;

        // One new tool per rank: D opens the season with bursts alone, C adds Speed Surge, B adds
        // Word Veil, A starts pairing attacks, and S is the season boss with everything plus enrage.
        // The whole ladder was softened ~15% on request — longer cadences, smaller bursts, shorter
        // veils, lighter health — while keeping every rank strictly meaner than the one before it.
        [SerializeField]
        private BossRankProfile[] profiles =
        {
            new BossRankProfile
            {
                rank = BossRank.D, attackInterval = 28f, firstAttackDelay = 15f, burstWords = 2,
                surgeMultiplier = 1.20f, surgeDuration = 3f, healthMultiplier = 0.95f,
                attacks = new[] { BossAttack.WordBurst }
            },
            new BossRankProfile
            {
                rank = BossRank.C, attackInterval = 22f, firstAttackDelay = 13f, burstWords = 2,
                surgeMultiplier = 1.28f, surgeDuration = 3.5f, healthMultiplier = 1.02f,
                attacks = new[] { BossAttack.WordBurst, BossAttack.SpeedSurge }
            },
            new BossRankProfile
            {
                rank = BossRank.B, attackInterval = 17f, firstAttackDelay = 12f, burstWords = 3,
                surgeMultiplier = 1.34f, surgeDuration = 4f,
                veilWords = 2, veilDuration = 2.2f, healthMultiplier = 1.08f,
                attacks = new[] { BossAttack.WordBurst, BossAttack.SpeedSurge, BossAttack.WordVeil }
            },
            new BossRankProfile
            {
                rank = BossRank.A, attackInterval = 13.5f, firstAttackDelay = 11f, burstWords = 3,
                surgeMultiplier = 1.40f, surgeDuration = 4.5f,
                veilWords = 3, veilDuration = 2.6f, healthMultiplier = 1.15f, doubleAttacks = true,
                attacks = new[] { BossAttack.WordBurst, BossAttack.SpeedSurge, BossAttack.WordVeil }
            },
            new BossRankProfile
            {
                rank = BossRank.S, attackInterval = 10f, firstAttackDelay = 8f, telegraph = 1.25f,
                burstWords = 4, surgeMultiplier = 1.50f, surgeDuration = 5f,
                veilWords = 3, veilDuration = 2.8f, healthMultiplier = 1.45f,
                doubleAttacks = true,
                attacks = new[] { BossAttack.WordBurst, BossAttack.SpeedSurge, BossAttack.WordVeil }
            }
        };

        public BossRankProfile ProfileFor(BossRank rank)
        {
            for (int i = 0; i < profiles.Length; i++)
                if (profiles[i] != null && profiles[i].rank == rank) return profiles[i];

            Debug.LogWarning($"[BossTuning] No profile for rank {rank}; falling back to the first entry.");
            return profiles.Length > 0 ? profiles[0] : new BossRankProfile();
        }
    }
}
