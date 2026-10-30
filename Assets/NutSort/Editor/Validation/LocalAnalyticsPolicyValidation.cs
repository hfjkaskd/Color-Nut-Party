using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;

namespace NutSort.Validation
{
    public static class LocalAnalyticsPolicyValidation
    {
        private sealed class MemoryPreferences:IOriginalUserPreferences
        {
            public readonly Dictionary<string,string> Values=new Dictionary<string,string>();
            public string GetString(string key,string fallback)=>Values.TryGetValue(key,out var value)?value:fallback;
            public void SetString(string key,string value){Values[key]=value;}
            public void Save(){}
        }
        private sealed class Recorder
        {
            public readonly List<KeyValuePair<string,Dictionary<string,object>>> Events=new List<KeyValuePair<string,Dictionary<string,object>>>();
            public readonly Dictionary<string,object> Properties=new Dictionary<string,object>();
            public void Track(string name,Dictionary<string,object> fields){Events.Add(new KeyValuePair<string,Dictionary<string,object>>(name,fields));}
            public void Props(Dictionary<string,object> fields){foreach(var p in fields)Properties[p.Key]=p.Value;}
            public int Count(string name){int count=0;foreach(var e in Events)if(e.Key==name)count++;return count;}
        }
        private static readonly JArray Checks=new JArray();
        private static void Check(bool value,string message){Checks.Add(new JObject{{"passed",value},{"check",message}});if(!value)throw new InvalidOperationException(message);}
        public static void Run()
        {
            Checks.Clear();var settings=ScriptableObject.CreateInstance<LocalAnalyticsSettings>();settings.IsTest=true;
            var defaults=Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults");
            var tables=new OriginalTables(Resources.Load<OriginalTableSettings>("Configuration/OriginalTables"));
            var prefs=new MemoryPreferences();var recorder=new Recorder();double now=0;long epoch=1791936000000;
            Func<LocalAnalyticsService> create=()=>
            {
                var engine=new LocalAnalyticsService(settings,prefs,()=>epoch+(long)(now*1000),()=>now,"1.0.1","1");
                engine.BindTransport(recorder.Track,recorder.Props);LocalAnalytics.Bind(engine);return engine;
            };
            try
            {
                var service=create();var store=new OriginalUserStore(defaults,prefs);var user=store.Data;
                service.BindUser(user,tables);service.BoardReady(false,4,8,2);store.SaveData(false,null);
                Check(recorder.Count("first_open")==1&&recorder.Count("session_start")==1,"Fresh local marker emits first_open and cold-start session once");
                service.ValidMove();service.ValidMove();store.SaveData(false,null);
                Check(recorder.Count("level_first_action")==1,"Two effective moves emit one first-action event");
                var restored=new OriginalUserStore(defaults,prefs);service.BindUser(restored.Data,tables);service.BoardReady(true,4,8,2);
                Check(recorder.Count("level_start")==1,"Saved attempt recovery keeps attempt ID without another start");
                service.LevelResult("fail","no_legal_move");service.LevelResult("restart","replay_confirmed");
                Check(recorder.Count("level_result")==1,"Failure followed by restart cannot produce two results for one attempt");
                service.NextBoardOrigin("failure_retry");service.BoardReady(false,4,8,2);
                Check(recorder.Count("level_start")==2,"New retry produces a distinct attempt");
                service.BindUser(user,tables);service.BoardReady(false,4,8,2);
                var manager=new OriginalItemManager(user,()=>store.SaveData(false,null),()=>{},(a,b,c)=>{},(a,b)=>{});
                int resources=recorder.Count("resource_change");
                using(service.Operation("rewarded_tool","tool-operation")){manager.AddTool(3,1,false);manager.AddTool(3,-1,false);}
                Check(recorder.Count("resource_change")==resources+2,"Grant then consume with net-zero balance retains two resource records");
                Check(recorder.Count("prop_use")==1,"Only actual tool consumption emits prop_use");
                var last=recorder.Events[recorder.Events.Count-1];
                Check(last.Key=="prop_use"&&(string)last.Value["operation_id"]=="tool-operation"&&!last.Value.ContainsKey("inventory_after"),"Tool behavior joins resource flow by operation without duplicate inventory fields");
                store.SaveData(false,null);Check(recorder.Count("resource_change")==resources+2,"Unchanged save does not manufacture resource flow");
                int index=(int)user.LocalAnalyticsData["mutation_index"];
                Check((int)new OriginalUserStore(defaults,prefs).Data.LocalAnalyticsData["mutation_index"]==index,"Mutation index persists with committed inventory");
                user.LocalPendingReward=new JObject{{"amount",1.25},{"diamonds",8},{"nextLevel",2}};string claim=LocalAnalytics.ClaimId(user.LocalPendingReward);
                using(service.Operation("level_reward","reward-operation")){Check(LocalCashSettlement.Claim(user),"Pending cash and diamond claim succeeds");store.SaveData(false,null);}
                Check(!LocalCashSettlement.Claim(user)&&recorder.Count("resource_change")==resources+4,"Repeated multi-resource claim cannot regrant or record extra flow");
                service.TutorialStep("welcome",0,false);service.TutorialStep("welcome",0,false);service.TutorialStep("welcome",0,true);
                for(int i=0;i<4;i++){service.TutorialStep("teaching_"+i,i+1,false);service.TutorialStep("teaching_"+i,i+1,true);}
                service.TutorialStep("teaching_3",4,true);
                Check(recorder.Count("tutorial_start")==1&&recorder.Count("tutorial_step_show")==5&&recorder.Count("tutorial_step_complete")==5&&recorder.Count("tutorial_complete")==1,"Welcome and four teaching steps deduplicate exposure and completion");
                service.BeginWithdrawal(false,0);var eligibility=new LocalWithdrawalEligibility();eligibility.Requirements.Add(new LocalWithdrawalRequirement{Key="balance",Current=1,Target=500});
                service.ObserveEligibility(eligibility,"test");service.ObserveEligibility(eligibility,"test");
                Check(recorder.Count("withdraw_eligibility_change")==1&&recorder.Count("withdraw_task_change")==1,"Identical requirements refresh does not duplicate snapshots");
                eligibility.Requirements[0].Current=500;service.ObserveEligibility(eligibility,"test");
                Check(recorder.Count("withdraw_eligibility_change")==2&&recorder.Count("withdraw_task_change")==2,"True qualification changes emit updated eligibility and task state");
                string submit=service.WithdrawSubmit(500,"PayPal");service.WithdrawResult(submit,"service_unavailable");
                Check(recorder.Count("withdraw_submit")==1&&recorder.Count("withdraw_submit_result")==1,"Both original withdrawal event names remain independent");
                var result=recorder.Events[recorder.Events.Count-1].Value;
                Check((string)result["operation_id"]==submit&&(string)result["result"]=="not_submitted"&&!(bool)result["request_sent"],"Withdrawal result retains the request ID and truthful local-only status");
                service.GameplayActive=true;now=61;service.Tick();service.Pause(true);now+=10;service.Pause(false);
                Check(recorder.Count("session_start")==1,"Short background stay keeps session");
                service.Pause(true);now+=1801;service.Pause(false);Check(recorder.Count("session_start")==2,"Long background stay creates one new session");
                service.Exception("game","sample","InvalidOperationException");service.Exception("game","sample","InvalidOperationException");service.Exception("game","sample","InvalidOperationException");
                Check(recorder.Count("runtime_exception")==1,"Repeated exception is aggregated instead of repeated immediately");
                now+=61;service.Tick();Check(recorder.Count("runtime_exception")==2,"Exception aggregate flushes in the next summary window");
                service.Quit();int abnormal=recorder.Count("abnormal_exit");service=create();Check(recorder.Count("abnormal_exit")==abnormal,"Normal quit closes the run marker");
                service=create();Check(recorder.Count("abnormal_exit")==abnormal+1,"Next cold launch records an unclosed run without claiming a crash");
                Check(recorder.Count("first_open")==1,"Repeated cold starts never manufacture first_open");
                var ids=new HashSet<string>();foreach(var e in recorder.Events)Check(ids.Add((string)e.Value["event_id"]),"Unique event ID: "+e.Key);
                File.WriteAllText(LocalAnalyticsOutput(),new JObject{{"passed",true},{"checks",Checks},{"events",JArray.FromObject(recorder.Events)},{"properties",JObject.FromObject(recorder.Properties)}}.ToString());
                Debug.Log("CNP_ANALYTICS_POLICY_PASS checks="+Checks.Count);
            }
            catch(Exception ex){Directory.CreateDirectory("Docs/AnalyticsValidation/20261014-implementation");File.WriteAllText(LocalAnalyticsOutput(),new JObject{{"passed",false},{"checks",Checks},{"error",ex.ToString()}}.ToString());throw;}
            finally{LocalAnalytics.Bind(null);UnityEngine.Object.DestroyImmediate(settings);}
        }
        private static string LocalAnalyticsOutput(){Directory.CreateDirectory("Docs/AnalyticsValidation/20261014-implementation");return "Docs/AnalyticsValidation/20261014-implementation/policy.json";}
    }
}
