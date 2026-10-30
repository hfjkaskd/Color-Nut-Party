using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalWithdrawalConfirmationPanel:MonoBehaviour,IOriginalWithdrawalConfirmationUI
    {
        [SerializeField] private Button GetBtn,CloseBtn,ReEnterBtn;
        [SerializeField] private Image Image,backdrop;
        [SerializeField] private TMP_Text Tip;
        [SerializeField] private RectTransform main,title;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private float queueDelay;
        private OriginalWithdrawalConfirmationFlow flow;
        private OriginalTables tables;private string language;
        private Func<bool> canClick;private Action<string> sound;
        private Action<int,object[]> show;private Action closed,hidden,next;
        private Action<float,Action> schedule;
        private float elapsed;private bool opening;
        private sealed class CloseTrack{public float Elapsed;public bool Started;public Vector3 Start;}
        private readonly List<CloseTrack> closes=new List<CloseTrack>();
        public Button ConfirmButton=>GetBtn;
        public Button CloseButton=>CloseBtn;
        public Button ReenterButton=>ReEnterBtn;
        public TMP_Text TipLabel=>Tip;
        public Image ChannelImage=>Image;
        public bool Closing=>closes.Count>0;
        public OriginalWithdrawalConfirmationFlow Flow=>flow;
        public void Bind(OriginalUserLocalData user,OriginalTables tables,string language,Func<string> country,Func<string> area,
            Func<bool> canClick,Action<string> sound,Action<int,object[]> show,Action<int,bool> goldGet,Action<object> guide,
            Action closed,Action hidden,Action<float,Action> schedule,Action next)
        {
            this.tables=tables;this.language=language;this.canClick=canClick;this.sound=sound;this.show=show;
            this.closed=closed;this.hidden=hidden;this.schedule=schedule;this.next=next;
            flow=new OriginalWithdrawalConfirmationFlow(user,tables.PayChannels,tables.ChannelInfos,country,area,this,goldGet,guide);
        }
        public void Init(object[] args)=>flow.Init(args,()=>
        {
            main.localScale=Vector3.zero;title.localScale=Vector3.zero;elapsed=0;opening=true;
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
        },()=>BindButton(GetBtn,flow.Get),()=>BindButton(ReEnterBtn,flow.Reenter),()=>BindButton(CloseBtn,flow.Close));
        private void BindButton(Button button,Action action)
        {button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(!canClick())return;LocalAnalyticsPopup.Action(this,button==GetBtn?"confirm":button==CloseBtn?"close":"edit_account");action();sound(settings.ClickSound);});}
        public void Refresh()=>flow.Refresh();
        public void SetConfirmVisible(bool value)=>GetBtn.gameObject.SetActive(value);
        public void SetTip(string value)=>Tip.text=value;
        public void SetIcon(string path)=>Image.sprite=Resources.Load<Sprite>(path);
        public void ShowPanel(int id,object[] args)=>show(id,args);
        public void Close(){if(this==null)return;closes.Add(new CloseTrack());}
        public void Hide(){hidden();schedule(queueDelay,next);}
        private void Awake()=>OriginalUIAnimationDriver.Register(this,Advance);
        private void OnDestroy()=>OriginalUIAnimationDriver.Unregister(this);
        public void Advance(float delta)
        {
            if(delta==0)return;
            if(opening)
            {
                elapsed+=delta;
                main.localScale=Vector3.one*settings.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/settings.PanelDurationTime));
                if(elapsed>=settings.TitleDelayTime)title.localScale=Vector3.one*settings.TitleCurve.Evaluate(Mathf.Clamp01((elapsed-settings.TitleDelayTime)/settings.TitleDurationTime));
                if(elapsed>=Mathf.Max(settings.PanelDurationTime,settings.TitleDelayTime+settings.TitleDurationTime))opening=false;
            }
            int count=closes.Count,index=0;
            for(int i=0;i<count;i++)
            {
                var c=closes[index];if(!c.Started){c.Started=true;c.Start=main.localScale;}
                c.Elapsed+=delta;float t=Mathf.Clamp01(c.Elapsed/settings.CloseDuration);
                main.localScale=Vector3.LerpUnclamped(c.Start,Vector3.zero,t*t*((settings.BackOvershoot+1)*t-settings.BackOvershoot));
                if(t>=1){closes.RemoveAt(index);closed();}else index++;
            }
        }
    }
}
