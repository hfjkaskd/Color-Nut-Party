using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string rewardsTopPrefabPath;
        private OriginalMainTopView rewardsTop;
        private OriginalRewardProgress topProgress;
        private OriginalGoldFormatter topFormatter;
        private string topCountry;
        private JObject projectedReward;
        public OriginalMainTopView RewardsTop=>rewardsTop;

        private void BindRewardsTop()
        {
            ProjectRewardStages();
            topCountry=audio.UserState.DisplayCountry(game.Tables);
            topFormatter=new OriginalGoldFormatter(()=>game.Tables.Countries.Get(topCountry).CountryLanguageCode);
            rewardsTop=Instantiate(Resources.Load<GameObject>(rewardsTopPrefabPath),startup.MainLevel.transform,false).GetComponent<OriginalMainTopView>();
            topProgress=new OriginalRewardProgress(game.User,game.Tables,n=>topFormatter.Format(n,game.Tables.Countries.Get(topCountry).CountryLanguageCode));
            rewardsTop.Gold.Bind(game.User,game.Tables,language,n=>topFormatter.Format(n,game.Tables.Countries.Get(topCountry).CountryLanguageCode),()=>topCountry,
                TryTopClick,done=>done(),id=>ShowTopRequirement(false),()=>audio.PlaySound(gameplaySession.SuccessPanelSound));
            rewardsTop.Coin.Bind(game.User,game.Tables,language,topFormatter,TryTopClick,done=>done(),
                id=>ShowTopRequirement(true),()=>audio.PlaySound(gameplaySession.SuccessPanelSound));
            rewardsTop.Hidden.Bind(game.User,game.Tables,language);
            rewardsTop.Hidden.Init();
            // Local buttons show their current requirements; no remote withdrawal is implied.
            rewardsTop.Gold.Click.onClick.RemoveAllListeners();
            rewardsTop.Gold.Click.onClick.AddListener(()=>{if(TryTopClick())ShowTopRequirement(false);});
            rewardsTop.Coin.Click.onClick.RemoveAllListeners();
            rewardsTop.Coin.Click.onClick.AddListener(()=>{if(TryTopClick())ShowTopRequirement(true);});
            audio.UserState.RewardConfigurationChanged+=RefreshRewardsTop;
            RefreshRewardsTop();
        }
        private bool TryTopClick()
        {
            if(game.InputBlocked||game.IsRestarting||HasLocalModal||startup.Replay.IsClickMasked)return false;
            return startup.Replay.BeginClick();
        }
        private void ShowTopRequirement(bool coin)
        {
            OpenLocalWithdrawal(coin);
        }
        public void RefreshRewardsTop()
        {
            if(rewardsTop==null)return;
            ProjectRewardStages();
            bool changed=topCountry!=audio.UserState.DisplayCountry(game.Tables);
            topCountry=audio.UserState.DisplayCountry(game.Tables);
            if(changed)rewardsTop.Gold.RefreshIcon();
            rewardsTop.Gold.RefreshGold();
            rewardsTop.Coin.RefreshCoin();
            rewardsTop.Coin.gameObject.SetActive(true);
            var stage=OriginalRewardProgressData.Read(game.User.GoldRewardTargetS2CData);
            bool hasHint=game.User.Level<=stage.Stage2RealLevel||!string.IsNullOrEmpty(game.User.TXTargetGold);
            rewardsTop.Gold.GoldHintText.text=hasHint?topProgress.GetDescription(language).Replace("\n"," "):string.Empty;
            rewardsTop.Gold.GoldHintText.transform.parent.gameObject.SetActive(hasHint);
            rewardsTop.Hidden.Refresh();
            rewardsTop.Hidden.SubRounds.SetActive(false); // The existing local subround strip owns this display.
            startup.MainLevel.Refresh(game.Tables,game.PlayerLevel,language,rewardsTop.Hidden.gameObject.activeSelf,false);
        }
        private void ProjectRewardStages()
        {
            JObject document=game.User.GoldRewardTargetS2CData;
            if(ReferenceEquals(projectedReward,document))return;
            var row=(JObject)document["bear_list"][2];
            string[] stages=((string)row["caliper_gates_rule"]).Split(',');
            int first=int.Parse(stages[0].Split('-')[1],CultureInfo.InvariantCulture);
            int second=int.Parse(stages[1].Split('-')[1],CultureInfo.InvariantCulture);
            int firstShow=game.Tables.GetShowLevel(first),secondShow=game.Tables.GetShowLevel(second);
            int firstStart=first,secondStart=second;
            while(firstStart>1&&game.Tables.GetShowLevel(firstStart-1)==firstShow)firstStart--;
            while(secondStart>1&&game.Tables.GetShowLevel(secondStart-1)==secondShow)secondStart--;
            row["StartLevel"]=firstStart;row["RealLevel"]=first;
            row["Stage2StartRealLevel"]=secondStart;row["Stage2RealLevel"]=second;
            row["Stage2StartShowLevel"]=secondShow;
            projectedReward=document;
        }
    }
}
