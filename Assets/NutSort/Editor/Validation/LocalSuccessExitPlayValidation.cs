using System;
using System.Collections.Generic;
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
    public static class LocalSuccessExitPlayValidation
    {
        private const string Key="NutSort.LocalSuccessExit";
        private static double timeout;
        private static int phase;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        static LocalSuccessExitPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            var board=new OriginalBoardState(new LevelData{B=new[]{Rod(1),Rod(1,1,1)}},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=4,NewGameplayUnlockIndex=1,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
            user.IsCompleteRecordGuide=true;
            OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static ScrewData Rod(params int[] colors)
        {var slots=new CData[4];for(int i=0;i<4;i++)slots[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};return new ScrewData{Id=1,C=slots};}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Success closing reload timeout");
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.InputBlocked||game.IsRestarting)return;
                var local=startup.LocalGameplay;
                if(phase==0)
                {
                    if(!game.IsInitDone)return;
                    foreach(var rod in game.Level.Board.Screws)foreach(var slot in rod.Slots)if(slot.Nut!=null&&game.Level.GetNut(slot.Nut).IsEntryAnimating)return;
                    Physics.SyncTransforms();
                    Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(0).Bounds.bounds.center)).Kind==ScrewOperationKind.Ready,"Default source selection");
                    Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(1).Bounds.bounds.center)).Kind==ScrewOperationKind.Moved,"Actual last move completes fixture");phase=1;return;
                }
                if(phase==1)
                {
                    if(!local.AwaitingNext||Vector3.Distance(local.SuccessPanel.Title.localScale,Vector3.one)>.001f)return;
                    local.NextButton.onClick.Invoke();
                    Check(local.SuccessPanel.Closing&&local.AwaitingNext&&!game.IsRestarting&&game.ModalInputBlocked,"Close starts while next board remains deferred and modal");
                    Check(game.PlayerLevel==5&&game.User.LevelSeed==0&&game.User.TodayPassLevelCount==1,"One local settlement before close");
                    phase=2;game=null;startup=null;SceneManager.LoadScene("LuoSiSortGame");return;
                }
                if(!game.IsInitDone)return;
                Check(game.PlayerLevel==5&&game.User.LevelSeed==0&&game.User.TodayPassLevelCount==1&&!game.ModalInputBlocked&&!local.AwaitingNext&&local.SuccessPanel==null,"Reload during native close resumes next board once without a stale modal or second settlement");
                Check(game.User.Gold>0&&game.User.Coin==0&&game.User.LocalPendingReward==null,"Claim persists without duplicate reward");
                Debug.Log("NUT_LOCAL_SUCCESS_EXIT_PLAY_PASS default actual last move, native success closing with deferred next board, scene reload before close callback, single level/pass-count checkpoint and normal next-board input; explicit board fixture, local claim persisted once.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
