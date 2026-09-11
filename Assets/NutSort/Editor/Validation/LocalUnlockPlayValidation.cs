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
    public static class LocalUnlockPlayValidation
    {
        private const string Key="NutSort.LocalUnlockPlay";
        private static readonly int[] levels={4,17,57,107};
        private static double timeout;
        private static float waitUntil;
        private static int index,phase;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        static LocalUnlockPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+180;EditorApplication.update+=Tick;}}
        private static string Fixture(int level,int unlocked)=>"{\"IsCompleteRecordGuide\":true,\"Level\":"+level+",\"NewGameplayUnlockIndex\":"+unlocked+"}";
        public static void Run()
        {
            OriginalPreferenceFixture.Begin(Fixture(4,0));EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Gameplay unlocks");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Reload(string json=null)
        {
            if(json!=null){PlayerPrefs.SetString(OriginalUserStore.Key,json);PlayerPrefs.Save();}
            game=null;startup=null;SceneManager.LoadScene("LuoSiSortGame");
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Unlock Play timeout phase="+phase+" index="+index);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone)return;
                var local=startup.LocalGameplay;
                Check(game.User.ServerConfigData==null&&game.User.GoldRewardTargetS2CData!=null&&game.User.Gold==0&&game.User.Coin==0,"Captured config loaded without awarding a balance");
                if(Time.time<waitUntil)return;
                if(phase==0)
                {
                    var panel=local.UnlockPanel;Check(panel!=null,"Default initialization routes milestone to original panel");
                    Check(game.PlayerLevel==levels[index]&&game.ModalInputBlocked,"Milestone and modal input gate");
                    for(int i=0;i<panel.Icons.Length;i++)Check(panel.Icons[i].activeSelf==(i==index),"Exactly the source milestone icon is active");
                    Check(panel.Tip.text==game.Tables.Text.GetText(index+63,"en"),"Original localized mechanic description");
                    Physics.SyncTransforms();var point=game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(0).Bounds.bounds.center);
                    Check(game.TryOperateAtScreenPoint(point).Kind==ScrewOperationKind.Ignored,"Unlock panel blocks actual world ray input");
                    waitUntil=Time.time+1;phase=1;return;
                }
                if(phase==1)
                {
                    ScreenCapture.CaptureScreenshot("Library/local-unlock-"+index+"-current.png");phase=2;return;
                }
                if(phase==2)
                {
                    var panel=local.UnlockPanel;panel.ContinueButton.onClick.Invoke();panel.ContinueButton.onClick.Invoke();
                    Check(panel.Closing&&game.User.NewGameplayUnlockIndex==index+1&&game.ModalInputBlocked,"Native Continue assignment, animated close and repeated-click gate");
                    waitUntil=Time.time+.6f;phase=3;return;
                }
                if(phase==3)
                {
                    Check(local.UnlockPanel==null&&!game.ModalInputBlocked,"Native host removes closed panel and releases input");
                    var saved=OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
                    Check(saved.NewGameplayUnlockIndex==index+1,"Local checkpoint persists acknowledged milestone");
                    phase=4;Reload();return;
                }
                if(phase==4)
                {
                    Check(local.UnlockPanel==null&&!game.ModalInputBlocked&&game.User.NewGameplayUnlockIndex==index+1,"Acknowledged milestone does not repeat after actual reload");
                    index++;
                    if(index<levels.Length){phase=0;Reload(Fixture(levels[index],index));return;}
                    var board=new OriginalBoardState(new LevelData{B=new[]{Rod(1,2,3,4),Rod(2,3,4,1),Rod(3,4,1,2)}},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
                    var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=4,NewGameplayUnlockIndex=0,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
                    phase=5;Reload(OriginalUserDataJson.Write(user));return;
                }
                if(phase==5)
                {
                    Check(game.IsFail&&local.UnlockPanel==null,"Saved deadlock precedes eligible unlock without a new move");
                    phase=6;return;
                }
                if(phase==6)
                {
                    if(!local.ResultVisible)return;
                    Check(game.ModalInputBlocked&&local.UnlockPanel==null,"Original delayed failure reaches retry UI");
                    local.RetryButton.onClick.Invoke();phase=7;return;
                }
                if(phase==7)
                {
                    Check(!game.IsFail&&game.User.LevelSeed==1&&local.UnlockPanel!=null,"Retry clears deadlock, builds original board, then shows pending unlock");
                    local.UnlockPanel.ContinueButton.onClick.Invoke();waitUntil=Time.time+.6f;phase=8;return;
                }
                Check(local.UnlockPanel==null&&!game.ModalInputBlocked&&game.User.NewGameplayUnlockIndex==1,"Confirmed retry unlock returns play");
                Debug.Log("NUT_LOCAL_UNLOCK_PLAY_PASS default original milestones 4/17/57/107, four native icons/localized descriptions, animated real Button confirmation, modal gating, persisted index and no-repeat reload; saved deadlock checked before unlock, native delayed retry and subsequent pending unlock; no fake server/reward documents.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static ScrewData Rod(params int[] colors)
        {
            var cells=new CData[4];for(int i=0;i<4;i++)cells[i]=new CData{LP=new LPData{y=i},BIM=new BIMData{Id=1,CI=colors[i]}};
            return new ScrewData{Id=1,C=cells};
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
