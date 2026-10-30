using System;
using System.Collections.Generic;
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
    [InitializeOnLoad]
    public static class OriginalSuccessPanelHostPlayValidation
    {
        private const string Key="NutSort.SuccessHostPlay";
        private static double timeout;
        private static OriginalGameScene game;
        private static OriginalSuccessPanelHost host;
        private static OriginalRewardPanelHost reward;
        private static Action<bool> ad;
        private static Action<JObject> response;
        private static float start,gold;
        private static double coin;
        private static int phase,requests,hidden,grants,flies,queued;
        private static bool requestedMore;
        static OriginalSuccessPanelHostPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;}}
        public static void Run(){OriginalPreferenceFixture.Begin();EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Success host Play timeout");
                if(phase==0)
                {
                    game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();if(game==null||game.Level==null||game.IsRestarting||game.InputBlocked)return;
                    Transform parent=null;foreach(var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())if(canvas.name=="UICanvas")parent=canvas.transform;
                    Check(parent!=null,"Actual scene canvas");game.User.Level=4;game.User.NormalGetTimes=0;game.User.InterAdTimes=0;gold=game.User.Gold;coin=game.User.Coin;
                    var factory=new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"),new OriginalGoldFormatter(()=>"en-US"),()=>"US");
                    var queue=new OriginalPanelActionQueue();queue.Add(()=>queued++);queue.Add(()=>queued++);
                    // Explicit balance/flight consumers verify real panel dispatch without
                    // substituting production HUD setters, flight setup or SDK outcomes.
                    var items=new OriginalItemManager(game.User,()=>{},()=>{},(v,a,b)=>{Check(a&&b,"Native cash flags");game.User.Gold=v;grants++;},(v,a)=>{Check(a,"Native coin flag");game.User.Coin=v;grants++;});
                    reward=new OriginalRewardPanelHost(parent,"Prefabs/Panels/RewardPanel",factory,items,(type,icon)=>{Check(icon!=null,"Actual reward icon");flies++;},game.ScheduleDelay,queue);
                    var audio=UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
                    host=new OriginalSuccessPanelHost(parent,"Prefabs/Panels/SuccessPanel",factory,game.Tables,"en",
                        ()=>game.CreateSuccessPanelFlow(()=>123,id=>throw new InvalidOperationException("No guide in level four")),
                        get=>new OriginalRewardGetFlow(game.User,()=>1,()=>0,cb=>ad=cb,(cb,b)=>{Check(!b,"Rewarded option false");ad=cb;},()=>456,v=>{},get),
                        (more,cb)=>{requestedMore=more;requests++;response=cb;},
                        (id,info)=>{Check(id==8&&host.IsOpen&&grants==0,"Panel 8 opens while SuccessPanel remains registered, before grant");reward.Show(info);},
                        (flow,baseHide)=>flow.Hide(baseHide,()=>false,()=>false,()=>{},()=>{}),
                        ()=>true,s=>audio.PlaySound(s),game.ScheduleDelay,queue,()=>{Check(host.IsOpen,"Registration survives HideAction");hidden++;});
                    host.Show(new OriginalItemGetInfo{ItemInfos=new List<OriginalItemInfo>{new OriginalItemInfo{ItemType=0,Count=5},new OriginalItemInfo{ItemType=1,Count=3}}});
                    Check(host.IsOpen&&host.Panel.Views.Count==2,"Host initializes and refreshes actual SuccessPanel");
                    host.Refresh();Check(host.Panel.transform.Find("main/Items").childCount==4,"Host refresh preserves native append behavior");
                    host.Panel.GetButton.onClick.Invoke();Check(ad!=null&&requests==0&&!reward.IsOpen,"Button enters SDK boundary with no premature request");
                    ad(false);Check(requests==0&&grants==0,"Cancelled ad makes no settlement request");
                    host.Panel.GetButton.onClick.Invoke();ad(true);ad(true);
                    Check(requests==1&&requestedMore&&response!=null&&!reward.IsOpen,"Successful retry reaches settlement once");
                    start=Time.time;phase=1;return;
                }
                if(phase==1)
                {
                    if(Time.time-start<.4f)return;
                    Check(host.IsOpen&&!reward.IsOpen&&grants==0&&game.User.Gold==gold&&game.User.Coin==coin,"Pending server response retains SuccessPanel and original balances");
                    // Explicit response fixture; no real network/SDK operation.
                    response(JObject.Parse(@"{""kinetic_data"":{""hg_amt"":""5"",""hg_psi"":""100"",""hg_zs_amt"":""3"",""hg_zs_psi"":""200.25""}}"));
                    Check(host.IsOpen&&reward.IsOpen&&reward.Panel.Views.Count==2&&grants==0,"Response shows actual RewardPanel before animated SuccessPanel close");
                    start=Time.time;phase=2;return;
                }
                float elapsed=Time.time-start;
                if(phase==2)
                {
                    if(elapsed<.4f)return;
                    Check(!host.IsOpen&&host.Panel==null&&hidden==1&&reward.IsOpen&&grants==0,"Registry removes SuccessPanel after close/Hide, reward remains pending");phase=3;return;
                }
                if(phase==3)
                {
                    if(elapsed<1.2f)return;
                    Check(grants==2&&flies==2&&game.User.Gold==100&&game.User.Coin==200.25&&reward.IsOpen,"RewardPanel one-second delay reaches real ItemManager with server totals");phase=4;return;
                }
                if(elapsed<4.2f)return;
                Check(!host.IsOpen&&!reward.IsOpen&&hidden==1&&queued==2,"Both native delayed Hide queue callbacks consumed after registry destruction");
                Debug.Log("NUT_SUCCESS_PANEL_HOST_PLAY_PASS actual SuccessPanel registry/button -> deferred ad -> deferred success response -> actual RewardPanel -> animated success hide -> delayed ItemManager grants and reward hide/queue; fixture SDK/server/balance/flight consumers, production startup pending.");Finish(0);
            }
            catch(Exception error){Debug.LogException(error);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
