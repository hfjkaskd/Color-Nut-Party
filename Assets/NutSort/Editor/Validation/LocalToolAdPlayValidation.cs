using System;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalToolAdPlayValidation
    {
        private const string Key="NutSort.ToolAd";
        private static int phase,requests;
        private static float ready;
        private static double deadline;
        private static Action<bool> completion;

        static LocalToolAdPlayValidation()
        {
            if(!SessionState.GetBool(Key,false))return;
            deadline=EditorApplication.timeSinceStartup+90;
            EditorApplication.update+=Tick;
        }

        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"IsCompleteRecordGuide\":true,\"Level\":4,\"NewGameplayUnlockIndex\":1,\"ExchangeCount\":0,\"AddScrewCount\":0}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(720,1280,"Tool ad callback validation");
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Timeout");
                var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||!game.IsInitDone||game.IsRestarting||game.InputBlocked||Time.time<ready||startup.Replay.IsClickMasked)return;
                var local=startup.LocalGameplay;
                switch(phase)
                {
                    case 0:
                        local.BindRewardedToolAds((type,done)=>{requests++;completion=done;});
                        local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();
                        Check(local.ToolOffer!=null&&requests==0,"Existing offer opens before requesting an ad");
                        Next();return;
                    case 1:
                        var claim=local.ToolOffer.ClaimButton;
                        claim.onClick.Invoke();claim.onClick.Invoke();
                        Check(requests==1&&local.ToolAdPending&&game.ModalInputBlocked&&game.User.ExchangeCount==0,"Pending ad guards duplicates and grants nothing");
                        completion(false);completion(true);
                        Check(!local.ToolAdPending&&game.User.ExchangeCount==0,"Failure and late success grant nothing");
                        Next();return;
                    case 2:
                        Check(!game.ModalInputBlocked,"Failure releases gameplay input");
                        local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();Next();return;
                    case 3:
                        local.ToolOffer.ClaimButton.onClick.Invoke();
                        completion(true);completion(true);
                        Check(requests==2&&game.User.ExchangeCount==1&&game.IsExchanging&&!local.ToolAdPending,"Successful retry grants exactly one tool");
                        Next();return;
                    case 4:
                        local.ExchangeMask.onClick.Invoke();Next();return;
                    case 5:
                        local.BindRewardedToolAds(null);
                        local.Bottom.GetOtherItem(4).Display.Click.onClick.Invoke();Next();return;
                    case 6:
                        local.ToolOffer.ClaimButton.onClick.Invoke();
                        Check(!local.ToolAdPending&&game.User.CurrentLevelAddScrewCount==0&&game.User.AddScrewCount==0,"Missing provider fails closed without simulated success");
                        Next();return;
                    case 7:
                        Check(!game.ModalInputBlocked,"No-fill path releases gameplay input");
                        local.BindRewardedToolAds((type,done)=>{Check(type==4,"Add screw rewarded placement");requests++;completion=done;});
                        local.Bottom.GetOtherItem(4).Display.Click.onClick.Invoke();Next();return;
                    case 8:
                        local.ToolOffer.ClaimButton.onClick.Invoke();
                        completion(true);completion(true);
                        Check(requests==3&&!local.ToolAdPending&&game.User.CurrentLevelAddScrewCount==4&&game.User.AddScrewCount==0,"SDK success alone unlocks rod and consumes reward once");
                        Next();return;
                }
                Debug.Log("NUT_LOCAL_TOOL_AD_PASS actual offer/buttons, failure/no-grant, pending duplicate guards, stale callbacks, success/once grant, missing provider/no simulation, world rod unlock");
                Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }

        private static void Next(){ready=Time.time+.7f;phase++;}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code)
        {
            OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick;EditorApplication.Exit(code);
        }
    }
}
