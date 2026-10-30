using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        private JObject earningConfiguration;
        private LocalEarningSequencePanel earningSequencePanel;
        private LocalCashHistoryPanel cashHistoryPanel;
        private OriginalGuideTargetPanel earlyRewardTarget;
        private int earlyStage;
        public LocalEarningSequencePanel EarningSequencePanel=>earningSequencePanel;
        public LocalCashHistoryPanel CashHistoryPanel=>cashHistoryPanel;
        public OriginalGuideTargetPanel TutorialBalancePanel=>earlyRewardTarget;
        public int EarlyWithdrawalStage=>earlyStage;
        private bool HasEarningModal=>earningSequencePanel!=null||cashHistoryPanel!=null||earlyRewardTarget!=null||HasEarlyAccountModal;
        private JObject EarningConfiguration
        {
            get
            {
                if(earningConfiguration==null)
                    earningConfiguration=JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningFlow").text);
                return earningConfiguration;
            }
        }
        private string EarningText(string key)=>LocalUiLocalization.Text((string)EarningConfiguration[key]);
        private JObject EarningState
        {
            get
            {
                if(game.User.LocalWithdrawalData==null)game.User.LocalWithdrawalData=new JObject();
                var state=game.User.LocalWithdrawalData["earningFlow"] as JObject;
                if(state==null){state=new JObject();game.User.LocalWithdrawalData["earningFlow"]=state;}
                return state;
            }
        }
        private void SaveEarningState()=>audio.UserState.Store.SaveData(false,null);
        private string FormatEarningAmount(float amount)=>new OriginalGoldFormatter(()=>audio.UserState.DisplayCulture(game.Tables)).Format(amount);
        private void CreateEarningSequence()
        {
            if(earningSequencePanel!=null)throw new InvalidOperationException("An earning presentation is already open.");
            earningSequencePanel=Instantiate(Resources.Load<GameObject>(EarningText("sequencePrefab")),transform.parent,false).GetComponent<LocalEarningSequencePanel>();
            game.ModalInputBlocked=true;
        }
        private void CloseEarningSequence()
        {
            if(earningSequencePanel!=null){Destroy(earningSequencePanel.gameObject);earningSequencePanel=null;}
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
        }
        private void ShowEarningReceipt(float amount,Action collected,Action completed)
        {
            CreateEarningSequence();
            earningSequencePanel.ShowReceipt(FormatEarningAmount(amount),rewardsTop.Gold.transform,
                ()=>{collected?.Invoke();RefreshRewardsTop();},
                ()=>{CloseEarningSequence();completed?.Invoke();},
                (float)EarningConfiguration["receiptCollectSeconds"],(float)EarningConfiguration["receiptTotalSeconds"]);
        }
        private void ShowEarningMessage(string title,string body,float seconds,Action completed)
            =>ShowEarningMessage(LocalEarningMessageStyle.Summary,title,body,seconds,completed);
        private void ShowEarningMessage(LocalEarningMessageStyle style,string title,string body,float seconds,Action completed)
        {
            CreateEarningSequence();
            earningSequencePanel.ShowMessage(style,title,body);
            StartCoroutine(FinishEarningPresentation(earningSequencePanel,seconds,completed));
        }
        private IEnumerator FinishEarningPresentation(LocalEarningSequencePanel panel,float seconds,Action completed)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if(panel==null||earningSequencePanel!=panel)yield break;
            CloseEarningSequence();completed?.Invoke();
        }
        private bool TryOpenEarningFlow()
        {
            if(HasEarningModal)return true;
            if(game.User.LocalPendingReward!=null)return false;
            int stage=LocalEarningFlowPolicy.NeedsEarlyGuide(game.User);
            if(stage>0&&((int?)EarningState["deferredEarlyStage"]??0)!=stage){OpenEarlyRewardTarget(stage);return true;}
            if(LocalEarningFlowPolicy.EligibleForActivation(game.User))
            {PlayActivationStep();return true;}
            string country=audio.UserState.DisplayCountry(game.Tables);
            if(LocalWithdrawalPolicy.TryGetTutorialBalanceNotice(game.User,country,WithdrawalConfiguration,out float amount))
            {
                ShowEarningReceipt(amount,()=>{},()=>
                {
                    LocalWithdrawalPolicy.AcknowledgeTutorialBalanceNotice(game.User);
                    SaveEarningState();BeginCoreInitialization();
                });
                return true;
            }
            float receipt=(float?)EarningState["settlementReceipt"]??0;
            if(receipt>0)
            {
                ShowEarningReceipt(receipt,()=>{},()=>
                {EarningState.Remove("settlementReceipt");SaveEarningState();BeginCoreInitialization();});
                return true;
            }
            return false;
        }
        private void PlayActivationStep()
        {
            int step=LocalEarningFlowPolicy.GetActivationStep(game.User);
            float seconds=(float)EarningConfiguration["messageSeconds"];
            Action next=()=>
            {
                LocalEarningFlowPolicy.SetActivationStep(game.User,step+1);SaveEarningState();PlayActivationStep();
            };
            switch(step)
            {
                case 0: ShowEarningMessage(LocalEarningMessageStyle.Assessment,EarningText("assessmentTitle"),EarningText("assessmentBody"),seconds,next);break;
                case 1: ShowEarningMessage(LocalEarningMessageStyle.Activating,EarningText("activatingTitle"),EarningText("activatingBody"),seconds,next);break;
                case 2:
                    CreateEarningSequence();
                    var values=(JArray)EarningConfiguration["bonuses"];
                    var captions=new string[values.Count];for(int i=0;i<captions.Length;i++)captions[i]=LocalUiLocalization.Text((string)values[i]);
                    earningSequencePanel.ShowBonuses(EarningText("bonusesTitle"),captions);
                    StartCoroutine(FinishEarningPresentation(earningSequencePanel,(float)EarningConfiguration["bonusSeconds"],next));break;
                case 3:
                    string country=audio.UserState.DisplayCountry(game.Tables);
                    float amount=(float?)game.User.LocalWithdrawalData?["tutorialBalanceAmount"]??
                        (game.User.Level==3?((float?)WithdrawalConfiguration["tutorialBalances"]?[country]??0):0);
                    if(amount<=0){next();return;}
                    ShowEarningReceipt(amount,()=>
                    {
                        using(var analytics=LocalAnalytics.Operation("tutorial_balance_adjustment"))
                        {
                        LocalWithdrawalPolicy.ApplyTutorialBalance(game.User,country,WithdrawalConfiguration);
                        SaveEarningState();
                        }
                    },()=>{LocalWithdrawalPolicy.AcknowledgeTutorialBalanceNotice(game.User);next();});break;
                case 4: ShowEarningMessage(LocalEarningMessageStyle.Activated,EarningText("activatedTitle"),EarningText("activatedBody"),seconds,next);break;
                case 5: ShowEarningMessage(LocalEarningMessageStyle.Summary,EarningText("summaryTitle"),EarningText("summaryBody"),seconds,next);break;
                default:
                    LocalEarningFlowPolicy.CompleteActivation(game.User);SaveEarningState();RefreshRewardsTop();BeginCoreInitialization();break;
            }
        }
        private float EarlyAccountAmount(int stage)
        {
            foreach(var record in LocalEarningFlowPolicy.GetRecords(game.User))if(record.Stage==stage)return record.Amount;
            throw new InvalidOperationException("No earned reward exists for this withdrawal.");
        }
        private void OpenEarlyRewardTarget(int stage)
        {
            earlyStage=stage;game.ModalInputBlocked=true;
            earlyRewardTarget=Instantiate(Resources.Load<GameObject>(EarningText("targetPrefab")),transform.parent,false).GetComponent<OriginalGuideTargetPanel>();
            earlyRewardTarget.InitializeNotice(game.Tables,language,()=>audio.UserState.DisplayCountry(game.Tables),
                FormatEarningAmount(EarlyAccountAmount(stage)),EarningText("targetTitle"),EarningText("withdraw"),
                ()=>!game.InputBlocked&&!game.IsRestarting&&!startup.Replay.IsClickMasked&&startup.Replay.BeginClick(),
                sound=>audio.PlaySound(sound),()=>{},()=>
                {
                    Destroy(earlyRewardTarget.gameObject);earlyRewardTarget=null;OpenEarlyRewardDetails();
                });
        }
        private void OpenEarlyRewardDetails()
        {
            LocalAnalytics.Current?.BeginWithdrawal(false,earlyStage);
            var eligibility=new LocalWithdrawalEligibility();
            eligibility.Requirements.Add(new LocalWithdrawalRequirement{Key="early_reward_available",Current=game.User.Gold,Target=EarlyAccountAmount(earlyStage)});
            LocalAnalytics.Current?.ObserveEligibility(eligibility,"early_reward_panel");
            string country=audio.UserState.DisplayCountry(game.Tables);
            withdrawalPanel=Instantiate(Resources.Load<GameObject>(withdrawalPrefabPath),transform.parent,false).GetComponent<OriginalWithdrawalPanel>();
            withdrawalPanel.InitLocal(game.Tables,language,country,null,FormatEarningAmount(EarlyAccountAmount(earlyStage)),
                game.Tables.Text.GetText(30,language,earlyStage),EarningText("earlyHint"),"1/1",string.Empty,1,
                TryWithdrawalClick,()=>OpenEarlyWithdrawalAccount(earlyStage),()=>FinishEarlyWithdrawal(false),sound=>audio.PlaySound(sound),
                player=>InitializeLocalStats(player,earlyStage));
            withdrawalPanel.SetLocalHistoryAction(OpenCashHistory,EarningText("historyEntry"));
        }
        private void FinishEarlyWithdrawal(bool submit)
        {
            if(earlyStage<1)return;
            bool archived=false;
            using(var analytics=LocalAnalytics.Operation("early_reward_record_transfer"))
            {
            LocalEarningFlowPolicy.ArchiveEarlyReward(game.User,earlyStage);
            foreach(var record in LocalEarningFlowPolicy.GetRecords(game.User))if(record.Stage==earlyStage)archived=record.Archived;
            if(!archived)EarningState["deferredEarlyStage"]=earlyStage;
            else if(((int?)EarningState["deferredEarlyStage"]??0)==earlyStage)EarningState.Remove("deferredEarlyStage");
            if(submit&&archived)LocalEarningFlowPolicy.SubmitEarlyRecord(game.User,earlyStage,DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            SaveEarningState();earlyStage=0;
            }
            DisposeEarlyAccount();
            if(withdrawalAccount!=null){Destroy(withdrawalAccount.gameObject);withdrawalAccount=null;}
            if(withdrawalConfirmation!=null){Destroy(withdrawalConfirmation.gameObject);withdrawalConfirmation=null;}
            if(withdrawalPanel!=null){Destroy(withdrawalPanel.gameObject);withdrawalPanel=null;}
            RefreshRewardsTop();
            if(!archived)ShowWithdrawalFeedback(EarningText("archiveUnavailable"));
            StartCoroutine(ResumeEarningFlow());
        }
        private IEnumerator ResumeEarningFlow()
        {
            yield return null;
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
            BeginCoreInitialization();
        }
        private void OpenCashHistory()
        {
            if(cashHistoryPanel!=null)return;
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(false);
            game.ModalInputBlocked=true;
            var rows=new List<LocalCashHistoryPanel.RowData>(3);
            foreach(var record in LocalEarningFlowPolicy.GetRecords(game.User))
            {
                long timestamp=record.Pending?record.SubmittedUtcSeconds:record.CreatedUtcSeconds;
                rows.Add(new LocalCashHistoryPanel.RowData{Stage=record.Stage,
                    Title=string.Format(CultureInfo.InvariantCulture,EarningText("rewardTitle"),record.Stage),
                    Amount=FormatEarningAmount(record.Amount),ActionCaption=record.Pending?EarningText("pending"):EarningText("withdraw"),
                    Time=timestamp>0?DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("g",CultureInfo.GetCultureInfo(audio.UserState.DisplayCulture(game.Tables))):string.Empty,
                    Interactable=!record.Pending});
            }
            rows.Add(new LocalCashHistoryPanel.RowData{Stage=3,Title=string.Format(CultureInfo.InvariantCulture,EarningText("rewardTitle"),3),
                Amount=FormatEarningAmount(game.User.Gold),ActionCaption=EarningText("withdraw"),Time=string.Empty,Interactable=true});
            cashHistoryPanel=Instantiate(Resources.Load<GameObject>(EarningText("historyPrefab")),transform.parent,false).GetComponent<LocalCashHistoryPanel>();
            cashHistoryPanel.Init(EarningText("historyTitle"),FormatEarningAmount(game.User.Gold),rows.ToArray(),stage=>
            {
                if(!TryAccountClick())return;
                CloseCashHistory();
                if(stage<=2){earlyStage=stage;OpenEarlyWithdrawalAccount(stage);}
                else if(earlyStage>0)FinishEarlyWithdrawal(false);
                else BeginCashWithdrawal();
            },CloseCashHistory);
        }
        private void CloseCashHistory()
        {
            if(cashHistoryPanel!=null){Destroy(cashHistoryPanel.gameObject);cashHistoryPanel=null;}
            if(withdrawalPanel!=null)withdrawalPanel.gameObject.SetActive(true);
            StartCoroutine(ReleaseWithdrawalInput());
        }
        private void DisposeEarningFlow()
        {
            DisposeEarlyAccount();DisposeEarningRewards();
            if(earningSequencePanel!=null)Destroy(earningSequencePanel.gameObject);
            if(cashHistoryPanel!=null)Destroy(cashHistoryPanel.gameObject);
            if(earlyRewardTarget!=null)Destroy(earlyRewardTarget.gameObject);
        }
    }
}
