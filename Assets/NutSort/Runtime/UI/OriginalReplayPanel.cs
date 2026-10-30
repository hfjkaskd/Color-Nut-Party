using System;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalReplayPanel : MonoBehaviour
    {
        [Serializable] public struct Label { public TMP_Text Text; public int Id; }
        [SerializeField] private Button ContinueBtn, ReplayBtn, CloseBtn;
        [SerializeField] private RectTransform main, title;
        [SerializeField] private Image backdrop;
        [SerializeField] private OriginalPanelSettings settings;
        [SerializeField] private Label[] labels;
        private OriginalReplayController owner;
        private float elapsed, openElapsed;
        private bool closing, opening;
        private Vector3 closeStart;
        public Button ContinueButton=>ContinueBtn;
        public Button ReplayButton=>ReplayBtn;
        public RectTransform Main=>main;
        public bool Closing=>closing;
        public void Initialize(OriginalReplayController value, OriginalTables tables, string language)
        {
            owner=value;
            // Binding order is significant: the original CloseBtn and ReplayBtn
            // reference the same Button, and AddLSSListener replaces old listeners.
            CloseBtn.onClick.RemoveAllListeners(); CloseBtn.onClick.AddListener(Cancel);
            ContinueBtn.onClick.RemoveAllListeners(); ContinueBtn.onClick.AddListener(Cancel);
            ReplayBtn.onClick.RemoveAllListeners(); ReplayBtn.onClick.AddListener(Replay);
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            backdrop.color=new Color(0,0,0,settings.BackdropAlpha);
            main.localScale=Vector3.zero;if(title!=null)title.localScale=Vector3.zero;
            elapsed=openElapsed=0;opening=true;closing=false;
        }
        private void Cancel() { if(owner.BeginClick()) { LocalAnalyticsPopup.Action(this,"cancel");Close();owner.EndClick(); } }
        private void Replay() { if(owner.BeginClick()) { LocalAnalyticsPopup.Action(this,"restart");Close();owner.Restart();owner.EndClick(); } }
        public void Close() { if(closing)return;closing=true;opening=false;elapsed=0;closeStart=main.localScale; }
        private void Awake() { OriginalUIAnimationDriver.Register(this, Advance); }
        private void OnDestroy() { OriginalUIAnimationDriver.Unregister(this); }
        public void Advance(float delta)
        {
            if(!opening&&!closing)return;
            elapsed+=delta;openElapsed+=delta;
            // The original title tween keeps running independently during close.
            if(title!=null)title.localScale=Vector3.one*settings.TitleCurve.Evaluate(Mathf.Clamp01((openElapsed-settings.TitleDelayTime)/settings.TitleDurationTime));
            if(closing)
            {
                float t=Mathf.Clamp01(elapsed/settings.CloseDuration);
                float eased=t*t*((settings.BackOvershoot+1)*t-settings.BackOvershoot);
                main.localScale=Vector3.LerpUnclamped(closeStart,Vector3.zero,eased);
                if(t>=1) { closing=false;owner.PanelClosed(this); }
                return;
            }
            main.localScale=Vector3.one*settings.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/settings.PanelDurationTime));
            if(elapsed>=Mathf.Max(settings.PanelDurationTime,settings.TitleDelayTime+settings.TitleDurationTime))opening=false;
        }
    }
}
