using System;
using NutSort.Gameplay;
using NutSort.Content;
using UnityEngine;
namespace NutSort.UI
{
 public sealed partial class LocalGameplayController
 {
  [SerializeField] private int normalClaimAdThreshold,interstitialClaimLimit;
  [SerializeField] private string interstitialCompleteText;
  private Action<Action<bool>> interstitialProvider,settlementRewardedProvider;
  private bool settlementAdPending;
  private int settlementAdRequest;
  public bool SettlementAdPending=>settlementAdPending;
  // Providers complete on the Unity main thread after close, failure, or no fill.
  public void BindSettlementAds(Action<Action<bool>> interstitial,Action<Action<bool>> rewarded)
  {interstitialProvider=interstitial;settlementRewardedProvider=rewarded;}
  private void RequestSettlementClaim(bool more)
  {
   if(settlementAdPending||game.User.LocalPendingReward==null)return;
   settlementOperation=LocalAnalytics.NewId();
   string operation=settlementOperation,claim=LocalAnalytics.ClaimId(game.User.LocalPendingReward);
   Action<Action<bool>,bool> requestAd=(done,rewarded)=>RequestSettlementAd(rewarded,result=>
   {
    done(result);
    if(!result&&game.User.LocalPendingReward!=null)
    {
     PersistPendingBoard();
     if(more){TrackClaim("level_reward",claim,operation,"not_granted","ad_not_completed");settlementOperation=null;}
    }
   });
   var flow=new OriginalRewardGetFlow(game.User,()=>normalClaimAdThreshold,()=>interstitialClaimLimit,
    done=>requestAd(done,false),(done,option)=>requestAd(done,true),
    ()=>EarningTime,value=>LocalLuckyRewardPolicy.State(game.User)["lastLuckyTime"]=value,
    bonus=>ClaimLocalReward(more&&bonus));
   // A normal claim persists with its earned base reward even on ad failure.
   // Never save the provisional ad counters while an ad is pending.
   if(more)flow.MoreGetCallback();else flow.GetCallback();
  }
  private void RequestSettlementAd(bool rewarded,Action<bool> completed)
  {
   settlementAdPending=true;int request=++settlementAdRequest;var panel=successPanel;
   Action<bool> finish=result=>
   {
    if(this==null||!settlementAdPending||request!=settlementAdRequest)return;
    settlementAdPending=false;
    if(successPanel!=panel||panel==null)return;
    if(result&&rewarded)RecordWithdrawalRewardVideo();
    completed(result);
    if(result&&!rewarded)ShowWithdrawalFeedback(interstitialCompleteText);
    if(!result&&game.User.LocalPendingReward!=null)ShowWithdrawalFeedback(toolAdFailureText);
   };
   var provider=rewarded?settlementRewardedProvider:interstitialProvider;
   try
   {
    if(provider!=null)provider(finish);
    else finish(false);
   }
   catch(Exception exception)
   {
    Debug.LogException(exception);
    finish(false);
   }
  }
 }
}
