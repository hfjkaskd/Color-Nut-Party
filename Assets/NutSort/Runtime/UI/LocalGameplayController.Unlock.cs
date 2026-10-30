using System;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.World;
using UnityEngine;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private OriginalSceneSession gameplaySession;
        [SerializeField] private string unlockPrefabPath;
        private OriginalGameplayUnlockPanelHost unlockHost;
        private OriginalGameplayUnlockConfig unlockConfig;
        private OriginalGameplayUnlock unlock;
        private readonly OriginalPanelActionQueue unlockQueue=new OriginalPanelActionQueue();
        public OriginalGameplayUnlockPanel UnlockPanel=>unlockHost==null?null:unlockHost.Panel;
        private bool HasLocalModal=>HasEarningModal||IsEarningRewardModal||settingsPanel!=null||userLevelPanel!=null||toolOffer!=null||toolAdPending||withdrawalStage!=null||withdrawalTooFast!=null||withdrawalConditions!=null||withdrawalAccount!=null||withdrawalConfirmation!=null||diamondWithdrawalPanel!=null||withdrawalPanel!=null||welcomePending||recordGuide!=null||ResultVisible||teachingPanel!=null||(unlockHost!=null&&unlockHost.IsOpen);

        private void BindCoreInitialization()
        {
            unlockConfig=new OriginalGameplayUnlockConfig(game.User,gameplaySession.GameplayUnlockLevels);
            unlockHost=new OriginalGameplayUnlockPanelHost(transform.parent,unlockPrefabPath,game.User,game.Tables,language,
                TryUnlockClick,s=>audio.PlaySound(s),callback=>throw new InvalidOperationException("Local mode does not supply a reward banner."),
                unlockQueue,game.ScheduleDelay,UnlockHidden);
            unlock=new OriginalGameplayUnlock(game.User,
                ()=>game.User.ServerConfigData==null?gameplaySession.GameplayUnlockLevels:unlockConfig.Read(),
                (id,index,banner)=>
                {
                    game.ModalInputBlocked=true;
                    unlockHost.Show(index,banner);
                });
        }
        private void BeginCoreInitialization()
        {
            if(TryOpenEarningFlow())return;
            if(TryOpenPendingLuckyReward())return;
            if(TryOpenCoinUnlockGuide())return;
            var board=game.Level.Board;
            if(game.Level.IsCannotMove())
            {
                game.Fail(id=>
                {
                    if(game.Level.Board==board&&!game.IsRestarting&&!game.IsSucceed)ShowFailure();
                });
                return;
            }
            // Rebuilding while this modal is open must not register it twice.
            if(!unlockHost.IsOpen)unlock.Run(false);
        }
        private bool TryUnlockClick()
        {
            if(game.InputBlocked||game.IsRestarting||!game.IsInitDone||startup.Replay.IsClickMasked||UnlockPanel==null||UnlockPanel.Closing)return false;
            return startup.Replay.BeginClick();
        }
        private void UnlockHidden()
        {
            // Native confirmation writes index+1. Local persistence checkpoints
            // that result on close so a relaunch does not repeat the modal.
            game.SaveUserData();
            StartCoroutine(ReleaseUnlockInput());
        }
        private System.Collections.IEnumerator ReleaseUnlockInput()
        {
            yield return null; // registry removes the panel after its Hide callback
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
        }
    }
}
