using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NutSort.Validation
{
    // Replays observed responses, not live transport or invented reward fixtures.
    [InitializeOnLoad]
    public static class CapturedRewardParityPlayValidation
    {
        private const string Key="NutSort.CapturedRewardParity";
        private static double timeout;
        private static float ready;
        private static int phase,caseIndex,requests;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalSuccessPanel panel;
        private static OriginalItemGetInfo info;
        private static Action<bool> adCompletion;
        private static Action<JObject> receiveClaim;
        private static bool requestedMore,closed;
        private static readonly JArray results=new JArray();
        private static readonly JObject countryFormatting=new JObject();
        private static readonly string[] expected={"$0.40","$0.60","$4.98"};
        static CapturedRewardParityPlayValidation()
        {if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"Level\":4,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(720,1280,"Original reward capture parity");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static JObject Read(string name)=>JObject.Parse(File.ReadAllText("Docs/RewardEvidence/"+name+".json"));
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Captured reward parity timeout");
                if(phase==0)
                {
                    game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                    startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                    if(game==null||startup==null||startup.MainLevel==null||game.InputBlocked)return;
                    var decoded=JObject.Parse(OriginalContentCodec.Decrypt((string)Read("reward-config")["kinetic_data"]["bear_delay_info"],"octluositxoctluo","873hs61k7q3l3eyk"));
                    Check(JToken.DeepEquals(decoded,Read("reward-rules-decoded")),"Complete encrypted rule document equals independent decoder output");
                    Check(((JArray)decoded["shuaijianList"]).Count==13,"All thirteen observed decay bands");
                    foreach(string culture in new[]{"en-US","pt-BR","ja-JP","id-ID","ur-PK"})
                        countryFormatting[culture]=new OriginalGoldFormatter(()=>culture).Format(4.98f);
                    Check((string)countryFormatting["en-US"]=="$4.98"&&((string)countryFormatting["pt-BR"]).Contains("4,98"),"Local country formatting changes representation of same number");
                    game.User.GoldRewardTargetS2CData=(JObject)Read("reward-config")["kinetic_data"];
                    ShowCase();phase=1;return;
                }
                if(Time.time<ready)return;
                if(phase==1)
                {
                    bool early=caseIndex<2;
                    Check(panel.Views.Count==1&&panel.Views[0].Count.text==expected[caseIndex],"Amount matches original screenshot: "+expected[caseIndex]);
                    Check(panel.MoreLabel.text==(early?"Withdraw":"Claim 10X"),"Native caption matches original");
                    Check(panel.GetButton.gameObject.activeSelf==!early&&panel.Ad.activeSelf==!early,"Native ordinary/ad visibility matches original");
                    Check(!panel.Views[0].Max.activeSelf,"Native Max remains hidden");
                    Check(game.User.Gold==507.78f,"Preview never grants the reward");
                    results.Add(new JObject{{"case",caseIndex+1},{"cashText",panel.Views[0].Count.text},{"moreCaption",panel.MoreLabel.text},{"ordinaryVisible",panel.GetButton.gameObject.activeSelf},{"adVisible",panel.Ad.activeSelf},{"baseAmount",info.ItemInfos[0].Count},{"moreAmount",info.ItemInfos[0].MoreCount}});
                    ScreenCapture.CaptureScreenshot("Library/reward-parity-level"+(caseIndex+1)+".png");
                    ready=Time.time+.3f;phase=2;return;
                }
                if(phase==2)
                {
                    if(caseIndex<2){UnityEngine.Object.Destroy(panel.gameObject);caseIndex++;ShowCase();phase=1;return;}
                    // The original had two ordinary lucky claims before this click.
                    panel.GetButton.onClick.Invoke();
                    Check(adCompletion!=null&&requests==0,"Ordinary Claim waits for observed ad callback");
                    adCompletion(true); // Replay only: original callback was observed after closing the real ad.
                    Check(requestedMore&&requests==1&&receiveClaim!=null,"Normal-ad callback requests the multiplied server reward");
                    Check(game.User.Gold==507.78f,"No grant before server claim response");
                    receiveClaim(Read("level3-claim"));
                    Check(Mathf.Abs(game.User.Gold-557.58f)<.0001f,"Absolute server balance matches original device 557.58");
                    Check(game.User.NormalGetTimes==0&&game.User.InterAdTimes==1,"Ad counters match original saved data");
                    ready=Time.time+.5f;phase=3;return;
                }
                Check(closed,"Claim closes native success panel after response");
                File.WriteAllText("Library/reward-parity-results.json",new JObject{{"status","PASS"},{"cases",results},{"claimAmount",49.8},{"balance",game.User.Gold},{"normalClaimAfterAdRequestsMore",requestedMore},{"sameNumericInputCountryFormatting",countryFormatting},{"crossCountryServerComparison","Not sampled"},{"crossUserServerComparison","Not sampled"}}.ToString());
                Debug.Log("NUT_CAPTURED_REWARD_PARITY_PASS original config/decryption, native settlement labels/buttons, ad dispatch, real claim response and absolute balance");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void ShowCase()
        {
            game.User.Level=caseIndex+2;game.User.Gold=507.78f;
            game.User.NormalGetTimes=2;game.User.InterAdTimes=0;
            new OriginalSuccessSettlementFlow(game.User,()=>{},id=>false,a=>a(),()=>{},(id,value)=>{Check(id==9,"Settlement routes to original success panel");info=value;})
                .Run(Read(caseIndex==2?"level3-settlement":"level2-settlement"));
            // First branch reuses the captured empty-amount envelope to test the
            // Level==2 config fallback; its expected $0.40 was observed on device.
            panel=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/Panels/SuccessPanel"),startup.MainLevel.transform.parent,false).GetComponent<OriginalSuccessPanel>();
            var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"),new OriginalGoldFormatter(()=>"en-US"),()=>"US");
            var inventory=new OriginalItemManager(game.User,()=>{},()=>{},(v,a,b)=>game.User.Gold=v,(v,a)=>game.User.Coin=v);
            var reward=new OriginalSuccessRewardFlow((more,callback)=>{requestedMore=more;requests++;receiveClaim=callback;},(id,value)=>
            {
                Check(id==8&&value.ItemInfos.Count==1,"Cash-only actual claim response");
                Check(Mathf.Abs(value.ItemInfos[0].Count-49.8f)<.0001f,"Actual claim amount, not preview amount");
                foreach(var item in value.ItemInfos)inventory.Add(item);
            },panel.ClosePanel);
            var serverConfig=Read("server-config-persisted");
            var claims=new OriginalRewardGetFlow(game.User,()=>(int)serverConfig["LSSLRFP"],()=>(int)serverConfig["LSSLRFV"],cb=>adCompletion=cb,(cb,option)=>adCompletion=cb,()=>0,v=>{},reward.GetReward);
            panel.Bind(new OriginalSuccessPanelFlow(game.User,()=>0,()=>{},id=>{}),claims,factory,game.Tables,"en",()=>true,s=>{},()=>closed=true,a=>a(),game.ScheduleDelay,new OriginalPanelActionQueue());
            panel.Init(info);panel.Refresh();ready=Time.time+1;
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
