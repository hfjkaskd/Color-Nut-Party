using System;
using NutSort.Gameplay;
using NutSort.Content;
using NutSort.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    // Offline progression and local settlement using captured country configuration.
    public sealed partial class LocalGameplayController : MonoBehaviour
    {
        [SerializeField] private TMP_Text modeLabel, message;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button dismiss;
        [SerializeField] private string modeText, unavailableText;
        [SerializeField] private int maximumAddedTiles;
        private OriginalGameScene game;
        private OriginalAudioPlayer audio;
        private OriginalStartupFlow startup;
        private string language;
        private bool awaitingNext, bound;
        public bool AwaitingNext => awaitingNext;
        public bool ResultVisible => resultPanel.activeSelf||failurePanel!=null||successPanel!=null;
        public Button NextButton => successPanel==null?null:successPanel.MoreButton;
        public Button RetryButton => failurePanel==null?null:failurePanel.Restart;
        public Button DismissButton => dismiss;
        public TMP_Text ModeLabel => modeLabel;

        public void Bind(OriginalGameScene scene, OriginalAudioPlayer player, OriginalStartupFlow flow, string languageCode)
        {
            if (bound) throw new InvalidOperationException("Local gameplay is already bound.");
            game=scene; audio=player; startup=flow; language=languageCode; bound=true;
            modeLabel.text=modeText;
            resultPanel.SetActive(false);
            dismiss.onClick.AddListener(Dismiss);
            BindTools();
            BindCoreInitialization();
            BindRewardsTop();
            game.BoardReady+=OnBoardReady;
            // The native initialization continuation needs unavailable reward-stage
            // documents. Local mode finishes only once the real world is ready.
            BindBoardCallbacks();
            if (!game.IsRestarting && game.Level.AreNutsInitialized) OnBoardReady();
        }

        private void BindBoardCallbacks()
        {
            var board=game.Level.Board;
            game.BindMoveCompletion((target,complete)=>
            {
                if (game.Level.Board!=board || game.IsRestarting) return;
                if (complete) game.Success();
            }, ClearGuide, ()=>{}, null, id=>
            {
                if (game.Level.Board==board && !game.IsRestarting && !game.IsSucceed) ShowFailure();
            });
            game.BindSuccess(()=>game.NewLevelMode,SaveTutorialTransition,()=>{},()=>{},transform.parent,
                captured=>
                {
                    if (game.Level.Board==board && game.IsSucceed && !game.IsRestarting) CompleteLocalLevel(captured);
                });
        }

        private void OnBoardReady()
        {
            awaitingNext=false;
            resultPanel.SetActive(false);
            game.IsFail=false;
            game.IsInitDone=true;
            BindBoardCallbacks();
            BindToolsScene();
            SetExchange(false,0);
            bottom.Refresh();
            RefreshLevel();
            if(game.User.LocalPendingReward!=null)
            {
                awaitingNext=true;game.IsInitDone=false;game.ModalInputBlocked=true;
                ShowLocalSuccess();return;
            }
            BeginWelcomeOrTeaching();
            game.SaveUserData();
        }
        private void RefreshLevel()
        {
            startup.MainLevel.Refresh(game.Tables,game.PlayerLevel,language,false,false);
            RefreshSubrounds();
            RefreshRewardsTop();
        }
        private void ClearGuide()
        {
            CloseTeaching();
            for (int i=0;i<game.Level.Board.Screws.Length;i++) game.Level.GetScrew(i).ClearGuide();
        }
        private void SaveTutorialTransition()
        {
            PersistPendingBoard();
            RefreshLevel();
            RefreshTeaching();
        }
        private void PersistPendingBoard()
        {
            // Persist the new progress with no previous board. A process exit at
            // this boundary reconstructs the next board, never settles twice.
            game.User.LevelInfo=string.Empty;
            audio.UserState.Store.SaveData(false,null);
        }
        private void CompleteLocalLevel(OriginalLevelInfo completedLevel)
        {
            if (awaitingNext) return;
            game.User.Level=checked(game.User.Level+1);
            game.User.LevelSeed=0;
            game.User.TodayPassLevelCount=checked(game.User.TodayPassLevelCount+1);
            if(completedLevel.SubTotalRound<=completedLevel.SubRound)
                LocalCashSettlement.Prepare(game.User,audio.UserState.Rewards,UnityEngine.Random.Range);
            PersistPendingBoard();
            // Success response 0xA00684 skips settlement presentation for an
            // intermediate subround and calls InitLevel(true,false,false).
            if (completedLevel.SubTotalRound>completedLevel.SubRound)
            {
                game.InitLevel(true,false,false);
                return;
            }
            awaitingNext=true;
            game.IsInitDone=false;
            game.ModalInputBlocked=true;
            ShowLocalSuccess();
        }
        private void Next()
        {
            if (!awaitingNext) return;
            awaitingNext=false;
            Destroy(successPanel.gameObject);successPanel=null;
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
            game.InitLevel(true,true,false);
        }
        private void ShowUnavailable() => ShowNotice(unavailableText);
        private void ShowNotice(string text)
        {
            if (resultPanel.activeSelf) return;
            game.ModalInputBlocked=true;
            message.text=text;
            dismiss.gameObject.SetActive(true);
            resultPanel.SetActive(true);
        }
        private void Dismiss()
        {
            if (!resultPanel.activeSelf || !dismiss.gameObject.activeSelf) return;
            resultPanel.SetActive(false);
            StartCoroutine(ReleaseNoticeInput());
        }
        private System.Collections.IEnumerator ReleaseNoticeInput()
        {
            // Keep the release that clicked the UI from also selecting a world rod.
            yield return null;
            if (HasLocalModal) yield break;
            if (game.IsFail) ShowFailure();
            else game.ModalInputBlocked=startup.Replay.Panel!=null;
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveSession();
        }
        private void SaveSession()
        {
            if (!bound || game==null || game.Level==null || awaitingNext || game.IsRestarting || !game.Level.AreNutsInitialized) return;
            game.SaveUserData();
        }
        private void OnDestroy()
        {
            if(audio!=null&&audio.UserState!=null)audio.UserState.RewardConfigurationChanged-=RefreshRewardsTop;
            if(rewardsTop!=null)Destroy(rewardsTop.gameObject);
            if(withdrawalAccount!=null)Destroy(withdrawalAccount.gameObject);
            if(withdrawalConfirmation!=null)Destroy(withdrawalConfirmation.gameObject);
            if(toolOffer!=null)Destroy(toolOffer.gameObject);
            if(withdrawalPanel!=null)Destroy(withdrawalPanel.gameObject);
            if(diamondWithdrawalPanel!=null)Destroy(diamondWithdrawalPanel.gameObject);
            if(recordGuide!=null)Destroy(recordGuide.gameObject);
            if (game!=null) { game.BoardReady-=OnBoardReady;game.OperationApplied-=RefreshToolsAfterOperation; }
            if(startup!=null&&startup.Replay!=null)startup.Replay.BindModalBlocker(null);
            dismiss.onClick.RemoveListener(Dismiss);
        }
    }
}
