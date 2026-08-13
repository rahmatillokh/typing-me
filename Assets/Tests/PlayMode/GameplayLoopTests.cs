using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TypingMe.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TypingMe.Tests
{
    /// <summary>
    /// Drives the real Game scene end to end. Characters are fed through
    /// <see cref="InputRouter.SubmitCharacter"/>, which is the same entry point the physical
    /// keyboard uses, so these cover the actual wiring rather than a stand-in.
    /// </summary>
    public sealed class GameplayLoopTests
    {
        private WordSpawner _spawner;
        private InputRouter _router;
        private MistakeTracker _mistakes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game");

            // One frame to load, one for Awake/Start across the scene.
            yield return null;
            yield return null;

            _spawner = Object.FindFirstObjectByType<WordSpawner>();
            _router = Object.FindFirstObjectByType<InputRouter>();
            _mistakes = Object.FindFirstObjectByType<MistakeTracker>();

            var runner = Object.FindFirstObjectByType<LevelRunner>();

            Assert.That(_spawner, Is.Not.Null, "Game scene has no WordSpawner.");
            Assert.That(_router, Is.Not.Null, "Game scene has no InputRouter.");
            Assert.That(_mistakes, Is.Not.Null, "Game scene has no MistakeTracker.");
            Assert.That(runner, Is.Not.Null, "Game scene has no LevelRunner.");

            // Pin the level instead of inheriting it from the save file. The tests share
            // persistentDataPath with the real game, so without this the words — and therefore
            // these assertions — change as soon as anyone plays far enough to unlock a new level.
            runner.StartLevel(1);

            yield return WaitForWordCount(1, 8f);
        }

        private IEnumerator WaitForWordCount(int count, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (_spawner.Active.Count < count && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(_spawner.Active.Count, Is.GreaterThanOrEqualTo(count),
                $"Only {_spawner.Active.Count} word(s) spawned within {timeoutSeconds}s.");
        }

        private static void PlaceAt(WordController word, float y)
        {
            var rect = (RectTransform)word.transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        [UnityTest]
        public IEnumerator TypingAWordClearsItAndAdvancesProgress()
        {
            WordController word = _spawner.Active[0];
            string text = word.Word;
            int clearedBefore = _spawner.ClearedCount;

            foreach (char c in text)
                _router.SubmitCharacter(c);

            Assert.That(word.IsAlive, Is.False, $"'{text}' should be cleared after typing it.");
            Assert.That(_spawner.ClearedCount, Is.EqualTo(clearedBefore + 1));
            Assert.That(_mistakes.Misses, Is.Zero);

            yield return null;
        }

        [UnityTest]
        public IEnumerator PartiallyTypedWordTracksMatchedLength()
        {
            WordController word = _spawner.Active[0];
            Assume.That(word.Word.Length, Is.GreaterThan(2));

            _router.SubmitCharacter(word.Word[0]);
            _router.SubmitCharacter(word.Word[1]);

            Assert.That(word.MatchedLength, Is.EqualTo(2));
            Assert.That(word.IsAlive, Is.True);
            Assert.That(_router.Target, Is.EqualTo(word), "The router should stay locked mid-word.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator WrongKeystrokeIsFeedbackOnlyAndNeverAMiss()
        {
            _spawner.StopSpawning();

            // A letter no live word is waiting for.
            char unused = '\0';
            for (char c = 'a'; c <= 'z'; c++)
            {
                bool matchesSomething = false;
                foreach (WordController active in _spawner.Active)
                    if (active.IsAlive && active.NextChar == c) matchesSomething = true;

                if (matchesSomething) continue;

                unused = c;
                break;
            }

            Assert.That(unused, Is.Not.EqualTo('\0'), "Could not find an unmatched letter to press.");

            int missesBefore = _mistakes.Misses;
            _router.SubmitCharacter(unused);

            // §11: only a word crossing the bottom line is a mistake.
            Assert.That(_mistakes.Misses, Is.EqualTo(missesBefore));
            Assert.That(_spawner.ClearedCount, Is.Zero);

            yield return null;
        }

        [UnityTest]
        public IEnumerator WordCrossingTheBottomLineRegistersAMiss()
        {
            _spawner.StopSpawning();

            WordController word = _spawner.Active[0];
            PlaceAt(word, _spawner.BottomLineY - 1f);

            yield return null;
            yield return null;

            Assert.That(word.IsAlive, Is.False);
            Assert.That(_mistakes.Misses, Is.EqualTo(1));
            Assert.That(_spawner.MissedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LockPrefersTheLowestWordWhenSeveralMatch()
        {
            yield return WaitForWordCount(2, 10f);
            _spawner.StopSpawning();

            WordController high = _spawner.Active[0];
            WordController low = _spawner.Active[1];

            char shared = FindSharedLetter(high.Word, low.Word);
            if (shared == '\0')
                Assert.Ignore($"'{high.Word}' and '{low.Word}' share no letter — nothing to disambiguate.");

            AdvanceUntilNextIs(high, shared);
            AdvanceUntilNextIs(low, shared);

            // Push every other word above both candidates so only these two compete.
            foreach (WordController other in _spawner.Active)
                if (other != high && other != low) PlaceAt(other, 400f);

            PlaceAt(high, 300f);
            PlaceAt(low, 0f);

            _router.SubmitCharacter(shared);

            Assert.That(_router.Target, Is.EqualTo(low),
                "The lock should go to the word closest to the bottom line (§9).");
        }

        [UnityTest]
        public IEnumerator LockHoldsUntilTheWordIsCleared()
        {
            _spawner.StopSpawning();

            WordController word = _spawner.Active[0];
            Assume.That(word.Word.Length, Is.GreaterThan(1));

            _router.SubmitCharacter(word.Word[0]);
            Assume.That(_router.Target, Is.EqualTo(word),
                "a lower word captured the first letter — nothing to measure");

            // A key that neither advances the lock nor continues its prefix on any other word:
            // with re-routing in play, only such a key is a true mismatch.
            char wrong = FindDeadEndLetter(word);
            Assume.That(wrong, Is.Not.EqualTo('\0'), "no dead-end letter available to press");

            _router.SubmitCharacter(wrong);

            Assert.That(_router.Target, Is.EqualTo(word), "A wrong key should not release the lock.");
            Assert.That(word.MatchedLength, Is.EqualTo(1), "A wrong key should not advance the word.");

            yield return null;
        }

        /// <summary>
        /// A letter that is not the locked word's next letter, and does not continue its typed
        /// prefix on any other live word — so the router has nowhere to re-route to.
        /// </summary>
        private char FindDeadEndLetter(WordController locked)
        {
            for (char c = 'a'; c <= 'z'; c++)
            {
                if (locked.NextChar == c) continue;

                bool continuesElsewhere = false;

                foreach (WordController other in _spawner.Active)
                {
                    if (other == null || !other.IsAlive || other == locked) continue;
                    if (other.Word.Length <= locked.MatchedLength) continue;
                    if (!other.Word.StartsWith(locked.Word.Substring(0, locked.MatchedLength))) continue;
                    if (other.Word[locked.MatchedLength] != c) continue;

                    continuesElsewhere = true;
                    break;
                }

                if (!continuesElsewhere) return c;
            }

            return '\0';
        }

        [UnityTest]
        public IEnumerator MismatchedSecondLetterReroutesTheLockToTheIntendedWord()
        {
            // Generation is deterministic, so pick a level whose opening words are guaranteed to
            // contain the scenario — two words sharing a first letter but not a second — instead
            // of hoping level 1 happens to provide one.
            var runner = Object.FindFirstObjectByType<LevelRunner>();
            int level = FindLevelWithFirstLetterPair();
            Assume.That(level, Is.GreaterThan(0), "no level opens with a first-letter pair");

            runner.StartLevel(level);
            _spawner.StopSpawning();
            _spawner.ClearActive();

            // The queue's first eight words, all on screen at once.
            _spawner.SpawnBurst(8);

            WordController first = null;
            WordController second = null;

            foreach (WordController a in _spawner.Active)
            {
                if (a == null || !a.IsAlive || a.Word.Length < 3) continue;

                foreach (WordController b in _spawner.Active)
                {
                    if (b == null || !b.IsAlive || b == a || b.Word.Length < 3) continue;
                    if (a.Word[0] != b.Word[0] || a.Word[1] == b.Word[1]) continue;

                    first = a;
                    second = b;
                    break;
                }

                if (first != null) break;
            }

            Assert.That(first, Is.Not.Null,
                $"level {level} was chosen for its first-letter pair but none is on screen");

            // Make 'first' win the initial lock, as the wrong guess.
            foreach (WordController other in _spawner.Active)
                if (other != first && other != second) PlaceAt(other, 400f);

            PlaceAt(first, -100f);
            PlaceAt(second, 100f);

            _router.SubmitCharacter(first.Word[0]);
            Assert.That(_router.Target, Is.EqualTo(first), "the lower word should take the first letter");

            // The player was typing 'second' all along; its second letter must steal the lock.
            _router.SubmitCharacter(second.Word[1]);

            Assert.That(_router.Target, Is.EqualTo(second),
                "the lock should re-route to the word that continues what was typed");
            Assert.That(second.MatchedLength, Is.EqualTo(2),
                "the re-routed word should inherit the prefix plus the new letter");
            Assert.That(first.MatchedLength, Is.Zero,
                "the abandoned word should read as untouched again");
            Assert.That(_mistakes.Misses, Is.Zero, "re-routing is typing, not a mistake");

            yield return null;
        }

        /// <summary>
        /// The first level whose opening eight words — the ones a single burst puts on screen —
        /// contain two words sharing a first letter but differing in the second.
        /// </summary>
        private static int FindLevelWithFirstLetterPair()
        {
            TypingMe.Core.GameManager game = TypingMe.Core.GameManager.Instance;
            if (game == null) return 0;

            for (int level = 1; level <= TypingMe.Data.SeasonCatalog.TotalLevels; level++)
            {
                IReadOnlyList<string> words = game.BuildLevel(level).Words;
                int window = Mathf.Min(8, words.Count);

                for (int i = 0; i < window; i++)
                {
                    for (int j = 0; j < window; j++)
                    {
                        if (i == j) continue;
                        if (words[i].Length < 3 || words[j].Length < 3) continue;

                        if (words[i][0] == words[j][0] && words[i][1] != words[j][1]) return level;
                    }
                }
            }

            return 0;
        }



        [UnityTest]
        public IEnumerator SimultaneousBurstsNeverStackWordsOnTopOfEachOther()
        {
            _spawner.StopSpawning();
            _spawner.ClearActive();

            // The worst case a double attack can produce: two bursts planned independently in the
            // same frame, arriving into the same height band.
            _spawner.SpawnBurst(2);
            _spawner.SpawnBurst(2);

            // Let the throws settle; the drift is ~95% spent within 0.7s.
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            var words = new List<WordController>();
            foreach (WordController word in _spawner.Active)
                if (word != null && word.IsAlive) words.Add(word);

            Assert.That(words.Count, Is.GreaterThanOrEqualTo(4), "both bursts should have produced words");

            for (int i = 0; i < words.Count; i++)
            {
                for (int j = i + 1; j < words.Count; j++)
                {
                    float dy = Mathf.Abs(words[i].AnchoredPosition.y - words[j].AnchoredPosition.y);
                    if (dy > 100f) continue;

                    float dx = Mathf.Abs(words[i].AnchoredPosition.x - words[j].AnchoredPosition.x);
                    Assert.That(dx, Is.GreaterThan(150f),
                        $"'{words[i].Word}' and '{words[j].Word}' sit {dy:F0}px apart vertically " +
                        $"but only {dx:F0}px horizontally — stacked and illegible");
                }
            }
        }

        [UnityTest]
        public IEnumerator ABurstSpreadsItsWordsIntoSeparateColumns()
        {
            _spawner.StopSpawning();
            _spawner.ClearActive();

            _spawner.SpawnBurst(4);

            // Let the throw play out; the drift is ~95% spent within 0.7s.
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < deadline) yield return null;

            var xs = new List<float>();
            foreach (WordController word in _spawner.Active)
                if (word != null && word.IsAlive) xs.Add(word.AnchoredPosition.x);

            Assert.That(xs.Count, Is.GreaterThanOrEqualTo(2), "the burst should have produced words");

            xs.Sort();
            for (int i = 1; i < xs.Count; i++)
            {
                Assert.That(xs[i] - xs[i - 1], Is.GreaterThan(120f),
                    $"burst words landed {xs[i] - xs[i - 1]:F0}px apart and would overlap illegibly");
            }
        }

        [UnityTest]
        public IEnumerator BeatingTheFinalLevelShowsTheFinaleInsteadOfThePanel()
        {
            var runner = Object.FindFirstObjectByType<LevelRunner>();
            var finale = Object.FindFirstObjectByType<TypingMe.UI.FinaleUI>(FindObjectsInactive.Include);
            Assert.That(finale, Is.Not.Null, "Game scene has no FinaleUI.");

            // Completing the last level writes real progress; the tests share persistentDataPath
            // with the actual game, so put the save back exactly as it was.
            TypingMe.Core.SaveData save = TypingMe.Core.SaveSystem.Data;
            int unlockedBefore = save.unlockedLevel;
            int lastPlayedBefore = save.lastPlayedLevel;
            int bestBefore = save.bestScore;

            try
            {
                runner.StartLevel(TypingMe.Data.SeasonCatalog.TotalLevels);
                _spawner.StopSpawning();

                BossController boss = runner.Boss;
                for (int i = 0; i < 500 && !boss.IsDefeated; i++)
                    boss.TakeDamage("aaaaa", out _);

                Assert.That(boss.IsDefeated, Is.True, "the final boss should fall to raw damage");
                Assert.That(finale.gameObject.activeSelf, Is.True,
                    "beating the final level should open the campaign finale");

                yield return null;
            }
            finally
            {
                save.unlockedLevel = unlockedBefore;
                save.lastPlayedLevel = lastPlayedBefore;
                save.bestScore = bestBefore;
                TypingMe.Core.SaveSystem.Save();
            }
        }

        /// <summary>
        /// A letter both words are waiting for, excluding one that would *finish* <paramref name="b"/> —
        /// completing a word clears it and correctly releases the lock, which is not what this test
        /// is measuring.
        /// </summary>
        private static char FindSharedLetter(string a, string b)
        {
            foreach (char c in a)
            {
                int index = b.IndexOf(c);
                if (index >= 0 && index < b.Length - 1) return c;
            }

            return '\0';
        }

        /// <summary>Types a word forward until its next expected letter is <paramref name="target"/>.</summary>
        private static void AdvanceUntilNextIs(WordController word, char target)
        {
            while (word.IsAlive && word.NextChar != target && word.NextChar != '\0')
                word.TryConsume(word.NextChar);
        }
    }
}
