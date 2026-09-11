using System;
using System.Collections.Generic;
using System.Text;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalGameplayPlayValidation
    {
        private const string Key="NutSort.LocalGameplayPlay";
        private static double deadline;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalBoardState board;
        private static List<Vector2Int> solution;
        private static int move,boards,phase;
        private static float nextAction;
        private static bool capturedSuccess;
        private static string resumeSnapshot;
        static LocalGameplayPlayValidation()
        {
            if(SessionState.GetBool(Key,false))
            {deadline=EditorApplication.timeSinceStartup+480;EditorApplication.update+=Tick;}
        }
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"IsCompleteRecordGuide\":true}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Local gameplay");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static ScrewData Rod(params int[] colors)
        {
            var cells=new CData[4];for(int i=0;i<4;i++)cells[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};
            return new ScrewData{Id=1,C=cells};
        }
        private static ScrewOperation Click(int index)
        {
            Physics.SyncTransforms();
            var point=game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(index).Bounds.bounds.center);
            if(startup.LocalGameplay.TeachingPanel!=null)
            {
                var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                if(hits.Count>0)
                {
                    var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                    if(handler!=null)ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);
                }
            }
            return game.TryOperateAtScreenPoint(point);
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Local gameplay timeout phase "+phase+" boards "+boards);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.Level.AreNutsInitialized)return;
                var local=startup.LocalGameplay;
                if(local.UnlockPanel!=null)
                {
                    if(!local.UnlockPanel.Closing)local.UnlockPanel.ContinueButton.onClick.Invoke();
                    return;
                }
                if(game.ModalInputBlocked&&!game.IsCanOperatorScrew&&!local.ResultVisible&&startup.Replay.Panel==null)return;
                Check(game.User.Gold>=0&&game.User.Coin==0&&game.User.GoldRewardTargetS2CData!=null,"Captured reward configuration is available during local progression");
                if(phase==2)
                {
                    Check(game.PlayerLevel==4&&game.User.LevelSeed==0&&!local.AwaitingNext&&game.IsInitDone,"Exit on settled board resumes next level exactly once");
                    phase=3;board=null;
                }
                if(phase==4)
                {
                    Check(game.PlayerLevel==4&&OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot())==resumeSnapshot,"Partially played real board and move history restored after scene reload");
                    Check(game.IsInitDone&&!game.ModalInputBlocked,"Restored board receives default input initialization");
                    nextAction=Time.time+1.2f;phase=5;return;
                }
                if(phase==5)
                {
                    if(Time.time<nextAction)return;
                    ScreenCapture.CaptureScreenshot("Library/local-gameplay-resumed-current.png");phase=6;return;
                }
                if(phase==6)
                {
                    // Explicit failure fixture only, loaded through real persistence.
                    var data=new LevelData{B=new[]{Rod(1,2,3,4),Rod(4,3,2,1),Rod()}};
                    var layout=Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout");
                    game.User.LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(new OriginalBoardState(data,layout),new List<OriginalMoveRecord>()));
                    PlayerPrefs.SetString(OriginalUserStore.Key,OriginalUserDataJson.Write(game.User));PlayerPrefs.Save();
                    phase=7;game=null;startup=null;SceneManager.LoadScene("LuoSiSortGame");return;
                }
                if(phase==7)
                {
                    Check(Click(0).Kind==ScrewOperationKind.Ready&&Click(2).Kind==ScrewOperationKind.Moved,"Real transfer triggers deadlock");
                    Check(game.IsFail,"Native move callback marks failure");phase=8;return;
                }
                if(phase==8)
                {
                    if(!local.ResultVisible)return;
                    Check(game.ModalInputBlocked&&!local.AwaitingNext,"Delayed failure blocks world");
                    ScreenCapture.CaptureScreenshot("Library/local-gameplay-failure-current.png");phase=9;return;
                }
                if(phase==9)
                {
                    int seed=game.User.LevelSeed;var retry=local.RetryButton;retry.onClick.Invoke();retry.onClick.Invoke();
                    Check(game.User.LevelSeed==seed+1&&!game.IsFail&&game.IsRestarting,"Native retry advances seed once and clears failure");phase=10;return;
                }
                if(phase==10)
                {
                    Check(game.PlayerLevel==4&&game.IsInitDone&&!game.ModalInputBlocked,"Failure retry returns playable original board");
                    local.Bottom.Replay.onClick.Invoke();Check(startup.Replay.Panel!=null,"Actual replay button opens source prefab");
                    nextAction=Time.time+1;phase=11;return;
                }
                if(phase==11)
                {
                    if(Time.time<nextAction)return;
                    startup.Replay.Panel.ReplayButton.onClick.Invoke();phase=12;nextAction=Time.time+1;return;
                }
                if(phase==12)
                {
                    if(Time.time<nextAction||startup.Replay.Panel!=null)return;
                    Check(game.PlayerLevel==4&&game.User.LevelSeed==1&&game.IsInitDone&&!game.ModalInputBlocked,"Source replay preserves level/seed and returns input");
                    Debug.Log("NUT_LOCAL_GAMEPLAY_PLAY_PASS default native teaching panel/markers/rule text and world-input override, local final-stage text, last-teaching close and later-level override reset; default boot; four tutorial seeds and levels 2/3 through real world ray clicks; native success prefab/title/close animation and raycast next with duplicate guard; settled/partial persistence reload; explicit deadlock fixture through native failure delay and retry Button; real replay popup Button; captured local rewards with explicit claims.");
                    Finish(0);return;
                }
                if(local.AwaitingNext)
                {
                    if(local.SuccessPanel.Closing)return;
                    if(Vector3.Distance(local.SuccessPanel.Main.localScale,Vector3.one)>.001f||Vector3.Distance(local.SuccessPanel.Title.localScale,Vector3.one)>.001f)return;
                    Check(!local.SuccessPanel.Ad.activeInHierarchy&&!local.SuccessPanel.GetButton.gameObject.activeInHierarchy&&local.SuccessPanel.MoreLabel.text=="Claim"&&local.SuccessPanel.Views.Count==1,"Native local success shows reward and ordinary claim");
                    Check(!game.IsInitDone&&game.ModalInputBlocked,"Settlement blocks world input");
                    var saved=OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
                    Check(saved.Level==game.PlayerLevel&&string.IsNullOrEmpty(saved.LevelInfo),"Atomic pending-board checkpoint");
                    if(!capturedSuccess)
                    {
                        ScreenCapture.CaptureScreenshot("Library/local-gameplay-success-current.png");
                        capturedSuccess=true;nextAction=Time.time+.5f;return;
                    }
                    if(Time.time<nextAction)return;
                    capturedSuccess=false;
                    if(game.PlayerLevel==4)
                    {
                        local.NextButton.onClick.Invoke();
                        phase=2;game=null;startup=null;board=null;SceneManager.LoadScene("LuoSiSortGame");return;
                    }
                    int level=game.PlayerLevel;var next=local.NextButton;var canvas=next.GetComponentInParent<Canvas>();
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,next.transform.position),button=PointerEventData.InputButton.Left};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==next.gameObject,"Actual UI raycast resolves native next-level button");
                    ExecuteEvents.Execute(next.gameObject,pointer,ExecuteEvents.pointerClickHandler);next.onClick.Invoke();
                    Check(game.PlayerLevel==level&&local.SuccessPanel.Closing&&!game.IsRestarting&&local.AwaitingNext,"Native close starts once before next-board initialization without incrementing again");return;
                }
                Check(game.IsInitDone,"Default local boot enables real input without test binding or flag overrides");
                if(Time.time<nextAction)return;
                if(board!=game.Level.Board)
                {
                    board=game.Level.Board;solution=Solve(board);move=0;boards++;
                    if(game.PlayerLevel==1)
                    {
                        var teaching=local.TeachingPanel;
                        Check(teaching!=null&&teaching.IsOpen&&game.IsCanOperatorScrew&&game.ModalInputBlocked,"Default native teaching modal permits world input through the original override");
                        Check(!teaching.HollowMask.gameObject.activeSelf,"Pure teaching hides hollow mask");
                        var serialized=new SerializedObject(teaching);var markers=serialized.FindProperty("TeachLevels");
                        string filled=serialized.FindProperty("filledMarkerPath").stringValue,empty=serialized.FindProperty("emptyMarkerPath").stringValue;
                        for(int i=0;i<markers.arraySize;i++)
                            Check(((Image)markers.GetArrayElementAtIndex(i).objectReferenceValue).sprite==Resources.Load<Sprite>(i<=game.User.LevelSeed?filled:empty),"Native inclusive teaching markers");
                        string expected=game.User.LevelSeed>2?new SerializedObject(local).FindProperty("localFinalTeachingText").stringValue:game.Tables.Text.GetText(70+game.User.LevelSeed,"en");
                        Check(teaching.Tip.text==expected,"Original first three rule messages and explicit local final-stage text");
                    }
                    else Check(local.TeachingPanel==null&&!game.IsCanOperatorScrew,"Later levels clear the tutorial input override");
                    Debug.Log("NUT_LOCAL_BOARD level="+game.PlayerLevel+" seed="+game.User.LevelSeed+" moves="+solution.Count);
                    nextAction=Time.time+.9f;return;
                }
                if(move>=solution.Count)return;
                foreach(var rod in board.Screws)if(!rod.IsCanOperator)return;
                ScreenCapture.CaptureScreenshot("Library/local-gameplay-board-current.png");
                if(game.PlayerLevel==1&&move==0)ScreenCapture.CaptureScreenshot("Library/local-teaching-"+game.User.LevelSeed+"-current.png");
                var step=solution[move++];
                Check(Click(step.x).Kind==ScrewOperationKind.Ready,"Actual screen selection "+step.x);
                Check(Click(step.y).Kind==ScrewOperationKind.Moved,"Actual screen transfer "+step.y);
                if(game.PlayerLevel==1&&game.User.LevelSeed>=3&&game.Level.Board.IsSuccess)
                    Check(local.TeachingPanel==null,"Last tutorial victory closes teaching through native move completion");
                nextAction=Time.time+1.2f;
                if(phase==3)
                {
                    Check(!game.Level.Board.IsSuccess,"Resume uses an unfinished original board");
                    game.SaveUserData();resumeSnapshot=OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot());
                    phase=4;game=null;startup=null;board=null;SceneManager.LoadScene("LuoSiSortGame");
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        // Bounded offline search chooses input only. It never mutates runtime
        // boards, completes animations, binds callbacks or grants inventory.
        internal static List<Vector2Int> Solve(OriginalBoardState value)
        {
            var stacks=new List<int>[value.Screws.Length];var capacity=new int[stacks.Length];
            for(int i=0;i<stacks.Length;i++)
            {
                var rod=value.Screws[i];stacks[i]=new List<int>();capacity[i]=rod.IsLocked?0:rod.Capacity;
                Check(!rod.IsHidden&&!rod.IsColorMask&&!rod.IsDontMove,"Early-board solver requires normal rods");
                foreach(var slot in rod.Slots)if(slot.Nut!=null)stacks[i].Add(slot.Nut.Color);
            }
            var path=new List<Vector2Int>();var visited=new HashSet<string>();
            Check(Search(stacks,capacity,path,visited),"Original board has a bounded normal-move solution");return path;
        }
        private static bool Uniform(List<int> s){for(int i=1;i<s.Count;i++)if(s[i]!=s[0])return false;return true;}
        private static bool Search(List<int>[] s,int[] c,List<Vector2Int> path,HashSet<string> seen)
        {
            bool done=true;for(int i=0;i<s.Length;i++)if(s[i].Count>0&&(s[i].Count!=c[i]||!Uniform(s[i])))done=false;
            if(done)return true;if(path.Count>=120||seen.Count>100000)return false;
            var key=new StringBuilder();for(int i=0;i<s.Length;i++){foreach(int color in s[i])key.Append(color).Append(',');key.Append(';');}
            if(!seen.Add(key.ToString()))return false;
            for(int i=0;i<s.Length;i++)
            {
                if(s[i].Count==0||c[i]==0||(s[i].Count==c[i]&&Uniform(s[i])))continue;
                int color=s[i][s[i].Count-1],group=1;while(group<s[i].Count&&s[i][s[i].Count-group-1]==color)group++;
                for(int j=0;j<s.Length;j++)
                {
                    if(i==j||s[j].Count>=c[j]||(s[j].Count>0&&s[j][s[j].Count-1]!=color))continue;
                    if(path.Count>0&&path[path.Count-1]==new Vector2Int(j,i))continue;
                    int count=Math.Min(group,c[j]-s[j].Count);
                    s[i].RemoveRange(s[i].Count-count,count);for(int n=0;n<count;n++)s[j].Add(color);path.Add(new Vector2Int(i,j));
                    if(Search(s,c,path,seen))return true;
                    path.RemoveAt(path.Count-1);s[j].RemoveRange(s[j].Count-count,count);for(int n=0;n<count;n++)s[i].Add(color);
                }
            }
            return false;
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Finish(int code)
        {OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
