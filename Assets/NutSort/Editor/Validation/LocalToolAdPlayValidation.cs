using System;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace NutSort.Validation
{
 [InitializeOnLoad] public static class LocalToolAdPlayValidation
 {
  const string Key="NutSort.ToolAd";static int phase,requests;static float ready;static double deadline;static Action<bool> completion;
  static LocalToolAdPlayValidation(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
  public static void Run(){OriginalPreferenceFixture.Begin("{\"IsCompleteRecordGuide\":true,\"Level\":4,\"NewGameplayUnlockIndex\":1,\"ExchangeCount\":0,\"AddScrewCount\":0}");EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");PlayModeWindow.SetCustomRenderingResolution(720,1280,"Tool ad feedback");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
  static void Tick(){if(!EditorApplication.isPlaying)return;try{
   Check(EditorApplication.timeSinceStartup<deadline,"Timeout");var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
   if(game==null||startup==null||startup.LocalGameplay==null||!game.IsInitDone||game.IsRestarting||game.InputBlocked||Time.time<ready)return;var local=startup.LocalGameplay;
   if(phase==0){if(startup.Replay.IsClickMasked)return;local.BindRewardedToolAds((type,done)=>{requests++;completion=done;});local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();Check(requests==1&&local.ToolAdPending&&game.ModalInputBlocked&&game.User.ExchangeCount==0,"Pending ad guards duplicates and grants nothing");completion(false);Check(!local.ToolAdPending&&game.User.ExchangeCount==0&&!game.ModalInputBlocked,"Failure releases without granting");ready=Time.time+.6f;phase=1;return;}
   if(phase==1){local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();completion(true);completion(true);Check(game.User.ExchangeCount==1&&game.IsExchanging&&!local.ToolAdPending,"Success grants once and enters exchange");Check(UnityEngine.Object.FindObjectsOfType<OriginalPopTip>().Length>0,"Reward feedback exists");ready=Time.time+.5f;phase=2;return;}
   if(phase==2){local.ExchangeMask.onClick.Invoke();ready=Time.time+.8f;phase=3;return;}
   if(phase==3){local.BindRewardedToolAds(null);local.Bottom.GetOtherItem(4).Display.Click.onClick.Invoke();Check(local.ToolAdPending&&game.User.CurrentLevelAddScrewCount==0,"Configured simulation is asynchronous");ready=Time.time+.7f;phase=4;return;}
   Check(!local.ToolAdPending&&!game.ModalInputBlocked&&game.User.CurrentLevelAddScrewCount==4&&game.User.AddScrewCount==0,"Simulated success unlocks rod and consumes reward");ScreenCapture.CaptureScreenshot("Library/local-tool-ad-success.png");if(phase==4){ready=Time.time+.3f;phase=5;return;}Debug.Log("NUT_LOCAL_TOOL_AD_PASS failure/no-grant, duplicate requests/callbacks, success feedback, simulated callback, world rod unlock");Finish(0);
  }catch(Exception e){Debug.LogException(e);Finish(1);}}
  static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
 }
}
