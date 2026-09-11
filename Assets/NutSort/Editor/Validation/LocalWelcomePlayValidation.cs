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
using UnityEngine.UI;

namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalWelcomePlayValidation
    {
        private const string Key="NutSort.LocalWelcomeValidation";
        private static double timeout;
        private static float readyAt;
        private static int phase;
        private static OriginalStartupFlow startup;
        private static OriginalGameScene game;

        static LocalWelcomePlayValidation()
        {
            if(SessionState.GetBool(Key,false))
            {
                timeout=EditorApplication.timeSinceStartup+100;
                EditorApplication.update+=Tick;
            }
        }
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"Level\":1,\"LevelSeed\":0,\"IsCompleteRecordGuide\":false}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(570,1014,"Welcome startup");
            SessionState.SetBool(Key,true);
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Welcome startup timeout, phase "+phase);
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null||game==null||startup.LocalGameplay==null||game.InputBlocked||game.IsRestarting||!game.IsInitDone)return;
                var local=startup.LocalGameplay;
                if(phase==0)
                {
                    if(local.RecordGuide==null)return;
                    Check(local.TeachingPanel==null&&game.ModalInputBlocked&&!game.IsCanOperatorScrew,"Welcome precedes teaching and blocks board");
                    local.RecordGuide.StartButton.onClick.Invoke();
                    Check(!game.User.IsCompleteRecordGuide&&!local.RecordGuide.Closing,"Delayed Start cannot complete early");
                    readyAt=Time.time+3;phase=1;return;
                }
                if(Time.time<readyAt)return;
                if(phase==1)
                {
                    Check(local.RecordGuide!=null&&local.TeachingPanel==null,"Welcome remains until Start");
                    Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(0).Bounds.bounds.center)).Kind!=ScrewOperationKind.Ready,"World input stays blocked");
                    ScreenCapture.CaptureScreenshot("Library/welcome-startup.png");
                    readyAt=Time.time+.3f;phase=2;return;
                }
                if(phase==2)
                {
                    var panel=local.RecordGuide;
                    var button=panel.StartButton;
                    Canvas.ForceUpdateCanvases();
                    var canvas=button.GetComponentInParent<Canvas>();
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,button.transform.position),button=PointerEventData.InputButton.Left};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Native raycast reaches Start");
                    ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    Check(game.User.IsCompleteRecordGuide&&panel.Closing&&local.TeachingPanel==null,"Start marks complete and closes before teaching");
                    button.onClick.Invoke();
                    readyAt=Time.time+1;phase=3;return;
                }
                if(phase==3)
                {
                    Check(local.RecordGuide==null&&local.TeachingPanel!=null&&game.IsCanOperatorScrew,"Close continues into playable teaching");
                    Check(PlayerPrefs.GetString(OriginalUserStore.Key).Contains("\"IsCompleteRecordGuide\":true"),"Local completion persists");
                    phase=4;Reload();return;
                }
                if(phase==4)
                {
                    Check(local.RecordGuide==null&&local.TeachingPanel!=null,"Relaunch skips completed welcome and keeps teaching");
                    PlayerPrefs.SetString(OriginalUserStore.Key,"{\"Level\":2,\"IsCompleteRecordGuide\":true}");PlayerPrefs.Save();
                    phase=5;Reload();return;
                }
                Check(local.RecordGuide==null&&local.TeachingPanel==null&&!game.ModalInputBlocked&&!game.IsCanOperatorScrew,"Returning level two has no stale welcome input block");
                Debug.Log("NUT_LOCAL_WELCOME_PASS fresh startup, delayed native Button, board gate, duplicate click, teaching continuation, persisted relaunch and returning player");
                Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Reload(){startup=null;game=null;readyAt=0;SceneManager.LoadScene("LuoSiSortGame");}
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void Finish(int code)
        {
            OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick;EditorApplication.Exit(code);
        }
    }
}
