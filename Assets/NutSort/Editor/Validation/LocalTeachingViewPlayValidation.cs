using System;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalTeachingViewPlayValidation
    {
        private const string Key="NutSort.LocalTeachingView";
        private static double timeout;
        private static float waitUntil;
        private static int seed,phase;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        static LocalTeachingViewPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"Level\":1,\"LevelSeed\":0,\"IsCompleteRecordGuide\":true}");EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Native teaching");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Teaching view timeout");
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone)return;
                if(Time.time<waitUntil)return;
                var local=startup.LocalGameplay;
                if(seed>=4)
                {
                    Check(game.PlayerLevel==2&&local.TeachingPanel==null&&!game.IsCanOperatorScrew&&!game.ModalInputBlocked,"Later local entry has no teaching modal or leaked override");
                    Check(local.RewardsTop.Coin.gameObject.activeInHierarchy&&local.RewardsTop.Hidden.gameObject.activeInHierarchy,"Normal diamond card and milestone row return after teaching");
                    Debug.Log("NUT_LOCAL_TEACHING_VIEW_PASS default four seeds render source teaching prefab/rule text without duplicate badge and with final text without withdrawal claims; native world selection works under teaching override; level two resets override and closes guide. Explicit starting-seed fixtures only.");Finish(0);return;
                }
                if(phase==0){Check(local.TeachingPanel!=null,"Default startup creates teaching view");waitUntil=Time.time+1;phase=1;return;}
                if(phase==1)
                {
                    var panel=local.TeachingPanel;var data=new SerializedObject(local);
                    Check(!local.RewardsTop.Coin.gameObject.activeInHierarchy&&!local.RewardsTop.Hidden.gameObject.activeInHierarchy,"Teaching progress has no overlapping normal HUD row");
                    string caption=data.FindProperty("localTeachingCaption").stringValue;
                    var labels=new SerializedObject(panel).FindProperty("labels");bool found=false;
                    for(int i=0;i<labels.arraySize;i++)
                    {
                        var label=labels.GetArrayElementAtIndex(i);if(label.FindPropertyRelative("Id").intValue!=1)continue;
                        Check(!((TMP_Text)label.FindPropertyRelative("Text").objectReferenceValue).gameObject.activeInHierarchy,"Duplicate teaching badge stays hidden");found=true;
                    }
                    Check(found,"Configured teaching badge found");
                    Check(panel.Tip.text==(seed>2?data.FindProperty("localFinalTeachingText").stringValue:game.Tables.Text.GetText(70+seed,"en")),"Teaching text follows original rules/local final boundary");
                    foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true))
                        if(text.gameObject.activeInHierarchy)Check(text.text.IndexOf("withdraw",StringComparison.OrdinalIgnoreCase)<0,"Local teaching must not promise an unavailable withdrawal");
                    ScreenCapture.CaptureScreenshot("Library/local-teaching-"+seed+"-current.png");phase=2;return;
                }
                Check(game.IsCanOperatorScrew&&game.ModalInputBlocked,"Native teaching uses registered-panel override");
                int target=-1;for(int i=0;i<game.Level.Board.Screws.Length;i++){var rod=game.Level.Board.Screws[i];if(!rod.IsDone&&!rod.IsNull&&!rod.IsHidden&&!rod.IsColorMask&&!rod.IsDontMove&&!rod.IsLocked){target=i;break;}}
                Check(target>=0,"Teaching board has a movable rod");Physics.SyncTransforms();
                Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(target).Bounds.bounds.center)).Kind==ScrewOperationKind.Ready,"Actual world input allowed by native teaching flag");
                seed++;phase=0;game=null;startup=null;
                PlayerPrefs.SetString(NutSort.Content.OriginalUserStore.Key,seed<4?"{\"IsCompleteRecordGuide\":true,\"Level\":1,\"LevelSeed\":"+seed+"}":"{\"IsCompleteRecordGuide\":true,\"Level\":2}");PlayerPrefs.Save();SceneManager.LoadScene("LuoSiSortGame");
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
