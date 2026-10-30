using System.Collections;
using NutSort.Content;
using NutSort.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NutSort.UI
{
    public sealed class OriginalStartupFlow : MonoBehaviour
    {
        public static event System.Action<LocalGameplayController> LocalGameplayReady;
        public static LocalGameplayController CurrentLocalGameplay { get; private set; }
        [SerializeField, Min(1)] private int targetFrameRate = 60;
        [SerializeField] private OriginalLoadingView loading;
        [SerializeField] private OriginalGameScene game;
        [SerializeField] private Transform panelCanvas,topCanvas;
        public Transform TopCanvas=>topCanvas;
        [SerializeField] private OriginalMainLevelSettings mainLevelSettings;
        [SerializeField] private OriginalAudioPlayer audioPlayer;
        [SerializeField] private GameObject clickMask;
        public OriginalReplayController Replay { get; private set; }
        public OriginalMainLevelView MainLevel { get; private set; }
        public OriginalTargetRewardBanner TargetReward { get; private set; }
        public OriginalLoadingView Loading => loading;
        public LocalGameplayController LocalGameplay { get; private set; }
        private string bootCountry;
        private Coroutine localeRefresh;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
        }

        private IEnumerator Start()
        {
            game.InputBlocked = true;
            loading.SetState(true);
            yield return null;
            audioPlayer.PlayBgm();
            // SDK initialization starts independently on its configured scene
            // node. Ad requests fail closed until its inventory is ready.
            loading.SetState(false);
            yield return null;
            // Source boot activates GameScene on the frame after the loading
            // completion signal, while its progress tween is still finishing.
            game.gameObject.SetActive(true);
            game.Initialize();
            bootCountry = audioPlayer.UserState.ActualCountry;
            string language = game.Tables.Countries.Get(audioPlayer.UserState.DisplayCountry(game.Tables)).LanguageCode;
            LocalUiLocalization.Configure(language, game.Tables.Text);
            var mainPrefab = Resources.Load<GameObject>(mainLevelSettings.PrefabPath);
            MainLevel = Instantiate(mainPrefab, panelCanvas, false).GetComponent<OriginalMainLevelView>();
            TargetReward = MainLevel.GetComponentInChildren<OriginalTargetRewardBanner>(true);
            TargetReward.Init();
            Replay = MainLevel.GetComponent<OriginalReplayController>();
            Replay.Bind(game, audioPlayer, panelCanvas, LocalUiLocalization.Language, clickMask);
            MainLevel.Refresh(game.Tables, game.PlayerLevel, LocalUiLocalization.Language, false, false);
            if (!string.IsNullOrEmpty(mainLevelSettings.LocalGameplayPath))
            {
                var localPrefab = Resources.Load<GameObject>(mainLevelSettings.LocalGameplayPath);
                LocalGameplay = Instantiate(localPrefab, panelCanvas, false).GetComponent<LocalGameplayController>();
                LocalGameplay.Bind(game, audioPlayer, this, LocalUiLocalization.Language);
                CurrentLocalGameplay = LocalGameplay;
                LocalGameplayReady?.Invoke(LocalGameplay);
            }
            while (loading.IsVisible) yield return null;
            game.InputBlocked = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused || string.IsNullOrEmpty(bootCountry) || localeRefresh != null) return;
            if (OriginalUserSession.DeviceCountry() != bootCountry) localeRefresh = StartCoroutine(RefreshDeviceLocale());
        }

        private IEnumerator RefreshDeviceLocale()
        {
            // Finish any reward/ad/modal before rebuilding localized views. Board data is preserved.
            var retry = new WaitForSecondsRealtime(.25f);
            while (LocalGameplay == null || !LocalGameplay.CanRefreshDeviceLocale) yield return retry;
            if (OriginalUserSession.DeviceCountry() != bootCountry)
            {
                game.SaveUserData();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            localeRefresh = null;
        }

        private void OnDestroy()
        {
            if(CurrentLocalGameplay == LocalGameplay)CurrentLocalGameplay = null;
        }
    }
}
