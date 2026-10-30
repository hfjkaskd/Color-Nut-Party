using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalWithdrawalStageZeroPanel:MonoBehaviour,IOriginalWithdrawalStageZeroUI
    {
        [SerializeField] private Button SureBtn,CloseBtn;
        [SerializeField] private Image backdrop;
        [SerializeField] private TMP_Text Gold,Des,Tip,KeFu;
        [SerializeField] private Transform Step;
        [SerializeField] private OriginalWithdrawalStageZeroAnimation.Timing timing;
        private OriginalWithdrawalStageZeroAnimation animation;
        private Action<int,object[]> show;
        [SerializeField] private RectTransform main,title;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private float queueDelay;
        private OriginalWithdrawalStageZeroFlow flow;
        private OriginalTables tables;private string language;
        private Func<bool> canClick;private Action<string> sound;
        private Action closed,hidden,next;
        private Action<float,Action> schedule;
        private float elapsed;private bool opening;
        private sealed class CloseTrack{public float Elapsed;public bool Started;public Vector3 Start;}
        private readonly List<CloseTrack> closes=new List<CloseTrack>();
        public Button SureButton=>SureBtn;
        public Button CloseButton=>CloseBtn;
        public Transform Steps=>Step;
        public bool HasGold=>Gold!=null;
        public int StepCount=>Step.childCount;
        public string Description{get=>Des.text;set=>Des.text=value;}
        public void SetGold(string value)=>Gold.text=value;
        private Transform FindTime(int number)
        {
            var row=Step.Find(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return row.Find("Content/Time");
        }
        public bool HasStepTime(int number)=>FindTime(number)!=null;
        public void SetStepTime(int number,string value)=>FindTime(number).GetComponent<TMP_Text>().text=value;
        public void SetDescription(int id,params object[] args)=>Des.text=tables.Text.GetText(id,language,args);
        public void SetService(int id,int number)=>KeFu.text=tables.Text.GetText(id,language,number);
        public void SetTip(int id,int level)=>Tip.text=tables.Text.GetText(id,language,level);
        public void ShowPanel(int id,object[] args)=>show(id,args);
        public void PlayFirstProgress()=>animation.PlayFirst();
        public void PlayProgress()=>animation.PlayProgress();
        public bool Closing=>closes.Count>0;
        public OriginalWithdrawalStageZeroFlow Flow=>flow;
        public void Bind(OriginalUserLocalData user,OriginalTables tables,string language,object[] args,Func<long> clock,
            Func<float,string> goldFormat,Func<long,string,string> timeFormat,Func<int,int,int> random,Func<bool> complete,Func<int> showLevel,
            Action save,Action<object> guide,Func<bool> canClick,Action<string> sound,Action<int,object[]> show,
            Action closed,Action hidden,Action<float,Action> schedule,Action next)
        {
            this.tables=tables;this.language=language;this.canClick=canClick;this.sound=sound;this.show=show;
            this.closed=closed;this.hidden=hidden;this.schedule=schedule;this.next=next;
            animation=new OriginalWithdrawalStageZeroAnimation(Step,SureBtn.transform,timing,OriginalUIAnimationDriver.Schedule,()=>show(33,Array.Empty<object>()));
            flow=new OriginalWithdrawalStageZeroFlow(user,this,args,clock,goldFormat,timeFormat,random,complete,showLevel,save,guide);
        }
        public void Init()=>flow.Init(()=>
        {
            main.localScale=Vector3.zero;title.localScale=Vector3.zero;elapsed=0;opening=true;
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
        },action=>BindButton(SureBtn,action),action=>BindButton(CloseBtn,action));
        private void BindButton(Button button,Action action)
        {button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(!canClick())return;LocalAnalyticsPopup.Action(this,button==CloseBtn?"close":"review");action();sound(settings.ClickSound);});}
        public void Refresh()=>flow.Refresh();
        public void SetLocalReview(string heading,string description,string service,string actionCaption,string completed,string pending,string next,string targetTip)
        {
            foreach(var label in labels)
            {
                if(label.Id==42)label.Text.text=heading;
                else if(label.Id==43)label.Text.text=description;
                else if(label.Id==50)label.Text.text=actionCaption;
                else if(label.Id==38)label.Text.text=completed;
                else if(label.Id==39)label.Text.text=pending;
                else if(label.Id==41)label.Text.text=next;
            }
            KeFu.text=service;
            Tip.text=targetTip;
        }
        public void Close(){if(this==null)return;closes.Add(new CloseTrack());}
        public void Hide(){hidden();schedule(queueDelay,next);}
        private void Awake()=>OriginalUIAnimationDriver.Register(this,Advance);
        private void OnDestroy()=>OriginalUIAnimationDriver.Unregister(this);
        public void Advance(float delta)
        {
            if(delta==0)return;
            animation?.Advance(delta);
            if(opening)
            {
                float before=elapsed;elapsed+=delta;
                if(before<settings.PanelDurationTime)main.localScale=Vector3.one*settings.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/settings.PanelDurationTime));
                if(before<settings.TitleDelayTime+settings.TitleDurationTime&&elapsed>=settings.TitleDelayTime)title.localScale=Vector3.one*settings.TitleCurve.Evaluate(Mathf.Clamp01((elapsed-settings.TitleDelayTime)/settings.TitleDurationTime));
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
