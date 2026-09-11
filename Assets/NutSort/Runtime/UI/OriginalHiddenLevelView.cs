using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class OriginalHiddenLevelView : MonoBehaviour
    {
        [Serializable] public struct Marker { public Image Background; public TextMeshProUGUI Level; public GameObject Get; }
        [SerializeField] private Marker[] levels;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private TextMeshProUGUI round4, round5, level5;
        [SerializeField] private Image progress;
        [SerializeField] private GameObject subRounds, subRoundTemplate;
        [SerializeField] private Sprite[] backgrounds;
        [SerializeField] private Sprite pendingDot, passedDot;
        [SerializeField] private float blinkDuration = 1, blinkAlpha = .5f;
        [SerializeField] private bool showDuringLocalProgress;
        private OriginalUserLocalData user;
        private OriginalTables tables;
        private OriginalRewardProgress reward;
        private readonly List<GameObject> dots = new List<GameObject>();
        private Image blinking;
        private float blinkStart;
        private Action<float> animationStep;
        public Image Progress => progress;
        public TextMeshProUGUI Round4 => round4;
        public TextMeshProUGUI Round5 => round5;
        public TextMeshProUGUI Level5 => level5;
        public Marker[] Levels => levels;
        public GameObject SubRounds => subRounds;
        public GameObject SubRoundTemplate => subRoundTemplate;
        public int DotCount => dots.Count;

        public void Bind(OriginalUserLocalData data, OriginalTables originalTables, string language)
        {
            user = data ?? throw new ArgumentNullException(nameof(data));
            tables = originalTables ?? throw new ArgumentNullException(nameof(originalTables));
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            reward = new OriginalRewardProgress(user,tables,n => n.ToString());
        }
        public void Init() { subRoundTemplate.SetActive(false); }
        public void Refresh() { RefreshLevels(); RefreshRound(); RefreshSubRound(); }

        public void RefreshLevels()
        {
            var row=OriginalRewardProgressData.Read(user.GoldRewardTargetS2CData);
            if(user.Level>row.Stage2RealLevel || (!showDuringLocalProgress&&(user.Level<2 || reward.IsGuidePassStage2Level())))
            { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            if(user.Level>row.RealLevel)
            {
                int show=tables.GetShowLevel(user.Level);
                for(int i=0;i<levels.Length-1;i++)
                {
                    int start=(i+1)*5, span=i<3?5:10;
                    levels[i].Level.text=start.ToString(); levels[i].Get.SetActive(false);
                    int state;
                    if(show>=start+span)state=2;
                    else if(show<start)state=1;
                    else { state=3; progress.fillAmount=i*.25f+(show-start)/(i==3?10f:5f)*.25f; }
                    levels[i].Background.sprite=backgrounds[state-1];
                }
                return;
            }
            var info=tables.GetLevelInfo(user.Level,user.Level);
            for(int i=0;i<levels.Length;i++)
            {
                int number=i+1; levels[i].Level.text=number.ToString();
                if(i==levels.Length-1)continue;
                levels[i].Get.SetActive(number>=user.Level && i!=2);
                int state=number<info.ShowLevel?2:number>info.ShowLevel?1:3;
                if(state==3)progress.fillAmount=i*.25f;
                levels[i].Background.sprite=backgrounds[state-1];
            }
            if(user.Level>=row.StartLevel)progress.fillAmount=1;
        }

        public void RefreshRound()
        {
            var info=tables.GetLevelInfo(user.Level,user.Level);
            if(info==null || info.TotalRound<=0)
            { round4.transform.parent.gameObject.SetActive(false); round5.transform.parent.gameObject.SetActive(false); }
            // The original continues after hiding both parents, including its
            // null-row failure; no fabricated level is substituted here.
            if(info.ShowLevel==4)
            {
                round4.text=string.Format("{0}/{1}",info.Round,info.TotalRound);
                round4.transform.parent.gameObject.SetActive(info.Round>1);
                round5.transform.parent.gameObject.SetActive(false); return;
            }
            round4.transform.parent.gameObject.SetActive(false);
            var row=OriginalRewardProgressData.Read(user.GoldRewardTargetS2CData);
            if(user.Level<=row.StartLevel)
            { level5.text=5.ToString(); round5.transform.parent.gameObject.SetActive(false); return; }
            if(user.Level<=row.RealLevel)level5.text=string.Format("{0}",5);
            else if(user.Level<=row.Stage2StartRealLevel)
            { level5.text=row.Stage2StartShowLevel.ToString(); round5.transform.parent.gameObject.SetActive(false); return; }
            else if(user.Level<=row.Stage2RealLevel)level5.text=string.Format("{0}",row.Stage2StartShowLevel);
            else return;
            round5.transform.parent.gameObject.SetActive(info.Round>1);
            round5.text=string.Format("{0}/{1}",info.Round,info.TotalRound);
        }

        public void RefreshSubRound()
        {
            var info=tables.GetLevelInfo(user.Level,user.Level);
            if(info==null || info.SubTotalRound<=0) { subRounds.SetActive(false); return; }
            subRounds.SetActive(true);
            // Original retains the list entries after destroying surplus objects.
            // Keep that observable behavior rather than silently inventing reuse.
            for(int i=info.SubTotalRound;i<dots.Count;i++)Destroy(dots[i]);
            while(dots.Count<info.SubTotalRound)
            {
                var dot=Instantiate(subRoundTemplate,subRoundTemplate.transform.parent);
                dot.SetActive(true); dots.Add(dot);
            }
            for(int i=0;i<dots.Count;i++)
            {
                Transform dot=dots[i].transform;
                Transform left=dot.Find("Line0"), right=dot.Find("Line1");
                if(i==0)left.gameObject.SetActive(false);
                else if(i==dots.Count-1)right.gameObject.SetActive(false);
                else { left.gameObject.SetActive(true); right.gameObject.SetActive(true); }
                var icon=dot.Find("Icon").GetComponent<Image>();
                icon.color=Color.white;
                if(i==info.SubRound-1)
                {
                    blinking=icon; blinkStart=Time.time;
                    if(animationStep==null)animationStep=Advance;
                    OriginalUIAnimationDriver.Register(this,animationStep);
                }
                icon.sprite=i<info.SubRound?passedDot:pendingDot;
            }
        }
        private void OnDestroy() { OriginalUIAnimationDriver.Unregister(this); }
        public void Advance(float delta) { UpdateBlink(); }
        private void OnEnable() { UpdateBlink(); }
        private void UpdateBlink()
        {
            if(blinking==null)return;
            float t=Mathf.PingPong(Time.time-blinkStart,blinkDuration)/blinkDuration;
            // DOTween's default OutQuad, 1 second, infinite Yoyo.
            var color=blinking.color; color.a=Mathf.Lerp(1,blinkAlpha,1-(1-t)*(1-t)); blinking.color=color;
        }
    }
}
