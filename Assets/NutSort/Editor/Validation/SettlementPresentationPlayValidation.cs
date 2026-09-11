using System;
using System.Collections.Generic;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class SettlementPresentationPlayValidation
    {
        private const string Key="NutSort.SettlementPresentation";
        private static double timeout;
        private static float ready;
        private static int phase,normal,more;
        private static bool allowClick=true,closed;
        private static OriginalSuccessPanel panel;
        static SettlementPresentationPlayValidation()
        {
            if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;}
        }
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"Level\":4,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Settlement presentation fixture");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Settlement presentation timeout");
                if(phase==0)
                {
                    var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                    var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                    if(game==null||startup==null||startup.MainLevel==null||game.InputBlocked)return;
                    panel=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/Panels/SuccessPanel"),startup.MainLevel.transform.parent,false).GetComponent<OriginalSuccessPanel>();
                    var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"),new OriginalGoldFormatter(()=>"en-US"),()=>"US");
                    // Explicit display-only fixture. No balance or server response is generated.
                    var info=new OriginalItemGetInfo{ItemInfos=new List<OriginalItemInfo>{new OriginalItemInfo{ItemType=0,Count=5,MoreCount=10},new OriginalItemInfo{ItemType=1,Count=3}}};
                    panel.InitSettlement(game.Tables,"en",()=>allowClick,s=>{},()=>closed=true,factory,info,b=>{if(b)more++;else normal++;},true,false,"Claim 2X");
                    ready=Time.time+.8f;phase=1;return;
                }
                if(Time.time<ready)return;
                if(phase==1)
                {
                    Check(panel.Views.Count==2&&panel.Views[0].Count.text=="$5"&&panel.Views[1].Count.text=="3","Original item prefab renders supplied rewards");
                    Check(panel.GetButton.gameObject.activeInHierarchy&&panel.MoreButton.gameObject.activeInHierarchy&&!panel.Ad.activeSelf,"Both claim controls without unsupported ad badge");
                    Check(panel.MoreLabel.text=="Claim 2X","Provider caption replaces the source fixed multiplier");
                    allowClick=false;panel.GetButton.onClick.Invoke();Check(normal==0,"Gate blocks claim");
                    allowClick=true;panel.GetButton.onClick.Invoke();panel.MoreButton.onClick.Invoke();
                    Check(normal==1&&more==1&&!panel.Closing,"Distinct claim callbacks wait for provider result");
                    ScreenCapture.CaptureScreenshot("Library/settlement-presentation-fixture.png");
                    ready=Time.time+.2f;phase=2;return;
                }
                if(phase==2){panel.ClosePanel();ready=Time.time+.5f;phase=3;return;}
                Check(closed,"Provider completion retains native close animation");
                Debug.Log("NUT_SETTLEMENT_PRESENTATION_PASS fixture rewards, native item prefabs, distinct gated claim callbacks and deferred close");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
