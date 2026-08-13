using System.Collections;
using TMPro;
using TypingMe.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TypingMe.Core
{
    /// <summary>
    /// Plays the Aysapps splash clip on launch, then hands off to the Menu scene.
    /// </summary>
    /// <remarks>
    /// Rendered through a RenderTexture rather than <c>CameraNearPlane</c>: near-plane video does not
    /// composite reliably under URP, whereas a RawImage is just UI. The handoff is also guarded — a
    /// clip that fails to prepare, errors, or simply never reports completion must not strand the
    /// player on a black screen, so a timeout always moves things along.
    /// </remarks>
    public sealed class SplashController : MonoBehaviour
    {
        [SerializeField] private VideoPlayer player;
        [SerializeField] private RawImage surface;
        [SerializeField] private AudioSource videoAudio;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private TMP_Text skipHint;

        [SerializeField] private string nextScene = GameManager.MenuSceneName;

        [Tooltip("Hard ceiling on the whole splash, however the clip behaves.")]
        [SerializeField] private float maxSeconds = 12f;

        [Tooltip("Seconds before the skip hint appears.")]
        [SerializeField] private float skipHintDelay = 1.2f;

        private RenderTexture _target;
        private bool _finished;

        /// <summary>Why the splash ended — a silent early-out is otherwise invisible.</summary>
        private string _reason = "unknown";
        private float _startedAt;
        private float _surfaceBrightness = -1f;

        /// <summary>Average luminance of the middle of the video target, or -1 if never sampled.</summary>
        private float SampleSurface()
        {
            if (_target == null) return -1f;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _target;

            const int size = 16;
            var probe = new Texture2D(size, size, TextureFormat.RGB24, false);
            probe.ReadPixels(new Rect(
                Mathf.Max(0, _target.width / 2 - size / 2),
                Mathf.Max(0, _target.height / 2 - size / 2), size, size), 0, 0);
            probe.Apply();

            RenderTexture.active = previous;

            float total = 0f;
            Color[] pixels = probe.GetPixels();
            for (int i = 0; i < pixels.Length; i++) total += pixels[i].grayscale;

            Destroy(probe);
            return total / pixels.Length;
        }

        private void Awake()
        {
            // The persistent services boot before the first scene, so the music is already running.
            if (AudioManager.Instance != null) AudioManager.Instance.PauseMusic();

            if (skipHint != null) skipHint.alpha = 0f;
            if (fade != null) fade.alpha = 0f;
        }

        private IEnumerator Start()
        {
            _startedAt = Time.realtimeSinceStartup;

            if (player == null || player.clip == null)
            {
                _reason = "no clip assigned";
                yield return Finish();
                yield break;
            }

            SetUpVideo();

            player.errorReceived += HandleVideoError;
            player.Prepare();

            float deadline = Time.realtimeSinceStartup + maxSeconds;

            // Ignore input for the first moment: a key still down from launching the app would
            // otherwise register as an immediate skip.
            float armSkipAt = Time.realtimeSinceStartup + 0.4f;

            while (!player.isPrepared && !_finished && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= armSkipAt && SkipRequested())
                {
                    _reason = "skipped while preparing";
                    break;
                }

                yield return null;
            }

            if (!_finished && player.isPrepared && _reason == "unknown")
            {
                player.Play();

                float hintAt = Time.realtimeSinceStartup + skipHintDelay;

                // The clip's own length is the primary bound; isPlaying alone is unreliable in the
                // frames right after Play().
                float clipEnd = Time.realtimeSinceStartup + (float)player.clip.length + 0.25f;

                while (Time.realtimeSinceStartup < deadline && !_finished)
                {
                    if (skipHint != null && Time.realtimeSinceStartup >= hintAt && skipHint.alpha < 0.55f)
                        skipHint.alpha = Mathf.MoveTowards(skipHint.alpha, 0.55f, Time.unscaledDeltaTime * 1.5f);

                    if (Time.realtimeSinceStartup >= armSkipAt && SkipRequested())
                    {
                        _reason = "skipped";
                        break;
                    }

                    if (Time.realtimeSinceStartup >= clipEnd)
                    {
                        _reason = "clip finished";
                        break;
                    }

                    // Sample once, mid-clip. Decoded frames prove the player is working; only reading
                    // the target back proves those frames reach the surface the player can see.
                    if (_surfaceBrightness < 0f && player.frame > 20) _surfaceBrightness = SampleSurface();

                    yield return null;
                }

                if (_reason == "unknown") _reason = "deadline";
            }
            else if (_reason == "unknown")
            {
                _reason = _finished ? "video error" : "never prepared";
            }

            yield return Finish();
        }

        private void SetUpVideo()
        {
            var width = (int)player.clip.width;
            var height = (int)player.clip.height;

            _target = new RenderTexture(Mathf.Max(2, width), Mathf.Max(2, height), 0)
            {
                name = "SplashTarget",
                hideFlags = HideFlags.DontSave
            };

            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = _target;
            player.isLooping = false;
            player.playOnAwake = false;
            player.waitForFirstFrame = true;

            if (videoAudio != null)
            {
                player.audioOutputMode = VideoAudioOutputMode.AudioSource;
                player.SetTargetAudioSource(0, videoAudio);

                // A brand sting shouldn't be louder than the player asked the game to be.
                videoAudio.volume = SaveSystem.Data.musicVolume;
            }
            else
            {
                player.audioOutputMode = VideoAudioOutputMode.None;
            }

            if (surface == null) return;

            surface.texture = _target;
            surface.color = Color.white;
        }

        private static bool SkipRequested()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[Splash] Video error, skipping to the menu: {message}");
            _finished = true;
        }

        private IEnumerator Finish()
        {
            bool surfaceLive = surface != null && surface.isActiveAndEnabled &&
                               ReferenceEquals(surface.texture, _target);

            Debug.Log($"[Splash] end after {Time.realtimeSinceStartup - _startedAt:F2}s — {_reason} " +
                      $"(clip={(player != null && player.clip != null ? player.clip.name : "none")}, " +
                      $"prepared={(player != null && player.isPrepared)}, " +
                      $"frame={(player != null ? player.frame : -1)}, " +
                      $"surfaceLive={surfaceLive}, brightness={_surfaceBrightness:F3})");

            if (fade != null)
            {
                float elapsed = 0f;
                const float duration = 0.35f;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    fade.alpha = Mathf.Clamp01(elapsed / duration);
                    yield return null;
                }
            }

            if (player != null)
            {
                player.errorReceived -= HandleVideoError;
                player.Stop();
            }

            if (AudioManager.Instance != null) AudioManager.Instance.ResumeMusic();

            SceneManager.LoadScene(nextScene);
        }

        private void OnDestroy()
        {
            if (player != null) player.errorReceived -= HandleVideoError;

            if (_target == null) return;

            _target.Release();
            Destroy(_target);
        }
    }
}
