using NUnit.Framework;
using TypingMe.Gameplay;
using UnityEngine;

namespace TypingMe.Tests
{
    /// <summary>The 3-miss rule (§3, §11).</summary>
    public sealed class MistakeTrackerTests
    {
        private GameObject _host;
        private MistakeTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("MistakeTrackerHost");
            _tracker = _host.AddComponent<MistakeTracker>();
            _tracker.ResetFor(3);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [Test]
        public void StartsWithAFullAllowance()
        {
            Assert.That(_tracker.Misses, Is.Zero);
            Assert.That(_tracker.Remaining, Is.EqualTo(3));
            Assert.That(_tracker.IsExhausted, Is.False);
        }

        [Test]
        public void ExhaustsOnTheThirdMiss()
        {
            int exhaustedCount = 0;
            _tracker.Exhausted += () => exhaustedCount++;

            _tracker.RegisterMiss();
            _tracker.RegisterMiss();
            Assert.That(_tracker.IsExhausted, Is.False, "Two misses should not end the run.");
            Assert.That(exhaustedCount, Is.Zero);

            _tracker.RegisterMiss();
            Assert.That(_tracker.IsExhausted, Is.True);
            Assert.That(exhaustedCount, Is.EqualTo(1));
        }

        [Test]
        public void ExtraMissesAfterExhaustionAreIgnored()
        {
            int exhaustedCount = 0;
            _tracker.Exhausted += () => exhaustedCount++;

            for (int i = 0; i < 8; i++) _tracker.RegisterMiss();

            // The run ends on the third miss; late misses from words already in flight must not
            // re-fire the game-over path.
            Assert.That(_tracker.Misses, Is.EqualTo(3));
            Assert.That(exhaustedCount, Is.EqualTo(1));
        }

        [Test]
        public void ResetClearsProgressAndAllowance()
        {
            _tracker.RegisterMiss();
            _tracker.ResetFor(5);

            Assert.That(_tracker.Misses, Is.Zero);
            Assert.That(_tracker.MaxMisses, Is.EqualTo(5));
            Assert.That(_tracker.Remaining, Is.EqualTo(5));
        }

        [Test]
        public void ChangedFiresWithCurrentCounts()
        {
            int lastMisses = -1;
            int lastMax = -1;
            _tracker.Changed += (misses, max) => { lastMisses = misses; lastMax = max; };

            _tracker.RegisterMiss();

            Assert.That(lastMisses, Is.EqualTo(1));
            Assert.That(lastMax, Is.EqualTo(3));
        }
    }
}
