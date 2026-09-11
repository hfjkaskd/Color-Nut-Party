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
    public static class LocalSpecialScrewPlayValidation
    {
        private const string Key="NutSort.LocalSpecialScrew";
        private static double timeout;
        private static float waitUntil;
        private static int phase;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        static LocalSpecialScrewPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            var board=new OriginalBoardState(new LevelData{B=new[]{
                Rod(new[]{11}),Rod(new[]{11,11,11},new OBIMData{Id=6}),
                Rod(new[]{1,2,1,2},new OBIMData{Id=4,Obj=new OBIMObjData{CI=11}}),
                Rod(Array.Empty<int>()),Rod(new[]{1,2,1,2},new OBIMData{Id=7}),
                Rod(new[]{1,2,1,2},new OBIMData{Id=7})}},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"))
            {Level=108,NewGameplayUnlockIndex=4,RevokeCount=2,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
            user.IsCompleteRecordGuide=true;
            OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Local special screws");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static ScrewData Rod(int[] colors,params OBIMData[] types)
        {
            var cells=new CData[4];for(int i=0;i<4;i++)cells[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};
            return new ScrewData{Id=1,C=cells,OBIM=types};
        }
        private static ScrewOperation Click(int index)
        {Physics.SyncTransforms();return game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(index).Bounds.bounds.center));}
        private static void Revealed(OriginalBoardState board)
        {Check(board.Screws[1].IsDontMove&&!board.Screws[2].IsColorMask&&!board.Screws[4].IsHidden&&board.Screws[5].IsHidden,"Fixed type retained; matching mask and adjacent cover cleared; diagonal cover retained");}
        private static OriginalUserLocalData Saved()=>OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
        private static void Reload(){game=null;startup=null;SceneManager.LoadScene("LuoSiSortGame");}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Local special screw timeout phase="+phase);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone||!game.Level.AreNutsInitialized)return;
                if(Time.time<waitUntil)return;
                var local=startup.LocalGameplay;var board=game.Level.Board;
                Check(game.PlayerLevel==108&&game.User.Gold==0&&game.User.Coin==0&&game.User.ServerConfigData==null&&game.User.GoldRewardTargetS2CData!=null,"Local fixture loads captured config without granting rewards");
                Check(!game.ModalInputBlocked&&!local.ResultVisible&&!game.IsFail&&!local.AwaitingNext,"Partial screw completion never opens settlement or failure");
                if(phase==0)
                {
                    if(!EntriesSettled())return;
                    Check(board.Screws[1].IsDontMove&&board.Screws[2].IsColorMask&&board.Screws[4].IsHidden&&board.Screws[5].IsHidden,"Default resume reconstructs all source special states");
                    waitUntil=Time.time+.7f;phase=1;return;
                }
                if(phase==1){ScreenCapture.CaptureScreenshot("Library/local-special-before-current.png");phase=2;return;}
                if(phase==2)
                {
                    Check(Click(0).Kind==ScrewOperationKind.Ready&&Click(1).Kind==ScrewOperationKind.Moved,"Default world input completes fixed rod");Revealed(board);
                    Check(game.User.ScrewDoneCount==1&&game.Level.MoveHistoryCount==1,"Original immediate completion count/history");
                    Revealed(OriginalBoardSnapshotJson.Read(Saved().LevelInfo).Restore(Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout")));
                    waitUntil=Time.time+.3f;phase=3;return;
                }
                if(phase==3)
                {
                    Check(game.Level.GetScrew(1).TypeView.IsDontMoveVisible&&game.Level.GetScrew(4).TypeView.IsHiddenVisible,"Native break visuals outlive immediate logical reveal");
                    ScreenCapture.CaptureScreenshot("Library/local-special-break-current.png");waitUntil=Time.time+2;phase=4;return;
                }
                if(phase==4)
                {
                    Check(!game.Level.GetScrew(1).TypeView.IsDontMoveVisible&&!game.Level.GetScrew(2).TypeView.IsMaskVisible&&!game.Level.GetScrew(4).TypeView.IsHiddenVisible&&game.Level.GetScrew(5).TypeView.IsHiddenVisible,"Default source callbacks finish each cover lifetime");
                    phase=5;Reload();return;
                }
                if(phase==5)
                {
                    if(!EntriesSettled())return;
                    Revealed(board);Check(game.Level.MoveHistoryCount==1&&game.User.ScrewDoneCount==1,"Actual reload preserves completion count and move history");
                    Check(!game.Level.GetScrew(1).TypeView.IsDontMoveVisible&&!game.Level.GetScrew(2).TypeView.IsMaskVisible&&!game.Level.GetScrew(4).TypeView.IsHiddenVisible&&game.Level.GetScrew(5).TypeView.IsHiddenVisible,"Reload does not replay removed covers");
                    ScreenCapture.CaptureScreenshot("Library/local-special-restored-current.png");phase=6;return;
                }
                if(phase==6)
                {
                    local.Bottom.GetOtherItem(2).Display.Click.onClick.Invoke();
                    Check(game.Level.MoveHistoryCount==0&&game.User.RevokeCount==1,"Actual undo Button restores history and consumes one owned item");
                    Revealed(board);waitUntil=Time.time+1.6f;phase=7;return;
                }
                if(phase==7)
                {
                    var saved=Saved();Check(saved.RevokeCount==1&&OriginalBoardSnapshotJson.Read(saved.LevelInfo).OperatorInfos.Count==0,"Undo inventory and removed history survive the native save boundary");
                    phase=9;Reload();return;
                }
                if(phase==9)
                {
                    if(!EntriesSettled())return;
                    Revealed(board);Check(game.Level.MoveHistoryCount==0&&game.User.RevokeCount==1,"Actual undo reload retains board and cost");
                    Check(!board.Screws[1].IsDone&&game.Level.GetScrew(1).TypeView.IsDontMoveVisible,"Retained fixed type restores idle cover on undone incomplete rod");
                    Check(Click(0).Kind==ScrewOperationKind.Ready&&Click(1).Kind==ScrewOperationKind.Moved,"Undo allows original transfer again");
                    Check(game.User.ScrewDoneCount==2,"Repeated completion uses original cumulative counter");waitUntil=Time.time+1.7f;phase=8;return;
                }
                Check(Click(4).Kind==ScrewOperationKind.Ready&&Click(3).Kind==ScrewOperationKind.Moved,"Previously hidden rod is playable after real reload and undo/redo");
                Check(game.Level.MoveHistoryCount==2,"Continued play creates normal history");
                Debug.Log("NUT_LOCAL_SPECIAL_SCREW_PLAY_PASS default saved-board startup, real world ray transfer, fixed type retention and matching-mask/adjacent-hidden logical release, independent native cover lifetimes, persisted reveal/history/count and actual scene reload, default undo Button, persisted cost/history and fixed-cover restoration after undo reload, then redo, continued play from revealed rod; explicit mechanics/inventory fixture, no callback or readiness replacement, no rewards.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static bool EntriesSettled()
        {
            foreach(var rod in game.Level.Board.Screws)foreach(var slot in rod.Slots)
            {
                if(slot.Nut==null)continue;
                var view=game.Level.GetNut(slot.Nut);if(view.IsEntryAnimating)return false;
                Check(view.Renderer.enabled&&view.Renderer.gameObject.activeInHierarchy&&view.transform.localScale==Vector3.one,"Every fixture nut renders at full scale after native entry");
            }
            return true;
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
