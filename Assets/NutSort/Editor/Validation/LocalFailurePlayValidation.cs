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
    public static class LocalFailurePlayValidation
    {
        private const string Key="NutSort.LocalFailure";
        private static double timeout;
        private static float waitUntil,failedAt;
        private static int phase,loseSounds,clickSounds;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        static LocalFailurePlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            var board=new OriginalBoardState(new LevelData{B=new[]{Rod(1,2,3,4),Rod(4,3,2,1),Rod()}},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults")){Level=4,NewGameplayUnlockIndex=1,LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))};
            user.IsCompleteRecordGuide=true;
            OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Native local failure");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static ScrewData Rod(params int[] colors)
        {var cells=new CData[4];for(int i=0;i<4;i++)cells[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};return new ScrewData{Id=1,C=cells};}
        private static ScrewOperation Click(int i){Physics.SyncTransforms();return game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(i).Bounds.bounds.center));}
        private static void Sound(string name){if(name=="GameLose")loseSounds++;if(name=="Click")clickSounds++;}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Native local failure timeout phase="+phase);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(audio==null){audio=UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();if(audio!=null)audio.SoundStarted+=Sound;}
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone)return;
                var local=startup.LocalGameplay;
                Check(game.User.ServerConfigData==null&&game.User.GoldRewardTargetS2CData!=null&&game.User.Gold==0&&game.User.Coin==0,"Captured config loaded without a grant or remote server state");
                if(Time.time<waitUntil)return;
                if(phase==0){waitUntil=Time.time+1.5f;phase=1;return;}
                if(phase==1)
                {
                    Check(Click(0).Kind==ScrewOperationKind.Ready&&Click(2).Kind==ScrewOperationKind.Moved&&game.IsFail,"Actual world transfer triggers original failure");
                    Check(local.FailurePanel==null,"Failure panel is deferred");failedAt=Time.time;phase=2;return;
                }
                if(phase==2)
                {
                    if(local.FailurePanel==null)return;
                    var panel=local.FailurePanel;var source=Resources.Load<GameObject>("Prefabs/Panels/FailPanel").GetComponent<OriginalFailurePanelView>();
                    Check(Time.time-failedAt>=2.9f&&loseSounds==1,"Native failure delay and exactly one lose sound");
                    Check(local.ResultVisible&&game.ModalInputBlocked&&Click(0).Kind==ScrewOperationKind.Ignored,"Registered failure panel blocks world input");
                    Check(!((GameObject)new SerializedObject(local).FindProperty("resultPanel").objectReferenceValue).activeSelf,"Generic local card is not the failure UI");
                    Check(panel.transform.Find("label_67").GetComponent<TMPro.TMP_Text>().text==game.Tables.Text.GetText(67,"en"),"Original no-moves text");
                    var actual=(RectTransform)panel.Restart.transform;var expected=(RectTransform)source.Restart.transform;
                    Check(actual.anchoredPosition==expected.anchoredPosition&&actual.sizeDelta==expected.sizeDelta,"Original Restart layout retained");
                    Check(!panel.Revive.gameObject.activeInHierarchy&&!panel.CoinValue.gameObject.activeInHierarchy,"Local prefab hides SDK-only revive and reward value");
                    ScreenCapture.CaptureScreenshot("Library/local-native-failure-current.png");phase=3;return;
                }
                if(phase==3)
                {
                    var button=local.RetryButton;var rect=(RectTransform)button.transform;
                    var canvas=button.GetComponentInParent<Canvas>();
                    var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
                    Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Actual UI raycast resolves native Restart Button; position="+data.position+" hit="+(hits.Count>0?hits[0].gameObject.name:"none")+" expected="+button.gameObject.name);
                    ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);button.onClick.Invoke();
                    Check(game.IsRestarting&&!game.IsFail&&game.User.LevelSeed==1&&local.FailurePanel==null&&clickSounds==1,"Native restart/close order, duplicate gate and single click sound");
                    var saved=Saved();Check(saved.LevelSeed==1&&string.IsNullOrEmpty(saved.LevelInfo),"Pending new board checkpoint survives immediate exit");phase=4;return;
                }
                if(phase==4)
                {
                    Check(!local.ResultVisible&&!game.ModalInputBlocked&&game.User.LevelSeed==1,"Original restart returns playable board");
                    Check(Saved().LevelInfo==OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot()),"Rebuilt board saved through readiness checkpoint");
                    phase=5;game=null;startup=null;audio=null;SceneManager.LoadScene("LuoSiSortGame");return;
                }
                Check(game.User.LevelSeed==1&&!game.IsFail&&!local.ResultVisible&&!game.ModalInputBlocked,"Reload after retry retains new seed without failure popup");
                Debug.Log("NUT_LOCAL_FAILURE_PLAY_PASS real deadlocking transfer, original failure delay/prefab/header/restart layout/lose sound, actual UI raycast dispatch, native seed-reset-close and duplicate guard, pending/rebuilt save and reload; local variant hides SDK reward and revive; no fake rewards.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static OriginalUserLocalData Saved()=>OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
