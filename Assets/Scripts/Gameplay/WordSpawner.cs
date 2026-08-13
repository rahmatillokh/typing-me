using System;
using System.Collections;
using System.Collections.Generic;
using TypingMe.Data;
using UnityEngine;

namespace TypingMe.Gameplay
{
    /// <summary>
    /// Drips words in from the top at the level's spawn interval (§3, §5) and owns the list of
    /// words currently in play. Also carries out the boss's Word Burst and Speed Surge attacks.
    /// </summary>
    public sealed class WordSpawner : MonoBehaviour
    {
        [SerializeField] private RectTransform wordLayer;
        [SerializeField] private WordController wordPrefab;

        [Header("Play area (reference px, 1920x1080 canvas)")]
        [SerializeField] private float spawnY = 430f;

        [Tooltip("The bottom line. A word crossing it is a miss (§11).")]
        [SerializeField] private float bottomY = -250f;

        [SerializeField] private float sidePadding = 120f;

        [Tooltip("Words spawn in lanes so they rarely overlap; the previous lane is skipped.")]
        [SerializeField] private int lanes = 5;

        [Tooltip("Hard ceiling on words in play. A boss burst can reach it; the drip then waits.")]
        [SerializeField] private int maxConcurrentWords = 8;

        [Header("Launch")]
        [Tooltip("The boss's key. Words are thrown from wherever it currently is; falls back to " +
                 "lane placement when unset.")]
        [SerializeField] private RectTransform launchAnchor;

        [Tooltip("Minimum horizontal gap between words thrown in the same burst.")]
        [SerializeField] private float burstSeparation = 250f;

        [Tooltip("Extra height per word in a burst, so they arrive as a cascade rather than a row.")]
        [SerializeField] private float burstStagger = 56f;

        [Tooltip("Minimum horizontal gap between any two words arriving into the same height band. " +
                 "Enforced at spawn regardless of which system — drip, burst or double attack — " +
                 "asked for the word, so simultaneous spawns can never land on top of each other.")]
        [SerializeField] private float spawnSeparation = 230f;

        [Tooltip("How close in height (reference px) two words must be for the separation above " +
                 "to apply. Words further apart than this already read as distinct.")]
        [SerializeField] private float separationHeightWindow = 130f;

        [Tooltip("How far a single dripped word is thrown from the boss.")]
        [SerializeField] private float dripThrowMin = 140f;

        [SerializeField] private float dripThrowMax = 420f;

        private readonly List<WordController> _active = new List<WordController>();

        private LevelData _level;
        private ThemeSO _theme;
        private AnimationCurve _easing;
        private Coroutine _loop;
        private Coroutine _surge;
        private int _nextIndex;
        private int _lastLane = -1;

        // Season wind on falling words (§ seasons): amplitude px / rate Hz handed to each word.
        // Spring is deliberately zero — the clean fall is the baseline the other seasons bend.
        private float _swayAmplitude;
        private float _swayRate;

        public IReadOnlyList<WordController> Active => _active;
        public int ClearedCount { get; private set; }
        public int MissedCount { get; private set; }
        public float BottomLineY => bottomY;

        /// <summary>Current fall-speed scale; above 1 while a Speed Surge is running.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        public event Action<WordController> WordSpawned;
        public event Action<WordController> WordCleared;
        public event Action<WordController> WordMissed;

