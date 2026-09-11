using System;
using System.Collections.Generic;
using NutSort.Content;
using NutSort.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalSuccessPanel : MonoBehaviour
    {
        [SerializeField] private Button Close,GetBtn,MoreGetBtn;
        [SerializeField] private Transform Items;
        [SerializeField] private RectTransform main,title;
        [SerializeField] private Image backdrop;
        [SerializeField] private TMP_Text moreLabel;
        [SerializeField] private GameObject ad;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private float queueDelay;
        private OriginalSuccessPanelFlow lifecycle;
        private OriginalRewardGetFlow claims;
        private OriginalRewardItemFactory factory;
        private OriginalItemGetInfo info;
        private OriginalTables tables;
        private string language;
        private Func<bool> gate;
        private Action<string> audio;
        private Action closed,hidden;
        private Action<Action> hideBranch;
        private Action<float,Action> schedule;
        private OriginalPanelActionQueue queue;
        private bool opening,titleOpening;
        private Action openingCompleted;
        public bool Closing=>closes.Count>0;
        private float elapsed;
        private struct CloseTrack { public float Elapsed;public Vector3 Start;public bool Started; }
        private readonly List<CloseTrack> closes=new List<CloseTrack>();
        public List<OriginalRewardItemView> Views { get; private set; }
        public Button GetButton=>GetBtn;
        public Button MoreButton=>MoreGetBtn;
        public Button CloseButton=>Close;
        public TMP_Text MoreLabel=>moreLabel;
        public GameObject Ad=>ad;
        public RectTransform Main=>main;
        public RectTransform Title=>title;
        public void Bind(OriginalSuccessPanelFlow lifecycle,OriginalRewardGetFlow claims,OriginalRewardItemFactory factory,
            OriginalTables tables,string language,Func<bool> gate,Action<string> audio,Action closed,
            Action<Action> hideBranch,Action<float,Action> schedule,OriginalPanelActionQueue queue,Action hidden=null)
        {
            this.lifecycle=lifecycle;this.claims=claims;this.factory=factory;this.tables=tables;this.language=language;
            this.gate=gate;this.audio=audio;this.closed=closed;this.hideBranch=hideBranch;
            this.schedule=schedule;this.queue=queue;this.hidden=hidden;
        }
        public void Init(OriginalItemGetInfo value)
        {
            openingCompleted=()=>lifecycle.TweenEndRefresh(()=>{});
            lifecycle.Init(()=>{InitializePresentation();info=value;});
        }
        // Explicit presentation-only composition for authorized local progression.
        // It never runs reward/account lifecycle or synthesizes claim data.
        public void InitLocal(OriginalTables tables,string language,Func<bool> gate,Action<string> audio,Action closed,string caption)
        {
            this.tables=tables;this.language=language;this.gate=gate;this.audio=audio;this.closed=closed;
            openingCompleted=null;
            InitializePresentation();moreLabel.text=caption;
            Close.onClick.RemoveAllListeners();Close.onClick.AddListener(ClickClose);
            GetBtn.onClick.RemoveAllListeners();GetBtn.onClick.AddListener(ClickClose);
            MoreGetBtn.onClick.RemoveAllListeners();MoreGetBtn.onClick.AddListener(ClickClose);
        }
        // Local and remote settlement providers share the original presentation;
        // the provider owns balances, persistence and the completion callback.
        public void InitSettlement(OriginalTables tables,string language,Func<bool> gate,
            Action<string> audio,Action closed,OriginalRewardItemFactory factory,
            OriginalItemGetInfo value,Action<bool> claim,bool allowMore,bool showAd,string moreCaption)
        {
            if(value==null||value.ItemInfos==null||value.ItemInfos.Count==0)
                throw new ArgumentException("Settlement requires reward items.",nameof(value));
            if(claim==null)throw new ArgumentNullException(nameof(claim));
            this.tables=tables;this.language=language;this.gate=gate;this.audio=audio;this.closed=closed;
            openingCompleted=null;
            InitializePresentation();
            if(allowMore)moreLabel.text=moreCaption;
            Items.gameObject.SetActive(true);
            GetBtn.gameObject.SetActive(true);
            MoreGetBtn.gameObject.SetActive(allowMore);
            ad.SetActive(allowMore&&showAd);
            if(Views!=null)
                foreach(var view in Views)Destroy(view.gameObject);
            Views=factory.GenerateItem(value,Items,true,true);
            Close.onClick.RemoveAllListeners();Close.onClick.AddListener(ClickClose);
            GetBtn.onClick.RemoveAllListeners();
            GetBtn.onClick.AddListener(()=>{if(!gate())return;claim(false);audio(settings.ClickSound);});
            MoreGetBtn.onClick.RemoveAllListeners();
            MoreGetBtn.onClick.AddListener(()=>{if(!allowMore||!gate())return;claim(true);audio(settings.ClickSound);});
        }
        private void InitializePresentation()
        {
            main.localScale=Vector3.zero;title.localScale=Vector3.zero;
            elapsed=0;opening=titleOpening=true;
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            Close.onClick.RemoveAllListeners();Close.onClick.AddListener(ClickClose);
            GetBtn.onClick.RemoveAllListeners();GetBtn.onClick.AddListener(ClickGet);
            MoreGetBtn.onClick.RemoveAllListeners();MoreGetBtn.onClick.AddListener(ClickMore);
        }
        public void Refresh()
        {
            lifecycle.Refresh(()=>Views=factory.GenerateItem(info,Items,true,true),
                ()=>moreLabel.rectTransform.anchoredPosition=Vector2.zero,
                id=>moreLabel.text=tables.Text.GetText(id,language),
                ()=>ad.SetActive(false),()=>GetBtn.gameObject.SetActive(false));
        }
        // Guide invokes the virtual action directly, bypassing the button wrapper.
        public void GetCallback(){lifecycle.GetCallback(ClosePanel,claims.GetCallback);}
        public void MoreGetCallback(){claims.MoreGetCallback();}
        private void ClickClose(){if(!gate())return;ClosePanel();audio(settings.ClickSound);}
        private void ClickGet(){if(!gate())return;GetCallback();audio(settings.ClickSound);}
        private void ClickMore(){if(!gate())return;MoreGetCallback();audio(settings.ClickSound);}
        public void Hide(){hideBranch(()=>{hidden?.Invoke();schedule(queueDelay,queue.Dequeue);});}
        public void ClosePanel(){closes.Add(new CloseTrack());}
        private void Awake(){OriginalUIAnimationDriver.Register(this,Advance);}
        private void OnDestroy(){OriginalUIAnimationDriver.Unregister(this);}
        public void Advance(float delta)
        {
            if(!opening&&!titleOpening&&closes.Count==0)return;
            elapsed+=delta;
            if(opening)
            {
                main.localScale=Vector3.one*settings.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/settings.PanelDurationTime));
                if(elapsed>=settings.PanelDurationTime){opening=false;openingCompleted?.Invoke();}
            }
            if(titleOpening)
            {
                title.localScale=Vector3.one*settings.TitleCurve.Evaluate(Mathf.Clamp01((elapsed-settings.TitleDelayTime)/settings.TitleDurationTime));
                if(elapsed>=settings.TitleDelayTime+settings.TitleDurationTime)titleOpening=false;
            }
            // Original CloseLssPanel creates independent tweens without killing
            // the opening/title tween or rejecting repeated direct calls.
            for(int i=0;i<closes.Count;)
            {
                var track=closes[i];if(!track.Started){track.Started=true;track.Start=main.localScale;}
                track.Elapsed+=delta;float t=Mathf.Clamp01(track.Elapsed/settings.CloseDuration);
                float eased=t*t*((settings.BackOvershoot+1)*t-settings.BackOvershoot);
                main.localScale=Vector3.LerpUnclamped(track.Start,Vector3.zero,eased);
                if(t>=1){closes.RemoveAt(i);closed();}else{closes[i]=track;i++;}
            }
        }
    }
}
