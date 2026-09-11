using UnityEngine;
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
            PersistPendingBoard();
            var pending=game.User.LocalPendingReward;
            string country=audio.UserState.DisplayCountry(game.Tables);
            string culture=audio.UserState.DisplayCulture(game.Tables);
            var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"),
                new OriginalGoldFormatter(()=>culture),()=>country);
            var info=new OriginalItemGetInfo{ItemInfos=new List<OriginalItemInfo>{new OriginalItemInfo{ItemType=0,Count=(float)pending["amount"]}}};
            if(pending["diamonds"]!=null&&(double)pending["diamonds"]>0)
                info.ItemInfos.Add(new OriginalItemInfo{ItemType=1,Count=(float)pending["diamonds"]});
            successPanel.InitSettlement(game.Tables,language,TrySuccessClick,s=>audio.PlaySound(s),Next,factory,
                info,more=>RequestSettlementClaim(),true,false,localNextCaption);
            successPanel.GetButton.gameObject.SetActive(false);
            successPanel.CloseButton.gameObject.SetActive(false);
            audio.PlaySound(gameplaySession.SuccessPanelSound);
        }
        private void ClaimLocalReward()
        {
            if(!LocalCashSettlement.Claim(game.User))return;
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
