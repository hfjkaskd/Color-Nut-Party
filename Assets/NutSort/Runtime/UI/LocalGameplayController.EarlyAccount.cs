using System;
using NutSort.Content;
using UnityEngine;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        private OriginalWithdrawalLoadingPanel earlyLoading;
        private OriginalWithdrawalLevelPanel earlyVerification;
        public OriginalWithdrawalLoadingPanel EarlyLoadingPanel=>earlyLoading;
        public OriginalWithdrawalLevelPanel EarlyVerificationPanel=>earlyVerification;
        public bool HasEarlyAccountModal=>earlyLoading!=null||earlyVerification!=null||
            (earlyStage>0&&(withdrawalAccount!=null||withdrawalConfirmation!=null));

        private void OpenEarlyWithdrawalAccount(int stage)
        {
            if(stage<1||stage>2)throw new ArgumentOutOfRangeException(nameof(stage));
            if(HasEarlyAccountModal)return;
            var prefab=Resources.Load<GameObject>((string)WithdrawalConfiguration["earlyLoadingPrefab"]);
            if(prefab==null||prefab.GetComponent<OriginalWithdrawalLoadingPanel>()==null)
                throw new InvalidOperationException("Early withdrawal loading prefab is not configured.");
            earlyStage=stage;
            game.ModalInputBlocked=true;
            earlyLoading=Instantiate(prefab,transform.parent,false).GetComponent<OriginalWithdrawalLoadingPanel>();
            var panel=earlyLoading;
            panel.Bind(game.Tables,language,()=>
            {
                if(panel!=null)Destroy(panel.gameObject);
                if(earlyLoading==panel)earlyLoading=null;
                if(earlyStage==stage)OpenWithdrawalAccount();
            },()=>{},()=>{},game.ScheduleDelay,()=>{});
            panel.Init();panel.Refresh();
        }

        private void OpenEarlyVerification(int stage)
        {
            if(earlyVerification!=null||earlyStage!=stage)return;
            EnsureAnalyticsWithdrawal(false,stage);
            var prefab=Resources.Load<GameObject>((string)WithdrawalConfiguration["earlyVerificationPrefab"]);
            if(prefab==null||prefab.GetComponent<OriginalWithdrawalLevelPanel>()==null)
                throw new InvalidOperationException("Early withdrawal verification prefab is not configured.");
            var rows=WithdrawalConfiguration["earlyVerificationSteps"] as Newtonsoft.Json.Linq.JArray;
            if(rows==null||rows.Count!=3)throw new InvalidOperationException("Early withdrawal verification descriptions are not configured.");
            string[] stepTexts={LocalUiLocalization.Text((string)rows[0]),LocalUiLocalization.Text((string)rows[1]),LocalUiLocalization.Text((string)rows[2])};
            game.ModalInputBlocked=true;
            earlyVerification=Instantiate(prefab,transform.parent,false).GetComponent<OriginalWithdrawalLevelPanel>();
            var panel=earlyVerification;
            var formatter=new OriginalGoldFormatter(()=>audio.UserState.DisplayCulture(game.Tables));
            bool submit=false;
            panel.InitLocalRecord(game.Tables,language,stage,formatter.Format(EarlyAccountAmount(stage)),
                WithdrawalText("earlyVerificationTitle"),WithdrawalText("earlyVerificationTip"),
                WithdrawalText("earlyVerificationConfirm"),WithdrawalText("earlyVerificationStatus"),stepTexts,
                ()=>panel!=null&&!panel.Closing&&TryAccountClick(),sound=>audio.PlaySound(sound),value=>
                {
                    submit=value;
                    if(value)
                    {
                        string operation=LocalAnalytics.Current?.WithdrawSubmit(EarlyAccountAmount(stage),AnalyticsChannel());
                        LocalAnalytics.Current?.WithdrawResult(operation,"service_unavailable","pending_local_record");
                    }
                },
                ()=>
                {
                    if(panel!=null)Destroy(panel.gameObject);
                    if(earlyVerification==panel)earlyVerification=null;
                    if(earlyStage==stage)FinishEarlyWithdrawal(submit);
                });
        }

        private void DisposeEarlyAccount()
        {
            if(earlyLoading!=null){Destroy(earlyLoading.gameObject);earlyLoading=null;}
            if(earlyVerification!=null){Destroy(earlyVerification.gameObject);earlyVerification=null;}
        }
    }
}
