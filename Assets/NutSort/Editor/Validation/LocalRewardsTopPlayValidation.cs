using System;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalRewardsTopPlayValidation
    {
        private const string Key="NutSort.LocalRewardsTop";
        private static double timeout;
        private static int phase;
        private static float ready;
        static LocalRewardsTopPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"Level\":4,\"Gold\":557.58,\"Coin\":0,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(720,1280,"Live main rewards header");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Rewards top timeout");
                var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null||game==null||startup.LocalGameplay==null||game.InputBlocked||Time.time<ready)return;
                var local=startup.LocalGameplay;var top=local.RewardsTop;
                var session=UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
                if(phase==0)
                {
                    Check(top!=null&&top.Gold.gameObject.activeInHierarchy&&top.Coin.gameObject.activeInHierarchy,"Actual boot mounts both currency displays");
                    session.ConfigureRewardCountry("BR");local.RefreshRewardsTop();ready=Time.time+2f;phase=1;return;
                }
                if(phase==1)
                {
                    Check(top.Gold.Value.text==new OriginalGoldFormatter(()=>"pt-BR").Format(557.58f),"Live cash balance with Brazilian display");
                    Check(top.Coin.Value.text=="0"&&top.Gold.Icon.sprite!=null&&top.Coin.Icon.sprite!=null,"Zero coin and icons render");
                    Check(top.Hidden.gameObject.activeInHierarchy&&top.Hidden.Levels.Length==5,"Five milestones visible at level four");
                    for(int i=0;i<5;i++)Check(top.Hidden.Levels[i].Level.gameObject.activeInHierarchy&&top.Hidden.Levels[i].Level.text==(i+1).ToString(),"Milestone number visibly rendered");
                    Check(Mathf.Abs(top.Hidden.Progress.fillAmount-.75f)<.001f,"Fourth milestone selected from actual level mapping");
                    Check(!string.IsNullOrEmpty(top.Gold.GoldHintText.text)&&top.Coin.HintText.text.Contains("500"),"Both captured reward requirements visible");
                    ScreenCapture.CaptureScreenshot("Library/local-rewards-top-BR.png");ready=Time.time+.4f;phase=2;return;
                }
                if(phase==2)
                {
                    session.ConfigureRewardCountry("IDN");local.RefreshRewardsTop();ready=Time.time+.4f;phase=3;return;
                }
                if(phase==3)
                {
                    Check(top.Gold.Value.text.Contains("Rp")&&top.Coin.HintText.text.Contains("Rp"),"Indonesian balances and requirement amounts");
                    ScreenCapture.CaptureScreenshot("Library/local-rewards-top-ID.png");ready=Time.time+.4f;phase=4;return;
                }
                if(phase==4)
                {
                    game.User.Gold=1234;game.User.Coin=25000;local.RefreshRewardsTop();
                    Check(top.Gold.Value.text==new OriginalGoldFormatter(()=>"id-ID").Format(1234)&&top.Coin.Value.text!="0","Refresh uses live balances");
                    top.Gold.Click.onClick.Invoke();
                    Check(local.WithdrawalPanel!=null&&game.ModalInputBlocked,"Cash Button opens withdrawal and blocks board input");
                    Check(game.User.Gold==1234&&game.User.Coin==25000,"Requirements click does not withdraw or grant");
                    ready=Time.time+.5f;phase=5;return;
                }
                if(phase==5){local.WithdrawalPanel.CloseButton.onClick.Invoke();ready=Time.time+.7f;phase=6;return;}
                Check(local.WithdrawalPanel==null&&!game.ModalInputBlocked,"Closing withdrawal restores input");
                game.User.Level=1;local.RefreshRewardsTop();Check(top.Hidden.gameObject.activeSelf,"First level milestones visible");
                game.User.Level=8;local.RefreshRewardsTop();Check(top.Hidden.Level5.text=="5","Internal subround maps to displayed milestone five");
                game.User.Level=52;local.RefreshRewardsTop();Check(!top.Hidden.gameObject.activeSelf&&startup.MainLevel.Group.gameObject.activeSelf,"Later progression restores ordinary level display");
                Debug.Log("NUT_LOCAL_REWARDS_TOP_PASS actual startup, BR/ID currency and hints, live balances, five milestones, stage transitions and standard Button input");Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
