using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using UnityEngine;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        private JObject earningRewardConfiguration;
        private OriginalServerConfigDefaults earningServerDefaults;
        private OriginalSuccessPanel luckyPanel;
        private OriginalNewbieGuideView coinUnlockGuide;
        private OriginalScrewDoneFlow earningScrewFlow;
        private bool luckyAdPending,luckyReceiptPending,coinGuidePending;
        private bool earningRecoveryChecked;
        private int luckyAdRequest;
        private Action<object> coinGuideCallback;
        public OriginalSuccessPanel LuckyPanel=>luckyPanel;
        public OriginalNewbieGuideView CoinUnlockGuide=>coinUnlockGuide;
        public bool LuckyAdPending=>luckyAdPending;
        public bool IsEarningRewardModal=>luckyPanel!=null||luckyAdPending||luckyReceiptPending||coinGuidePending||coinUnlockGuide!=null;
        public bool ShouldAllowDiamondRewards
        {
            get
            {
                // Pending settlement already advances User.Level to the next
                // board, while its reward still belongs to the completed board.
                int level=game.User.LocalPendingReward==null?game.User.Level:
                    ((int?)game.User.LocalPendingReward["nextLevel"]??game.User.Level)-1;
                return game.User.IsShowCoin||game.Tables.GetShowLevel(Math.Max(1,level))>=4;
            }
        }
        public float SettlementBonusMultiplier=>LocalLuckyRewardPolicy.Multiplier(audio.UserState.Rewards,false);
        private JObject EarningRewardsConfiguration
        {
            get
            {
                if(earningRewardConfiguration==null)
                {
                    var asset=Resources.Load<TextAsset>("Configuration/LocalEarningRewards");
                    if(asset==null)throw new InvalidOperationException("Missing local earning reward configuration.");
                    earningRewardConfiguration=JObject.Parse(asset.text);
                }
                return earningRewardConfiguration;
            }
        }
        private string LuckyText(string key)=>LocalUiLocalization.Text((string)EarningRewardsConfiguration[key]);
        private static long EarningTime=>DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        private void SaveEarningRewards()
        {
            if(awaitingNext||game.User.LocalPendingReward!=null)PersistPendingBoard();
            else game.SaveUserData();
        }
        public void PrepareEarningBoard()=>EnsureEarningScrewFlow();

        // Run once only after the world and success callback are initialized.
        // A pending settlement refers to the completed board, not User.Level.
        private bool ResumeInterruptedScrewCompletions()
        {
            if(earningRecoveryChecked)return false;
            earningRecoveryChecked=true;
            int completedLevel=game.User.LocalPendingReward==null?game.User.Level:
                ((int?)game.User.LocalPendingReward["nextLevel"]??game.User.Level)-1;
            using(var analytics=LocalAnalytics.Operation("screw_reward_recovery"))
            {
            int restored=LocalLuckyRewardPolicy.RecoverScrewCompletions(game.User,
                game.Tables.GetShowLevel(Math.Max(1,completedLevel)),
                LocalLuckyRewardPolicy.Number(game.User.GoldRewardTargetS2CData?["bear_torque"]),
                LocalLuckyRewardPolicy.Number(game.User.GoldRewardTargetS2CData?["zs_ea"]));
            if(restored>0){SaveEarningRewards();RefreshRewardsTop();}
            }
            // Recovery may have saved the rod reward, then exited before the
            // delayed victory. Resume victory even when the reward gap is zero.
            if(game.User.LocalPendingReward==null&&game.IsSuccess())
            {
                game.ModalInputBlocked=true;
                game.Success();
                return true;
            }
            return false;
        }
        private void EnsureEarningScrewFlow(bool handlingCompletion=false)
        {
            if(earningScrewFlow!=null)return;
            earningServerDefaults=Resources.Load<OriginalServerConfigDefaults>("Configuration/OriginalServerConfigDefaults");
            if(earningServerDefaults==null)throw new InvalidOperationException("Missing original server defaults.");
            JObject state=LocalLuckyRewardPolicy.State(game.User);
            if(state["lastLuckyTime"]==null)state["lastLuckyTime"]=EarningTime;
            if(!handlingCompletion)LocalLuckyRewardPolicy.BeginScrewCompletionTracking(game.User);
            earningScrewFlow=new OriginalScrewDoneFlow(()=>game.User,()=>game.ShowLevel,game.Success,new EarningScrewEffects(this));
        }

        // OriginalMoveCompletionFlow increments ScrewDoneCount before this
        // delayed callback. Save its receipt with inventory to prevent duplicates.
        public void OnEarningScrewCompleted(bool boardComplete)
        {
            EnsureEarningScrewFlow(true);
            int level=game.User.Level,seed=game.User.LevelSeed;
            using(var analytics=LocalAnalytics.Operation("screw_completion_reward"))
            {
            if(LocalLuckyRewardPolicy.TryConsumeScrewCompletion(game.User))
                earningScrewFlow.Run(null,boardComplete);
            else if(boardComplete&&!game.IsSucceed)
                // Skip/landing completion does not increment ScrewDoneCount.
                // Board success must never depend on a new rod reward receipt.
                game.Success();
            if(awaitingNext||game.User.Level!=level||game.User.LevelSeed!=seed)PersistPendingBoard();
            else game.SaveUserData();
            }
        }

        private sealed class EarningScrewEffects:IOriginalScrewDoneEffects
        {
            private readonly LocalGameplayController owner;
            public EarningScrewEffects(LocalGameplayController owner){this.owner=owner;}
            public float RemoveGold=>LocalLuckyRewardPolicy.Number(owner.game.User.GoldRewardTargetS2CData?["bear_torque"]);
            public float RemoveCoin=>owner.ShouldAllowDiamondRewards?LocalLuckyRewardPolicy.Number(owner.game.User.GoldRewardTargetS2CData?["zs_ea"]):0;
            public void FlyGold(ScrewState target){} // Original HUD gain pulse is invoked below.
            public void FlyCoin(ScrewState target){}
            public void AddGold(float amount,bool refreshUI,bool synchronize)
            {
                if(amount<=0)return;owner.game.User.Gold+=amount;
                if(refreshUI&&owner.rewardsTop!=null)owner.rewardsTop.Gold.RefreshGold(true,amount);
            }
            public void AddCoin(float amount,bool refreshUI)
            {
                if(amount<=0)return;owner.game.User.Coin+=amount;
                if(refreshUI&&owner.rewardsTop!=null)owner.rewardsTop.Coin.RefreshCoin(true,amount);
            }
            public bool HasPanel=>owner.HasLocalModal||owner.game.User.LocalPendingReward!=null||owner.game.IsSucceed||owner.game.IsSuccess();
            public int Config(string field)
            {
                // Only the requested Lucky reward branch is enabled; no wheel
                // or rating feature is introduced by this earning-flow repair.
                if(field=="LSSLR1")return (int?)owner.game.User.ServerConfigData?[field]??owner.earningServerDefaults.LSSLR1;
                if(field=="LSSLR2")return (int?)owner.game.User.ServerConfigData?[field]??owner.earningServerDefaults.LSSLR2;
                return int.MaxValue;
            }
            public long TimeSeconds=>EarningTime;
            public long LuckyRewardTime
            {
                get=>(long?)LocalLuckyRewardPolicy.State(owner.game.User)["lastLuckyTime"]??EarningTime;
                set=>LocalLuckyRewardPolicy.State(owner.game.User)["lastLuckyTime"]=value;
            }
            public long LuckyDrawTime{get=>EarningTime;set{}}
            public void RequestLuckyReward()
            {
                LocalLuckyRewardPolicy.Prepare(owner.game.User,owner.audio.UserState.Rewards,owner.EarningRewardsConfiguration,UnityEngine.Random.Range);
                owner.SaveEarningRewards();owner.TryOpenPendingLuckyReward();
            }
            public void ShowPanel(int id){}
        }

        public bool TryOpenPendingLuckyReward()
        {
            EnsureEarningScrewFlow();
            if(luckyPanel!=null||luckyReceiptPending)return true;
            var pending=LocalLuckyRewardPolicy.Pending(game.User);
            if(pending==null||game.User.LocalPendingReward!=null)return false;
            bool newClaimId=pending["analytics_claim_id"]==null;
            LocalAnalytics.ClaimId(pending);
            if(newClaimId)SaveEarningRewards();
            game.ModalInputBlocked=true;
            if((bool?)pending["claimed"]??false){ShowLuckyReceipt();return true;}
            var prefab=Resources.Load<GameObject>((string)EarningRewardsConfiguration["luckyPrefab"]);
            if(prefab==null||prefab.GetComponent<OriginalSuccessPanel>()==null)throw new InvalidOperationException("Missing Lucky reward prefab.");
            luckyPanel=Instantiate(prefab,transform.parent,false).GetComponent<OriginalSuccessPanel>();
            string country=audio.UserState.DisplayCountry(game.Tables),culture=audio.UserState.DisplayCulture(game.Tables);
            var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>((string)EarningRewardsConfiguration["luckyItemSettings"]),
                new OriginalGoldFormatter(()=>culture),()=>country);
            var info=new OriginalItemGetInfo{ItemInfos=new List<OriginalItemInfo>{new OriginalItemInfo{ItemType=0,Count=(float)pending["amount"]}}};
            string moreCaption=string.Format(CultureInfo.InvariantCulture,LuckyText("moreCaption"),(float)pending["multiplier"]);
            luckyPanel.InitSettlement(game.Tables,language,TryLuckyClick,s=>audio.PlaySound(s),CloseLuckyPanel,factory,
                info,RequestLuckyClaim,true,true,moreCaption);
            luckyPanel.SetPresentationTitle(LuckyText("luckyTitle"));
            luckyPanel.CloseButton.gameObject.SetActive(false);
            audio.PlaySound(gameplaySession.SuccessPanelSound);
            return true;
        }
        private bool TryLuckyClick()
        {
            if(luckyPanel==null||luckyPanel.Closing||luckyAdPending||game.InputBlocked||game.IsRestarting||
                !game.IsInitDone||startup.Replay.IsClickMasked)return false;
            return startup.Replay.BeginClick();
        }
        private void RequestLuckyClaim(bool more)
        {
            var pending=LocalLuckyRewardPolicy.Pending(game.User);
            if(pending==null||((bool?)pending["claimed"]??false)||luckyAdPending)return;
            luckyOperation=LocalAnalytics.NewId();
            var flow=new OriginalRewardGetFlow(game.User,()=>normalClaimAdThreshold,()=>interstitialClaimLimit,
                done=>RequestLuckyAd(false,done),(done,option)=>RequestLuckyAd(true,done),()=>EarningTime,
                time=>LocalLuckyRewardPolicy.State(game.User)["lastLuckyTime"]=time,
                bonus=>CommitLuckyClaim(more&&bonus));
            if(more)flow.MoreGetCallback();else flow.GetCallback();
        }
        private void RequestLuckyAd(bool rewarded,Action<bool> completed)
        {
            luckyAdPending=true;int request=++luckyAdRequest;var panel=luckyPanel;
            bool finished=false;
            Action<bool> finish=result=>
            {
                if(finished||this==null||request!=luckyAdRequest||!luckyAdPending||luckyPanel!=panel||panel==null)return;
                finished=true;luckyAdPending=false;
                if(result&&rewarded)RecordWithdrawalRewardVideo();
                completed(result);
                if(!result&&LocalLuckyRewardPolicy.Pending(game.User)!=null&&
                    !((bool?)LocalLuckyRewardPolicy.Pending(game.User)["claimed"]??false))
                {
                    TrackClaim("lucky_reward",LocalAnalytics.ClaimId(LocalLuckyRewardPolicy.Pending(game.User)),luckyOperation,"not_granted","ad_not_completed");
                    luckyOperation=null;SaveEarningRewards();ShowWithdrawalFeedback(toolAdFailureText);
                }
            };
            var provider=rewarded?settlementRewardedProvider:interstitialProvider;
            try{if(provider!=null)provider(finish);else finish(false);}
            catch(Exception exception){Debug.LogException(exception);finish(false);}
        }
        private void CommitLuckyClaim(bool more)
        {
            var pending=LocalLuckyRewardPolicy.Pending(game.User);
            string claim=LocalAnalytics.ClaimId(pending),operation=luckyOperation??LocalAnalytics.NewId();
            using(var analytics=LocalAnalytics.Operation("lucky_reward",operation))
            {
            if(!LocalLuckyRewardPolicy.Claim(game.User,more,out _))return;
            SaveEarningRewards();luckyPanel.ClosePanel();
            TrackClaim("lucky_reward",claim,operation,"granted","local_committed");luckyOperation=null;
            }
        }
        private void CloseLuckyPanel()
        {
            if(luckyPanel==null)return;
            Destroy(luckyPanel.gameObject);luckyPanel=null;
            StartCoroutine(OpenLuckyReceiptAfterClose());
        }
        private IEnumerator OpenLuckyReceiptAfterClose()
        {
            luckyReceiptPending=true;yield return null;luckyReceiptPending=false;ShowLuckyReceipt();
        }
        private void ShowLuckyReceipt()
        {
            var pending=LocalLuckyRewardPolicy.Pending(game.User);
            if(pending==null)return;
            luckyReceiptPending=true;
            ShowEarningReceipt((float)pending["award"],RefreshRewardsTop,()=>
            {
                LocalLuckyRewardPolicy.CompleteReceipt(game.User);SaveEarningRewards();luckyReceiptPending=false;
                StartCoroutine(ReleaseEarningRewardInput());
            });
        }

        public bool TryOpenCoinUnlockGuide()
        {
            if(coinGuidePending||coinUnlockGuide!=null)return true;
            if(game.ShowLevel<4||game.User.IsCompleteShowCoinRewardHint||game.User.LocalPendingReward!=null)return false;
            coinGuidePending=true;game.ModalInputBlocked=true;
            string amount=LocalLuckyRewardPolicy.Number(game.User.GoldRewardTargetS2CData?["zs_newer"]).ToString("0",CultureInfo.InvariantCulture);
            ShowEarningMessage(LuckyText("coinPreviewTitle"),
                string.Format(CultureInfo.InvariantCulture,LuckyText("coinPreviewBody"),amount),
                (float)EarningRewardsConfiguration["coinPreviewSeconds"],OpenCoinUnlockGuide);
            return true;
        }
        private void OpenCoinUnlockGuide()
        {
            if(this==null)return;
            coinGuidePending=false;game.User.GuideIndex=3;
            coinUnlockGuide=Instantiate(Resources.Load<GameObject>(teachingPrefabPath),transform.parent,false).GetComponent<OriginalNewbieGuideView>();
            coinUnlockGuide.BindTeaching(game.Tables,language,value=>game.IsCanOperatorScrew=value,CloseCoinUnlockGuide);
            coinUnlockGuide.InitializeLabels(game.Tables,language);
            coinUnlockGuide.InitializeInteractions(game.User,false,TryCoinGuideClick,()=>audio.PlaySound(gameplaySession.SuccessPanelSound));
            coinUnlockGuide.BindMask(game.ScheduleDelay);
            coinUnlockGuide.SetTeachingVisible(false);game.IsCanOperatorScrew=false;
            coinUnlockGuide.ShowCoinGuide(game.User,(RectTransform)rewardsTop.Coin.transform,RefreshRewardsTop,SaveEarningRewards,
                showMask=>{game.User.IsCompleteShowCoinRewardHint=true;SaveEarningRewards();},showBanner=>{});
            coinUnlockGuide.Tip.text=LuckyText("coinGuideBody");
            coinGuideCallback=OriginalNewbieGuideView.CallbackAction;
        }
        private bool TryCoinGuideClick()
        {
            if(coinUnlockGuide==null||game.InputBlocked||game.IsRestarting||startup.Replay.IsClickMasked)return false;
            return startup.Replay.BeginClick();
        }
        private void CloseCoinUnlockGuide()
        {
            if(coinUnlockGuide==null)return;
            Destroy(coinUnlockGuide.gameObject);coinUnlockGuide=null;SaveEarningRewards();
            if(ReferenceEquals(OriginalNewbieGuideView.CallbackAction,coinGuideCallback))OriginalNewbieGuideView.CallbackAction=null;
            coinGuideCallback=null;StartCoroutine(ReleaseEarningRewardInput());
        }
        private IEnumerator ReleaseEarningRewardInput()
        {
            yield return null;game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;BeginCoreInitialization();
        }
        public void DisposeEarningRewards()
        {
            luckyAdRequest++;luckyAdPending=false;
            if(luckyPanel!=null){Destroy(luckyPanel.gameObject);luckyPanel=null;}
            if(coinUnlockGuide!=null){Destroy(coinUnlockGuide.gameObject);coinUnlockGuide=null;}
            if(ReferenceEquals(OriginalNewbieGuideView.CallbackAction,coinGuideCallback))OriginalNewbieGuideView.CallbackAction=null;
            coinGuideCallback=null;luckyReceiptPending=coinGuidePending=false;
        }
    }
}
