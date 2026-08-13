using System;
using System.Linq;
using TypingMe.Data;
using TypingMe.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TypingMe.EditorTools
{
    /// <summary>
    /// Prints a generated level's spawn queue. Because generation is seeded from the level number
    /// (§5), this is exactly what the player will see — useful for balance review, for reproducing a
    /// bug report from a level number alone, and for scripted playthroughs.
    /// </summary>
    public static class LevelInspector
    {
        [MenuItem("Typing Me/Print Level 1 Queue", false, 40)]
        public static void PrintLevelOne() => Print(1);

        /// <summary>Headless: <c>-executeMethod TypingMe.EditorTools.LevelInspector.PrintFromArgs -level 3</c></summary>
        public static void PrintFromArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            int level = 1;

            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-level" && int.TryParse(args[i + 1], out int parsed)) level = parsed;

            Print(level);
        }

        private static void Print(int levelNumber)
        {
            var bank = AssetDatabase.LoadAssetAtPath<WordBankSO>("Assets/Data/WordBank.asset");
            var tuning = AssetDatabase.LoadAssetAtPath<LevelTuningSO>("Assets/Data/LevelTuning.asset");

            if (bank == null || tuning == null)
            {
                Debug.LogError("[Typing Me] WordBank or LevelTuning asset missing.");
                return;
            }

            LevelData level = LevelGenerator.Generate(levelNumber, bank, tuning);

            Debug.Log($"LEVELQUEUE {levelNumber} " +
                      $"speed={level.FallSpeed:F2} interval={level.SpawnInterval:F3} " +
                      $"target={level.TargetClears} " +
                      $"words={string.Join(",", level.Words.ToArray())}");
        }
    }
}
