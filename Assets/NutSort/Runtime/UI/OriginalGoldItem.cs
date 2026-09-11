using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class OriginalGoldItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI value, goldHintText;
        [SerializeField] private Button click;
        [SerializeField] private Transform target;
        [SerializeField] private OriginalGoldItemSettings settings;
        [SerializeField] private OriginalGoldImage goldImage;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        private OriginalUserLocalData user;
        private OriginalRewardProgress progress;
        private Func<float,string> formatGold;
        private Func<string> country;
        private Func<bool> beginClick;
        private Action<Action> requestReward;
        private Action<int> showPanel;
        private Action clickSound, refreshTop, rewardReady;
        private string language;
        private struct Pulse { public float Elapsed, Gold; public Vector3 Start; public bool Started; }
        private struct FloatPair
        {
            public Image Icon;
            public TextMeshProUGUI Text;
            public Vector3 IconStart, TextStart;
            public Color IconColor, TextColor;
            public float Elapsed;
        }
        private readonly List<Pulse> pulses = new List<Pulse>(4);
        private readonly List<FloatPair> floats = new List<FloatPair>(4);
        public Button Click => click;
        public TextMeshProUGUI Value => value;
        public TextMeshProUGUI GoldHintText => goldHintText;
        public Image Icon => icon;
        public int PulseCount => pulses.Count;
        public int FloatingPairCount => floats.Count;

        public void Bind(OriginalUserLocalData data, OriginalTables tables, string languageCode,
            Func<float,string> currencyFormatter, Func<string> currentCountry, Func<bool> tryClick, Action<Action> requestGoldReward,
            Action<int> openPanel, Action playClick, Action refreshMainTop = null)
        {
            user = data ?? throw new ArgumentNullException(nameof(data));
            formatGold = currencyFormatter ?? throw new ArgumentNullException(nameof(currencyFormatter));
            country = currentCountry ?? throw new ArgumentNullException(nameof(currentCountry));
            goldImage.Bind(country);
            beginClick = tryClick ?? throw new ArgumentNullException(nameof(tryClick));
            requestReward = requestGoldReward ?? throw new ArgumentNullException(nameof(requestGoldReward));
            showPanel = openPanel ?? throw new ArgumentNullException(nameof(openPanel));
            clickSound = playClick ?? throw new ArgumentNullException(nameof(playClick));
            refreshTop = refreshMainTop; language = languageCode;
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            progress = new OriginalRewardProgress(user,tables,formatGold);
            rewardReady = RewardReady;
        }

        public void Init()
        {
            // GoldItem explicitly opts out of AddLSSListener's press tween.
            click.transition = Selectable.Transition.ColorTint;
            click.onClick.RemoveAllListeners(); click.onClick.AddListener(Clicked);
            HideGoldHintText(); RefreshHint();
        }
        private void Clicked()
        {
            if(!beginClick())return;
            requestReward(rewardReady);
            clickSound();
        }
        private void RewardReady() { showPanel(18); }
        public void HideGoldHintText() { goldHintText.transform.parent.gameObject.SetActive(false); }
        public void ShowGoldHintText()
        {
            var data = OriginalRewardProgressData.Read(user.GoldRewardTargetS2CData);
            goldHintText.transform.parent.gameObject.SetActive(user.Level <= data.Stage2RealLevel);
        }
        public void RefreshHint()
        {
            var data = OriginalRewardProgressData.Read(user.GoldRewardTargetS2CData);
            if(user.Level > data.Stage2RealLevel)HideGoldHintText();
            goldHintText.text = progress.GetDescription(language).Replace("\n"," ");
            // Source skips both dependent refreshes when MainPanel is absent.
            refreshTop?.Invoke();
        }
        public void Refresh() { RefreshGold(); }
        public void RefreshIcon() { goldImage.Apply(); }
        public void RefreshGold(bool showTip = false, float addGold = 0)
        {
            value.text = formatGold(user.Gold);
            if(!showTip)return;
            RefreshHint();
            pulses.Add(new Pulse { Gold = addGold });
            Schedule();
        }
        private Action<float> animationStep;
        private void Schedule()
        {
            if(animationStep==null)animationStep=Advance;
            OriginalUIAnimationDriver.Register(this,animationStep);
        }
        private void OnDestroy() { OriginalUIAnimationDriver.Unregister(this); }

        public void Advance(float delta)
        {
            if(delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
            if(delta==0)return;
            for(int i=0;i<floats.Count;)
            {
                FloatPair pair=floats[i]; pair.Elapsed+=delta;
                float t=Mathf.Clamp01(pair.Elapsed/settings.FloatDuration);
                Vector3 p=pair.IconStart; p.y=Mathf.Lerp(p.y,settings.FloatTargetY,t); pair.Icon.transform.localPosition=p;
                p=pair.TextStart; p.y=Mathf.Lerp(p.y,settings.FloatTargetY,t); pair.Text.transform.localPosition=p;
                float cubic=t*t*t;
                pair.Icon.color=Color.LerpUnclamped(pair.IconColor,settings.FloatColor,cubic);
                pair.Text.color=Color.LerpUnclamped(pair.TextColor,settings.FloatColor,cubic);
                if(t>=1) { Destroy(pair.Icon.gameObject); Destroy(pair.Text.gameObject); floats.RemoveAt(i); }
                else { floats[i]=pair; i++; }
            }
            for(int i=0;i<pulses.Count;)
            {
                Pulse pulse=pulses[i]; pulse.Elapsed+=delta;
                float time=pulse.Elapsed-settings.PulseDelay;
                if(time<=0) { pulses[i]=pulse; i++; continue; }
                if(!pulse.Started)
                {
                    pulse.Started=true; pulse.Start=target.localScale;
                    SpawnFloat(pulse.Gold);
                }
                if(time>=settings.PulseDuration*settings.PulseLoops)
                {
                    target.localScale=Vector3.one; pulses.RemoveAt(i); continue;
                }
                int loop=(int)(time/settings.PulseDuration);
                float t=(time-loop*settings.PulseDuration)/settings.PulseDuration;
                if((loop&1)!=0)t=1-t;
                target.localScale=Vector3.LerpUnclamped(pulse.Start,Vector3.one*settings.PulseScale,Mathf.Sin(t*Mathf.PI*.5f));
                pulses[i]=pulse; i++;
            }
            if(pulses.Count==0 && floats.Count==0)OriginalUIAnimationDriver.Unregister(this);
        }
        private void SpawnFloat(float addGold)
        {
            // Source copies the two already-configured visual objects once per
            // low-frequency reward presentation, not per pulse loop or frame.
            Image image=Instantiate(icon,icon.transform.parent,false);
            image.GetComponent<OriginalGoldImage>().Bind(country);
            TextMeshProUGUI text=Instantiate(value,value.transform.parent,false);
            Vector3 ip=image.transform.localPosition; ip.x+=settings.IconOffsetX; ip.z=0;
            Vector3 tp=text.transform.localPosition; tp.x+=settings.TextOffsetX; tp.z=0;
            image.transform.localPosition=ip; text.transform.localPosition=tp;
            image.transform.localScale=text.transform.localScale=Vector3.one*settings.FloatScale;
            text.text="+"+formatGold(addGold);
            floats.Add(new FloatPair { Icon=image,Text=text,IconStart=ip,TextStart=tp,IconColor=image.color,TextColor=text.color });
        }
    }
}
