using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string withdrawalPrefabPath,withdrawalCoinIconPath,withdrawalServiceUnavailable;
        [SerializeField] private string diamondWithdrawalPrefabPath;
        private LocalDiamondWithdrawalPanel diamondWithdrawalPanel;
        public LocalDiamondWithdrawalPanel DiamondWithdrawalPanel=>diamondWithdrawalPanel;
        private OriginalWithdrawalPanel withdrawalPanel;
        public OriginalWithdrawalPanel WithdrawalPanel=>withdrawalPanel;
        private void OpenLocalWithdrawal(bool coin)
        {
            if(withdrawalPanel!=null||diamondWithdrawalPanel!=null)return;
            RefreshWithdrawalCalendar();
            LocalAnalytics.Current?.BeginWithdrawal(coin,0);
            if(coin){OpenDiamondWithdrawal();return;}
            LocalAnalytics.Current?.ObserveEligibility(LocalWithdrawalPolicy.Evaluate(game.User,game.Tables,false,0),"panel_open");
            RefreshRewardsTop();
            string country=audio.UserState.DisplayCountry(game.Tables);
            var formatter=new OriginalGoldFormatter(()=>game.Tables.Countries.Get(country).CountryLanguageCode);
            string hint,amount,title,progressText,progressHint;
            double current,total;
            {
                var data=OriginalRewardProgressData.Read(game.User.GoldRewardTargetS2CData);
                current=Math.Max(0,game.Tables.GetShowLevel(game.User.Level)-1);
                total=game.Tables.GetShowLevel(game.User.Level<=data.RealLevel?data.RealLevel:data.Stage2RealLevel);
                bool cashTarget=false;
                if(game.User.Level>data.Stage2RealLevel&&!string.IsNullOrEmpty(game.User.TXTargetGold))
                {
                    int stage=topProgress.GetStage(game.User.Level);
                    if(stage==4){current=game.User.Gold;total=double.Parse(game.User.TXTargetGold,CultureInfo.InvariantCulture);cashTarget=true;}
                    else if(stage==5){current=game.User.TodayPassLevelCount;total=data.DailyLevels;}
                    else if(stage==6){current=game.User.LoginDay;total=data.LoginDays;}
                    else if(stage==7){current=game.User.UserLevel;total=data.UserLevel;}
                }
                hint=game.User.Level<=data.Stage2RealLevel?game.Tables.Text.GetText(22,language,(int)total):rewardsTop.Gold.GoldHintText.text;
                if(string.IsNullOrEmpty(hint))hint=LocalUiLocalization.Text(withdrawalServiceUnavailable);
                amount=formatter.Format(game.User.Gold);
                title=game.Tables.Text.GetText(30,language,game.Tables.GetShowLevel(game.User.Level));
                progressText=string.Format(CultureInfo.InvariantCulture,"{0:0}/{1:0}",Math.Min(current,total),total);
                progressHint=game.Tables.Text.GetText(25,language,Math.Max(0,total-current).ToString("0",CultureInfo.InvariantCulture));
                if(cashTarget){progressText=formatter.Format((float)Math.Min(current,total))+"/"+formatter.Format((float)total);progressHint=formatter.Format((float)Math.Max(0,total-current));}
                else if(game.User.Level>data.Stage2RealLevel)progressHint=Math.Max(0,total-current).ToString("0",CultureInfo.InvariantCulture);
            }
            game.ModalInputBlocked=true;
            withdrawalPanel=Instantiate(Resources.Load<GameObject>(withdrawalPrefabPath),transform.parent,false).GetComponent<OriginalWithdrawalPanel>();
            withdrawalPanel.InitLocal(game.Tables,language,country,coin?Resources.Load<Sprite>(withdrawalCoinIconPath):null,
                amount,title,hint,progressText,progressHint,total>0?(float)(current/total):0,
                TryWithdrawalClick,BeginCashWithdrawal,
                CloseLocalWithdrawal,s=>audio.PlaySound(s),player=>InitializeLocalStats(player,game.Tables.GetShowLevel(game.User.Level)));
            withdrawalPanel.SetLocalHistoryAction(OpenCashHistory,EarningText("historyEntry"));
        }
        private void InitializeLocalStats(OriginalPlayerInfo player,int displayLevel)
        {
            var marquee=OriginalMarqueeData.Read(audio.UserState.Rewards.CreateDisplayDocument());
            marquee.Init();
            string culture=audio.UserState.DisplayCulture(game.Tables);
            var snapshot=OriginalSimulatedStats.Create(displayLevel,game.User,marquee,player.SimulatedStatsSettings,
                culture,DateTime.Now,DateTime.UtcNow);
            var formatter=new OriginalGoldFormatter(()=>culture);
            player.InitSimulated(snapshot,game.Tables,language,value=>formatter.Format(value));
            if(snapshot.DisplayStateChanged)audio.UserState.Store.SaveData(false,null);
        }
        private void OpenDiamondWithdrawal()
        {
            game.ModalInputBlocked=true;
            string country=audio.UserState.DisplayCountry(game.Tables);
            diamondWithdrawalPanel=Instantiate(Resources.Load<GameObject>(diamondWithdrawalPrefabPath),transform.parent,false).GetComponent<LocalDiamondWithdrawalPanel>();
            diamondWithdrawalPanel.Init(game.User,game.Tables,language,new OriginalGoldFormatter(()=>game.Tables.Countries.Get(country).CountryLanguageCode),
                ()=>!game.InputBlocked&&!game.IsRestarting&&!startup.Replay.IsClickMasked&&startup.Replay.BeginClick(),
                ()=>{Destroy(diamondWithdrawalPanel.gameObject);diamondWithdrawalPanel=null;StartCoroutine(ReleaseWithdrawalInput());},
                startup.Replay.EndClick,ShowWithdrawalFeedback,BeginDiamondWithdrawal);
        }
        private void ShowWithdrawalFeedback(string message)
        {
            var tip=Instantiate(Resources.Load<GameObject>(popTipPrefabPath),startup.TopCanvas,false).GetComponent<OriginalPopTip>();
            tip.Show(LocalUiLocalization.Text(message),game.ScheduleDelay);
        }
        private bool TryWithdrawalClick()
        {
            if(withdrawalPanel==null||withdrawalPanel.Closing||game.InputBlocked||game.IsRestarting||startup.Replay.IsClickMasked)return false;
            return startup.Replay.BeginClick();
        }
        private void CloseLocalWithdrawal()
        {
            Destroy(withdrawalPanel.gameObject);withdrawalPanel=null;
            StartCoroutine(ReleaseWithdrawalInput());
        }
        private IEnumerator ReleaseWithdrawalInput()
        {
            yield return null;
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
        }
    }
}