        /// <summary>Words on screen that can still be typed.</summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _active.Count; i++)
                    if (_active[i] != null && _active[i].IsAlive) count++;
                return count;
            }
        }

        public void Begin(LevelData level, ThemeSO theme, AnimationCurve easing)
        {
            StopSpawning();

            _level = level;
            _theme = theme;
            _easing = easing;
            _nextIndex = 0;
            _lastLane = -1;
            ClearedCount = 0;
            MissedCount = 0;
            SpeedMultiplier = 1f;

            SetSeasonWind(SeasonCatalog.SeasonOf(level.LevelNumber));

            ClearActive();
            _loop = StartCoroutine(SpawnLoop());
        }

        /// <summary>
        /// How the season handles a falling word: Spring drops clean, Summer shimmers, Autumn
        /// gusts sideways, Winter floats. This is the mechanical half of a season change — the
        /// palette is the visual half.
        /// </summary>
        private void SetSeasonWind(Season season)
        {
            switch (season)
            {
                case Season.Summer:
                    _swayAmplitude = 10f;
                    _swayRate = 0.55f;
                    break;
                case Season.Autumn:
                    _swayAmplitude = 30f;
                    _swayRate = 0.75f;
                    break;
                case Season.Winter:
                    _swayAmplitude = 18f;
                    _swayRate = 0.35f;
                    break;
                default:
                    _swayAmplitude = 0f;
                    _swayRate = 0f;
                    break;
            }
        }

        public void StopSpawning()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }

            if (_surge == null) return;

            StopCoroutine(_surge);
            _surge = null;
            SetSpeedMultiplier(1f);
        }

        public void ClearActive()
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i] != null) Destroy(_active[i].gameObject);

            _active.Clear();
        }

        /// <summary>
        /// Points the spawner at the boss's key. Wired at runtime rather than in the scene because
        /// the key is built by <see cref="TypingMe.UI.BossUI"/> on Awake, per rank.
        /// </summary>
        public void SetLaunchAnchor(RectTransform anchor) => launchAnchor = anchor;

        public void ApplyTheme(ThemeSO theme)
        {
            _theme = theme;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i] != null) _active[i].ApplyTheme(theme);
        }

        /// <summary>
        /// The level runs until the boss falls, not until a word count is hit, so this drips
        /// indefinitely and only backs off when the play area is already full.
        /// </summary>
        private IEnumerator SpawnLoop()
        {
            // A short beat before the first word so the player can read the HUD.
            yield return new WaitForSeconds(0.75f);
            SpawnNext();

            var wait = new WaitForSeconds(_level.SpawnInterval);

            while (true)
            {
                yield return wait;
                if (AliveCount >= maxConcurrentWords) continue;

                SpawnNext();
            }
        }

        #region Boss attacks

        /// <summary>
        /// Word Burst: several words at once, each aimed at its own column so the burst stays readable.
        /// </summary>
        /// <remarks>
        /// Every word leaves the boss from the same point, so separation has to be designed rather
        /// than left to random spread — throwing them all "away from centre" by a random amount put
        /// them within a word's width of each other and they piled up illegibly.
        /// </remarks>
        public void SpawnBurst(int count)
        {
            int room = Mathf.Max(0, maxConcurrentWords + 2 - AliveCount);
            int spawn = Mathf.Clamp(count, 0, room);
            if (spawn <= 0) return;

            float usable = UsableWidth();
            float span = Mathf.Min(usable, burstSeparation * Mathf.Max(1, spawn - 1));
            float step = spawn > 1 ? span / (spawn - 1) : 0f;

            // Centre the fan on the boss, then slide it to fit on screen. Anchoring the columns to
            // the middle instead would mean a boss at the edge hurling words over 1000px, which
            // reads as a snap rather than a throw.
            float half = usable * 0.5f;
            float origin = launchAnchor != null ? launchAnchor.anchoredPosition.x : 0f;
            float centre = span >= usable
                ? 0f
                : Mathf.Clamp(origin, -half + span * 0.5f, half - span * 0.5f);

            // Shuffle the columns so a burst doesn't always sweep left-to-right.
            var columns = new int[spawn];
            for (int i = 0; i < spawn; i++) columns[i] = i;

            for (int i = spawn - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (columns[i], columns[j]) = (columns[j], columns[i]);
            }

            for (int i = 0; i < spawn; i++)
            {
                float targetX = spawn > 1 ? centre - span * 0.5f + columns[i] * step : centre;
                SpawnNext(i * burstStagger, targetX);
            }
        }

        private float UsableWidth() =>
            Mathf.Max(1f, (wordLayer != null ? wordLayer.rect.width : Reference) - sidePadding * 2f);

        private const float Reference = 1920f;

        /// <summary>Speed Surge: everything on screen — and anything spawned during it — falls faster.</summary>
        public void ApplySpeedSurge(float multiplier, float duration)
        {
            if (_surge != null) StopCoroutine(_surge);
            _surge = StartCoroutine(SurgeRoutine(Mathf.Max(1f, multiplier), Mathf.Max(0.2f, duration)));
        }

        private IEnumerator SurgeRoutine(float multiplier, float duration)
        {
            SetSpeedMultiplier(multiplier);
            yield return new WaitForSeconds(duration);

            SetSpeedMultiplier(1f);
            _surge = null;
        }

        private void SetSpeedMultiplier(float value)
        {
            SpeedMultiplier = value;

            for (int i = 0; i < _active.Count; i++)
                if (_active[i] != null) _active[i].SpeedMultiplier = value;
        }

        /// <summary>Word Veil: hides the unread letters of up to <paramref name="count"/> words.</summary>
        public void VeilWords(int count, WordController exclude)
        {
            int veiled = 0;

            for (int i = 0; i < _active.Count && veiled < count; i++)
            {
                WordController word = _active[i];
                if (word == null || !word.IsAlive || word == exclude || word.IsVeiled) continue;

                word.SetVeiled(true);
                veiled++;
            }
        }

        public void ClearVeils()
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i] != null) _active[i].SetVeiled(false);
        }

        #endregion

        /// <param name="targetX">
        /// Where the word should end up horizontally. Null lets a single dripped word pick its own
        /// direction; a burst passes explicit columns so its words cannot overlap.
        /// </param>
        private void SpawnNext(float extraHeight = 0f, float? targetX = null)
        {
            if (_level == null || wordPrefab == null || wordLayer == null) return;
            if (_level.Words.Count == 0) return;

            // The queue is long, but a slow run can still exhaust it; the modulo recycles words
            // rather than starving the player of anything to type.
            string word = _level.Words[_nextIndex % _level.Words.Count];
            _nextIndex++;

            WordController instance = Instantiate(wordPrefab, wordLayer);
            instance.Cleared += HandleCleared;
            instance.Missed += HandleMissed;

            // Thrown from the boss when there is one, so its patrol decides where words come from.
            bool thrown = launchAnchor != null;
            float spawnX = thrown
                ? launchAnchor.anchoredPosition.x + UnityEngine.Random.Range(-14f, 14f)
                : PickLaneX();

            instance.Initialise(word, _level.FallSpeed, new Vector2(spawnX, spawnY + extraHeight),
                bottomY, _easing, _theme);
            instance.SpeedMultiplier = SpeedMultiplier;

            // Keep long words fully on screen.
            float halfWidth = instance.MeasuredWidth * 0.5f;
            float limit = Mathf.Max(0f, wordLayer.rect.width * 0.5f - halfWidth - 24f);
            var rect = (RectTransform)instance.transform;
            Vector2 position = rect.anchoredPosition;
            position.x = Mathf.Clamp(position.x, -limit, limit);
            rect.anchoredPosition = position;

            float height = spawnY + extraHeight;

            if (thrown)
            {
                // Burst columns and drip throws are planned by their own systems, but the edge
                // clamp above can squeeze them together — and a drip racing a burst, or a double
                // attack, plans nothing at all. The resolver is the one gate all of them pass:
                // no word may settle on top of another arriving at the same height.
                float desired = Mathf.Clamp(targetX ?? PickDripTarget(position.x), -limit, limit);
                float destination = ResolveDestination(instance, desired, limit, height);

                instance.ThrowBy(destination - position.x, limit);
                instance.PlannedX = destination;
            }
            else
            {
                position.x = ResolveDestination(instance, position.x, limit, height);
                rect.anchoredPosition = position;
                instance.PlannedX = position.x;
            }

            if (_swayAmplitude > 0f)
            {
                // Phase steps with the queue index so simultaneous words never sway in lockstep.
                instance.SetSway(_swayAmplitude, _swayRate, _nextIndex * 1.7f, limit);
            }

            // Registration must happen on every path — an unregistered word is invisible to the
            // targeting rule and to the miss check.
            _active.Add(instance);
            WordSpawned?.Invoke(instance);
        }

        /// <summary>Where a single dripped word should land — away from the boss, and on screen.</summary>
        private float PickDripTarget(float spawnX)
        {
            float half = UsableWidth() * 0.5f;

            // Throw away from whichever side the boss is on; near the middle, pick a side.
            float direction = Mathf.Abs(spawnX) < half * 0.25f
                ? (UnityEngine.Random.value < 0.5f ? -1f : 1f)
                : -Mathf.Sign(spawnX);

            float distance = UnityEngine.Random.Range(dripThrowMin, dripThrowMax);
            return Mathf.Clamp(spawnX + direction * distance, -half, half);
        }

        private float PickLaneX()
        {
            int laneCount = Mathf.Max(1, lanes);
            float usable = Mathf.Max(1f, wordLayer.rect.width - sidePadding * 2f);
            float laneWidth = usable / laneCount;

            int lane = UnityEngine.Random.Range(0, laneCount);
            if (laneCount > 1 && lane == _lastLane)
                lane = (lane + 1 + UnityEngine.Random.Range(0, laneCount - 1)) % laneCount;

            _lastLane = lane;

            float centre = -usable * 0.5f + (lane + 0.5f) * laneWidth;
            float jitter = laneWidth * 0.25f;
            return centre + UnityEngine.Random.Range(-jitter, jitter);
        }

        /// <summary>
        /// Moves <paramref name="desired"/> to the nearest slot that keeps
        /// <see cref="spawnSeparation"/> of clearance from every live word arriving into the same
        /// height band. On a screen too crowded to satisfy that, returns the least-bad slot rather
        /// than looping forever.
        /// </summary>
        private float ResolveDestination(WordController spawning, float desired, float limit, float height)
        {
            if (MinClearance(spawning, desired, height) >= spawnSeparation) return desired;

            const float step = 40f;

            float bestX = desired;
            float bestClearance = MinClearance(spawning, desired, height);

            for (float offset = step; offset <= limit * 2f; offset += step)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float candidate = desired + side * offset;
                    if (candidate < -limit || candidate > limit) continue;

                    float clearance = MinClearance(spawning, candidate, height);
                    if (clearance >= spawnSeparation) return candidate;

                    if (clearance <= bestClearance) continue;

                    bestClearance = clearance;
                    bestX = candidate;
                }
            }

            return bestX;
        }

        /// <summary>
        /// Distance from <paramref name="candidate"/> to the nearest live word within the height
        /// band, measured against where each word will settle rather than where it happens to be
        /// mid-throw.
        /// </summary>
        private float MinClearance(WordController spawning, float candidate, float height)
        {
            float clearance = float.MaxValue;

            for (int i = 0; i < _active.Count; i++)
            {
                WordController word = _active[i];
                if (word == null || !word.IsAlive || ReferenceEquals(word, spawning)) continue;
                if (Mathf.Abs(word.CurrentY - height) > separationHeightWindow) continue;

                clearance = Mathf.Min(clearance, Mathf.Abs(word.PlannedX - candidate));
            }

            return clearance;
        }

        private void HandleCleared(WordController word)
        {
            ClearedCount++;
            _active.Remove(word);
            WordCleared?.Invoke(word);
        }

        private void HandleMissed(WordController word)
        {
            MissedCount++;
            _active.Remove(word);
            WordMissed?.Invoke(word);
        }
    }
}
