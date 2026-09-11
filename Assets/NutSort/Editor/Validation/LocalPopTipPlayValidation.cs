using System;
using System.Collections.Generic;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalPopTipPlayValidation
    {
        private const string Key="NutSort.LocalPopTip";
        private static double timeout,pausedAt;
        private static float waitUntil,shownAt,firstY,secondY;
        private static int phase;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalPopTip first,second;
        static LocalPopTipPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            var board=new OriginalBoardState(new LevelData{B=new[]{Rod(1,2,1,2),Rod()}},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=4,NewGameplayUnlockIndex=1,RevokeCount=2,AddScrewCount=2,CurrentLevelAddScrewCount=12,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
            user.IsCompleteRecordGuide=true;
            OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Native tool tips");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static ScrewData Rod(params int[] colors)
        {var c=new CData[4];for(int i=0;i<4;i++)c[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};return new ScrewData{Id=1,C=c};}
        private static void Click(Button button)
        {
            var canvas=button.GetComponentInParent<Canvas>();var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,button.transform.position),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Actual UI raycast resolves core tool Button");
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Native tip timeout phase="+phase);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone)return;
                if(Time.time<waitUntil)return;
                var local=startup.LocalGameplay;
                if(phase==0){waitUntil=Time.time+1.5f;phase=1;return;}
                if(phase==1)
                {
                    Click(local.Bottom.GetOtherItem(2).Display.Click);first=startup.TopCanvas.GetComponentInChildren<OriginalPopTip>();
                    Check(first!=null&&first.Content.text==game.Tables.Text.GetText(3,"en")&&first.transform.parent==startup.TopCanvas,"Source text id 3 on TopCanvas");
                    Check(!local.ResultVisible&&!game.ModalInputBlocked&&game.User.RevokeCount==2,"No history shows no modal and consumes no tool");
                    Check(((RectTransform)first.transform).sizeDelta==new Vector2(800,100)&&first.GetComponent<Image>().sprite!=null,"Original toast geometry and restored sprite");
                    firstY=first.transform.localPosition.y;shownAt=Time.time;waitUntil=Time.time+.35f;phase=2;return;
                }
                if(phase==2)
                {
                    Check(first!=null&&Mathf.Abs(first.transform.localPosition.y-firstY-100*(Time.time-shownAt))<100*Time.deltaTime+2,"Source 100 units per scaled second drift");
                    Physics.SyncTransforms();Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(0).Bounds.bounds.center)).Kind==ScrewOperationKind.Ready,"Visible toast permits actual world selection");
                    ScreenCapture.CaptureScreenshot("Library/local-pop-tip-current.png");
                    Click(local.Bottom.GetOtherItem(4).Display.Click);var tips=startup.TopCanvas.GetComponentsInChildren<OriginalPopTip>();
                    Check(tips.Length==2,"Repeated tips instantiate independently");second=tips[0]==first?tips[1]:tips[0];
                    Check(second.Content.text==game.Tables.Text.GetText(5,"en")&&game.User.AddScrewCount==2&&game.User.CurrentLevelAddScrewCount==12,"Source max-space text without inventory mutation");
                    firstY=first.transform.localPosition.y;secondY=second.transform.localPosition.y;Time.timeScale=0;pausedAt=EditorApplication.timeSinceStartup;phase=3;return;
                }
                if(phase==3)
                {
                    if(EditorApplication.timeSinceStartup-pausedAt<.5)return;
                    Check(first!=null&&second!=null&&first.transform.localPosition.y==firstY&&second.transform.localPosition.y==secondY,"Both lifetime and drift pause with scaled game time");
                    Time.timeScale=1;waitUntil=Time.time+.75f;phase=4;return;
                }
                if(phase==4)
                {
                    Check(first==null&&second!=null&&!game.ModalInputBlocked&&!local.ResultVisible,"First expires while later toast survives, no modal release needed");
                    waitUntil=Time.time+.4f;phase=5;return;
                }
                Check(second==null&&startup.TopCanvas.GetComponentsInChildren<OriginalPopTip>().Length==0,"Each source one-second callback destroys only its own instance");
                Debug.Log("NUT_LOCAL_POP_TIP_PLAY_PASS default undo/max-add Buttons through UI raycasts, source text 3/5 and TopCanvas prefab, no modal/tool cost, real world selection under toast, 100-unit scaled drift, independent one-second lifetimes and paused clock; explicit saved-board/inventory fixture.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){Time.timeScale=1;OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
