using System;
using NutSort.UI;using NutSort.World;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
namespace NutSort.Validation{
[InitializeOnLoad] public static class LocalToolOfferPlayValidation{
const string Key="NutSort.ToolOffer";static int phase,requests;static float ready;static double deadline;static Action<bool> done;
static LocalToolOfferPlayValidation(){if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
public static void Run(){OriginalPreferenceFixture.Begin("{\"IsCompleteRecordGuide\":true,\"Level\":4,\"NewGameplayUnlockIndex\":1,\"ExchangeCount\":0}");EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");PlayModeWindow.SetCustomRenderingResolution(720,1280,"Tool offer");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
static void Tick(){if(!EditorApplication.isPlaying)return;try{
Check(EditorApplication.timeSinceStartup<deadline,"Timeout");var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||!game.IsInitDone||game.InputBlocked||Time.time<ready||startup.Replay.IsClickMasked)return;var local=startup.LocalGameplay;
if(phase==0){local.BindRewardedToolAds((type,callback)=>{requests++;done=callback;});local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();Check(local.ToolOffer!=null&&requests==0&&game.ModalInputBlocked,"Offer appears before ad");ready=Time.time+.4f;phase=1;return;}
if(phase==1){ScreenCapture.CaptureScreenshot("Library/local-tool-offer-refresh.png");ready=Time.time+.3f;phase=2;return;}
if(phase==2){local.ToolOffer.CloseButton.onClick.Invoke();ready=Time.time+.5f;phase=3;return;}
if(phase==3){Check(local.ToolOffer==null&&!game.ModalInputBlocked&&requests==0&&game.User.ExchangeCount==0,"Close cancels without advertising or reward");local.Bottom.GetOtherItem(3).Display.Click.onClick.Invoke();ready=Time.time+.5f;phase=4;return;}
if(phase==4){local.ToolOffer.ClaimButton.onClick.Invoke();Check(local.ToolOffer==null&&local.ToolAdPending&&requests==1&&game.User.ExchangeCount==0,"Claim requests ad without early grant");done(true);done(true);Check(game.User.ExchangeCount==1&&game.IsExchanging&&!local.ToolAdPending,"Success grants once and activates tool");ready=Time.time+.5f;phase=5;return;}
local.ExchangeMask.onClick.Invoke();Debug.Log("NUT_LOCAL_TOOL_OFFER_PASS original prefab, close/no-ad, Claim/ad, success/one grant and exchange");Finish(0);
}catch(Exception e){Debug.LogException(e);Finish(1);}}
static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
}}
