using System;
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
    public static class LocalGameplayNoticePlayValidation
    {
        private const string Key="NutSort.LocalNoticePlay";
        private static double deadline;
        private static OriginalGameScene game;
        private static LocalGameplayController local;
        private static int phase,index;
        static LocalGameplayNoticePlayValidation(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+70;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"IsCompleteRecordGuide\":true,\"Level\":4,\"NewGameplayUnlockIndex\":1,\"AddScrewCount\":0}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static ScrewOperation Click()
        {
            Physics.SyncTransforms();return game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(index).Bounds.bounds.center));
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Local notice timeout");
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(local==null)local=UnityEngine.Object.FindObjectOfType<LocalGameplayController>();
                if(game==null||local==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone)return;
                if(phase==0)
                {
                    index=-1;for(int i=0;i<game.Level.Board.Screws.Length;i++)if(game.Level.Board.Screws[i].IsLocked){index=i;break;}
                    Check(index>=0,"Original later board has a locked rod");
                    Check(Click().Kind==ScrewOperationKind.AddScrewRequested,"Actual locked-rod click reaches original tool flow");
                    Check(local.ResultVisible&&game.ModalInputBlocked&&game.User.AddScrewCount==0,"SDK skip notice without grant");
                    local.DismissButton.onClick.Invoke();
                    Check(game.ModalInputBlocked&&Click().Kind==ScrewOperationKind.Ignored,"Dismiss release cannot click through to world");phase=1;return;
                }
                if(game.ModalInputBlocked)return;
                Check(!local.ResultVisible,"Notice releases input on following frame");
                // Explicit owned-inventory test fixture, never a production grant.
                game.User.AddScrewCount=1;
                Check(Click().Kind==ScrewOperationKind.AddScrewRequested,"Owned tool reaches same native route");
                Check(!game.Level.Board.Screws[index].IsLocked&&game.User.AddScrewCount==0&&game.User.CurrentLevelAddScrewCount==4,"Original unlock and item cost applied");
                var saved=OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
                Check(saved.AddScrewCount==0&&saved.CurrentLevelAddScrewCount==4,"Original item manager saved the unlock cost");
                Debug.Log("NUT_LOCAL_NOTICE_PLAY_PASS real locked-rod ray click, explicit SDK skip notice/no grant, dismiss click-through guard, owned-inventory fixture consumes native item and saves unlock.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
