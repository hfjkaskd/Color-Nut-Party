using System;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        private JObject withdrawalConfiguration;
        private LocalWithdrawalConditionsPanel withdrawalConditions;
        private bool withdrawalIsDiamond;
        private int withdrawalTier;
        private OriginalWithdrawalStageZeroPanel withdrawalStage;
        private OriginalWithdrawalTooFastPanel withdrawalTooFast;
        public OriginalWithdrawalStageZeroPanel WithdrawalStage=>withdrawalStage;
        public OriginalWithdrawalTooFastPanel WithdrawalTooFast=>withdrawalTooFast;
        public LocalWithdrawalConditionsPanel WithdrawalConditions=>withdrawalConditions;
        private JObject WithdrawalConfiguration
        {
            get
            {
                if(withdrawalConfiguration==null)
                {
                    var asset=Resources.Load<TextAsset>("Configuration/LocalWithdrawal");
                    if(asset==null)throw new InvalidOperationException("Missing withdrawal configuration.");
                    withdrawalConfiguration=JObject.Parse(asset.text);
                }
                return withdrawalConfiguration;
            }
        }
        private string WithdrawalText(string key)=>LocalUiLocalization.Text((string)WithdrawalConfiguration[key]);
        private void RefreshWithdrawalCalendar(bool applyTutorial=true)
        {
            bool changed=LocalWithdrawalPolicy.RefreshCalendar(game.User,DateTime.Now.Date);
            changed=LocalEarningFlowPolicy.MigrateLegacyRecords(game.User)||changed;
            if(changed)audio.UserState.Store.SaveData(false,null);
        }
        private LocalWithdrawalEligibility WithdrawalEligibility()
        {
            RefreshWithdrawalCalendar();
            var result=LocalWithdrawalPolicy.Evaluate(game.User,game.Tables,withdrawalIsDiamond,withdrawalTier);
            LocalAnalytics.Current?.ObserveEligibility(result,"requirements_check");
            return result;
        }
        private void BeginCashWithdrawal()
        {
            withdrawalIsDiamond=false;withdrawalTier=0;
            EnsureAnalyticsWithdrawal(false,0);
            // Preserve the early-account form, but never fabricate an early payout.
            if(game.User.Level<=2){OpenWithdrawalAccount();return;}
            OpenWithdrawalConditions();
        }
        private void BeginDiamondWithdrawal(int tier)
        {withdrawalIsDiamond=true;withdrawalTier=tier;EnsureAnalyticsWithdrawal(true,tier);OpenWithdrawalConditions();}
        private void OpenWithdrawalConditions()
        {
            if(withdrawalConditions!=null)return;
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(false);
            if(diamondWithdrawalPanel!=null)diamondWithdrawalPanel.gameObject.SetActive(false);
            game.ModalInputBlocked=true;
            withdrawalConditions=Instantiate(Resources.Load<GameObject>((string)WithdrawalConfiguration["conditionsPrefab"]),transform.parent,false).GetComponent<LocalWithdrawalConditionsPanel>();
            withdrawalConditions.Init(WithdrawalConfiguration,game.Tables,withdrawalIsDiamond,
                new OriginalGoldFormatter(()=>audio.UserState.DisplayCulture(game.Tables)),WithdrawalEligibility,TryAccountClick,
                result=>
                {
                    CloseWithdrawalConditions();
                    if(result.Eligible)OpenWithdrawalAccount();
                    else if(!withdrawalIsDiamond&&result.FirstIncomplete=="secondLevel")OpenWithdrawalStage();
                    else ReturnToGameFromWithdrawal();
                },CloseWithdrawalConditions);
        }
        private void CloseWithdrawalConditions()
        {
            if(withdrawalConditions!=null){Destroy(withdrawalConditions.gameObject);withdrawalConditions=null;}
            RestoreWithdrawalParent();
        }
        private void ConfirmLocalWithdrawal()
        {
            EnsureAnalyticsWithdrawal(withdrawalIsDiamond,withdrawalTier);
            double amount=withdrawalIsDiamond?(double)game.User.GoldRewardTargetS2CData["bear_zs_list"][withdrawalTier]["caliper_psi"]:game.User.Gold;
            string operation=LocalAnalytics.Current?.WithdrawSubmit(amount,AnalyticsChannel());
            if(game.User.Level<=2&&!withdrawalIsDiamond)
            {LocalAnalytics.Current?.WithdrawResult(operation,"service_unavailable");ShowWithdrawalFeedback(WithdrawalText("service"));return;}
            if(!WithdrawalEligibility().Eligible){LocalAnalytics.Current?.WithdrawResult(operation,"client_requirements_not_met");OpenWithdrawalConditions();return;}
            LocalAnalytics.Current?.WithdrawResult(operation,"service_unavailable");
            ShowWithdrawalFeedback(WithdrawalText("service"));
        }
        private void OpenWithdrawalStage()
        {
            if(withdrawalStage!=null)return;
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(false);
            game.ModalInputBlocked=true;
            withdrawalStage=Instantiate(Resources.Load<GameObject>((string)WithdrawalConfiguration["stagePrefab"]),transform.parent,false).GetComponent<OriginalWithdrawalStageZeroPanel>();
            var formatter=new OriginalGoldFormatter(()=>audio.UserState.DisplayCulture(game.Tables));
            var clock=new OriginalWithdrawalTime(()=>audio.UserState.DisplayCulture(game.Tables));
            withdrawalStage.Bind(game.User,game.Tables,language,Array.Empty<object>(),OriginalWithdrawalTime.LocalSeconds,
                value=>formatter.Format(value),clock.Format,UnityEngine.Random.Range,topProgress.IsCompletePassStage2Level,
                ()=>game.Tables.GetShowLevel(game.User.Level),()=>audio.UserState.Store.SaveData(false,null),value=>{},
                ()=>withdrawalStage!=null&&!withdrawalStage.Closing&&withdrawalTooFast==null&&TryAccountClick(),
                sound=>audio.PlaySound(sound),(id,args)=>{if(id==33&&withdrawalStage!=null)OpenWithdrawalTooFast();},
                ()=>{if(withdrawalTooFast!=null){Destroy(withdrawalTooFast.gameObject);withdrawalTooFast=null;}Destroy(withdrawalStage.gameObject);withdrawalStage=null;RestoreWithdrawalParent();},
                ()=>{},game.ScheduleDelay,()=>{});
            withdrawalStage.Init();withdrawalStage.Refresh();
            withdrawalStage.SetLocalReview(WithdrawalText("progressTitle"),WithdrawalText("progressDescription"),WithdrawalText("notSubmitted"),
                WithdrawalText("back"),WithdrawalText("firstStageComplete"),WithdrawalText("nextStagePending"),WithdrawalText("nextReview"),
                string.Format(System.Globalization.CultureInfo.InvariantCulture,WithdrawalText("stageTarget"),OriginalRewardProgressData.Read(game.User.GoldRewardTargetS2CData).Stage2StartShowLevel));
        }
        private void OpenWithdrawalTooFast()
        {
            if(withdrawalTooFast!=null||withdrawalStage==null)return;
            withdrawalTooFast=Instantiate(Resources.Load<GameObject>((string)WithdrawalConfiguration["tooFastPrefab"]),transform.parent,false).GetComponent<OriginalWithdrawalTooFastPanel>();
            withdrawalTooFast.Bind(game.User,game.Tables,language,
                ()=>withdrawalTooFast!=null&&!withdrawalTooFast.Closing&&TryAccountClick(),sound=>audio.PlaySound(sound),
                ()=>game.Tables.GetShowLevel(game.User.Level),()=>()=>{if(withdrawalStage!=null)withdrawalStage.PlayProgress();},
                ()=>{Destroy(withdrawalTooFast.gameObject);withdrawalTooFast=null;},()=>{},game.ScheduleDelay,()=>{});
            withdrawalTooFast.Init();withdrawalTooFast.Refresh();
            withdrawalTooFast.SetLocalReview(WithdrawalText("reviewTitle"),WithdrawalText("reviewDescription"),
                WithdrawalText("videoCaption"),(int?)game.User.LocalWithdrawalData?["rewardVideos"]??0,(int)WithdrawalConfiguration["videoReferenceCount"]);
        }
        private void ReturnToGameFromWithdrawal()
        {
            if(withdrawalPanel!=null){Destroy(withdrawalPanel.gameObject);withdrawalPanel=null;}
            if(diamondWithdrawalPanel!=null){Destroy(diamondWithdrawalPanel.gameObject);diamondWithdrawalPanel=null;}
            StartCoroutine(ReleaseWithdrawalInput());
        }
        private void RecordWithdrawalRewardVideo()
        {
            if(game.User.LocalWithdrawalData==null)game.User.LocalWithdrawalData=new JObject();
            game.User.LocalWithdrawalData["rewardVideos"]=checked(((int?)game.User.LocalWithdrawalData["rewardVideos"]??0)+1);
            audio.UserState.Store.SaveData(false,null);
        }
    }
}
