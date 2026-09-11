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
            game.ModalInputBlocked=true;
            string country=audio.UserState.DisplayCountry(game.Tables);
            withdrawalAccount=Instantiate(Resources.Load<GameObject>(withdrawalAccountPrefabPath),transform.parent,false).GetComponent<OriginalWithdrawalUserInfoPanel>();
            withdrawalAccount.Bind(game.User,game.Tables,language,()=>country,()=>game.Tables.Countries.Get(country).PhoneAreaNumber,
                ()=>withdrawalAccount!=null&&!withdrawalAccount.Closing&&TryAccountClick(),s=>audio.PlaySound(s),ShowToolTip,RouteAccountPanel,
                ()=>audio.UserState.Store.SaveData(false,null),v=>{},()=>{},
                ()=>{Destroy(withdrawalAccount.gameObject);withdrawalAccount=null;RestoreWithdrawalParent();},()=>{},game.ScheduleDelay,()=>{});
            withdrawalAccount.Init(new object[]{game.User.Level});withdrawalAccount.Refresh();
        }
        private void RouteAccountPanel(int id,object[] args)
        {
            if(id==20){OpenWithdrawalAccount();return;}
            if(id!=21){ShowWithdrawalFeedback(withdrawalAccountHelp);return;}
            if(withdrawalConfirmation!=null)return;
            string country=audio.UserState.DisplayCountry(game.Tables);
            withdrawalConfirmation=Instantiate(Resources.Load<GameObject>(withdrawalConfirmationPrefabPath),transform.parent,false).GetComponent<OriginalWithdrawalConfirmationPanel>();
            withdrawalConfirmation.Bind(game.User,game.Tables,language,()=>country,()=>game.Tables.Countries.Get(country).PhoneAreaNumber,
                ()=>withdrawalConfirmation!=null&&!withdrawalConfirmation.Closing&&TryAccountClick(),s=>audio.PlaySound(s),RouteAccountPanel,
                (level,hasArgs)=>ShowWithdrawalFeedback(withdrawalServiceUnavailable),v=>{},
                ()=>{Destroy(withdrawalConfirmation.gameObject);withdrawalConfirmation=null;RestoreWithdrawalParent();},()=>{},game.ScheduleDelay,()=>{});
            withdrawalConfirmation.Init(args);withdrawalConfirmation.Refresh();
        }
        private void RestoreWithdrawalParent()
        {
            if(withdrawalAccount!=null||withdrawalConfirmation!=null)return;
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(true);
            StartCoroutine(ReleaseWithdrawalInput());
        }
    }
}
