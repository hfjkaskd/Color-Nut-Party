using NutSort.Content;
using NutSort.Gameplay;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private OriginalMainBottomView bottom;
        [SerializeField] private Button exchangeMask;
        [SerializeField] private float exchangeCancelDelay;
        [SerializeField] private string popTipPrefabPath;
        [SerializeField] private int localToolRefillCount;
        [SerializeField] private bool simulateRewardedToolAds;
        [SerializeField] private float simulatedToolAdDelay;
        [SerializeField] private string toolAdSuccessText,toolAdFailureText;
        private System.Action<int,System.Action<bool>> rewardedToolAdProvider;
        private bool toolAdPending;
        private int toolAdRequest;
        public bool ToolAdPending=>toolAdPending;
        public void BindRewardedToolAds(System.Action<int,System.Action<bool>> provider)=>rewardedToolAdProvider=provider;
        [SerializeField] private string toolOfferPrefabPath;
        private LocalToolOfferPanel toolOffer;
        public LocalToolOfferPanel ToolOffer=>toolOffer;
        private OriginalItemManager tools;
        public OriginalMainBottomView Bottom=>bottom;
        public Button ExchangeMask=>exchangeMask;

        private void BindTools()
        {
            tools=new OriginalItemManager(game.User,game.SaveUserData,bottom.Refresh,(n,r,s)=>{},(n,r)=>{});
            startup.Replay.ReplayButton.transform.parent.gameObject.SetActive(false);
            startup.Replay.BindModalBlocker(()=>HasLocalModal);
            BindToolsScene();bottom.Init();
            exchangeMask.onClick.RemoveAllListeners();exchangeMask.onClick.AddListener(CancelExchange);
            exchangeMask.gameObject.SetActive(false);
            game.OperationApplied+=RefreshToolsAfterOperation;
        }
        private void BindToolsScene()
        {
            var board=game.Level.Board;
            bottom.BindScene(game,tools,()=>maximumAddedTiles,()=>false,SetExchange,
                (id,type)=>RefillLocalTool(type),ShowToolTip,TryToolClick,startup.Replay.EndClick,id=>
                {
                    if(game.Level.Board!=board||game.IsRestarting)return;
                    if(id==17)startup.Replay.OpenPanel();
                    else if(id==10&&!game.IsSucceed)ShowFailure();
                    else ShowUnavailable();
                });
        }
        private void RefillLocalTool(int type)
        {
            if(toolOffer!=null||toolAdPending)return;
            if(type==2&&game.Level.MoveHistoryCount<=0){ShowToolTip(3);return;}
            if(type==4&&game.User.CurrentLevelAddScrewCount>=maximumAddedTiles){ShowToolTip(5);return;}
            toolOffer=Instantiate(Resources.Load<GameObject>(toolOfferPrefabPath),transform.parent,false).GetComponent<LocalToolOfferPanel>();
            game.ModalInputBlocked=true;
            toolOffer.Init(type,game.Tables,language,()=>!toolAdPending&&!game.InputBlocked&&!startup.Replay.IsClickMasked&&startup.Replay.BeginClick(),
                ()=>{CloseToolOffer();RequestToolAd(type);},CloseToolOffer);
        }
        private void CloseToolOffer()
        {
            if(toolOffer==null)return;
            Destroy(toolOffer.gameObject);toolOffer=null;
            StartCoroutine(ReleaseWithdrawalInput());
        }
        private void RequestToolAd(int type)
        {
            if(toolAdPending||type<2||type>4)return;
            if(type==2&&game.Level.MoveHistoryCount<=0){ShowToolTip(3);return;}
            if(type==4&&game.User.CurrentLevelAddScrewCount>=maximumAddedTiles){ShowToolTip(5);return;}
            if(localToolRefillCount<=0){ShowUnavailable();return;}
            toolAdPending=true;game.ModalInputBlocked=true;
            int request=++toolAdRequest;var board=game.Level.Board;
            System.Action<bool> complete=success=>
            {
                if(this==null||!toolAdPending||request!=toolAdRequest)return;
                toolAdPending=false;
                if(game.Level.Board==board&&!game.IsRestarting)
                {
                    if(success){ApplyToolRefill(type);ShowWithdrawalFeedback(toolAdSuccessText);}
                    else ShowWithdrawalFeedback(toolAdFailureText);
                }
                game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
            };
            if(rewardedToolAdProvider!=null)rewardedToolAdProvider(type,complete);
            else if(simulateRewardedToolAds)StartCoroutine(SimulateToolAd(complete));
            else complete(false);
        }
        private System.Collections.IEnumerator SimulateToolAd(System.Action<bool> complete)
        {
            yield return new WaitForSeconds(simulatedToolAdDelay);
            complete(true);
        }
        private void ApplyToolRefill(int type)
        {
            if(localToolRefillCount<=0){ShowUnavailable();return;}
            if(type==2)
            {
                if(game.Level.MoveHistoryCount<=0){ShowToolTip(3);return;}
                if(game.User.RevokeCount<=0)tools.AddTool(type,localToolRefillCount,true);
                game.Revoke(bottom.RefreshOtherItemButtonState,id=>{if(id==10&&!game.IsSucceed)ShowFailure();});
                tools.AddTool(type,-1,true);
            }
            else if(type==3)
            {
                if(game.User.ExchangeCount<=0)tools.AddTool(type,localToolRefillCount,true);
                SetExchange(true,0);
            }
            else if(type==4)
            {
                if(game.User.CurrentLevelAddScrewCount>=maximumAddedTiles){ShowToolTip(5);return;}
                if(game.User.AddScrewCount<=0)tools.AddTool(type,localToolRefillCount,true);
                game.AddScrew();
            }
        }
        private bool TryToolClick()
        {
            if(game.InputBlocked||game.ModalInputBlocked||game.IsRestarting||!game.IsInitDone||game.IsSucceed)return false;
            return startup.Replay.BeginClick();
        }
        private void RefreshToolsAfterOperation(ScrewOperation operation,ScrewState target) => bottom.Refresh();
        private void ShowToolTip(int id)
        {
            var tip=Instantiate(Resources.Load<GameObject>(popTipPrefabPath),startup.TopCanvas,false).GetComponent<OriginalPopTip>();
            tip.Show(game.Tables.Text.GetText(id,language),game.ScheduleDelay);
        }
        private void SetExchange(bool active,float delay) => game.SetExchangeState(exchangeMask.gameObject,active,delay);
        private void CancelExchange()
        {
            if(!TryToolClick())return;
            SetExchange(false,exchangeCancelDelay);
            startup.Replay.EndClick();
        }
    }
}
