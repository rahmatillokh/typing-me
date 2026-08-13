using TypingMe.Data;
using TypingMe.Gameplay;
using TypingMe.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TypingMe.Core
{
    /// <summary>
    /// Session-level flow that outlives any single scene (§9): which level is active, scene transitions,
    /// and writing progress. Per-level runtime lives in <see cref="LevelRunner"/>.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public const string MenuSceneName = "Menu";
        public const string GameSceneName = "Game";

        public static GameManager Instance { get; private set; }

        [SerializeField] private WordBankSO wordBank;
        [SerializeField] private LevelTuningSO tuning;
        [SerializeField] private BossTuningSO bossTuning;

        public GameStateMachine State { get; } = new GameStateMachine();
        public WordBankSO WordBank => wordBank;
        public LevelTuningSO Tuning => tuning;
        public BossTuningSO BossTuning => bossTuning;

        /// <summary>The level the player is on or about to play.</summary>
        public int CurrentLevel { get; private set; } = 1;

        public int HighestUnlockedLevel => SaveSystem.Data.unlockedLevel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SaveSystem.Load();
            CurrentLevel = SaveSystem.Data.lastPlayedLevel;

            // Parse the word list once at boot (§4) rather than on the first spawn, so the
            // first level doesn't hitch.
            if (wordBank != null) wordBank.EnsureLoaded();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public LevelData BuildLevel(int levelNumber) =>
            LevelGenerator.Generate(levelNumber, wordBank, tuning);

        public BossDefinition BuildBoss(int levelNumber, LevelData level) =>
            BossGenerator.Generate(levelNumber, level, bossTuning);

        public void PlayLevel(int levelNumber)
        {
            // Any route out of a level must clear a pause; doing it here as well means no future
            // exit path can strand the game at timeScale 0.
            Time.timeScale = 1f;

            CurrentLevel = Mathf.Clamp(levelNumber, 1, Mathf.Min(HighestUnlockedLevel, SeasonCatalog.TotalLevels));

            SaveSystem.Data.lastPlayedLevel = CurrentLevel;
            SaveSystem.Save();

            // The palette belongs to the level's season, so replaying an old season also replays
            // its look, and stepping into a fresh season restyles the game before the scene loads.
            if (ThemeManager.Instance != null) ThemeManager.Instance.ApplyLevelSeason(CurrentLevel);

            State.Set(GameState.Playing);
            SceneManager.LoadScene(GameSceneName);
        }

        /// <summary>"Continue" on Home — jumps to the last active level (§3).</summary>
        public void ContinueRun() => PlayLevel(SaveSystem.Data.lastPlayedLevel);

        public void RetryLevel() => PlayLevel(CurrentLevel);

        public void PlayNextLevel() => PlayLevel(CurrentLevel + 1);

        public void GoHome()
        {
            Time.timeScale = 1f;

            // The menu always wears the season the campaign currently stands in.
            if (ThemeManager.Instance != null) ThemeManager.Instance.ApplyProgressSeason();

            State.Set(GameState.Home);
            SceneManager.LoadScene(MenuSceneName);
        }

        /// <summary>Unlocks the next level and persists progress (§9).</summary>
        public void NotifyLevelCompleted(int levelNumber, int score)
        {
            SaveData data = SaveSystem.Data;

            // The campaign is finite: 4 seasons of 20. Clearing the last level unlocks nothing more.
            int next = Mathf.Min(levelNumber + 1, SeasonCatalog.TotalLevels);

            if (next > data.unlockedLevel) data.unlockedLevel = next;

            data.lastPlayedLevel = Mathf.Min(next, data.unlockedLevel);
            if (score > data.bestScore) data.bestScore = score;

            SaveSystem.Save();
            State.Set(GameState.LevelComplete);
        }

        public void NotifyGameOver() => State.Set(GameState.GameOver);
    }
}
