using System;
using NutSort.Gameplay;
using UnityEngine;
namespace NutSort.UI
{
 public sealed partial class LocalGameplayController
 {
  [SerializeField] private int normalClaimAdThreshold,interstitialClaimLimit;
  [SerializeField] private bool simulateSettlementAds;
  [SerializeField] private string interstitialCompleteText;
  private Action<Action<bool>> interstitialProvider,settlementRewardedProvider;
  private bool settlementAdPending;
  private int settlementAdRequest;
  public bool SettlementAdPending=>settlementAdPending;
  // Providers complete on the Unity main thread after close, failure, or no fill.
  public void BindSettlementAds(Action<Action<bool>> interstitial,Action<Action<bool>> rewarded)
  {interstitialProvider=interstitial;settlementRewardedProvider=rewarded;}
  private void RequestSettlementClaim()
  {
   if(settlementAdPending||game.User.LocalPendingReward==null)return;
   var flow=new OriginalRewardGetFlow(game.User,()=>normalClaimAdThreshold,()=>interstitialClaimLimit,
    done=>RequestSettlementAd(false,done),(done,option)=>RequestSettlementAd(true,done),
    ()=>0,value=>{},more=>ClaimLocalReward());
   flow.GetCallback();PersistPendingBoard();
  }
  private void RequestSettlementAd(bool rewarded,Action<bool> completed)
  {
   settlementAdPending=true;int request=++settlementAdRequest;var panel=successPanel;
   Action<bool> finish=result=>
   {
    if(this==null||!settlementAdPending||request!=settlementAdRequest)return;
    settlementAdPending=false;
    if(successPanel!=panel||panel==null)return;
    if(result&&!rewarded)ShowWithdrawalFeedback(interstitialCompleteText);
    completed(result);
   };
   var provider=rewarded?settlementRewardedProvider:interstitialProvider;
   if(provider!=null)provider(finish);
   else if(simulateSettlementAds)StartCoroutine(SimulateToolAd(finish));
   else finish(false);
  }
 }
}
