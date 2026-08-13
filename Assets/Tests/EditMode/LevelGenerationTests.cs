using System.Collections.Generic;
using NUnit.Framework;
using TypingMe.Data;
using TypingMe.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TypingMe.Tests
{
    /// <summary>
    /// Exercises the generated assets rather than hand-built fakes, so these also catch a broken
    /// word list or a mis-wired WordBank asset.
    /// </summary>
    public sealed class LevelGenerationTests
    {
        private WordBankSO _bank;
        private LevelTuningSO _tuning;

        [SetUp]
        public void SetUp()
        {
            _bank = AssetDatabase.LoadAssetAtPath<WordBankSO>("Assets/Data/WordBank.asset");
            _tuning = AssetDatabase.LoadAssetAtPath<LevelTuningSO>("Assets/Data/LevelTuning.asset");

            Assert.That(_bank, Is.Not.Null, "WordBank.asset missing — run Typing Me/Rebuild Project Assets.");
            Assert.That(_tuning, Is.Not.Null, "LevelTuning.asset missing — run Typing Me/Rebuild Project Assets.");

            _bank.Reload();
        }

        [Test]
        public void EveryTierHasWords()
        {
            foreach (WordTier tier in System.Enum.GetValues(typeof(WordTier)))
                Assert.That(_bank.CountFor(tier), Is.GreaterThan(0), $"Tier {tier} is empty.");
        }

        [Test]
        public void TierPoolsRespectTheirLengthBounds()
        {
            AssertLengths(WordTier.Easy, 3, 5);
            AssertLengths(WordTier.Medium, 5, 7);
            AssertLengths(WordTier.Hard, 7, 24);
        }

        private void AssertLengths(WordTier tier, int min, int max)
        {
            IReadOnlyList<string> pool = _bank.GetPool(tier);

            for (int i = 0; i < pool.Count; i++)
            {
                Assert.That(pool[i].Length, Is.InRange(min, max),
                    $"'{pool[i]}' in tier {tier} is outside {min}-{max} letters.");
            }
        }

        [Test]
        public void WordsAreLowercaseLettersOnly()
        {
            IReadOnlyList<string> pool = _bank.GetPool(WordTier.Medium);

            for (int i = 0; i < pool.Count; i++)
            {
                foreach (char c in pool[i])
                {
                    // Explicit comparison rather than Is.InRange: NUnit can't order char bounds.
                    Assert.That(c >= 'a' && c <= 'z', Is.True,
                        $"'{pool[i]}' contains the non a-z character '{c}'.");
                }
            }
        }

        [Test]
        public void GenerationIsDeterministicForALevelNumber()
        {
            LevelData first = LevelGenerator.Generate(7, _bank, _tuning);
            LevelData second = LevelGenerator.Generate(7, _bank, _tuning);

            CollectionAssert.AreEqual(first.Words, second.Words);
            Assert.That(first.FallSpeed, Is.EqualTo(second.FallSpeed));
            Assert.That(first.SpawnInterval, Is.EqualTo(second.SpawnInterval));
        }

        [Test]
        public void DifferentLevelsProduceDifferentWordSets()
        {
            LevelData first = LevelGenerator.Generate(3, _bank, _tuning);
            LevelData second = LevelGenerator.Generate(4, _bank, _tuning);

            CollectionAssert.AreNotEqual(first.Words, second.Words);
        }

        [Test]
        public void QueueIsLongerThanTheClearTargetSoAMissIsSurvivable()
        {
            LevelData level = LevelGenerator.Generate(1, _bank, _tuning);

            Assert.That(level.TargetClears, Is.EqualTo(_tuning.wordsPerLevel));
            Assert.That(level.Words.Count, Is.GreaterThanOrEqualTo(level.TargetClears + _tuning.maxMisses));
        }

        [Test]
        public void SpeedRisesAndIntervalFallsWithLevelNumber()
        {
            LevelData early = LevelGenerator.Generate(1, _bank, _tuning);
            LevelData late = LevelGenerator.Generate(15, _bank, _tuning);

            Assert.That(late.FallSpeed, Is.GreaterThan(early.FallSpeed));
            Assert.That(late.SpawnInterval, Is.LessThan(early.SpawnInterval));
        }

        [Test]
        public void SpeedAndIntervalStayWithinTheirCaps()
        {
            for (int level = 1; level <= SeasonCatalog.TotalLevels; level++)
            {
                LevelData data = LevelGenerator.Generate(level, _bank, _tuning);

                Assert.That(data.FallSpeed, Is.LessThanOrEqualTo(_tuning.maxSpeed), $"level {level}");
                Assert.That(data.SpawnInterval, Is.GreaterThanOrEqualTo(_tuning.minInterval), $"level {level}");
                Assert.That(data.Words.Count, Is.EqualTo(_tuning.QueueSizeFor()), $"level {level}");
            }
        }

        [Test]
        public void LevelsBeyondTheCampaignClampToTheFinalLevel()
        {
            LevelData last = LevelGenerator.Generate(SeasonCatalog.TotalLevels, _bank, _tuning);
            LevelData beyond = LevelGenerator.Generate(SeasonCatalog.TotalLevels + 40, _bank, _tuning);

            CollectionAssert.AreEqual(last.Words, beyond.Words);
        }

        [Test]
        public void LevelZeroAndNegativesClampToLevelOne()
        {
            LevelData one = LevelGenerator.Generate(1, _bank, _tuning);

            CollectionAssert.AreEqual(one.Words, LevelGenerator.Generate(0, _bank, _tuning).Words);
            CollectionAssert.AreEqual(one.Words, LevelGenerator.Generate(-5, _bank, _tuning).Words);
        }

        [Test]
        public void TierWeightsAlwaysNormaliseToOne()
        {
            for (int level = 1; level <= 200; level++)
            {
                _tuning.WeightsFor(level, out float easy, out float medium, out float hard);

                Assert.That(easy + medium + hard, Is.EqualTo(1f).Within(0.0001f), $"level {level}");
                Assert.That(easy, Is.GreaterThanOrEqualTo(0f));
                Assert.That(medium, Is.GreaterThanOrEqualTo(0f));
                Assert.That(hard, Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void DifficultyShiftsFromEasyTowardsHard()
        {
            _tuning.WeightsFor(1, out float earlyEasy, out _, out float earlyHard);
            _tuning.WeightsFor(30, out float lateEasy, out _, out float lateHard);

            Assert.That(lateEasy, Is.LessThan(earlyEasy));
            Assert.That(lateHard, Is.GreaterThan(earlyHard));
        }

        [Test]
        public void GeneratedWordsAreNeverEmpty()
        {
            LevelData level = LevelGenerator.Generate(12, _bank, _tuning);

            foreach (string word in level.Words)
                Assert.That(string.IsNullOrWhiteSpace(word), Is.False);
        }
    }
}
