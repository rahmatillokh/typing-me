using System.Collections;
using TMPro;
using TypingMe.Audio;
using TypingMe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TypingMe.UI
{
    /// <summary>Settings tab: volumes and reset progress (§7).</summary>
    /// <remarks>
    /// There is deliberately no theme picker here. The palette belongs to the season the campaign
    /// stands in, and the only way to change it is to beat the season — see <see cref="ThemeManager"/>.
    /// </remarks>
    public sealed class SettingsUI : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Danger zone")]
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text resetLabel;

        [SerializeField] private TMP_Text versionLabel;

        private const string ResetIdleText = "RESET PROGRESS";
        private const string ResetConfirmText = "TAP AGAIN TO CONFIRM";

        private Coroutine _confirmTimeout;
        private bool _awaitingConfirm;

        private void Awake()
        {
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSfxVolume);
            if (resetButton != null) resetButton.onClick.AddListener(HandleResetPressed);
            if (versionLabel != null) versionLabel.text = $"v{Application.version}";
        }

        private void OnEnable() => SyncFromSave();

        private void OnDisable() => CancelConfirm();

        private void SyncFromSave()
        {
            SaveData data = SaveSystem.Data;

            // SetValueWithoutNotify: assigning .value would fire onValueChanged and re-save on every open.
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(data.musicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(data.sfxVolume);
        }

        private void SetMusicVolume(float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.MusicVolume = value;
        }

        private void SetSfxVolume(float value)
        {
            if (AudioManager.Instance == null) return;

            AudioManager.Instance.SfxVolume = value;
            AudioManager.Instance.PlayUi();
        }

        /// <summary>Two-step confirm — wiping progress shouldn't be one stray tap away.</summary>
        private void HandleResetPressed()
        {
            if (!_awaitingConfirm)
            {
                _awaitingConfirm = true;
                if (resetLabel != null) resetLabel.text = ResetConfirmText;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayUi();

                _confirmTimeout = StartCoroutine(ConfirmTimeout());
                return;
            }

            CancelConfirm();
            SaveSystem.ResetProgress();

            // Progress is back at Spring 01, so the palette snaps back to Spring with it.
            if (ThemeManager.Instance != null) ThemeManager.Instance.ApplyProgressSeason();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayMiss();
            SyncFromSave();
        }

        private IEnumerator ConfirmTimeout()
        {
            yield return new WaitForSecondsRealtime(3.5f);
            CancelConfirm();
        }

        private void CancelConfirm()
        {
            if (_confirmTimeout != null)
            {
                StopCoroutine(_confirmTimeout);
                _confirmTimeout = null;
            }

            _awaitingConfirm = false;
            if (resetLabel != null) resetLabel.text = ResetIdleText;
        }
    }
}
