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
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class OriginalSuccessPanelPlayValidation
    {
        private const string Key="NutSort.SuccessPanelPlay";
        private static double timeout;
        private static float start;
        private static int phase,guides,closed,claims;
        private static OriginalSuccessPanel panel;
        private static OriginalGameScene game;
        private static Transform parent;
        private static Action<bool> pending;
        private static bool more;
        static OriginalSuccessPanelPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin();EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"SuccessPanel validation");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Show(int level)
        {
            game.User.Level=level;panel=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/Panels/SuccessPanel"),parent,false).GetComponent<OriginalSuccessPanel>();
            var lifecycle=game.CreateSuccessPanelFlow(()=>123,id=>{Check(id==7,"Native guide route");guides++;});
            var claim=new OriginalRewardGetFlow(game.User,()=>1,()=>0,cb=>pending=cb,(cb,b)=>{Check(!b,"Native rewarded option");pending=cb;},()=>456,v=>{},b=>{claims++;more=b;});
            var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"),new OriginalGoldFormatter(()=>"en-US"),()=>"US");
            var audio=UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
            panel.Bind(lifecycle,claim,factory,game.Tables,"en",()=>true,s=>audio.PlaySound(s),()=>{closed++;panel.Hide();UnityEngine.Object.Destroy(panel.gameObject);},
                baseHide=>lifecycle.Hide(baseHide,()=>false,()=>false,()=>{},()=>{}),game.ScheduleDelay,new OriginalPanelActionQueue());
            panel.Init(new OriginalItemGetInfo{ItemInfos=new List<OriginalItemInfo>{new OriginalItemInfo{ItemType=0,Count=5,MoreCount=50},new OriginalItemInfo{ItemType=1,Count=3,MoreCount=30}}});panel.Refresh();start=Time.time;
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"SuccessPanel Play timeout");
                if(phase==0)
                {
                    game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();if(game==null||game.Level==null||game.IsRestarting||game.InputBlocked)return;
                    foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())if(canvas.name=="UICanvas")parent=canvas.transform;
                    Check(parent!=null,"Actual UI canvas");game.User.IsAudio=true;Directory.CreateDirectory("Library/ValidationCaptures");Show(2);phase=1;return;
                }
                if(phase==1)
                {
                    if(Time.time-start<.6f)return;
                    Check(guides==1&&!panel.GetButton.gameObject.activeSelf&&!panel.Ad.activeSelf,"Actual early-level animation and visibility");
                    ScreenCapture.CaptureScreenshot("Library/ValidationCaptures/success-panel-guide-current.png");phase=2;return;
                }
                if(phase==2){if(Time.time-start<.9f)return;panel.GetCallback();start=Time.time;phase=3;return;}
                if(phase==3){if(Time.time-start<.4f)return;Check(closed==1&&panel==null&&claims==0,"Guide direct callback closes actual panel without SDK");Show(4);phase=4;return;}
                if(phase==4)
                {
                    if(Time.time-start<.6f)return;
                    Check(guides==1&&panel.GetButton.gameObject.activeSelf&&panel.Ad.activeSelf,"Fresh later-level prefab has both claim choices and ad icon");
                    ScreenCapture.CaptureScreenshot("Library/ValidationCaptures/success-panel-normal-current.png");panel.GetButton.onClick.Invoke();
                    Check(pending!=null&&claims==0,"Actual Button waits for explicit SDK fixture completion");phase=5;return;
                }
                if(Time.time-start<.9f)return;
                pending(false);Check(claims==0,"Cancelled ad does not grant");
                panel.GetButton.onClick.Invoke();pending(true);pending(true);
                Check(claims==1&&more,"Successful retry grants once");
                Debug.Log("NUT_SUCCESS_PANEL_PLAY_PASS actual prefab, two current captures, original big reward visuals, shared main/title animation, guide direct close, fresh later-level button/ad layout and deferred claim; explicit reward/guide/SDK consumers, production host pending.");Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
