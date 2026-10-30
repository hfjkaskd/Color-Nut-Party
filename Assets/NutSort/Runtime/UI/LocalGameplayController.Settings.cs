using System;
using UnityEngine;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string settingsPrefabPath;
        [SerializeField] private string userLevelPrefabPath;
        private LocalSettingsPanel settingsPanel;
        private LocalUserLevelPanel userLevelPanel;
        public LocalSettingsPanel SettingsPanel => settingsPanel;
        public LocalUserLevelPanel UserLevelPanel => userLevelPanel;

        private void OpenSettings()
        {
            // The existing Bottom Button wrapper has already acquired its click mask.
            if (!bound || settingsPanel != null || game.InputBlocked || game.IsRestarting || !game.IsInitDone ||
                game.IsSucceed || HasLocalModal || startup.Replay.Panel != null) return;
            var prefab = Resources.Load<GameObject>(settingsPrefabPath);
            if (prefab == null || prefab.GetComponent<LocalSettingsPanel>() == null)
                throw new InvalidOperationException("Settings panel resource is not configured.");
            settingsPanel = Instantiate(prefab, transform.parent, false).GetComponent<LocalSettingsPanel>();
            game.ModalInputBlocked = true;
            settingsPanel.Bind(audio, TrySettingsClick, CloseSettings, startup.Replay.EndClick, game.SaveUserData,
                OpenSettingsUserLevel, OpenSettingsHistory, OpenSettingsReplay);
        }

        private bool TrySettingsClick()
        {
            if (settingsPanel == null || settingsPanel.Closing || game.InputBlocked || game.IsRestarting ||
                !game.IsInitDone || startup.Replay.IsClickMasked) return false;
            return startup.Replay.BeginClick();
        }

        private void CloseSettings()
        {
            if (settingsPanel == null) return;
            Destroy(settingsPanel.gameObject);
            settingsPanel = null;
            StartCoroutine(ReleaseWithdrawalInput());
        }

        private void OpenSettingsHistory()
        {
            CloseSettings();
            OpenCashHistory();
        }

        private void OpenSettingsReplay()
        {
            CloseSettings();
            // The settings Button already acquired the shared click mask.
            startup.Replay.OpenPanel();
        }

        private void OpenSettingsUserLevel()
        {
            if (userLevelPanel != null) return;
            var prefab = Resources.Load<GameObject>(userLevelPrefabPath);
            if (prefab == null || prefab.GetComponent<LocalUserLevelPanel>() == null)
                throw new InvalidOperationException("User-level panel resource is not configured.");
            CloseSettings();
            userLevelPanel = Instantiate(prefab, transform.parent, false).GetComponent<LocalUserLevelPanel>();
            game.ModalInputBlocked = true;
            userLevelPanel.Bind(game.User, TryUserLevelClick, CloseUserLevel, startup.Replay.EndClick);
        }

        private bool TryUserLevelClick()
        {
            if (userLevelPanel == null || userLevelPanel.Closing || game.InputBlocked || game.IsRestarting ||
                !game.IsInitDone || startup.Replay.IsClickMasked) return false;
            return startup.Replay.BeginClick();
        }

        private void CloseUserLevel()
        {
            if (userLevelPanel == null) return;
            Destroy(userLevelPanel.gameObject);
            userLevelPanel = null;
            StartCoroutine(ReleaseWithdrawalInput());
        }
    }
}
