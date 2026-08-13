using System;
using System.IO;
using TypingMe.Data;
using UnityEngine;

namespace TypingMe.Core
{
    /// <summary>Everything persisted between runs (§2, §9).</summary>
    /// <remarks>
    /// The theme is deliberately absent: it is derived from campaign progress — each season carries
    /// its own palette and beating a season is the only way to move to the next one — so persisting
    /// a choice would only let the save disagree with the rule.
    /// </remarks>
    [Serializable]
    public sealed class SaveData
    {
        public int saveVersion = 1;
        public int unlockedLevel = 1;
        public int lastPlayedLevel = 1;
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.85f;
        public int bestScore;
    }

    /// <summary>
    /// JSON save in <see cref="Application.persistentDataPath"/>. Written on level-complete and on
    /// volume change (§9). Writes go through a temp file so a crash mid-write cannot corrupt progress.
    /// </summary>
    public static class SaveSystem
    {
        private const string FileName = "typingme.save.json";

        private static SaveData _cache;

        /// <summary>Raised after any successful save, so UI can refresh without polling.</summary>
        public static event Action<SaveData> Changed;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Data
        {
            get
            {
                if (_cache == null) Load();
                return _cache;
            }
        }

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    _cache = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
                }
                else
                {
                    _cache = new SaveData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to read save, starting fresh: {e.Message}");
                _cache = new SaveData();
            }

            Sanitise(_cache);
            return _cache;
        }

        public static void Save()
        {
            if (_cache == null) Load();
            Sanitise(_cache);

            try
            {
                string json = JsonUtility.ToJson(_cache, true);
                string temp = SavePath + ".tmp";

                File.WriteAllText(temp, json);

                if (File.Exists(SavePath))
                    File.Replace(temp, SavePath, SavePath + ".bak");
                else
                    File.Move(temp, SavePath);

                Changed?.Invoke(_cache);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Failed to write save: {e.Message}");
            }
        }

        /// <summary>Backs the "reset progress" action in Settings (§7).</summary>
        public static void ResetProgress()
        {
            float music = Data.musicVolume;
            float sfx = Data.sfxVolume;

            _cache = new SaveData
            {
                // Resetting progress shouldn't throw away the player's volume choices.
                musicVolume = music,
                sfxVolume = sfx
            };

            Save();
        }

        /// <summary>Guards against hand-edited or version-skewed save files.</summary>
        /// <remarks>
        /// The level clamp also migrates saves from when a season was 25 levels: progress past the
        /// new campaign end simply lands on the final level rather than an unmappable number.
        /// </remarks>
        private static void Sanitise(SaveData data)
        {
            data.unlockedLevel = Mathf.Clamp(data.unlockedLevel, 1, SeasonCatalog.TotalLevels);
            data.lastPlayedLevel = Mathf.Clamp(data.lastPlayedLevel, 1, data.unlockedLevel);
            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
            data.bestScore = Mathf.Max(0, data.bestScore);
        }
    }
}
