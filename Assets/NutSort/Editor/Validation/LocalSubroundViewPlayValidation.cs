using System;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalSubroundViewPlayValidation
    {
        private const string Key="NutSort.LocalSubroundView";
        private static readonly int[] levels={8,9,10,12,13,22,23};
        private static int index,phase;
        private static double deadline,pausedAt;
        private static float waitUntil,alpha;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalHiddenLevelView retained;
        private static Image blinking;
        static LocalSubroundViewPlayValidation(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=8,NewGameplayUnlockIndex=2};
            user.IsCompleteRecordGuide=true;
            OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Local source subround dots");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Local subround view timeout");
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone||Time.time<waitUntil)return;
                var view=startup.LocalGameplay.SubroundView;
                Check(game.User.GoldRewardTargetS2CData!=null&&game.User.Gold==0&&game.User.Coin==0,"Captured config loaded without an unsolicited grant");
                Check(!game.ModalInputBlocked,"Subround strip is nonmodal");
                if(phase==4){Next();return;}
                if(phase==2)
                {
                    if(EditorApplication.timeSinceStartup-pausedAt<.4)return;
                    Check(blinking.color.a==alpha,"Native blink pauses with scaled clock");Time.timeScale=1;phase=3;waitUntil=Time.time+.35f;return;
                }
                if(phase==3)
                {
                    Check(Mathf.Abs(blinking.color.a-alpha)>.01f,"Native blink resumes");Next();return;
                }
                if(phase==1)
                {
                    if(view!=null&&view.SubRounds.activeSelf)
                    {
                        foreach(var rod in game.Level.Board.Screws)foreach(var slot in rod.Slots)
                            if(slot.Nut!=null&&game.Level.GetNut(slot.Nut).IsEntryAnimating)return;
                        ScreenCapture.CaptureScreenshot("Library/local-subround-dots-"+game.PlayerLevel+"-current.png");
                        Check(blinking.color.a>=.49f&&blinking.color.a<.999f,"Source current dot blinks between .5 and 1");
                        if(game.PlayerLevel==9)
                        {
                            Physics.SyncTransforms();
                            Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(0).Bounds.bounds.center)).Kind==ScrewOperationKind.Ready,"Native world selection remains available under subround UI");
                            alpha=blinking.color.a;Time.timeScale=0;pausedAt=EditorApplication.timeSinceStartup;phase=2;return;
                        }
                    }
                    phase=4;waitUntil=Time.time+.1f;return; // let the screenshot render before clearing the board
                }
                var info=game.Tables.GetLevelInfo(game.PlayerLevel,game.PlayerLevel);
                if(index==0)Check(view==null,"No strip is loaded before first subround");
                else
                {
                    if(retained==null)retained=view;
                    Check(view==retained&&view.transform.parent==startup.MainLevel.Group.parent,"Same source view reused under original Top parent");
                    Check(!view.transform.Find("Progress").gameObject.activeSelf&&!view.transform.Find("Levels").gameObject.activeSelf,"Reward-dependent milestones remain disabled in prefab");
                    Check(((RectTransform)view.transform).anchoredPosition==new Vector2(0,-248.1f)&&((RectTransform)view.SubRounds.transform).anchoredPosition==new Vector2(1.6f,-91),"Original root and subround coordinates");
                    Check(!view.SubRoundTemplate.activeSelf&&view.SubRounds.activeSelf==(info.SubTotalRound>0),"Source template hidden and table-driven group visibility");
                    if(info.SubTotalRound>0)
                    {
                        Check(view.DotCount==info.SubTotalRound&&view.SubRounds.GetComponent<HorizontalLayoutGroup>()!=null,"Original native dot count and layout");
                        int dotIndex=0;Sprite passed,pending;
                        // Compare the source serialized sprite references.
                        var serialized=new SerializedObject(view);passed=(Sprite)serialized.FindProperty("passedDot").objectReferenceValue;pending=(Sprite)serialized.FindProperty("pendingDot").objectReferenceValue;
                        foreach(Transform dot in view.SubRounds.transform)
                        {
                            if(dot.gameObject==view.SubRoundTemplate)continue;
                            var icon=dot.Find("Icon").GetComponent<Image>();
                            Check(icon.sprite==(dotIndex<info.SubRound?passed:pending),"Original inclusive passed-dot assignment");
                            if(dotIndex==info.SubRound-1)blinking=icon;
                            if(dotIndex==0)Check(!dot.Find("Line0").gameObject.activeSelf,"First left connector hidden");
                            if(dotIndex==view.DotCount-1)Check(!dot.Find("Line1").gameObject.activeSelf,"Last right connector hidden");
                            dotIndex++;
                        }
                    }
                    else Check(view.DotCount==5,"Leaving subrounds hides without discarding reusable dots");
                }
                Debug.Log("NUT_LOCAL_SUBROUND_VIEW level="+game.PlayerLevel+" total="+info.SubTotalRound+" current="+info.SubRound);
                phase=1;waitUntil=Time.time+1.35f;
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Next()
        {
            if(++index==levels.Length){Debug.Log("NUT_LOCAL_SUBROUND_VIEW_PLAY_PASS default boot and explicit live level fixtures 8/9/10/12/13/22/23; lazy source prefab, original coordinates/sprites/connectors, current scaled blink and pause/resume, four-to-five reuse, hide after subrounds and live world selection; no reward state.");Finish(0);return;}
            game.User.Level=levels[index];game.InitLevel(true,false,false);phase=0;
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){Time.timeScale=1;OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
