using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalWithdrawalConditionsPlayValidation
    {
        private const string Key="NutSort.WithdrawalConditions.Validation";
        private static int phase;
        private static double deadline;
        private static float ready;
        private const string Output="Docs/WithdrawalImplementation";
        private static Canvas previewCanvas;
        private static LocalWithdrawalConditionsPanel previewPanel;
        static LocalWithdrawalConditionsPlayValidation()
        {if(SessionState.GetBool(Key,false)){deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            ValidatePolicy();
            LocalWithdrawalConditionsBuilder.Run();
            OriginalPreferenceFixture.Begin("{\"Level\":9,\"Gold\":9.71,\"Coin\":512.64,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":4,\"IsCompleteExtraGoldGuide\":true,\"IsCompleteCoinNewPeopleReward\":true}");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(720,1280,"Withdrawal requirements review");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void ValidatePolicy()
        {
            var defaults=ScriptableObject.CreateInstance<OriginalUserDefaults>();
            var user=new OriginalUserLocalData(defaults);
            var tables=new OriginalTables(Resources.Load<OriginalTableSettings>("Configuration/OriginalTables"));
            var config=JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalWithdrawal").text);
            user.Level=3;user.Gold=1;
            Check(LocalWithdrawalPolicy.ApplyTutorialBalance(user,"US",config)&&user.Gold==500,"Tutorial balance is absolute 500");
            Check(!LocalWithdrawalPolicy.ApplyTutorialBalance(user,"US",config)&&user.Gold==500,"Tutorial balance idempotent");
            var restored=OriginalUserDataJson.Read(OriginalUserDataJson.Write(user),defaults);
            Check(!LocalWithdrawalPolicy.ApplyTutorialBalance(restored,"US",config),"Initialization marker survives reload");
            user.LocalWithdrawalData=null;user.Level=9;
            Check(!LocalWithdrawalPolicy.ApplyTutorialBalance(user,"US",config),"No speculative old-save grant");
            user.TodayPassLevelCount=3;
            var date=new DateTime(2026,10,9);
            LocalWithdrawalPolicy.RefreshCalendar(user,date);
            Check(user.TodayPassLevelCount==3&&user.LoginDay==1,"First calendar entry preserves current progress");
            Check(!LocalWithdrawalPolicy.RefreshCalendar(user,date)&&user.LoginDay==1,"Same day no duplicate login");
            LocalWithdrawalPolicy.RefreshCalendar(user,date.AddDays(1));
            Check(user.TodayPassLevelCount==0&&user.LoginDay==2,"Next day resets daily count and increments once");
            Check(!LocalWithdrawalPolicy.RefreshCalendar(user,date)&&user.LoginDay==2,"Backwards clock no rewards");
            user.GoldRewardTargetS2CData=JObject.Parse(Resources.Load<TextAsset>("Configuration/CountryRewards/BR-reward-config").text);
            var row=(JObject)user.GoldRewardTargetS2CData["bear_list"][2];row["RealLevel"]=22;row["Stage2RealLevel"]=51;
            user.Gold=1000;user.Coin=50000;user.LoginDay=5;user.LoginDayCoin=5;user.TodayPassLevelCount=10;user.UserLevel=31;
            user.TXTargetGold="500";user.Level=22;
            Check(!LocalWithdrawalPolicy.Evaluate(user,tables,false,0).Eligible,"Final subround must be completed");
            user.Level=52;
            Check(LocalWithdrawalPolicy.Evaluate(user,tables,false,0).Eligible,"Cash complete requirements");
            user.TXTargetGold=null;
            Check(!LocalWithdrawalPolicy.Evaluate(user,tables,false,0).Eligible,"Missing cash target never authorizes withdrawal");
            Check(LocalWithdrawalPolicy.Evaluate(user,tables,true,0).Eligible,"Diamond tier zero complete");
            Check(!LocalWithdrawalPolicy.Evaluate(user,tables,true,1).Eligible,"Selected diamond tier checked independently");
            user.TodayPassLevelCount=0;
            Check(LocalWithdrawalPolicy.Evaluate(user,tables,true,0).Eligible,"Completed login stage supersedes daily stage as native");
            user.LoginDayCoin=0;
            Check(!LocalWithdrawalPolicy.Evaluate(user,tables,true,0).Eligible,"Cash login cannot satisfy diamond login");
            UnityEngine.Object.DestroyImmediate(defaults);
            Debug.Log("NUT_LOCAL_WITHDRAWAL_POLICY_PASS");
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<deadline,"Withdrawal integration timed out phase "+phase);
                var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.InputBlocked||game.IsRestarting||!game.IsInitDone||startup.Replay.IsClickMasked||Time.time<ready)return;
                var local=startup.LocalGameplay;
                if(phase==0){local.RewardsTop.Gold.Click.onClick.Invoke();ready=Time.time+1;phase++;return;}
                if(phase==1)
                {
                    Check(local.WithdrawalPanel!=null,"Cash entry opens");
                    ScreenCapture.CaptureScreenshot(Output+"/cash-before.png");
                    local.WithdrawalPanel.WithdrawalButton.onClick.Invoke();
                    Check(local.WithdrawalConditions!=null&&local.WithdrawalAccount==null,"Cash cannot bypass unmet requirements");
                    ready=Time.time+.8f;phase++;return;
                }
                if(phase==2){Capture("cash-replaced.png");ready=Time.time+.3f;phase=200;return;}
                if(phase==200){local.WithdrawalConditions.ActionButton.onClick.Invoke();ready=Time.time+.8f;phase=3;return;}
                if(phase==3)
                {
                    Check(local.WithdrawalAccount==null&&local.WithdrawalPanel==null&&!game.ModalInputBlocked,"Continue returns to actual gameplay without account");
                    game.User.Level=23;local.RewardsTop.Gold.Click.onClick.Invoke();ready=Time.time+.8f;phase=301;return;
                }
                if(phase==301){local.WithdrawalPanel.WithdrawalButton.onClick.Invoke();ready=Time.time+.8f;phase=4;return;}
                if(phase==4){Capture("cash-stage2-replaced.png");ready=Time.time+.3f;phase=400;return;}
                if(phase==400){local.WithdrawalConditions.ActionButton.onClick.Invoke();ready=Time.time+4;phase=5;return;}
                if(phase==5)
                {
                    Check(local.WithdrawalStage!=null&&local.WithdrawalAccount==null,"Stage two routes original progress prefab");
                    ScreenCapture.CaptureScreenshot(Output+"/progress-replaced.png");
                    ready=Time.time+.3f;phase=500;return;
                }
                if(phase==500){ready=Time.time+3;phase=501;return;}
                if(phase==501)
                {
                    Check(local.WithdrawalTooFast!=null,"Original progress routes operation hint");
                    Check(local.WithdrawalTooFast.VideoValue.text=="0/10"&&local.WithdrawalTooFast.VideoFill.fillAmount==0,"No fabricated watched videos");
                    ScreenCapture.CaptureScreenshot(Output+"/operation-hint-replaced.png");ready=Time.time+.3f;phase=502;return;
                }
                if(phase==502){local.WithdrawalTooFast.SureButton.onClick.Invoke();ready=Time.time+4;phase=503;return;}
                if(phase==503){Check(local.WithdrawalTooFast==null,"Hint confirmation returns to progress");ScreenCapture.CaptureScreenshot(Output+"/progress-complete-replaced.png");ready=Time.time+.3f;phase=504;return;}
                if(phase==504){local.WithdrawalStage.CloseButton.onClick.Invoke();ready=Time.time+.8f;phase=6;return;}
                if(phase==6){Check(local.WithdrawalStage==null,"Stage close removes modal");local.WithdrawalPanel.CloseButton.onClick.Invoke();ready=Time.time+.8f;phase++;return;}
                if(phase==7){local.RewardsTop.Coin.Click.onClick.Invoke();ready=Time.time+.6f;phase++;return;}
                if(phase==8){local.DiamondWithdrawalPanel.WithdrawalButton.onClick.Invoke();Check(local.WithdrawalConditions!=null,"Diamond opens full requirements");ready=Time.time+.6f;phase++;return;}
                if(phase==9){Capture("diamond-replaced.png");ready=Time.time+.3f;phase=900;return;}
                if(phase==900){local.WithdrawalConditions.CloseButton.onClick.Invoke();ready=Time.time+.5f;phase=10;return;}
                if(phase==10)
                {
                    game.User.Coin=50000;game.User.LoginDayCoin=5;game.User.TodayPassLevelCount=10;game.User.UserLevel=31;
                    local.DiamondWithdrawalPanel.WithdrawalButton.onClick.Invoke();ready=Time.time+.5f;phase++;return;
                }
                if(phase==11){Capture("diamond-ready-replaced.png");ready=Time.time+.3f;phase=1100;return;}
                if(phase==1100){local.WithdrawalConditions.ActionButton.onClick.Invoke();Check(local.WithdrawalAccount!=null,"Eligible diamond opens shared account");ready=Time.time+.8f;phase=12;return;}
                if(phase==12)
                {
                    local.WithdrawalAccount.Name="Review User";local.WithdrawalAccount.Email="review@example.test";local.WithdrawalAccount.Number="123456789";
                    local.WithdrawalAccount.GetButton.onClick.Invoke();Check(local.WithdrawalConfirmation!=null,"Account confirmation present");ready=Time.time+.8f;phase++;return;
                }
                if(phase==13)
                {
                    game.User.Coin=0;local.WithdrawalConfirmation.ConfirmButton.onClick.Invoke();
                    Check(local.WithdrawalConditions!=null,"Confirmation rechecks changed balance");ready=Time.time+.8f;phase++;return;
                }
                if(phase==14){local.WithdrawalConditions.CloseButton.onClick.Invoke();ready=Time.time+.6f;phase++;return;}
                if(phase==15){local.DiamondWithdrawalPanel.CloseButton.onClick.Invoke();ready=Time.time+.7f;phase++;return;}
                if(phase==16)
                {
                    Check(!game.ModalInputBlocked&&local.WithdrawalConditions==null&&local.WithdrawalAccount==null&&local.WithdrawalConfirmation==null,"Modal input restored");
                    Check(Math.Abs(game.User.Gold-9.71f)<.001f,"Withdrawal never debits cash");
                    BuildPreview(game,false);ready=Time.time+.5f;phase=17;return;
                }
                if(phase==17){ScreenCapture.CaptureScreenshot(Output+"/cash-effect.png");ready=Time.time+.3f;phase=18;return;}
                if(phase==18){UnityEngine.Object.Destroy(previewPanel.gameObject);BuildPreview(game,true);ready=Time.time+.5f;phase=19;return;}
                if(phase==19){ScreenCapture.CaptureScreenshot(Output+"/diamond-effect.png");ready=Time.time+.3f;phase=20;return;}
                if(phase==20){Debug.Log("NUT_LOCAL_WITHDRAWAL_CONDITIONS_PLAY_PASS");Finish(0);}
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        private static void BuildPreview(OriginalGameScene game,bool diamonds)
        {
            if(previewCanvas==null)
            {
                foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())canvas.enabled=false;
                foreach(var camera in UnityEngine.Object.FindObjectsOfType<Camera>())camera.enabled=false;
                var cameraObject=new GameObject("Review camera",typeof(Camera));var reviewCamera=cameraObject.GetComponent<Camera>();reviewCamera.clearFlags=CameraClearFlags.SolidColor;reviewCamera.backgroundColor=new Color(.93f,.87f,.73f);reviewCamera.cullingMask=0;
                var canvasObject=new GameObject("Prefab effect review",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
                previewCanvas=canvasObject.GetComponent<Canvas>();previewCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            }
            game.User.Level=9;game.User.Coin=512.64;game.User.LoginDayCoin=1;game.User.LoginDay=1;game.User.TodayPassLevelCount=0;game.User.UserLevel=0;
            previewPanel=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/Panels/LocalWithdrawalConditions"),previewCanvas.transform,false).GetComponent<LocalWithdrawalConditionsPanel>();
            previewPanel.Init(JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalWithdrawal").text),game.Tables,diamonds,new OriginalGoldFormatter(()=>"en-US"),
                ()=>LocalWithdrawalPolicy.Evaluate(game.User,game.Tables,diamonds,0),()=>false,result=>{},()=>{});
        }
        private static void Capture(string filename)
        {
            var panel=UnityEngine.Object.FindObjectOfType<LocalWithdrawalConditionsPanel>();Check(panel!=null,"Conditions rendered");
            Canvas.ForceUpdateCanvases();
            foreach(var row in panel.Rows)
                if(row.Root.activeInHierarchy){row.Name.ForceMeshUpdate();row.Value.ForceMeshUpdate();row.Status.ForceMeshUpdate();Check(!row.Name.isTextOverflowing&&!row.Value.isTextOverflowing&&!row.Status.isTextOverflowing,"Requirement text fits prefab");}
            ScreenCapture.CaptureScreenshot(Output+"/"+filename);
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
