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
using UnityEngine.SceneManagement;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalSubroundPlayValidation
    {
        private const string Key="NutSort.LocalSubround";
        private static double deadline;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalBoardState board;
        private static List<Vector2Int> solution;
        private static int move,phase;
        private static readonly HashSet<int> transitions=new HashSet<int>();
        private static float nextAction;
        static LocalSubroundPlayValidation(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+600;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=9,NewGameplayUnlockIndex=1};
            user.IsCompleteRecordGuide=true;
            OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Native subround progression");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Subround timeout phase="+phase);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null)return;
                var local=startup.LocalGameplay;
                Check((phase==0?game.User.Gold==0:game.User.Gold>0)&&game.User.Coin==0&&game.User.GoldRewardTargetS2CData!=null,"Intermediate subrounds do not grant; final claim grants once");
                if(game.PlayerLevel<=12)Check(local.SuccessPanel==null&&!local.AwaitingNext,"Intermediate rounds never create a success modal");
                if(game.IsRestarting&&game.PlayerLevel>=10&&game.PlayerLevel<=12&&!transitions.Contains(game.PlayerLevel))
                {
                    Check(!local.ResultVisible&&!game.ModalInputBlocked,"Intermediate success directly reconstructs without modal UI");
                    var saved=OriginalUserDataJson.Read(PlayerPrefs.GetString("UserLocalData"),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
                    Check(saved.Level==game.PlayerLevel&&saved.LevelSeed==0&&string.IsNullOrEmpty(saved.LevelInfo)&&saved.TodayPassLevelCount==game.PlayerLevel-9,"Pending subround is durable exactly once before reconstruction");
                    transitions.Add(game.PlayerLevel);
                    if(game.PlayerLevel==10)
                    {
                        game=null;startup=null;board=null;SceneManager.LoadScene("LuoSiSortGame");return;
                    }
                }
                if(game.InputBlocked||game.IsRestarting||!game.Level.AreNutsInitialized)return;
                if(phase==1)
                {
                    if(!game.IsInitDone||local.SuccessPanel!=null)return;
                    Check(game.PlayerLevel==13&&game.User.TodayPassLevelCount==4&&!game.ModalInputBlocked&&!local.AwaitingNext,"Final Next initializes next subround group without duplicate settlement");
                    Check(local.SubroundView!=null&&local.SubroundView.DotCount==5&&local.SubroundView.SubRounds.activeSelf,"Next group grows source progress strip from four to five dots");
                    Debug.Log("NUT_LOCAL_SUBROUND_PLAY_PASS actual source boards 9/10/11/12 solved through world rays; intermediate rounds automatically restart without settlement UI, first-transition scene reload preserves flushed progress, last round alone shows native success and real next Button advances once; captured local reward granted only after the final claim.");Finish(0);return;
                }
                if(local.AwaitingNext)
                {
                    Check(transitions.Count==3&&game.PlayerLevel==13&&game.User.TodayPassLevelCount==4,"Only last source subround settles");
                    if(Vector3.Distance(local.SuccessPanel.Title.localScale,Vector3.one)>.001f||Vector3.Distance(local.SuccessPanel.Main.localScale,Vector3.one)>.001f)return;
                    ScreenCapture.CaptureScreenshot("Library/local-subround-success-current.png");
                    var next=local.NextButton;var canvas=next.GetComponentInParent<Canvas>();
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,next.transform.position),button=PointerEventData.InputButton.Left};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==next.gameObject,"Actual final-next Button receives UI raycast");
                    ExecuteEvents.Execute(next.gameObject,pointer,ExecuteEvents.pointerClickHandler);next.onClick.Invoke();
                    Check(local.SuccessPanel.Closing&&!game.IsRestarting&&game.PlayerLevel==13,"Single native closing before final next reconstruction");phase=1;return;
                }
                if(!game.IsInitDone||game.ModalInputBlocked||Time.time<nextAction)return;
                if(board!=game.Level.Board)
                {
                    Check(local.SubroundView!=null&&local.SubroundView.DotCount==4&&local.SubroundView.SubRounds.activeSelf,"Actual automatic subround progression refreshes native dot strip");
                    board=game.Level.Board;solution=LocalGameplayPlayValidation.Solve(board);move=0;
                    Check(game.PlayerLevel>=9&&game.PlayerLevel<=12&&startup.MainLevel.Label.text==game.Tables.Text.GetText(2,"en",5),"All four subrounds retain source display level 5");
                    Check(game.User.TodayPassLevelCount==game.PlayerLevel-9,"Resume/automatic rebuild does not settle twice");
                    Debug.Log("NUT_LOCAL_SUBROUND_BOARD level="+game.PlayerLevel+" moves="+solution.Count);
                    nextAction=Time.time+1.5f;return;
                }
                if(move>=solution.Count)return;
                foreach(var rod in board.Screws)if(!rod.IsCanOperator)return;
                Physics.SyncTransforms();var step=solution[move++];
                Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(step.x).Bounds.bounds.center)).Kind==ScrewOperationKind.Ready,"Actual world selection");
                Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(step.y).Bounds.bounds.center)).Kind==ScrewOperationKind.Moved,"Actual world transfer");
                if(move%20==0)Debug.Log("NUT_LOCAL_SUBROUND_MOVES level="+game.PlayerLevel+" completed="+move);
                nextAction=Time.time+1.2f;
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
