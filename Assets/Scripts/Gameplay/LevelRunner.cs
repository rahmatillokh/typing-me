using System.Collections;
using System.Collections.Generic;
using TypingMe.Audio;
using TypingMe.Core;
using TypingMe.Data;
using TypingMe.Fx;
using TypingMe.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// Runs one level inside the Game scene: wires spawner, router, mistakes, boss, HUD and keyboard
    /// together, carries out the boss's attacks, and decides when the level is won or lost.
    /// </summary>
    /// <remarks>
    /// The win condition is the boss's health reaching zero, not a word count — clearing words is how
    /// you damage it. Losing is unchanged: three missed words end the run (§11).
    /// </remarks>
    public sealed class LevelRunner : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private WordSpawner spawner;
        [SerializeField] private InputRouter router;
        [SerializeField] private MistakeTracker mistakes;
        [SerializeField] private BossController boss;

        [Header("Presentation")]
        [SerializeField] private KeyboardVisualUI keyboard;
        [SerializeField] private HudUI hud;
        [SerializeField] private BossUI bossUI;
        [SerializeField] private RankUpUI rankUp;
        [SerializeField] private FinaleUI finale;
        [SerializeField] private GameEndPanel gameOverPanel;
        [SerializeField] private GameEndPanel levelCompletePanel;
        [SerializeField] private GameEndPanel pausePanel;
        [SerializeField] private RectTransform shakeRoot;
        [SerializeField] private Image missFlash;
        [SerializeField] private ShardBurst clearBurst;

        [Header("Scoring")]
        [SerializeField] private int pointsPerLetter = 10;
        [SerializeField] private int maxComboMultiplier = 5;

        private LevelData _level;
        private BossDefinition _bossDefinition;
        private int _score;
        private int _combo;
        private bool _finished;

        private Coroutine _veilRoutine;

        public BossController Boss => boss;

        private void OnEnable()
        {
            if (spawner != null)
            {
                spawner.WordCleared += HandleWordCleared;
                spawner.WordMissed += HandleWordMissed;
            }

            if (router != null)
            {
                router.CorrectKey += HandleCorrectKey;
                router.WrongKey += HandleWrongKey;
                router.TargetChanged += HandleTargetChanged;
            }

            if (mistakes != null)
            {
                mistakes.Changed += HandleMissesChanged;
                mistakes.Exhausted += HandleMissesExhausted;
            }

            if (boss == null) return;

            boss.HealthChanged += HandleBossHealthChanged;
            boss.AttackTelegraphed += HandleAttackTelegraphed;
            boss.AttackFired += HandleAttackFired;
            boss.Enraged += HandleBossEnraged;
            boss.Defeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            if (spawner != null)
            {
                spawner.WordCleared -= HandleWordCleared;
                spawner.WordMissed -= HandleWordMissed;
            }

            if (router != null)
            {
                router.CorrectKey -= HandleCorrectKey;
                router.WrongKey -= HandleWrongKey;
                router.TargetChanged -= HandleTargetChanged;
            }

            if (mistakes != null)
            {
                mistakes.Changed -= HandleMissesChanged;
                mistakes.Exhausted -= HandleMissesExhausted;
            }

            if (boss == null) return;

            boss.HealthChanged -= HandleBossHealthChanged;
            boss.AttackTelegraphed -= HandleAttackTelegraphed;
            boss.AttackFired -= HandleAttackFired;
            boss.Enraged -= HandleBossEnraged;
            boss.Defeated -= HandleBossDefeated;
        }

        private void Start()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[LevelRunner] No GameManager; the persistent services prefab failed to load.");
                return;
            }

            StartLevel(GameManager.Instance.CurrentLevel);
        }

        public void StartLevel(int levelNumber)
        {
            GameManager game = GameManager.Instance;
            LevelTuningSO tuning = game.Tuning;

            levelNumber = SeasonCatalog.Clamp(levelNumber);

            _level = game.BuildLevel(levelNumber);
            _bossDefinition = game.BuildBoss(levelNumber, _level);
            _score = 0;
            _combo = 0;
            _finished = false;

            ClearPause();
            ClearVeil();

            if (pausePanel != null) pausePanel.Hide();
            if (mistakes != null) mistakes.ResetFor(tuning.maxMisses);

            if (hud != null)
            {
                hud.SetLevelLabel(_bossDefinition.SeasonLabel);
                hud.SetRank(_bossDefinition.Rank);
                hud.SetupMisses(tuning.maxMisses);
                hud.SetBossHealth(_bossDefinition.MaxHealth, _bossDefinition.MaxHealth);
                hud.SetScore(0);
            }

            if (bossUI != null)
            {
                bossUI.Bind(_bossDefinition);

                // The boss's body is built per rank on Bind, so this can only be linked at runtime.
                if (spawner != null) spawner.SetLaunchAnchor(bossUI.BodyRect);
            }

            if (gameOverPanel != null) gameOverPanel.Hide();
            if (levelCompletePanel != null) levelCompletePanel.Hide();
            if (rankUp != null) rankUp.Hide();
            if (finale != null) finale.Hide();
            if (keyboard != null) keyboard.ClearHighlight();

            if (missFlash != null) missFlash.color = Color.clear;

            if (router != null)
            {
                router.Bind(spawner);
                router.ResetTarget();
                router.AcceptInput = true;
            }

            if (spawner != null) spawner.Begin(_level, ThemeManager.ActiveTheme, tuning.fallEasing);
            if (boss != null) boss.Begin(_bossDefinition, game.BossTuning);

            game.State.Set(GameState.Playing);
        }

        #region Typing

        private void HandleCorrectKey(char typed, WordController word)
        {
            if (keyboard != null)
            {
                keyboard.FlashCorrect(typed);
                keyboard.HighlightNext(router.Target != null ? router.Target.NextChar : '\0');
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayKey();
        }

        private void HandleWrongKey(char typed)
        {
            if (keyboard != null) keyboard.FlashWrong(typed);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();
        }

        private void HandleTargetChanged(WordController target)
        {
            if (keyboard != null) keyboard.HighlightNext(target != null ? target.NextChar : '\0');
        }

        private void HandleWordCleared(WordController word)
        {
            _combo++;

            bool sigilHit = false;
            if (boss != null && !_finished) boss.TakeDamage(word.Word, out sigilHit);

            int multiplier = Mathf.Clamp(_combo, 1, Mathf.Max(1, maxComboMultiplier));
            int points = pointsPerLetter * word.Word.Length * multiplier;

            // Sigil words are worth chasing on the scoreboard as well as against the boss.
            if (sigilHit) points = Mathf.RoundToInt(points * 1.5f);
            _score += points;

            if (hud != null) hud.SetScore(_score);
            if (bossUI != null) bossUI.PulseDamage(sigilHit);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayClear();

            if (clearBurst == null) return;

            ThemeSO theme = ThemeManager.ActiveTheme;
            Color colour = theme == null
                ? Color.cyan
                : (sigilHit ? theme.accentSecondary : theme.accentPrimary);

            clearBurst.Play(word.AnchoredPosition, colour);
        }

        private void HandleWordMissed(WordController word)
        {
            _combo = 0;

            if (router != null && ReferenceEquals(router.Target, word)) router.ResetTarget();
            if (keyboard != null) keyboard.ClearHighlight();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMiss();

            if (shakeRoot != null) StartCoroutine(Juice.ShakeAnchored(shakeRoot, 20f, 0.3f));

            if (missFlash != null)
            {
                ThemeSO theme = ThemeManager.ActiveTheme;
                Color flash = theme != null ? theme.accentAlert : Color.red;
                flash.a = 0.28f;
                StartCoroutine(Juice.FlashColor(missFlash, flash, Color.clear, 0.45f));
            }

            // MistakeTracker owns the count so the 3-miss rule lives in one place (§11).
            if (mistakes != null) mistakes.RegisterMiss();
        }

        private void HandleMissesChanged(int misses, int maxMisses)
        {
            if (hud != null) hud.SetMisses(misses, maxMisses);
        }

        private void HandleMissesExhausted() => EndRun();

        #endregion

        #region Boss

        private void HandleBossHealthChanged(float current, float max)
        {
            if (hud != null) hud.SetBossHealth(current, max);
        }

        private void HandleBossEnraged()
        {
            if (bossUI != null) bossUI.PlayEnrage();
            if (shakeRoot != null) StartCoroutine(Juice.ShakeAnchored(shakeRoot, 26f, 0.45f));
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMiss();
        }

        private void HandleAttackTelegraphed(BossAttack attack)
        {
            if (bossUI != null) bossUI.ShowTelegraph(attack, _bossDefinition.Profile.telegraph);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();
        }

        private void HandleAttackFired(BossAttack attack)
        {
            if (_finished) return;

            BossRankProfile profile = _bossDefinition.Profile;

            switch (attack)
            {
                case BossAttack.WordBurst:
                    if (spawner != null) spawner.SpawnBurst(profile.burstWords);
                    break;

                case BossAttack.SpeedSurge:
                    if (spawner != null) spawner.ApplySpeedSurge(profile.surgeMultiplier, profile.surgeDuration);
                    break;

                case BossAttack.WordVeil:
                    if (_veilRoutine != null) StopCoroutine(_veilRoutine);
                    _veilRoutine = StartCoroutine(WordVeilRoutine(profile.veilWords, profile.veilDuration));
                    break;
            }

            if (bossUI != null) bossUI.PlayAttackRecoil();
            if (shakeRoot != null) StartCoroutine(Juice.ShakeAnchored(shakeRoot, 12f, 0.22f));
        }

        private IEnumerator WordVeilRoutine(int count, float duration)
        {
            if (spawner != null) spawner.VeilWords(count, router != null ? router.Target : null);

            yield return new WaitForSeconds(duration);

            ClearVeil();
            _veilRoutine = null;
        }

        private void ClearVeil()
        {
            if (spawner != null) spawner.ClearVeils();
        }

        private void HandleBossDefeated() => CompleteLevel();

        #endregion

        #region Pause

        public bool IsPaused { get; private set; }

        private void Update()
        {
            if (_finished) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) TogglePause();
        }

        /// <summary>Wired to the HUD pause button and to Escape.</summary>
        public void TogglePause()
        {
            if (_finished) return;

            if (IsPaused) Resume();
            else Pause();
        }

        private void Pause()
        {
            if (IsPaused || _finished) return;

            IsPaused = true;

            // timeScale freezes falling, spawning and the boss's attack timers in one move; the UI,
            // background scroll and audio all run on unscaled time and keep going.
            Time.timeScale = 0f;

            if (router != null)
            {
                router.AcceptInput = false;
                router.ResetTarget();
            }

            if (keyboard != null) keyboard.ClearHighlight();
            if (bossUI != null) bossUI.HideTelegraph();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            GameManager.Instance.State.Set(GameState.Paused);

            if (pausePanel == null) return;

            GameManager game = GameManager.Instance;

            pausePanel.Show(
                "PAUSED",
                $"{_bossDefinition.SeasonLabel}  ·  RANK {_bossDefinition.Rank}  ·  {_score:N0} pts",
                "RESUME", Resume,
                "RETRY", () => { ClearPause(); game.RetryLevel(); },
                "HOME", () => { ClearPause(); game.GoHome(); });
        }

        private void Resume()
        {
            if (!IsPaused) return;

            ClearPause();

            if (pausePanel != null) pausePanel.Hide();
            if (router != null) router.AcceptInput = true;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

            GameManager.Instance.State.Set(GameState.Playing);
        }

        /// <summary>Drops the pause without touching the panel or game state — for exit paths.</summary>
        private void ClearPause()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }

        #endregion

        #region Outcomes

        private void CompleteLevel()
        {
            if (_finished) return;
            _finished = true;

            Freeze();
            if (bossUI != null) bossUI.PlayDefeat();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLevelComplete();
                AudioManager.Instance.DuckMusic(0.35f, 1.4f);
            }

            GameManager game = GameManager.Instance;
            game.NotifyLevelCompleted(_level.LevelNumber, _score);

            // Beating a rank's last boss earns the interstitial: the send-off line plus a live
            // teaser of the next rank's actual boss. Beating a season boss is the season handover —
            // the interstitial is also where the palette flips to the new season. Beating the last
            // level of Winter outranks both: the campaign finale takes over the whole ending.
            bool campaignOver = SeasonCatalog.IsFinalLevel(_level.LevelNumber);

            int nextLevel = _level.LevelNumber + 1;
            bool rankCleared = !campaignOver &&
                               SeasonCatalog.RankFor(nextLevel) != _bossDefinition.Rank;

            if (campaignOver && finale != null)
            {
                finale.Show(_score);
            }
            else if (rankCleared && rankUp != null)
            {
                BossDefinition next = game.BuildBoss(nextLevel, game.BuildLevel(nextLevel));
                rankUp.Show(_bossDefinition, next, ShowLevelCompletePanel);
            }
            else
            {
                ShowLevelCompletePanel();
            }
        }

        private void ShowLevelCompletePanel()
        {
            if (levelCompletePanel == null) return;

            GameManager game = GameManager.Instance;
            bool campaignOver = SeasonCatalog.IsFinalLevel(_level.LevelNumber);

            string title = campaignOver ? "ALL SEASONS CLEARED"
                : _bossDefinition.IsSeasonBoss ? $"{SeasonCatalog.DisplayName(_bossDefinition.Season)} CLEARED"
                : "BOSS DOWN";

            string subtitle = $"RANK {_bossDefinition.Rank}  ·  {_score:N0} pts";

            levelCompletePanel.Show(
                title,
                subtitle,
                campaignOver ? null : "NEXT LEVEL",
                campaignOver ? (System.Action)null : () => game.PlayNextLevel(),
                "HOME", () => game.GoHome());
        }

        private void EndRun()
        {
            if (_finished) return;
            _finished = true;

            Freeze();
            if (spawner != null) spawner.ClearActive();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayGameOver();
                AudioManager.Instance.DuckMusic(0.25f, 1.6f);
            }

            GameManager game = GameManager.Instance;
            game.NotifyGameOver();

            if (gameOverPanel == null) return;

            int hpLeft = boss != null ? Mathf.CeilToInt(boss.Health) : 0;
            int hpMax = boss != null ? Mathf.CeilToInt(boss.MaxHealth) : 0;

            gameOverPanel.Show(
                "SYSTEM FAILURE",
                $"BOSS SURVIVED ON {hpLeft} / {hpMax} HP  ·  {_score:N0} pts",
                "RETRY", () => game.RetryLevel(),
                "HOME", () => game.GoHome());
        }

        private void Freeze()
        {
            // A level can end while paused (Retry from the pause panel), so never leave time stopped.
            ClearPause();
            if (pausePanel != null) pausePanel.Hide();

            if (router != null)
            {
                router.AcceptInput = false;
                router.ResetTarget();
            }

            if (boss != null) boss.StopFighting();
            if (spawner != null) spawner.StopSpawning();

            ClearVeil();

            if (keyboard == null) return;

            keyboard.ClearHighlight();
            if (bossUI != null) bossUI.HideTelegraph();
        }

        /// <summary>Wired to the in-run back button.</summary>
        public void QuitToHome()
        {
            Freeze();
            if (GameManager.Instance != null) GameManager.Instance.GoHome();
        }

        #endregion
    }
}
