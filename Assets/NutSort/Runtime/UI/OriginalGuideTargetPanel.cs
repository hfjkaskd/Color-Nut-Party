using System;
using NutSort.Content;
using NutSort.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalGuideTargetPanel : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text tip;
        [SerializeField] private OriginalGoldImage goldImage;
        [SerializeField] private RectTransform main,title;
        [SerializeField] private Image backdrop;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private string clickSound;
        [SerializeField] private float queueDelay;
        private OriginalPanelActionQueue actionQueue;
        private Action<float,Action> schedule;
        private Action hidden;
        private Func<bool> gate;
        private Action<string> audio;
        private Action closed;
        private OriginalGuideTargetCompletion continuation;
        private Action noticeConfirmed;


        private bool opening,closing;
        private float elapsed,closeElapsed;
        private Vector3 closeStart;
        public Button ContinueButton=>continueButton;
        public TMP_Text Tip=>tip;

        public bool Closing=>closing;
        public void Initialize(OriginalUserLocalData user,int level,OriginalTables tables,string language,
            Func<string> country,Func<float,string> formatGold,Func<bool> gate,Action<string> audio,
            Action<bool,bool> initializeDone,Action closed)
        {
            this.gate=gate;this.audio=audio;this.closed=closed;
            noticeConfirmed=null;
            continuation=new OriginalGuideTargetCompletion(user,level,formatGold,Close,initializeDone);
            InitializePresentation(tables,language,country);
        }
        // Presentation of an already persisted reward. The caller owns its receipt;
        // this path never claims another reward or runs withdrawal-guide callbacks.
        public void InitializeNotice(OriginalTables tables,string language,Func<string> country,string amount,string titleText,string buttonText,
            Func<bool> gate,Action<string> audio,Action confirmed,Action closed)
        {
            this.gate=gate;this.audio=audio;this.closed=closed;
            continuation=null;noticeConfirmed=confirmed;
            InitializePresentation(tables,language,country);
            tip.text=amount;
            foreach(var label in labels)
            {
                if(label.Id==162)label.Text.text=titleText;
                else if(label.Id==1)label.Text.text=buttonText;
            }
        }
        private void InitializePresentation(OriginalTables tables,string language,Func<string> country)
        {
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            goldImage.Bind(country);
            continueButton.onClick.RemoveAllListeners();continueButton.onClick.AddListener(Continue);
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            main.localScale=Vector3.zero;title.localScale=Vector3.zero;
            elapsed=closeElapsed=0;opening=true;closing=false;
        }
        public void Refresh(){if(continuation!=null)tip.text=continuation.Refresh();}
        public void BindHide(OriginalPanelActionQueue actionQueue,Action<float,Action> schedule,Action hidden=null)
        {
            this.actionQueue=actionQueue;this.schedule=schedule;this.hidden=hidden;
        }
        public void Hide()
        {
            hidden?.Invoke();
            schedule(queueDelay,actionQueue.Dequeue);
        }
        private void Continue()
        {
            if(!gate())return;
            LocalAnalyticsPopup.Action(this,"continue");
            if(noticeConfirmed!=null){noticeConfirmed();Close();}
            else continuation.Continue();
            audio(clickSound);
        }
        public void Close(){if(closing)return;closing=true;opening=false;closeElapsed=0;closeStart=main.localScale;}
        private void Awake(){OriginalUIAnimationDriver.Register(this,Advance);}
        private void OnDestroy(){OriginalUIAnimationDriver.Unregister(this);}
        public void Advance(float delta)
        {
            if(!opening&&!closing)return;
            elapsed+=delta;
            title.localScale=Vector3.one*settings.TitleCurve.Evaluate(Mathf.Clamp01((elapsed-settings.TitleDelayTime)/settings.TitleDurationTime));
            if(closing)
            {
                closeElapsed+=delta;float t=Mathf.Clamp01(closeElapsed/settings.CloseDuration);
                float eased=t*t*((settings.BackOvershoot+1)*t-settings.BackOvershoot);
                main.localScale=Vector3.LerpUnclamped(closeStart,Vector3.zero,eased);
                if(t>=1){closing=false;closed();}
            }
            else
            {
                main.localScale=Vector3.one*settings.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/settings.PanelDurationTime));
                if(elapsed>=Mathf.Max(settings.PanelDurationTime,settings.TitleDelayTime+settings.TitleDurationTime))opening=false;
            }
        }
    }
}
