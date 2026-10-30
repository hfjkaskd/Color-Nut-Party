using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalWithdrawalTooFastPanel:MonoBehaviour,IOriginalWithdrawalTooFastUI
    {
        [SerializeField] private Button IKnowBtn;
        [SerializeField] private Image backdrop;
        [SerializeField] private TMP_Text LevelTip,VideoTip,LevelPregressValue,VideoPregressValue;
        [SerializeField] private Image LevelPregress,VideoPregress;
        [SerializeField] private RectTransform main,title;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private float queueDelay;
        private OriginalWithdrawalTooFastFlow flow;
        private OriginalTables tables;private string language;
        private Func<bool> canClick;private Action<string> sound;
        private Action closed,hidden,next;
        private Action<float,Action> schedule;
        private float elapsed;private bool opening;
        private sealed class CloseTrack{public float Elapsed;public bool Started;public Vector3 Start;}
        private readonly List<CloseTrack> closes=new List<CloseTrack>();
        public Button SureButton=>IKnowBtn;
        public TMP_Text LevelLabel=>LevelTip;
        public TMP_Text VideoLabel=>VideoTip;
        public TMP_Text LevelValue=>LevelPregressValue;
        public TMP_Text VideoValue=>VideoPregressValue;
        public Image LevelFill=>LevelPregress;
        public Image VideoFill=>VideoPregress;
        public bool Closing=>closes.Count>0;
        public OriginalWithdrawalTooFastFlow Flow=>flow;
        public void Bind(OriginalUserLocalData user,OriginalTables tables,string language,Func<bool> canClick,Action<string> sound,
            Func<int> showLevel,Func<Action> findStageProgress,Action closed,Action hidden,Action<float,Action> schedule,Action next)
        {
            this.tables=tables;this.language=language;this.canClick=canClick;this.sound=sound;
            this.closed=closed;this.hidden=hidden;this.schedule=schedule;this.next=next;
            flow=new OriginalWithdrawalTooFastFlow(user,this,showLevel,findStageProgress);
        }
        public void Init()=>flow.Init(()=>
        {
            main.localScale=Vector3.zero;title.localScale=Vector3.zero;elapsed=0;opening=true;
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
        },action=>BindButton(IKnowBtn,action));
        private void BindButton(Button button,Action action)
        {button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(!canClick())return;LocalAnalyticsPopup.Action(this,"continue");action();sound(settings.ClickSound);});}
        public void Refresh()=>flow.Refresh();
        public void SetLocalReview(string heading,string description,string videoCaption,int completed,int total)
        {
            foreach(var label in labels)
            {if(label.Id==46)label.Text.text=description;else if(label.Id==51)label.Text.text=heading;}
            VideoTip.text=videoCaption;
            VideoPregress.fillAmount=total>0?Mathf.Clamp01((float)completed/total):0;
            VideoPregressValue.text=completed.ToString(System.Globalization.CultureInfo.InvariantCulture)+"/"+total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        public void SetLevelTip(int id,int level)=>LevelTip.text=tables.Text.GetText(id,language,level);
        public void SetVideoTip(int id,int count)=>VideoTip.text=tables.Text.GetText(id,language,count);
        public void SetLevelFill(float value)=>LevelPregress.fillAmount=value;
        public void SetVideoFill(float value)=>VideoPregress.fillAmount=value;
        public void SetLevelValue(string value)=>LevelPregressValue.text=value;
        public void SetVideoValue(string value)=>VideoPregressValue.text=value;
        public void Close(){if(this==null)return;closes.Add(new CloseTrack());}
        public void Hide(){hidden();schedule(queueDelay,next);}
        private void Awake()=>OriginalUIAnimationDriver.Register(this,Advance);
        private void OnDestroy()=>OriginalUIAnimationDriver.Unregister(this);
        public void Advance(float delta)
        {
            if(delta==0)return;
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
