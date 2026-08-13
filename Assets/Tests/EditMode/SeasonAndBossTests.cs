using NUnit.Framework;
using TypingMe.Data;
using TypingMe.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TypingMe.Tests
{
    /// <summary>The campaign structure — 4 seasons of 20 — and the boss derived from each level.</summary>
    public sealed class SeasonAndBossTests
    {
        private WordBankSO _bank;
        private LevelTuningSO _tuning;
        private BossTuningSO _bossTuning;

        [SetUp]
        public void SetUp()
        {
            _bank = AssetDatabase.LoadAssetAtPath<WordBankSO>("Assets/Data/WordBank.asset");
            _tuning = AssetDatabase.LoadAssetAtPath<LevelTuningSO>("Assets/Data/LevelTuning.asset");
            _bossTuning = AssetDatabase.LoadAssetAtPath<BossTuningSO>("Assets/Data/BossTuning.asset");

            Assert.That(_bank, Is.Not.Null, "WordBank.asset missing — run Typing Me/Rebuild Project Assets.");
            Assert.That(_tuning, Is.Not.Null, "LevelTuning.asset missing.");
            Assert.That(_bossTuning, Is.Not.Null, "BossTuning.asset missing.");

            _bank.Reload();
        }

        private BossDefinition BossFor(int level) =>
            BossGenerator.Generate(level, LevelGenerator.Generate(level, _bank, _tuning), _bossTuning);

        #region Campaign structure

        [Test]
        public void CampaignIsFourSeasonsOfTwenty()
        {
            Assert.That(SeasonCatalog.SeasonCount, Is.EqualTo(4));
            Assert.That(SeasonCatalog.LevelsPerSeason, Is.EqualTo(20));
            Assert.That(SeasonCatalog.TotalLevels, Is.EqualTo(80));
        }

        [Test]
        public void PlayStartsInSpringAndEndsInWinter()
        {
            Assert.That(SeasonCatalog.SeasonOf(1), Is.EqualTo(Season.Spring));
            Assert.That(SeasonCatalog.SeasonOf(20), Is.EqualTo(Season.Spring));
            Assert.That(SeasonCatalog.SeasonOf(21), Is.EqualTo(Season.Summer));
            Assert.That(SeasonCatalog.SeasonOf(41), Is.EqualTo(Season.Autumn));
            Assert.That(SeasonCatalog.SeasonOf(61), Is.EqualTo(Season.Winter));
            Assert.That(SeasonCatalog.SeasonOf(80), Is.EqualTo(Season.Winter));
        }

        [Test]
        public void LevelWithinSeasonWrapsEveryTwenty()
        {
            Assert.That(SeasonCatalog.LevelInSeason(1), Is.EqualTo(1));
            Assert.That(SeasonCatalog.LevelInSeason(20), Is.EqualTo(20));
            Assert.That(SeasonCatalog.LevelInSeason(21), Is.EqualTo(1));
            Assert.That(SeasonCatalog.LevelInSeason(80), Is.EqualTo(20));
        }

        [Test]
        public void GlobalLevelRoundTripsThroughSeasonAndPosition()
        {
            for (int level = 1; level <= SeasonCatalog.TotalLevels; level++)
            {
                Season season = SeasonCatalog.SeasonOf(level);
                int inSeason = SeasonCatalog.LevelInSeason(level);

                Assert.That(SeasonCatalog.GlobalLevel(season, inSeason), Is.EqualTo(level));
            }
        }

        #endregion

        #region Ranks

        [Test]
        public void RanksClimbFromDToAAcrossTheNineteenOrdinaryLevels()
        {
            var expected = new[]
            {
                BossRank.D, BossRank.D, BossRank.D, BossRank.D, BossRank.D,
                BossRank.C, BossRank.C, BossRank.C, BossRank.C, BossRank.C,
                BossRank.B, BossRank.B, BossRank.B, BossRank.B, BossRank.B,
                BossRank.A, BossRank.A, BossRank.A, BossRank.A,
                BossRank.S
            };

            for (int i = 0; i < expected.Length; i++)
                Assert.That(SeasonCatalog.RankFor(i + 1), Is.EqualTo(expected[i]), $"level {i + 1}");
        }

        [Test]
        public void EverySeasonRepeatsTheSameRankArc()
        {
            for (int inSeason = 1; inSeason <= SeasonCatalog.LevelsPerSeason; inSeason++)
            {
                BossRank first = SeasonCatalog.RankFor(inSeason);

                for (int season = 1; season < SeasonCatalog.SeasonCount; season++)
                {
                    int level = SeasonCatalog.GlobalLevel((Season)season, inSeason);
                    Assert.That(SeasonCatalog.RankFor(level), Is.EqualTo(first), $"level {level}");
                }
            }
        }

        [Test]
        public void SRankIsReservedForTheSeasonBoss()
        {
            int sRanks = 0;

            for (int level = 1; level <= SeasonCatalog.TotalLevels; level++)
            {
                bool isBoss = SeasonCatalog.IsSeasonBoss(level);
                bool isS = SeasonCatalog.RankFor(level) == BossRank.S;

                Assert.That(isS, Is.EqualTo(isBoss), $"level {level}");
                if (isS) sRanks++;
            }

            Assert.That(sRanks, Is.EqualTo(SeasonCatalog.SeasonCount), "one S rank per season");
        }

        #endregion

        #region Difficulty

        [Test]
        public void DifficultyRampsWithinASeason()
        {
            Assert.That(_tuning.FallSpeedFor(19), Is.GreaterThan(_tuning.FallSpeedFor(2)));
            Assert.That(_tuning.SpawnIntervalFor(19), Is.LessThan(_tuning.SpawnIntervalFor(2)));
        }

        [Test]
        public void LaterSeasonsAreHarderAtTheSamePositionInTheSeason()
        {
            for (int inSeason = 1; inSeason <= 19; inSeason++)
            {
                int spring = SeasonCatalog.GlobalLevel(Season.Spring, inSeason);
                int winter = SeasonCatalog.GlobalLevel(Season.Winter, inSeason);

                Assert.That(_tuning.FallSpeedFor(winter), Is.GreaterThan(_tuning.FallSpeedFor(spring)),
                    $"in-season {inSeason}");
                Assert.That(_tuning.SpawnIntervalFor(winter), Is.LessThan(_tuning.SpawnIntervalFor(spring)),
                    $"in-season {inSeason}");
            }
        }

        [Test]
        public void TheSeasonBossSpikesAboveTheLevelBeforeIt()
        {
            foreach (Season season in System.Enum.GetValues(typeof(Season)))
            {
                int boss = SeasonCatalog.GlobalLevel(season, 20);
                int before = SeasonCatalog.GlobalLevel(season, 19);

                Assert.That(_tuning.FallSpeedFor(boss), Is.GreaterThan(_tuning.FallSpeedFor(before)),
                    $"{season} boss");
            }
        }

        #endregion

        #region Boss

        [Test]
        public void BossGenerationIsDeterministic()
        {
            BossDefinition first = BossFor(37);
            BossDefinition second = BossFor(37);

            Assert.That(second.Sigil, Is.EqualTo(first.Sigil));
            Assert.That(second.MaxHealth, Is.EqualTo(first.MaxHealth));
            Assert.That(second.Rank, Is.EqualTo(first.Rank));
        }

        [Test]
        public void EveryLevelProducesAUsableBoss()
        {
            for (int level = 1; level <= SeasonCatalog.TotalLevels; level++)
            {
                BossDefinition boss = BossFor(level);

                // Explicit comparison: NUnit can't order char bounds.
                Assert.That(boss.Sigil >= 'a' && boss.Sigil <= 'z', Is.True,
                    $"level {level} produced sigil '{boss.Sigil}'");
                Assert.That(boss.MaxHealth, Is.GreaterThan(0f), $"level {level}");
                Assert.That(boss.Profile, Is.Not.Null, $"level {level}");
                Assert.That(boss.Profile.attacks, Is.Not.Empty, $"level {level}");
                Assert.That(boss.Rank, Is.EqualTo(SeasonCatalog.RankFor(level)), $"level {level}");
            }
        }

        [Test]
        public void TheSigilAppearsInSomeButNotAllOfTheLevelsWords()
        {
            for (int level = 1; level <= SeasonCatalog.TotalLevels; level += 7)
            {
                LevelData data = LevelGenerator.Generate(level, _bank, _tuning);
                BossDefinition boss = BossGenerator.Generate(level, data, _bossTuning);

                int considered = Mathf.Min(data.Words.Count, data.TargetClears);
                int containing = 0;

                for (int i = 0; i < considered; i++)
                    if (data.Words[i].IndexOf(boss.Sigil) >= 0) containing++;

                // A sigil in every word makes the bonus free; a sigil in none makes it dead weight.
                Assert.That(containing, Is.GreaterThan(0), $"level {level} sigil '{boss.Sigil}' never appears");
                Assert.That(containing, Is.LessThan(considered), $"level {level} sigil '{boss.Sigil}' is in every word");
            }
        }

        [Test]
        public void SeasonBossesAreToughest()
        {
            foreach (Season season in System.Enum.GetValues(typeof(Season)))
            {
                BossDefinition boss = BossFor(SeasonCatalog.GlobalLevel(season, 20));
                BossDefinition ordinary = BossFor(SeasonCatalog.GlobalLevel(season, 19));

                Assert.That(boss.IsSeasonBoss, Is.True);
                Assert.That(boss.Profile.healthMultiplier, Is.GreaterThan(ordinary.Profile.healthMultiplier));
                Assert.That(boss.Profile.attackInterval, Is.LessThan(ordinary.Profile.attackInterval));
            }
        }

        [Test]
        public void AttackCadenceTightensAsRankClimbs()
        {
            float previous = float.MaxValue;

            foreach (BossRank rank in new[] { BossRank.D, BossRank.C, BossRank.B, BossRank.A, BossRank.S })
            {
                float interval = _bossTuning.ProfileFor(rank).attackInterval;
                Assert.That(interval, Is.LessThan(previous), $"rank {rank}");
                previous = interval;
            }
        }

        #endregion

        #region Damage

        [Test]
        public void SigilWordsHitHarderThanPlainOnes()
        {
            var host = new GameObject("BossHost");

            try
            {
                var controller = host.AddComponent<BossController>();
                BossDefinition definition = BossFor(3);

                controller.Begin(definition, _bossTuning);
                float startHealth = controller.Health;

                // Two words of equal length, one carrying the sigil and one that cannot.
                char other = definition.Sigil == 'q' ? 'x' : 'q';
                string plain = new string(other, 5);
                string sigilWord = new string(definition.Sigil, 5);

                controller.TakeDamage(plain, out bool plainHit);
                float afterPlain = controller.Health;

                controller.TakeDamage(sigilWord, out bool sigilHit);
                float afterSigil = controller.Health;

                Assert.That(plainHit, Is.False);
                Assert.That(sigilHit, Is.True);

                float plainDamage = startHealth - afterPlain;
                float sigilDamage = afterPlain - afterSigil;

                Assert.That(sigilDamage, Is.EqualTo(plainDamage * _bossTuning.sigilDamageMultiplier).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BossDiesWhenHealthReachesZero()
        {
            var host = new GameObject("BossHost");

            try
            {
                var controller = host.AddComponent<BossController>();
                BossDefinition definition = BossFor(2);

                int defeated = 0;
                controller.Defeated += () => defeated++;
                controller.Begin(definition, _bossTuning);

                // Hammer it well past its health pool.
                for (int i = 0; i < 500 && !controller.IsDefeated; i++)
                    controller.TakeDamage("aaaaa", out _);

                Assert.That(controller.IsDefeated, Is.True);
                Assert.That(controller.Health, Is.Zero);
                Assert.That(defeated, Is.EqualTo(1), "Defeated should fire exactly once");

                // Damage after death must not re-fire it.
                controller.TakeDamage("aaaaa", out _);
                Assert.That(defeated, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void HealthIsRoughlyTwentyWordsOfDamage()
        {
            LevelData data = LevelGenerator.Generate(4, _bank, _tuning);
            BossDefinition boss = BossGenerator.Generate(4, data, _bossTuning);

            int letters = 0;
            for (int i = 0; i < data.TargetClears; i++) letters += data.Words[i].Length;

            // With the sigil bonus landing at its designed rate, ~20 words should be enough.
            float expectedDamage = letters * (1f + (_bossTuning.sigilDamageMultiplier - 1f) * _bossTuning.sigilTargetCoverage);

            Assert.That(boss.MaxHealth, Is.LessThanOrEqualTo(expectedDamage * 1.25f));
            Assert.That(boss.MaxHealth, Is.GreaterThan(letters * 0.8f));
        }

        #endregion
    }
}
