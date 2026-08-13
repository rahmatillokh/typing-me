using TypingMe.Core;
using UnityEngine;

namespace TypingMe.Audio
{
    /// <summary>
    /// Music/SFX playback with volumes persisted in the save file (§7).
    /// Lives on the persistent services object.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Optional authored clips — generated fallbacks are used when empty")]
        [Tooltip("Authored music. When empty the generated tracks are used instead.")]
        [SerializeField] private AudioClip[] musicPlaylist;

        [SerializeField] private AudioClip keyClip;
        [SerializeField] private AudioClip clearClip;
        [SerializeField] private AudioClip missClip;
        [SerializeField] private AudioClip wrongClip;
        [SerializeField] private AudioClip uiClip;
        [SerializeField] private AudioClip levelCompleteClip;
        [SerializeField] private AudioClip gameOverClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            BuildClips();
            ApplyVolumes();

            if (musicSource == null) return;

            BuildPlaylistOrder();
            PlayCurrentTrack();

            StartCoroutine(RotateTracks());
            StartCoroutine(ReportAudioHealth());
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.State.Changed -= HandleGameStateChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Fills any clip the inspector left empty. Pitches sit in A minor so the effects agree with
        /// the background loop instead of fighting it.
        /// </summary>
        private void BuildClips()
        {
            // Plays on every keystroke, so it is deliberately short and quiet — anything richer
            // becomes fatiguing at typing speed.
            keyClip ??= ProceduralSfx.Create("key", 900f, 720f, 0.05f, WaveShape.Square, 22f, 0f, 0.22f);

            // Rising third plus a shimmer above it: the reward sound.
            clearClip ??= ProceduralSfx.CreateSequence("clear",
                new ProceduralSfx.Blip(0f, 0.10f, 1046.5f, 1046.5f, WaveShape.Square, 0.30f, 14f),
                new ProceduralSfx.Blip(0.055f, 0.14f, 1318.5f, 1318.5f, WaveShape.Square, 0.26f, 12f),
                new ProceduralSfx.Blip(0.10f, 0.22f, 1760f, 1760f, WaveShape.Sine, 0.20f, 9f));

            wrongClip ??= ProceduralSfx.Create("wrong", 190f, 140f, 0.09f, WaveShape.Square, 20f, 0.45f, 0.28f);

            missClip ??= ProceduralSfx.CreateSequence("miss",
                new ProceduralSfx.Blip(0f, 0.36f, 300f, 70f, WaveShape.Saw, 0.40f, 6f),
                new ProceduralSfx.Blip(0f, 0.18f, 120f, 50f, WaveShape.Sine, 0.35f, 9f, 0.25f));

            uiClip ??= ProceduralSfx.Create("ui", 620f, 820f, 0.07f, WaveShape.Triangle, 16f, 0f, 0.28f);

            // Ascending A minor arpeggio.
            levelCompleteClip ??= ProceduralSfx.CreateSequence("levelComplete",
                new ProceduralSfx.Blip(0.00f, 0.40f, 440f, 440f, WaveShape.Square, 0.26f, 5f),
                new ProceduralSfx.Blip(0.09f, 0.40f, 523.25f, 523.25f, WaveShape.Square, 0.26f, 5f),
                new ProceduralSfx.Blip(0.18f, 0.45f, 659.25f, 659.25f, WaveShape.Square, 0.26f, 4.5f),
                new ProceduralSfx.Blip(0.27f, 0.65f, 880f, 880f, WaveShape.Sine, 0.32f, 3.2f));

            // The same shape inverted — falling, and detuned flat at the end.
            gameOverClip ??= ProceduralSfx.CreateSequence("gameOver",
                new ProceduralSfx.Blip(0.00f, 0.35f, 440f, 440f, WaveShape.Saw, 0.24f, 6f),
                new ProceduralSfx.Blip(0.14f, 0.35f, 349.23f, 349.23f, WaveShape.Saw, 0.24f, 6f),
                new ProceduralSfx.Blip(0.28f, 0.70f, 261.63f, 246f, WaveShape.Saw, 0.28f, 3.5f),
                new ProceduralSfx.Blip(0.28f, 0.70f, 110f, 100f, WaveShape.Sine, 0.30f, 3f));

        }

        #region Playlist

        private int[] _order;
        private int _position;
        private AudioClip[] _generated;
        private bool _musicPaused;

