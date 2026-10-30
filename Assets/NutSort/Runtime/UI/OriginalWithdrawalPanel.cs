using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public interface IOriginalWithdrawalServices
    {
        bool CanClick { get; }
        bool OnlineTimeHint { get; set; }
        bool Progress2Guide { set; }
        string MainGoldHint { get; }
        string Country { get; }
        void SetGold(float value,bool refresh,bool showHint);
        void Save();
        void ShowPanel(int id,object[] arguments);
        void ShowTip(string text);
        void PlaySound(string sound);
        void Closed();
        void Hidden();
        void Schedule(float delay,Action action);
        void NextPanel();
        void InitializePlayerInfo(OriginalPlayerInfo player,int level);
    }
    public sealed class OriginalWithdrawalPanel:MonoBehaviour,IOriginalWithdrawalHeaderUI,
        IOriginalWithdrawalProgressUI,IOriginalWithdrawalRefreshHost,IOriginalWithdrawalClaimUI
    {
        [SerializeField] private Button Close,TXBtn;
        [SerializeField] private TMP_Text TXText,GoldCount,Title,Tip,ProgressValue,ProgressTipValue,Level1Title,Level2Title;
        [SerializeField] private Transform PlayerInfoPos;
        [SerializeField] private Image Progress,backdrop;
        [SerializeField] private RectTransform ProgressTip,main;
        [SerializeField] private GameObject Level1,Level2;
        [SerializeField] private OriginalGoldImage goldImage;
        [SerializeField] private OriginalWithdrawalGoldTween goldTween;
        [SerializeField] private OriginalPlayerInfo playerInfoPrefab;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private float queueDelay;
        public static bool IsPlayGoldTween;
        private OriginalUserLocalData user;
        private OriginalTables tables;
        private string language;
        private IOriginalWithdrawalServices services;
        private object[] arguments;
        private OriginalWithdrawalPanelFlow lifecycle;
        private OriginalWithdrawalPendingGold pending;
        private OriginalWithdrawalEarlyProgress progress;
        private OriginalWithdrawalRefreshFlow refresh;
        private OriginalWithdrawalClaimFlow claims;
        private float elapsed;
        private bool opening;
        [SerializeField] private string localMarqueePath,localMissingValue,localMissingFeed;
        private bool localPresentation;
        private Action localClosed;
        private OriginalSimpleMarquee localMarquee;
        private Button localHistoryButton;
        private Action localHistoryAction;
        private Func<bool> localCanClick;
        private Action<string> localPlaySound;
        private string localHistoryCaption;
        public bool Closing=>closes.Count>0;
        private struct CloseTrack { public float Elapsed;public Vector3 Start;public bool Started; }
        private readonly List<CloseTrack> closes=new List<CloseTrack>();
        public Button WithdrawalButton=>TXBtn;
        public Button CloseButton=>Close;
        public Button HistoryButton=>localHistoryButton;
        public TMP_Text GoldLabel=>GoldCount;
        public TMP_Text ClaimLabel=>TXText;
        public TMP_Text ProgressLabel=>ProgressValue;
        public TMP_Text TipLabel=>Tip;
        public RectTransform Main=>main;
        public Transform PlayerParent=>PlayerInfoPos;
        public OriginalWithdrawalGoldTween GoldTween=>goldTween;
        public bool IsDoneTask=>localPresentation?Fill>=settings.WithdrawalCompleteThreshold:progress.IsDoneTask;
        public void Bind(OriginalUserLocalData user,OriginalTables tables,string language,Func<float,string> format,IOriginalWithdrawalServices services)
        {
            this.user=user;this.tables=tables;this.language=language;this.services=services;
            goldImage.Bind(()=>services.Country);
            lifecycle=new OriginalWithdrawalPanelFlow(user,()=>services.OnlineTimeHint,b=>services.OnlineTimeHint=b,
                ()=>pending.IsShowTargetHint,id=>services.ShowPanel(id,Array.Empty<object>()),ClosePanel);
            var header=new OriginalWithdrawalHeader(user,()=>tables.GetShowLevel(user.Level),format,this);
            pending=new OriginalWithdrawalPendingGold(user,()=>IsPlayGoldTween,services.SetGold,services.Save,goldTween.Play,format,value=>GoldCount.text=value,goldTween.Duration);
            progress=new OriginalWithdrawalEarlyProgress(()=>tables.GetShowLevel(user.Level),this,settings);
            var stages=new OriginalRewardProgress(user,tables,format);
            refresh=new OriginalWithdrawalRefreshFlow(user,lifecycle,header,pending,progress,stages,this,format,StartLevelShow,()=>tables.LastLevel.ShowLevel);
            claims=new OriginalWithdrawalClaimFlow(user,stages,this);
        }
        public void Init(object[] value)
        {
            arguments=value;
            lifecycle.Init(value,()=>
            {
                main.localScale=Vector3.zero;elapsed=0;opening=true;
                backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
                foreach(var label in labels)label.Text.text=Text(label.Id);
            },()=>{TXBtn.onClick.RemoveAllListeners();TXBtn.onClick.AddListener(ClickWithdrawal);},
              ()=>{Close.onClick.RemoveAllListeners();Close.onClick.AddListener(ClickClose);});
        }
        // Same prefab presentation, backed by the local balance and requirements.
        public void InitLocal(OriginalTables tables,string language,string country,Sprite icon,
            string amount,string title,string tip,string progressText,string progressHint,float fill,
            Func<bool> canClick,Action claim,Action closed,Action<string> playSound,Action<OriginalPlayerInfo> initializeStats)
        {
            this.tables=tables;this.language=language;localPresentation=true;localClosed=closed;
            localCanClick=canClick;localPlaySound=playSound;
            goldImage.Bind(()=>country);
            if(icon!=null)goldImage.SetOverride(icon);
            main.localScale=Vector3.zero;elapsed=0;opening=true;
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            foreach(var label in labels)label.Text.text=Text(label.Id);
            var player=Instantiate(playerInfoPrefab,PlayerInfoPos,false);
            initializeStats(player);
            if(localHistoryButton!=null)localHistoryButton.onClick.RemoveListener(ClickLocalHistory);
            localMarquee=Instantiate(Resources.Load<GameObject>(localMarqueePath),player.MarqueeParent,false).GetComponent<OriginalSimpleMarquee>();
            localMarquee.InitUnavailable(LocalUiLocalization.Text(localMissingFeed));
            localHistoryButton=localMarquee.GetComponentInChildren<Button>(true);
            if(localHistoryButton!=null)localHistoryButton.interactable=false;
            ApplyLocalHistoryAction();
            GoldCount.text=amount;Title.text=LocalUiLocalization.Text(title);Tip.text=LocalUiLocalization.Text(tip);
            ProgressValue.text=progressText;Progress.fillAmount=Mathf.Clamp01(fill);
            SetMarkerX(Mathf.Clamp01(fill)*settings.WithdrawalProgressTravel+settings.WithdrawalProgressOffset);
            ProgressTipValue.text=progressHint;
            TXText.text=Text(1);
            Level1.SetActive(false);Level2.SetActive(false);
            TXBtn.onClick.RemoveAllListeners();TXBtn.onClick.AddListener(()=>{if(!canClick()||Closing)return;LocalAnalyticsPopup.Action(this,"withdraw");claim();playSound(settings.ClickSound);});
            Close.onClick.RemoveAllListeners();Close.onClick.AddListener(()=>{if(!canClick()||Closing)return;LocalAnalyticsPopup.Action(this,"close");ClosePanel();playSound(settings.ClickSound);});
        }
        public void SetLocalNotice(string text){Tip.text=LocalUiLocalization.Text(text);}
        public void SetLocalHistoryAction(Action openHistory,string caption)
        {
            localHistoryAction=openHistory??throw new ArgumentNullException(nameof(openHistory));
            localHistoryCaption=LocalUiLocalization.Text(caption??string.Empty);
            ApplyLocalHistoryAction();
        }
        private void ApplyLocalHistoryAction()
        {
            if(!localPresentation||localMarquee==null||localHistoryAction==null)return;
            if(localHistoryButton==null)throw new InvalidOperationException("Local history strip prefab must contain a standard Button.");
            localMarquee.InitUnavailable(localHistoryCaption);
            localHistoryButton.interactable=true;
            localHistoryButton.onClick.RemoveListener(ClickLocalHistory);
            localHistoryButton.onClick.AddListener(ClickLocalHistory);
        }
        private void ClickLocalHistory()
        {
            if(!localPresentation||Closing||!localCanClick())return;
            localPlaySound(settings.ClickSound);
            localHistoryAction();
        }
        public void Refresh()=>refresh.Refresh(arguments);
        public void GetCallback()=>claims.Run(lifecycle.Level,arguments,progress.IsDoneTask,pending.IsShowTargetHint);
        private void ClickWithdrawal(){if(!services.CanClick)return;GetCallback();services.PlaySound(settings.ClickSound);}
        private void ClickClose(){if(!services.CanClick)return;lifecycle.CloseCallback();services.PlaySound(settings.ClickSound);}
        public void ClosePanel(){if(this==null)return;closes.Add(new CloseTrack());}
        public void Hide(){services.Hidden();services.Schedule(queueDelay,services.NextPanel);}
        private void Awake()=>OriginalUIAnimationDriver.Register(this,Advance);
        private void OnDestroy()
        {
            OriginalUIAnimationDriver.Unregister(this);
            if(localHistoryButton!=null)localHistoryButton.onClick.RemoveListener(ClickLocalHistory);
        }
        public void Advance(float delta)
        {
            if(opening)
            {
                elapsed+=delta;main.localScale=Vector3.one*settings.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/settings.PanelDurationTime));
                if(elapsed>=settings.PanelDurationTime){opening=false;if(!localPresentation)lifecycle.TweenEnd();}
            }
            for(int i=0;i<closes.Count;)
            {
                var track=closes[i];if(!track.Started){track.Started=true;track.Start=main.localScale;}
                track.Elapsed+=delta;float t=Mathf.Clamp01(track.Elapsed/settings.CloseDuration);
                main.localScale=Vector3.LerpUnclamped(track.Start,Vector3.zero,t*t*((settings.BackOvershoot+1)*t-settings.BackOvershoot));
                if(t>=1){closes.RemoveAt(i);if(localPresentation)localClosed();else services.Closed();}else{closes[i]=track;i++;}
            }
        }
        public float Fill{get=>Progress.fillAmount;set=>Progress.fillAmount=value;}
        public void SetTip(int id,params object[] args)=>Tip.text=tables.Text.GetText(id,language,args);
        void IOriginalWithdrawalRefreshHost.SetTip(int id)=>SetTip(id);
        public void SetProgress(string value)=>ProgressValue.text=value;
        public void SetProgressTip(int id,params object[] args)=>ProgressTipValue.text=tables.Text.GetText(id,language,args);
        public void SetMarkerX(float x){var p=ProgressTip.anchoredPosition;p.x=x;ProgressTip.anchoredPosition=p;}
        public void SetLevelTitle(int slot,int id,int value)=>(slot==1?Level1Title:Level2Title).text=tables.Text.GetText(id,language,value);
        public void SetGold(string value)=>GoldCount.text=value;
        public void SetTitle(int id,int value)=>Title.text=tables.Text.GetText(id,language,value);
        public string MainGoldHint=>services.MainGoldHint;
        public bool PlayGoldTween{get=>IsPlayGoldTween;set=>IsPlayGoldTween=value;}
        public void SetRawTip(string value)=>Tip.text=value;
        public void SetClaimText(int id)=>TXText.text=Text(id);
        public void InitializePlayerInfo(int level){var player=Instantiate(playerInfoPrefab,PlayerInfoPos,false);services.InitializePlayerInfo(player,level);}
        public bool OnlineTimeHint{get=>services.OnlineTimeHint;set=>services.OnlineTimeHint=value;}
        public bool Progress2Guide{set=>services.Progress2Guide=value;}
        string IOriginalWithdrawalClaimUI.Tip=>Tip.text;
        public string Text(int id)=>tables.Text.GetText(id,language);
        public void ShowPanel(int id,object[] args)=>services.ShowPanel(id,args);
        public void ShowTip(int id)=>services.ShowTip(Text(id));
        public void ShowTip(string text)=>services.ShowTip(text);
        void IOriginalWithdrawalClaimUI.Close()=>ClosePanel();
        private int StartLevelShow()
        {
            var list=(JArray)Field(user.GoldRewardTargetS2CData,"bear_list");
            int level=(int?)Field((JObject)list[2],"StartLevel")??0;
            return tables.GetLevelInfo(level,user.Level).ShowLevel;
        }
        private static JToken Field(JObject row,string name)
        {
            if(row==null)throw new NullReferenceException(name);
            JToken value=null;foreach(var p in row.Properties())if(string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase))value=p.Value;return value;
        }
    }
}
