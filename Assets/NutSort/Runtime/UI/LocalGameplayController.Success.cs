using UnityEngine;
using System;
using System.Globalization;
using System.Collections.Generic;
using NutSort.Content;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string successPrefabPath,localNextCaption;
        private OriginalSuccessPanel successPanel;
        public OriginalSuccessPanel SuccessPanel=>successPanel;
        private void ShowLocalSuccess()
        {
            successPanel=Instantiate(Resources.Load<GameObject>(successPrefabPath),transform.parent,false).GetComponent<OriginalSuccessPanel>();
            LocalCashSettlement.AddConfiguredDiamonds(game.User);
            if(!ShouldAllowDiamondRewards)LocalCashSettlement.SetDiamonds(game.User,0);
            PersistPendingBoard();
            var pending=game.User.LocalPendingReward;
            bool newClaimId=pending["analytics_claim_id"]==null;
            LocalAnalytics.ClaimId(pending);
            if(newClaimId)PersistPendingBoard();
            int earlyReward=((int?)pending["nextLevel"]??game.User.Level)-1;
            bool early=earlyReward==1||earlyReward==2;
            string country=audio.UserState.DisplayCountry(game.Tables);
            string culture=audio.UserState.DisplayCulture(game.Tables);
            var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"),
                new OriginalGoldFormatter(()=>culture),()=>country);
            var info=new OriginalItemGetInfo{ItemInfos=new List<OriginalItemInfo>{new OriginalItemInfo{ItemType=0,Count=(float)pending["amount"]}}};
            if(pending["diamonds"]!=null&&(double)pending["diamonds"]>0)
                info.ItemInfos.Add(new OriginalItemInfo{ItemType=1,Count=(float)pending["diamonds"]});
            successPanel.InitSettlement(game.Tables,language,TrySuccessClick,s=>audio.PlaySound(s),Next,factory,
                info,more=>{if(early)ClaimLocalReward(false);else RequestSettlementClaim(more);},true,!early,
                early?EarningText("withdraw"):string.Format(CultureInfo.InvariantCulture,EarningText("bonusClaimCaption"),SettlementBonusMultiplier));
            successPanel.GetButton.gameObject.SetActive(!early);
            successPanel.CloseButton.gameObject.SetActive(false);
            audio.PlaySound(gameplaySession.SuccessPanelSound);
        }
        private void ClaimLocalReward(bool more)
        {
            var pending=game.User.LocalPendingReward;
            if(pending==null)return;
            string claim=LocalAnalytics.ClaimId(pending);
            string operation=string.IsNullOrEmpty(settlementOperation)?LocalAnalytics.NewId():settlementOperation;
            settlementOperation=null;
            using(var analytics=LocalAnalytics.Operation("level_reward",operation))
            {
            int stage=((int?)pending["nextLevel"]??game.User.Level)-1;
            bool early=stage==1||stage==2;
            if(more&&!early)
            {
                pending["amount"]=(float)pending["amount"]*SettlementBonusMultiplier;
                pending["diamonds"]=((double?)pending["diamonds"]??0)*SettlementBonusMultiplier;
            }
            float amount=(float)pending["amount"];
            if(!LocalCashSettlement.Claim(game.User))return;
            if(early)LocalEarningFlowPolicy.EnsureEarlyReward(game.User,stage,amount,DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            else EarningState["settlementReceipt"]=amount;
            PersistPendingBoard();
            TrackClaim("level_reward",claim,operation,"granted","local_committed");
            }
            RefreshWithdrawalCalendar();
            PersistPendingBoard();
            RefreshRewardsTop();
            successPanel.ClosePanel();
        }
        private bool TrySuccessClick()
        {
            if(settlementAdPending||!awaitingNext||successPanel==null||successPanel.Closing||game.InputBlocked||game.IsRestarting||startup.Replay.IsClickMasked)return false;
            return startup.Replay.BeginClick();
        }
    }
}
