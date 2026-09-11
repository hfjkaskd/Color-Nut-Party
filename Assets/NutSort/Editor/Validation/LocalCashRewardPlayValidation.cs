using System;
using System.Collections.Generic;
using System.IO;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalCashRewardPlayValidation
    {
        private const string Key="NutSort.LocalCashReward";
        private static double timeout;
        private static int phase,index;
        private static float amount,ready;
        private static double diamondAmount;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        static LocalCashRewardPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin(Fixture());
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(720,1280,"Country cash reward integration");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static string Fixture()
        {
            var board=new OriginalBoardState(new LevelData{B=new[]{Rod(1),Rod(1,1,1)}},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=4,NewGameplayUnlockIndex=1,IsCompleteRecordGuide=true,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
            return OriginalUserDataJson.Write(user);
        }
        private static ScrewData Rod(params int[] colors)
        {var slots=new CData[4];for(int i=0;i<4;i++)slots[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};return new ScrewData{Id=1,C=slots};}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Local cash reward timeout");
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.InputBlocked||game.IsRestarting)return;
                var local=startup.LocalGameplay;
                if(phase==0)
                {
                    if(!game.IsInitDone)return;
                    foreach(var rod in game.Level.Board.Screws)foreach(var slot in rod.Slots)if(slot.Nut!=null&&game.Level.GetNut(slot.Nut).IsEntryAnimating)return;
                    var session=UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
                    session.ConfigureRewardCountry(index==0?"US":"IDN");
                    // Test early targets and the distinct last band without replacing live user state.
                    var sample=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=2};
                    Check((float)LocalCashSettlement.Prepare(sample,session.Rewards,(a,b)=>1)["amount"]==(index==0?.4f:400),"First reward target");
                    sample.LocalPendingReward=null;sample.Level=3;
                    Check((float)LocalCashSettlement.Prepare(sample,session.Rewards,(a,b)=>1)["amount"]==(index==0?.6f:600),"Second reward target");
                    sample.LocalPendingReward=null;sample.Level=4;sample.Gold=index==0?298:299000;
                    Check((float)LocalCashSettlement.Prepare(sample,session.Rewards,(a,b)=>1)["amount"]==(index==0?.01f:5),"Country-specific final decay band");
                    Physics.SyncTransforms();
                    Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(0).Bounds.bounds.center)).Kind==ScrewOperationKind.Ready,"Select real rod");
                    Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(1).Bounds.bounds.center)).Kind==ScrewOperationKind.Moved,"Real last move triggers settlement");phase=1;return;
                }
                if(phase==1)
                {
                    if(!local.AwaitingNext||Vector3.Distance(local.SuccessPanel.Title.localScale,Vector3.one)>.001f)return;
                    amount=(float)game.User.LocalPendingReward["amount"];
                    diamondAmount=(double)game.User.LocalPendingReward["diamonds"];
                    Check(diamondAmount>0,"Captured diamond reward present");
                    Check(amount>=(index==0?1.2f:1200)&&amount<=(index==0?1.8f:1800),"Captured initial band and random range");
                    Check(game.User.Gold==0&&game.PlayerLevel==5,"Preview does not grant and level advances once");
                    Check(local.SuccessPanel.Views.Count==2&&local.SuccessPanel.Views[0].ItemInfo.Count==amount,"Actual success panel contains reward item");
                    Check(local.SuccessPanel.Views[0].Icon.sprite!=null,"Country cash sprite resolves in actual Resources loader");
                    Check(local.SuccessPanel.Views[0].Count.text.Contains(index==0?"$":"Rp"),"Country-specific visible currency");
                    Check(local.NextButton.gameObject.activeInHierarchy&&!local.SuccessPanel.Ad.activeSelf,"Visible normal claim without unavailable ad offer");
                    ScreenCapture.CaptureScreenshot("Library/local-cash-"+(index==0?"BR":"ID")+".png");
                    ready=Time.time+.5f;phase=2;return;
                }
                if(phase==2)
                {
                    if(Time.time<ready)return;
                    phase=3;Reload();return;
                }
                if(phase==3)
                {
                    if(!local.AwaitingNext||Vector3.Distance(local.SuccessPanel.Title.localScale,Vector3.one)>.001f)return;
                    Check((float)game.User.LocalPendingReward["amount"]==amount&&game.User.Gold==0,"Unclaimed amount survives reload without reroll");
                    var reloadedSession=UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
                    Check(local.SuccessPanel.Views[0].Count.text.Contains(new OriginalGoldFormatter(()=>reloadedSession.DisplayCulture(game.Tables)).Format(amount)),"Pending numeric amount uses actual country display after reload");
                    game.User.NormalGetTimes=2;game.User.InterAdTimes=index==0?0:5;
                    Action<bool> adDone=null;int route=0;
                    local.BindSettlementAds(done=>{route=1;adDone=done;},done=>{route=2;adDone=done;});
                    local.NextButton.onClick.Invoke();local.NextButton.onClick.Invoke();
                    Check(route==(index==0?1:2)&&local.SettlementAdPending&&game.User.Gold==0,"Threshold routes interstitial then rewarded and waits for completion");
                    adDone(index!=0);adDone(true);
                    Check(!local.SettlementAdPending&&game.User.NormalGetTimes==0&&game.User.InterAdTimes==(index==0?1:6),"Native counters and duplicate completion guard");
                    Check(game.User.Coin==diamondAmount&&game.User.Gold==amount&&game.User.LocalPendingReward==null,"Claim grants exactly once");
                    string activeCulture=UnityEngine.Object.FindObjectOfType<OriginalUserSession>().DisplayCulture(game.Tables);
                    Check(local.RewardsTop.Gold.Value.text==new OriginalGoldFormatter(()=>activeCulture).Format(amount),"Actual claim refreshes header balance");
                    Check(local.SuccessPanel.Closing,"Claim closes native panel");
                    phase=4;Reload();return;
                }
                if(!game.IsInitDone)return;
                Check(game.User.Coin==diamondAmount&&game.User.Gold==amount&&game.PlayerLevel==5&&game.User.LocalPendingReward==null&&local.SuccessPanel==null,"Claim persists across reload during close without duplicate settlement");
                Check(game.User.TodayPassLevelCount==1&&!game.ModalInputBlocked,"Next board stays playable");
                if(index==0){index=1;phase=0;PlayerPrefs.SetString(OriginalUserStore.Key,Fixture());PlayerPrefs.Save();Reload();return;}
                Debug.Log("NUT_LOCAL_CASH_REWARD_PASS BR fallback and ID actual last move, visible item/currency, country bands, pending reload, one-time claim, persisted balance and next board");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Reload(){game=null;startup=null;SceneManager.LoadScene("LuoSiSortGame");}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
