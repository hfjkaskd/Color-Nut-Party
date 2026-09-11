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
    public static class LocalDiamondRewardPlayValidation
    {
        private const string Key="NutSort.LocalDiamondReward";
        private static double timeout;
        private static int phase,index;
        private static float amount,ready;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        static LocalDiamondRewardPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Tick;}}
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
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=5,LocalPendingReward=Newtonsoft.Json.Linq.JObject.Parse(@"{""country"":""BR"",""amount"":3.01,""diamonds"":81.19,""diamondSource"":""explicit-test-fixture"",""nextLevel"":5}"),NewGameplayUnlockIndex=1,IsCompleteRecordGuide=true,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
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
                    if(!local.AwaitingNext)return;
                    phase=1;return;
                }
                if(phase==1)
                {
                    if(!local.AwaitingNext||Vector3.Distance(local.SuccessPanel.Title.localScale,Vector3.one)>.001f)return;
                    amount=(float)game.User.LocalPendingReward["amount"];
                    Check(Math.Abs(amount-3.01f)<.001f,"Fixture cash");
                    Check(game.User.Gold==0&&game.PlayerLevel==5,"Preview does not grant and level advances once");
                    Check(local.SuccessPanel.Views.Count==2&&local.SuccessPanel.Views[0].ItemInfo.Count==amount,"Actual success panel contains reward item");
                    Check(local.SuccessPanel.Views[0].Icon.sprite!=null,"Country cash sprite resolves in actual Resources loader");
                    Check(local.SuccessPanel.Views[0].Count.text.Contains("$"),"Country-specific visible currency");
                    Check(local.NextButton.gameObject.activeInHierarchy&&!local.SuccessPanel.Ad.activeSelf,"Visible normal claim without unavailable ad offer");
                    Check(local.SuccessPanel.Views[1].Icon.sprite!=null&&Math.Abs(local.SuccessPanel.Views[1].ItemInfo.Count-81.19f)<.001,"Diamond icon and amount");
                    ScreenCapture.CaptureScreenshot("Library/local-diamond-settlement.png");
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
                    Check(Math.Abs(game.User.Coin-81.19)<.0001&&game.User.Gold==amount&&game.User.LocalPendingReward==null,"Claim grants exactly once");
                    string activeCulture=UnityEngine.Object.FindObjectOfType<OriginalUserSession>().DisplayCulture(game.Tables);
                    Check(local.RewardsTop.Gold.Value.text==new OriginalGoldFormatter(()=>activeCulture).Format(amount),"Actual claim refreshes header balance");
                    Check(local.SuccessPanel.Closing,"Claim closes native panel");
                    phase=4;Reload();return;
                }
                if(!game.IsInitDone)return;
                Check(Math.Abs(game.User.Coin-81.19)<.0001&&game.User.Gold==amount&&game.PlayerLevel==5&&game.User.LocalPendingReward==null&&local.SuccessPanel==null,"Claim persists across reload during close without duplicate settlement");
                Check(!game.ModalInputBlocked,"Next board stays playable");
                
                Debug.Log("NUT_LOCAL_DIAMOND_REWARD_PASS dual icons, pending reload, one-time dual claim and persisted balances");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Reload(){game=null;startup=null;SceneManager.LoadScene("LuoSiSortGame");}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
