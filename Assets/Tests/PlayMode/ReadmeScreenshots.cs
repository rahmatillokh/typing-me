using System.Collections;
using System.IO;
using NUnit.Framework;
using TypingMe.Core;
using TypingMe.Data;
using TypingMe.Gameplay;
using TypingMe.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TypingMe.Tests
{
    /// <summary>
    /// Not a test — a capture tool. Renders the real scenes into an off-screen 1920×1080 target and
    /// writes the README's four screenshots to docs/screenshots/. Marked <c>[Explicit]</c> so normal
    /// test runs skip it; run it on demand with:
    /// <c>-runTests -testPlatform PlayMode -testFilter "TypingMe.Tests.ReadmeScreenshots.CaptureAll"</c>
    /// </summary>
    public sealed class ReadmeScreenshots
    {
        private const int Width = 1920;
        private const int Height = 1080;
        private const string OutDir = "docs/screenshots";

        [UnityTest]
        [Explicit("README capture tool, not a correctness test")]
        [Timeout(180000)]
        public IEnumerator CaptureAll()
        {
            SaveData save = SaveSystem.Data;
            int unlockedBefore = save.unlockedLevel;
            int lastPlayedBefore = save.lastPlayedLevel;
            int bestBefore = save.bestScore;

            try
            {
                Directory.CreateDirectory(OutDir);

                // A mid-campaign save makes the shots honest: Spring palette, several rank rows
                // unlocked, a real best score on the menu.
                save.unlockedLevel = Mathf.Max(save.unlockedLevel, 17);
                save.lastPlayedLevel = 17;
                if (save.bestScore <= 0) save.bestScore = 7930;

                // ---- 1. Menu ----------------------------------------------------------------
                SceneManager.LoadScene("Menu");
                yield return null;
                yield return null;

                if (ThemeManager.Instance != null) ThemeManager.Instance.ApplyProgressSeason();
                yield return Wait(0.8f);
                yield return Capture("menu.png");

                // ---- 2. Gameplay — Spring 17, the Golem -------------------------------------
                SceneManager.LoadScene("Game");
                yield return null;
                yield return null;

                var runner = Object.FindFirstObjectByType<LevelRunner>();
                var spawner = Object.FindFirstObjectByType<WordSpawner>();
                var router = Object.FindFirstObjectByType<InputRouter>();
                Assert.That(runner, Is.Not.Null);

                runner.StartLevel(17);
                yield return Wait(6.5f);

                TypeForTheCamera(spawner, router);
                yield return Wait(0.35f);
                yield return Capture("gameplay.png");

                // ---- 3. Rank-up — D cleared, the Thorn teased --------------------------------
                runner.StartLevel(5);
                yield return Wait(1.5f);
                KillBoss(runner);
                yield return Wait(1.7f);
                yield return Capture("rankup.png");

                // ---- 4. Finale — the tribute mid-type ---------------------------------------
                runner.StartLevel(SeasonCatalog.TotalLevels);

                // Type a few real words first so FINAL RUN shows an honest score, not 0.
                yield return Wait(2.6f);
                for (int i = 0; i < 3; i++)
                {
                    WordController word = LowestAliveWord(spawner, null);
                    if (word == null) break;

                    foreach (char c in word.Word) router.SubmitCharacter(c);
                }

                KillBoss(runner);
                yield return Wait(7.5f);
                yield return Capture("finale.png");
            }
            finally
            {
                save.unlockedLevel = unlockedBefore;
                save.lastPlayedLevel = lastPlayedBefore;
                save.bestScore = bestBefore;
                SaveSystem.Save();
            }
        }

        /// <summary>Clears the lowest word and half-types the next, so the shot shows live typing.</summary>
        private static void TypeForTheCamera(WordSpawner spawner, InputRouter router)
        {
            if (spawner == null || router == null) return;

            WordController first = LowestAliveWord(spawner, null);
            if (first != null)
            {
                string word = first.Word;
                foreach (char c in word) router.SubmitCharacter(c);
            }

            WordController second = LowestAliveWord(spawner, first);
            if (second == null || second.Word.Length < 3) return;

            router.SubmitCharacter(second.Word[0]);
            router.SubmitCharacter(second.Word[1]);
        }

        private static WordController LowestAliveWord(WordSpawner spawner, WordController exclude)
        {
            WordController best = null;
            float lowestY = float.MaxValue;

            foreach (WordController word in spawner.Active)
            {
                if (word == null || !word.IsAlive || word == exclude) continue;
                if (word.CurrentY >= lowestY) continue;

                lowestY = word.CurrentY;
                best = word;
            }

            return best;
        }

        private static void KillBoss(LevelRunner runner)
        {
            BossController boss = runner.Boss;
            for (int i = 0; i < 500 && !boss.IsDefeated; i++)
                boss.TakeDamage("aaaaa", out _);

            Assert.That(boss.IsDefeated, Is.True, "the boss should fall to raw damage");
        }

        /// <summary>
        /// Renders the current camera into an off-screen 1920×1080 target and writes the PNG. The
        /// Screen Space – Camera canvas follows the camera's viewport, so the UI lays itself out at
        /// exactly the reference resolution regardless of the headless window size.
        /// </summary>
        private static IEnumerator Capture(string fileName)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "no main camera in the loaded scene");

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;

            // Two player-loop frames: one for the canvas to adopt the new viewport, one rendered
            // into the target with that layout.
            yield return null;
            yield return null;

            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();

            camera.targetTexture = null;
            RenderTexture.active = null;

            // A silently black frame would ship a black README — fail loudly instead.
            Color32[] pixels = texture.GetPixels32();
            long total = 0;
            for (int i = 0; i < pixels.Length; i += 997)
                total += pixels[i].r + pixels[i].g + pixels[i].b;

            float brightness = total / (pixels.Length / 997f * 3f);
            Assert.That(brightness, Is.GreaterThan(1.5f),
                $"{fileName} rendered near-black ({brightness:F2}) — headless rendering failed");

            File.WriteAllBytes(Path.Combine(OutDir, fileName), texture.EncodeToPNG());
            Debug.Log($"[ReadmeScreenshots] wrote {OutDir}/{fileName} (brightness {brightness:F1})");

            Object.Destroy(texture);
            target.Release();
            Object.Destroy(target);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
