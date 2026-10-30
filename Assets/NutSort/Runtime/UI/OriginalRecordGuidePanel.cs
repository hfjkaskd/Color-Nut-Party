using System;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class OriginalRecordGuidePanel : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text tip;
        [SerializeField] private ParticleSystemRenderer currencyParticles;
        [SerializeField] private RectTransform main, title, startContainer;
        [SerializeField] private Image backdrop;
        [SerializeField] private OriginalPanelSettings panels;
        [SerializeField] private OriginalRecordGuideSettings settings;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private float queueDelay;
        private OriginalPanelActionQueue actionQueue;
        private Action<float,Action> schedule;
        private Action hidden;
        private Func<bool> beginClick;
        private Action started, closed, clickSound;
        private Material ownedMaterial;
        private float elapsed, closeElapsed;
        private bool initialized, closing;
        private bool bodyOpening, titleOpening, startOpening;
        private Vector3 closeStart;
        public Button StartButton => startButton;
        public RectTransform Main => main;
        public RectTransform StartContainer => startContainer;
        public ParticleSystemRenderer CurrencyParticles => currencyParticles;
        public bool Closing => closing;

        public void Initialize(OriginalTables tables, string language, string country,
            Func<bool> tryClick, Action onStarted, Action onClosed, Action playClick, bool refresh = true)
        {
            beginClick=tryClick ?? throw new ArgumentNullException(nameof(tryClick));
            started=onStarted ?? throw new ArgumentNullException(nameof(onStarted));
            closed=onClosed ?? throw new ArgumentNullException(nameof(onClosed));
            clickSound=playClick ?? throw new ArgumentNullException(nameof(playClick));
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            backdrop.color=new Color(0,0,0,panels.BackdropAlpha);
            main.localScale=Vector3.zero;title.localScale=Vector3.zero;startContainer.localScale=Vector3.zero;
            elapsed=closeElapsed=0;closing=false;initialized=true;
            bodyOpening=titleOpening=startOpening=true;
            startButton.onClick.RemoveAllListeners();startButton.onClick.AddListener(StartClicked);
            if(refresh)Refresh(tables,language,country);
        }

        public void Refresh(OriginalTables tables,string language,string country)
        {
            tip.text=tables.Text.GetText(163,language);
            if(ownedMaterial==null)ownedMaterial=currencyParticles.material;
            ownedMaterial.SetTexture("_MainTex",Resources.Load<Texture2D>(OriginalCurrencyArtSettings.ResolvePath(settings.GoldTexturePrefix+"{0}{1}",country,1)));
        }

        public void BindHide(OriginalPanelActionQueue queue,Action<float,Action> delay,Action onHidden)
        {
            actionQueue=queue;schedule=delay;hidden=onHidden;
        }
        public void Hide()
        {
            hidden?.Invoke();
            schedule(queueDelay,actionQueue.Dequeue);
        }

        // CountryLssInfo.GoldCode 0x9F7538; exact case-sensitive original aliases.
        public static string GoldCode(string country)
        {
            if(country=="DE" || country=="FR")return "DE";
            if(country=="US" || country=="CA" || country=="AU")return "US";
            return country;
        }

        private void StartClicked()
        {
            if(!beginClick())return;
            LocalAnalyticsPopup.Action(this,"start");
            Close();started();clickSound();
        }
        public void Close()
        {
            if(closing)return;
            closing=true;bodyOpening=false;closeElapsed=0;closeStart=main.localScale;
        }
        private void Awake(){OriginalUIAnimationDriver.Register(this,Advance);}
        public void Advance(float delta)
        {
            if(!initialized)return;
            if(delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
            if(!closing && !bodyOpening && !titleOpening && !startOpening)return;
            elapsed+=delta;
            if(titleOpening)
            {
                title.localScale=Vector3.one*panels.TitleCurve.Evaluate(Mathf.Clamp01((elapsed-panels.TitleDelayTime)/panels.TitleDurationTime));
                titleOpening=elapsed<panels.TitleDelayTime+panels.TitleDurationTime;
            }
            if(startOpening && elapsed>=settings.StartDelay)
            {
                float startT=Mathf.Clamp01((elapsed-settings.StartDelay)/settings.StartDuration)-1f;
                float startScale=1f+startT*startT*((panels.BackOvershoot+1f)*startT+panels.BackOvershoot);
                startContainer.localScale=Vector3.one*startScale;
                startOpening=elapsed<settings.StartDelay+settings.StartDuration;
            }
            if(closing)
            {
                closeElapsed+=delta;
                float t=Mathf.Clamp01(closeElapsed/panels.CloseDuration);
                float eased=t*t*((panels.BackOvershoot+1f)*t-panels.BackOvershoot);
                main.localScale=Vector3.LerpUnclamped(closeStart,Vector3.zero,eased);
                if(t>=1){initialized=false;closed();}
            }
            else if(bodyOpening)
            {
                main.localScale=Vector3.one*panels.PanelOpenCurve.Evaluate(Mathf.Clamp01(elapsed/panels.PanelDurationTime));
                bodyOpening=elapsed<panels.PanelDurationTime;
            }
        }
        private void OnDestroy(){OriginalUIAnimationDriver.Unregister(this);if(ownedMaterial!=null)Destroy(ownedMaterial);}
    }
}