        /// <summary>How many tracks are in rotation.</summary>
        public int TrackCount => musicPlaylist is { Length: > 0 } ? musicPlaylist.Length : ProceduralMusic.TrackCount;

        /// <summary>Name of the track currently playing, for UI.</summary>
        public string CurrentTrackName =>
            musicSource != null && musicSource.clip != null ? musicSource.clip.name : string.Empty;

        /// <summary>Shuffles the running order so a session doesn't always open the same way.</summary>
        private void BuildPlaylistOrder()
        {
            int count = Mathf.Max(1, TrackCount);

            _order = new int[count];
            for (int i = 0; i < count; i++) _order[i] = i;

            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }

            _position = 0;
            _generated = new AudioClip[ProceduralMusic.TrackCount];
        }

        /// <summary>
        /// Generated tracks are built on first use rather than all at boot — four 20-second buffers
        /// is a noticeable stall and a chunk of memory for music that may never be reached.
        /// </summary>
        private AudioClip ResolveTrack(int index)
        {
            if (musicPlaylist is { Length: > 0 })
                return musicPlaylist[Mathf.Abs(index) % musicPlaylist.Length];

            int slot = Mathf.Abs(index) % ProceduralMusic.TrackCount;
            return _generated[slot] ??= ProceduralMusic.CreateTrack(slot);
        }

        private void PlayCurrentTrack()
        {
            if (musicSource == null || _order == null || _order.Length == 0) return;

            AudioClip clip = ResolveTrack(_order[_position % _order.Length]);
            if (clip == null) return;

            musicSource.clip = clip;

            // Not looping: the rotation moves on to the next track when this one ends.
            musicSource.loop = false;
            musicSource.Play();
        }

        /// <summary>Skips to the next track immediately.</summary>
        public void NextTrack()
        {
            if (_order == null || _order.Length == 0) return;

            _position = (_position + 1) % _order.Length;
            PlayCurrentTrack();
        }

        private System.Collections.IEnumerator RotateTracks()
        {
            var poll = new WaitForSecondsRealtime(0.5f);

            while (true)
            {
                yield return poll;

                // isPlaying is false both when a track ends and while it is paused, so the paused
                // flag is what tells "finished" apart from "held for the splash".
                if (_musicPaused || musicSource == null || musicSource.clip == null) continue;
                if (musicSource.isPlaying) continue;

                NextTrack();
            }
        }

        #endregion

        public float MusicVolume
        {
            get => SaveSystem.Data.musicVolume;
            set
            {
                SaveSystem.Data.musicVolume = Mathf.Clamp01(value);
                ApplyVolumes();
                SaveSystem.Save();
            }
        }

        public float SfxVolume
        {
            get => SaveSystem.Data.sfxVolume;
            set
            {
                SaveSystem.Data.sfxVolume = Mathf.Clamp01(value);
                ApplyVolumes();
                SaveSystem.Save();
            }
        }

        /// <summary>Music level outside active gameplay, as a share of the player's setting.</summary>
        public const float OutOfGameMusicScale = 0.5f;

        private float _contextScale = OutOfGameMusicScale;
        private float _duckScale = 1f;

        private void Start()
        {
            if (GameManager.Instance == null) return;

            GameManager.Instance.State.Changed += HandleGameStateChanged;
            SetMusicContext(GameManager.Instance.State.Current == GameState.Playing);
        }

        private void HandleGameStateChanged(GameState previous, GameState next) =>
            SetMusicContext(next == GameState.Playing);

        /// <summary>
        /// Menus, pause and end-of-run panels run the music at half level; only live gameplay gets
        /// the player's full setting.
        /// </summary>
        public void SetMusicContext(bool inGameplay)
        {
            _contextScale = inGameplay ? 1f : OutOfGameMusicScale;
            ApplyMusicVolume();
        }

        public void ApplyVolumes()
        {
            ApplyMusicVolume();
            if (sfxSource != null) sfxSource.volume = SaveSystem.Data.sfxVolume;
        }

        /// <summary>
        /// Single place the music level is computed. Context and ducking are separate multipliers so
        /// a sting duck can't overwrite the menu level, or vice versa.
        /// </summary>
        private void ApplyMusicVolume()
        {
            if (musicSource == null) return;

            musicSource.volume = SaveSystem.Data.musicVolume * _contextScale * _duckScale;
        }

        /// <summary>Silences the music without losing its position — used while the splash plays.</summary>
        public void PauseMusic()
        {
            _musicPaused = true;
            if (musicSource != null && musicSource.isPlaying) musicSource.Pause();
        }

        public void ResumeMusic()
        {
            _musicPaused = false;

            if (musicSource == null || musicSource.clip == null || musicSource.isPlaying) return;

            musicSource.UnPause();
            if (!musicSource.isPlaying) musicSource.Play();
        }

        /// <summary>Slight pitch scatter keeps rapid typing from sounding like a machine gun.</summary>
        public void PlayKey() => Play(keyClip, Random.Range(0.94f, 1.08f));

        public void PlayClear() => Play(clearClip, Random.Range(0.98f, 1.04f));

        public void PlayMiss() => Play(missClip);

        public void PlayWrong() => Play(wrongClip, Random.Range(0.9f, 1.02f));

        public void PlayUi() => Play(uiClip);

        public void PlayLevelComplete() => Play(levelCompleteClip);

        public void PlayGameOver() => Play(gameOverClip);

        /// <summary>
        /// Logs what is actually reaching the output, once, shortly after boot.
        /// </summary>
        /// <remarks>
        /// <c>AudioSource.isPlaying</c> is not evidence of sound: it stays true with no AudioListener
        /// in the scene, which is exactly how this project shipped silently once. Reading the mixed
        /// output back through the listener is the only check that can tell the difference.
        /// </remarks>
        private System.Collections.IEnumerator ReportAudioHealth()
        {
            var clipSamples = new float[4096];
            AudioClip track = musicSource != null ? musicSource.clip : null;
            if (track != null) track.GetData(clipSamples, 0);
            float clipPeak = 0f;
            for (int i = 0; i < clipSamples.Length; i++) clipPeak = Mathf.Max(clipPeak, Mathf.Abs(clipSamples[i]));

            var output = new float[1024];
            float outputPeak = 0f;

            // Retried rather than sampled once: the mix reads silent for the first couple of seconds
            // while the audio engine warms up, and an unfocused player is paused outright while
            // runInBackground is off. A single early reading reports silence on a healthy mix.
            for (int attempt = 0; attempt < 4 && outputPeak <= 0f; attempt++)
            {
                yield return new WaitForSecondsRealtime(attempt == 0 ? 1.5f : 2f);

                AudioListener.GetOutputData(output, 0);
                for (int i = 0; i < output.Length; i++) outputPeak = Mathf.Max(outputPeak, Mathf.Abs(output[i]));
            }

            var listener = FindFirstObjectByType<AudioListener>();

            string verdict = outputPeak > 0f ? "audible" :
                listener == null ? "SILENT — no AudioListener in the scene" : "SILENT — check mixer/volume";

            Debug.Log($"[Audio] {verdict}: track='{(track != null ? track.name : "none")}' " +
                      $"{(track != null ? track.length : 0f):F1}s of {TrackCount} " +
                      $"playing={musicSource.isPlaying} volume={musicSource.volume:F2} " +
                      $"listener={(listener != null ? "yes" : "MISSING")} " +
                      $"clipPeak={clipPeak:F3} outputPeak={outputPeak:F3} focused={Application.isFocused}");
        }

        /// <summary>Ducks the music under an end-of-run sting, then restores it.</summary>
        public void DuckMusic(float toVolume, float seconds)
        {
            if (musicSource == null || !isActiveAndEnabled) return;

            // Cancel only a duck in flight — StopAllCoroutines would also kill unrelated work.
            if (_duck != null) StopCoroutine(_duck);
            _duck = StartCoroutine(DuckRoutine(Mathf.Clamp01(toVolume), Mathf.Max(0.05f, seconds)));
        }

        private Coroutine _duck;

        private System.Collections.IEnumerator DuckRoutine(float toVolume, float seconds)
        {
            _duckScale = toVolume;
            ApplyMusicVolume();

            yield return new WaitForSecondsRealtime(seconds);

            float elapsed = 0f;
            const float fade = 0.6f;

            while (elapsed < fade)
            {
                elapsed += Time.unscaledDeltaTime;
                _duckScale = Mathf.Lerp(toVolume, 1f, elapsed / fade);
                ApplyMusicVolume();
                yield return null;
            }

            _duckScale = 1f;
            ApplyMusicVolume();
        }

        private void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null || sfxSource == null) return;

            sfxSource.pitch = pitch;

            // sfxSource.volume already carries the saved level; PlayOneShot's scale multiplies with
            // it, so passing the volume again here would square it.
            sfxSource.PlayOneShot(clip);
        }
    }
}
