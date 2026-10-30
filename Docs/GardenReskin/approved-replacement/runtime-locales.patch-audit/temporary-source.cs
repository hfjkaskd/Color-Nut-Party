using System;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalWithdrawalPlayValidation
    {
        const string Key="NutSort.LocalWithdrawal";
        static double timeout;static float ready;static int phase,index;
        static LocalWithdrawalPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"Level\":4,\"Gold\":1.76,\"Coin\":0,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");PlayModeWindow.SetCustomRenderingResolution(720,1280,"Live withdrawal entry");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Withdrawal integration timeout phase="+phase);
                var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.InputBlocked||game.IsRestarting||!game.IsInitDone||startup.Replay.IsClickMasked||Time.time<ready)return;
                var local=startup.LocalGameplay;
                if(phase==0)
                {
                    UnityEngine.Object.FindObjectOfType<OriginalUserSession>().ConfigureRewardCountry(index==0?"BR":"IDN");
                    local.RewardsTop.Gold.Click.onClick.Invoke();local.RewardsTop.Gold.Click.onClick.Invoke();
                    Check(local.WithdrawalPanel!=null&&!local.ResultVisible,"Cash opens withdrawal prefab rather than generic notice");
                    Check(UnityEngine.Object.FindObjectsOfType<OriginalWithdrawalPanel>().Length==1&&game.ModalInputBlocked,"Duplicate click guarded and board blocked");
                    ready=Time.time+1;phase=1;return;
                }
                if(phase==1)
                {
                    var panel=local.WithdrawalPanel;
                    Check(Vector3.Distance(panel.Main.localScale,Vector3.one)<.001f,"Native panel opening animation");
                    Check(panel.GoldLabel.text==new OriginalGoldFormatter(()=>index==0?"pt-BR":"id-ID").Format(1.76f),"Live cash amount in country format");
                    Check(panel.ProgressLabel.text=="3/5"&&Mathf.Abs(panel.Fill-.6f)<.001f,"Current level withdrawal progress");
                    ScreenCapture.CaptureScreenshot("Library/local-withdrawal-cash-"+(index==0?"BR":"ID")+".png");
                    ready=Time.time+.2f;phase=101;return;
                }
                if(phase==101)
                {
                    var panel=local.WithdrawalPanel;
                    panel.WithdrawalButton.onClick.Invoke();Check(game.User.Gold==1.76f&&game.User.Coin==0,"Unmet withdrawal cannot alter balances");
                    ready=Time.time+.8f;phase=10;return;
                }
                if(phase==10)
                {
                    Check(local.WithdrawalAccount!=null&&!local.WithdrawalPanel.gameObject.activeSelf,"Cash Withdraw opens account form");
                    ScreenCapture.CaptureScreenshot("Library/local-withdrawal-account-"+(index==0?"BR":"ID")+".png");
                    ready=Time.time+.3f;phase=17;return;
                }
                if(phase==17)
                {
                    local.WithdrawalAccount.Name="";local.WithdrawalAccount.Email="";local.WithdrawalAccount.Number="";
                    local.WithdrawalAccount.GetButton.onClick.Invoke();
                    Check(!local.WithdrawalAccount.Closing&&UnityEngine.Object.FindObjectOfType<OriginalPopTip>()!=null,"Blank account rejected visibly");
                    ready=Time.time+.4f;phase=11;return;
                }
                if(phase==11)
                {
                    local.WithdrawalAccount.Name="Test User";local.WithdrawalAccount.Email="fixture@example.test";local.WithdrawalAccount.Number="123456789";
                    local.WithdrawalAccount.GetButton.onClick.Invoke();
                    Check(local.WithdrawalConfirmation!=null,"Account routes to confirmation");ready=Time.time+.7f;phase=12;return;
                }
                if(phase==12)
                {
                    Check(local.WithdrawalAccount==null,"Form releases after confirmation opens");
                    local.WithdrawalConfirmation.ReenterButton.onClick.Invoke();ready=Time.time+.7f;phase=14;return;
                }
                if(phase==14)
                {
                    Check(local.WithdrawalAccount!=null&&local.WithdrawalAccount.Name=="Test User","Reenter restores entered account");
                    local.WithdrawalAccount.ChannelButton(1).onClick.Invoke();Check(local.WithdrawalAccount.Flow.GetTypeIndex==1,"Channel selection switches");
                    ready=Time.time+.4f;phase=15;return;
                }
                if(phase==15){local.WithdrawalAccount.GetButton.onClick.Invoke();ready=Time.time+.7f;phase=16;return;}
                if(phase==16){local.WithdrawalConfirmation.ConfirmButton.onClick.Invoke();ready=Time.time+.7f;phase=13;return;}
                if(phase==13)
                {
                    Check(local.WithdrawalConfirmation==null&&local.WithdrawalPanel.gameObject.activeSelf&&game.ModalInputBlocked,"Confirmation returns to cash modal");
                    Check(game.User.Gold==1.76f&&game.User.Coin==0,"Account submission does not pay out");
                    game.User.UserLssInfo=null;phase=2;return;
                }
                if(phase==2){local.WithdrawalPanel.CloseButton.onClick.Invoke();ready=Time.time+.7f;phase=3;return;}
                if(phase==3)
                {
                    Check(local.WithdrawalPanel==null&&!game.ModalInputBlocked,"Close restores world input");
                    local.RewardsTop.Coin.Click.onClick.Invoke();Check(local.DiamondWithdrawalPanel!=null,"Coin Cash opens dedicated diamond panel");
                    ready=Time.time+.7f;phase=4;return;
                }
                if(phase==4)
                {
                    Check(local.DiamondWithdrawalPanel.BalanceLabel.text==new OriginalGoldFormatter(()=>index==0?"pt-BR":"id-ID").Format(0),"Coin balance displayed");
                    Check(local.DiamondWithdrawalPanel.ProgressLabel.text.Contains(index==0?"500":"1.000.000"),"Country-specific diamond target");
                    ScreenCapture.CaptureScreenshot("Library/local-withdrawal-coin-"+(index==0?"BR":"ID")+".png");
                    ready=Time.time+.4f;phase=5;return;
                }
                if(phase==5){local.DiamondWithdrawalPanel.Options[3].onClick.Invoke();Check(local.DiamondWithdrawalPanel.SelectedIndex==3,"Fourth tier selected");ready=Time.time+.4f;phase=7;return;}
                if(phase==7)
                {
                    local.DiamondWithdrawalPanel.WithdrawalButton.onClick.Invoke();
                    var tip=UnityEngine.Object.FindObjectOfType<OriginalPopTip>();
                    Check(tip!=null&&tip.Content.text.Contains(index==0?"2.000":"8.000.000"),"Withdraw shows selected tier insufficient balance feedback");
                    Check(game.User.Coin==0&&game.User.Gold==1.76f,"Withdraw feedback preserves balances");
                    ready=Time.time+.2f;phase=8;return;
                }
                if(phase==8){ScreenCapture.CaptureScreenshot("Library/local-diamond-withdraw-feedback-"+(index==0?"BR":"ID")+".png");ready=Time.time+.4f;phase=9;return;}
                if(phase==9){local.DiamondWithdrawalPanel.CloseButton.onClick.Invoke();ready=Time.time+.7f;phase=6;return;}
                Check(local.WithdrawalPanel==null&&local.DiamondWithdrawalPanel==null&&!game.ModalInputBlocked&&game.User.Gold==1.76f,"No stale modal or payout side effect");
                if(index++==0){phase=0;return;}
                Debug.Log("NUT_LOCAL_WITHDRAWAL_PASS actual cash/coin buttons, BR/ID amounts and progress, native panel animation, duplicate guard, no payout and close input restoration");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
