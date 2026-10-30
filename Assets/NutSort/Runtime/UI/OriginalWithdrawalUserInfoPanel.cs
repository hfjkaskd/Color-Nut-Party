using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalWithdrawalUserInfoPanel:MonoBehaviour,IOriginalWithdrawalUserInfoUI
    {
        [Serializable] public sealed class Channel
        {
            public Button Button;public Image Icon,Checkmark;public bool On;
            [NonSerialized] public readonly List<Action<bool>> Listeners=new List<Action<bool>>();
        }
        [SerializeField] private Button GetBtn,CloseBtn,ChannelInfoBtn;
        [SerializeField] private TMP_InputField InputName,InputEmail,InputAreaNumber,InputNumber;
        [SerializeField] private Channel[] channels;
        [SerializeField] private RectTransform main,title;
        [SerializeField] private Image backdrop;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private string iconPath;
        [SerializeField] private float queueDelay,selectionFade;
        private OriginalWithdrawalUserInfoFlow flow;
        private OriginalTables tables;private string language;
        private Func<bool> canClick;private Action<string> sound;
        private Action<int> tip;private Action<int,object[]> show;
        private Action closed,hidden,next;private Action<float,Action> schedule;
        private float elapsed;private bool opening;
        private sealed class CloseTrack{public float Elapsed;public bool Started;public Vector3 Start;}
        private readonly List<CloseTrack> closes=new List<CloseTrack>();
        public OriginalWithdrawalUserInfoFlow Flow=>flow;
        public Button GetButton=>GetBtn;
        public Button CloseButton=>CloseBtn;
        public Button ChannelInfoButton=>ChannelInfoBtn;
        public Button ChannelButton(int index)=>channels[index].Button;
        public bool Closing=>closes.Count>0;
        public void Bind(OriginalUserLocalData user,OriginalTables tables,string language,Func<string> country,Func<string> area,
            Func<bool> canClick,Action<string> sound,Action<int> tip,Action<int,object[]> show,Action save,Action<object> guide,Action clearHint,
            Action closed,Action hidden,Action<float,Action> schedule,Action next)
        {
            this.tables=tables;this.language=language;this.canClick=canClick;this.sound=sound;this.tip=tip;this.show=show;
            this.closed=closed;this.hidden=hidden;this.schedule=schedule;this.next=next;
            flow=new OriginalWithdrawalUserInfoFlow(user,tables.PayChannels,tables.ChannelInfos,country,area,this,save,guide,clearHint);
        }
        public void Init(object[] args)
        {
            flow.Init(args,()=>
            {
                main.localScale=Vector3.zero;title.localScale=Vector3.zero;elapsed=0;opening=true;
                backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
                foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            },()=>BindButton(GetBtn,flow.Get),()=>BindButton(CloseBtn,flow.Close),()=>BindButton(ChannelInfoBtn,flow.ChannelInfo));
            for(int i=0;i<channels.Length;i++)
            {
                int index=i;var c=channels[i];c.Checkmark.canvasRenderer.SetAlpha(c.On?1:0);
                c.Button.onClick.RemoveAllListeners();c.Button.onClick.AddListener(()=>{LocalAnalyticsPopup.Action(this,"select_channel",index.ToString());SetChannelOn(index,!channels[index].On);});
            }
        }
        private void BindButton(Button button,Action action)
        {button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(!canClick())return;LocalAnalyticsPopup.Action(this,button==GetBtn?"save_account":button==CloseBtn?"close":"channel_help");action();sound(settings.ClickSound);});}
        public void Refresh()=>flow.Refresh();
        public string Name{get=>InputName.text;set=>InputName.text=value;}
        public string Email{get=>InputEmail.text;set=>InputEmail.text=value;}
        public string Number{get=>InputNumber.text;set=>InputNumber.text=value;}
        public string AreaNumber{set=>InputAreaNumber.text=value;}
        public int ChannelCount=>channels.Length;
        public bool IsChannelOn(int index)=>channels[index].On;
        public void SetChannelOn(int index,bool value)
        {
            var c=channels[index];if(c.On==value)return;c.On=value;
            if(value)
                for(int i=0;i<channels.Length;i++)if(i!=index&&channels[i].On&&channels[i].Button.gameObject.activeInHierarchy)SetChannelOn(i,false);
            c.Checkmark.CrossFadeAlpha(c.On?1:0,selectionFade,true);
            int count=c.Listeners.Count;for(int i=0;i<count;i++)c.Listeners[i](c.On);
        }
        public void SetChannelVisible(int index,bool value)=>channels[index].Button.gameObject.SetActive(value);
        public void SetChannelIcon(int index,string channel)=>channels[index].Icon.sprite=Resources.Load<Sprite>(string.Format(iconPath,channel));
        public void AddChannelListener(int index,Action<bool> listener)=>channels[index].Listeners.Add(listener);
        public void SetEmailVisible(bool value)=>InputEmail.gameObject.SetActive(value);
        public void SetAreaVisible(bool value)=>InputAreaNumber.gameObject.SetActive(value);
        public void SetNumberVisible(bool value)=>InputNumber.gameObject.SetActive(value);
        public void ShowTip(int id)=>tip(id);
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
