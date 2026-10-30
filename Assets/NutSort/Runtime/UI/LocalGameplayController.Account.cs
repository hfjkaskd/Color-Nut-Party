using System;
using UnityEngine;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string withdrawalAccountPrefabPath,withdrawalConfirmationPrefabPath,withdrawalAccountHelp;
        private OriginalWithdrawalUserInfoPanel withdrawalAccount;
        private OriginalWithdrawalConfirmationPanel withdrawalConfirmation;
        public OriginalWithdrawalUserInfoPanel WithdrawalAccount=>withdrawalAccount;
        public OriginalWithdrawalConfirmationPanel WithdrawalConfirmation=>withdrawalConfirmation;
        private bool TryAccountClick()=>!game.InputBlocked&&!game.IsRestarting&&!startup.Replay.IsClickMasked&&startup.Replay.BeginClick();
        private void OpenWithdrawalAccount()
        {
            if(withdrawalAccount!=null)return;
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(false);
            if(diamondWithdrawalPanel!=null)diamondWithdrawalPanel.gameObject.SetActive(false);
            game.ModalInputBlocked=true;
            string country=audio.UserState.DisplayCountry(game.Tables);
            int accountStage=earlyStage;
            bool finishEarlyOnClose=false;
            withdrawalAccount=Instantiate(Resources.Load<GameObject>(withdrawalAccountPrefabPath),transform.parent,false).GetComponent<OriginalWithdrawalUserInfoPanel>();
            var panel=withdrawalAccount;
            panel.Bind(game.User,game.Tables,language,()=>country,()=>game.Tables.Countries.Get(country).PhoneAreaNumber,
                ()=>panel!=null&&!panel.Closing&&TryAccountClick(),s=>audio.PlaySound(s),ShowToolTip,RouteAccountPanel,
                ()=>audio.UserState.Store.SaveData(false,null),v=>{if(accountStage>0)finishEarlyOnClose=true;},()=>{},
                ()=>
                {
                    if(panel!=null)Destroy(panel.gameObject);
                    if(withdrawalAccount==panel)withdrawalAccount=null;
                    if(finishEarlyOnClose&&earlyStage==accountStage)FinishEarlyWithdrawal(false);
                    else RestoreWithdrawalParent();
                },()=>{},game.ScheduleDelay,()=>{});
            panel.Init(new object[]{accountStage>0?accountStage:game.User.Level});panel.Refresh();
        }
        private void RouteAccountPanel(int id,object[] args)
        {
            if(id==20){OpenWithdrawalAccount();return;}
            if(id!=21){ShowWithdrawalFeedback(withdrawalAccountHelp);return;}
            if(withdrawalConfirmation!=null)return;
            string country=audio.UserState.DisplayCountry(game.Tables);
            int confirmationStage=earlyStage;
            bool finishEarlyOnClose=false,verifyAfterClose=false;
            withdrawalConfirmation=Instantiate(Resources.Load<GameObject>(withdrawalConfirmationPrefabPath),transform.parent,false).GetComponent<OriginalWithdrawalConfirmationPanel>();
            var panel=withdrawalConfirmation;
            panel.Bind(game.User,game.Tables,language,()=>country,()=>game.Tables.Countries.Get(country).PhoneAreaNumber,
                ()=>panel!=null&&!panel.Closing&&TryAccountClick(),s=>audio.PlaySound(s),RouteAccountPanel,
                (level,hasArgs)=>{if(confirmationStage>0)verifyAfterClose=true;else ConfirmLocalWithdrawal();},
                v=>{if(confirmationStage>0)finishEarlyOnClose=true;},
                ()=>
                {
                    if(panel!=null)Destroy(panel.gameObject);
                    if(withdrawalConfirmation==panel)withdrawalConfirmation=null;
                    if(confirmationStage>0&&earlyStage==confirmationStage)
                    {
                        if(finishEarlyOnClose){FinishEarlyWithdrawal(false);return;}
                        if(verifyAfterClose){OpenEarlyVerification(confirmationStage);return;}
                    }
                    RestoreWithdrawalParent();
                },()=>{},game.ScheduleDelay,()=>{});
            panel.Init(confirmationStage>0?new object[]{confirmationStage}:args);panel.Refresh();
            if(confirmationStage>0)panel.SetConfirmVisible(true);
        }
        private void RestoreWithdrawalParent()
        {
            if(HasEarlyAccountModal||withdrawalAccount!=null||withdrawalConfirmation!=null||withdrawalConditions!=null||withdrawalStage!=null||withdrawalTooFast!=null)return;
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(true);
            if(diamondWithdrawalPanel!=null)diamondWithdrawalPanel.gameObject.SetActive(true);
            StartCoroutine(ReleaseWithdrawalInput());
        }
    }
}
