using System;
using NutSort.Content;
using NutSort.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class LocalSettingsPanel : MonoBehaviour
    {
        [SerializeField] private Button soundButton, vibrateButton, closeButton, userLevelButton, historyButton, replayButton;
        [SerializeField] private TMP_Text soundStateLabel, vibrateStateLabel, userNameLabel, userIdLabel, userLevelLabel;
        [SerializeField] private string soundOnText, soundOffText, userIdFormat, userLevelFormat;
        [SerializeField] private Sprite soundOnSprite, soundOffSprite;
        [SerializeField] private Image soundIcon, vibrateIcon;
        [SerializeField] private Sprite soundOnIcon, soundOffIcon, vibrateOnIcon, vibrateOffIcon;
        private OriginalAudioPlayer audioPlayer;
        private Func<bool> canClick;
        private Action closed, playClick, saveSettings;
        private Action openUserLevel, openHistory, openReplay;
        private bool bound, closing;

        public Button SoundButton => soundButton;
        public Button VibrateButton => vibrateButton;
        public Button CloseButton => closeButton;
        public Button UserLevelButton => userLevelButton;
        public Button HistoryButton => historyButton;
        public Button ReplayButton => replayButton;
        public TMP_Text StateLabel => soundStateLabel;
        public TMP_Text VibrateStateLabel => vibrateStateLabel;
        public TMP_Text UserNameLabel => userNameLabel;
        public TMP_Text UserIdLabel => userIdLabel;
        public TMP_Text UserLevelLabel => userLevelLabel;
        public Image SoundIcon => soundIcon;
        public Image VibrateIcon => vibrateIcon;
        public bool Closing => closing;

        public void Bind(OriginalAudioPlayer player, Func<bool> tryClick, Action close, Action clickSound, Action save,
            Action userLevel, Action history, Action replay)
        {
            if (bound) throw new InvalidOperationException("Settings panel is already bound.");
            if (soundButton == null || soundButton.image == null || vibrateButton == null || vibrateButton.image == null ||
                closeButton == null || userLevelButton == null || historyButton == null || replayButton == null ||
                soundStateLabel == null || vibrateStateLabel == null || userNameLabel == null || userIdLabel == null || userLevelLabel == null ||
                soundIcon == null || vibrateIcon == null || soundOnIcon == null || soundOffIcon == null || vibrateOnIcon == null || vibrateOffIcon == null ||
                soundOnSprite == null || soundOffSprite == null || string.IsNullOrEmpty(soundOnText) || string.IsNullOrEmpty(soundOffText) ||
                string.IsNullOrEmpty(userIdFormat) || string.IsNullOrEmpty(userLevelFormat))
                throw new InvalidOperationException("Settings panel prefab references are incomplete.");
            audioPlayer = player != null ? player : throw new ArgumentNullException(nameof(player));
            canClick = tryClick ?? throw new ArgumentNullException(nameof(tryClick));
            closed = close ?? throw new ArgumentNullException(nameof(close));
            playClick = clickSound ?? throw new ArgumentNullException(nameof(clickSound));
            saveSettings = save ?? throw new ArgumentNullException(nameof(save));
            openUserLevel = userLevel ?? throw new ArgumentNullException(nameof(userLevel));
            openHistory = history ?? throw new ArgumentNullException(nameof(history));
            openReplay = replay ?? throw new ArgumentNullException(nameof(replay));
            soundButton.onClick.AddListener(ToggleSound);
            vibrateButton.onClick.AddListener(ToggleVibrate);
            closeButton.onClick.AddListener(Close);
            userLevelButton.onClick.AddListener(OpenUserLevel);
            historyButton.onClick.AddListener(OpenHistory);
            replayButton.onClick.AddListener(OpenReplay);
            bound = true;
            Refresh();
        }

        private void ToggleSound()
        {
            if (closing || !canClick()) return;
            audioPlayer.SetAudioEnabled(!audioPlayer.AudioEnabled);
            LocalAnalyticsPopup.Action(this,"toggle_sound",audioPlayer.AudioEnabled?"on":"off");
            saveSettings();
            RefreshSwitches();
            playClick();
        }

        private void ToggleVibrate()
        {
            if (closing || !canClick()) return;
            audioPlayer.SetVibrationEnabled(!audioPlayer.VibrationEnabled);
            LocalAnalyticsPopup.Action(this,"toggle_vibration",audioPlayer.VibrationEnabled?"on":"off");
            saveSettings();
            RefreshSwitches();
            playClick();
        }

        public void Refresh()
        {
            var user = audioPlayer.UserState.Data;
            userNameLabel.text = user.UserName ?? string.Empty;
            userIdLabel.text = LocalUiLocalization.Format(userIdFormat, user.UserId ?? string.Empty);
            userLevelLabel.text = LocalUiLocalization.Format(userLevelFormat, user.UserLevel);
            RefreshSwitches();
        }

        private void RefreshSwitches()
        {
            bool enabled = audioPlayer.AudioEnabled;
            soundStateLabel.text = LocalUiLocalization.Text(enabled ? soundOnText : soundOffText);
            soundButton.image.sprite = enabled ? soundOnSprite : soundOffSprite;
            soundIcon.sprite = enabled ? soundOnIcon : soundOffIcon;
            bool vibration = audioPlayer.VibrationEnabled;
            vibrateStateLabel.text = LocalUiLocalization.Text(vibration ? soundOnText : soundOffText);
            vibrateButton.image.sprite = vibration ? soundOnSprite : soundOffSprite;
            vibrateIcon.sprite = vibration ? vibrateOnIcon : vibrateOffIcon;
        }

        private void OpenUserLevel() => Navigate(openUserLevel);
        private void OpenHistory() => Navigate(openHistory);
        private void OpenReplay() => Navigate(openReplay);

        private void Navigate(Action action)
        {
            if (closing || !canClick()) return;
            LocalAnalyticsPopup.Action(this,action==openUserLevel?"user_level":action==openHistory?"history":"replay");
            closing = true;
            playClick();
            action();
        }

        private void Close()
        {
            if (closing || !canClick()) return;
            LocalAnalyticsPopup.Action(this,"close");
            closing = true;
            playClick();
            closed();
        }

        private void OnDestroy()
        {
            if (!bound) return;
            soundButton.onClick.RemoveListener(ToggleSound);
            vibrateButton.onClick.RemoveListener(ToggleVibrate);
            closeButton.onClick.RemoveListener(Close);
            userLevelButton.onClick.RemoveListener(OpenUserLevel);
            historyButton.onClick.RemoveListener(OpenHistory);
            replayButton.onClick.RemoveListener(OpenReplay);
        }
    }
}
