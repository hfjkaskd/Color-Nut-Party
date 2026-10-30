using System;
using System.Collections.Generic;
using NutSort.Content;
using NutSort.Gameplay;
using UnityEngine;
namespace NutSort.Validation
{
    public static class OriginalRewardGetFlowValidation
    {
        public static void Validate()
        {
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            var trace=new List<string>();Action<bool> pending=null;int normal=2,inter=1;long last=0;
            var flow=new OriginalRewardGetFlow(user,
                ()=>{trace.Add("normal:"+user.NormalGetTimes);return normal;},
                ()=>{trace.Add("inter:"+user.InterAdTimes+":"+user.NormalGetTimes);return inter;},
                cb=>{trace.Add("interstitial");pending=cb;},
                (cb,flag)=>{Check(!flag,"Native rewarded option is false");trace.Add("rewarded");pending=cb;},
                ()=>{Check(user.LuckyScrewDoneCount==0,"Lucky count reset precedes clock");trace.Add("time");return 123;},
                value=>{last=value;trace.Add("last");},more=>trace.Add(more?"more":"normalReward"));
            user.NormalGetTimes=0;user.InterAdTimes=0;
            flow.GetCallback();Equal(trace,"normal:1,normalReward");Check(pending==null,"Below threshold no SDK request");
            trace.Clear();flow.GetCallback();Equal(trace,"normal:2,inter:1:0,interstitial");
            Check(user.NormalGetTimes==0&&user.InterAdTimes==1,"Counters update before asynchronous request");
            var first=pending;
            trace.Clear();flow.GetCallback();flow.MoreGetCallback();Equal(trace,"");
            first(false);first(true);first(false);Equal(trace,"normalReward");
            Check(user.NormalGetTimes==0&&user.InterAdTimes==1,"Interstitial timeout completes the base claim without resetting its ad counters");
            trace.Clear();flow.GetCallback();Equal(trace,"normal:1,normalReward");
            user.NormalGetTimes=1;user.InterAdTimes=0;
            trace.Clear();flow.GetCallback();Equal(trace,"normal:2,inter:1:0,interstitial");
            trace.Clear();pending(true);pending(true);first(true);Equal(trace,"more");
            trace.Clear();user.NormalGetTimes=1;flow.GetCallback();Equal(trace,"normal:2,inter:2:0,rewarded");
            trace.Clear();pending(false);pending(true);Equal(trace,"normalReward");
            Check(user.NormalGetTimes==0&&user.InterAdTimes==2,"A normal claim keeps its earned base reward when the rewarded route has no fill");
            user.NormalGetTimes=1;user.InterAdTimes=1;
            trace.Clear();flow.GetCallback();Equal(trace,"normal:2,inter:2:0,rewarded");
            trace.Clear();pending(true);pending(true);Equal(trace,"more");
            user.NormalGetTimes=9;user.LuckyScrewDoneCount=7;trace.Clear();flow.MoreGetCallback();Equal(trace,"rewarded");
            Check(user.NormalGetTimes==0&&user.InterAdTimes==2&&user.LuckyScrewDoneCount==7&&last==0,"More click resets only normal count; completion remains pending");
            trace.Clear();pending(false);pending(true);Equal(trace,"");
            Check(user.NormalGetTimes==9&&user.LuckyScrewDoneCount==7&&last==0,"Cancelled bonus ad changes neither lucky state nor reward");
            trace.Clear();flow.MoreGetCallback();Equal(trace,"rewarded");
            trace.Clear();pending(true);pending(true);Equal(trace,"time,last,more");Check(last==123,"Successful bonus ad updates shared lucky timestamp exactly once");
            trace.Clear();user.NormalGetTimes=int.MaxValue;flow.GetCallback();Equal(trace,"normal:"+int.MinValue+",normalReward");
            trace.Clear();normal=0;user.NormalGetTimes=0;user.InterAdTimes=int.MaxValue;flow.GetCallback();Equal(trace,"normal:1,inter:"+int.MinValue+":0,interstitial");
            pending(false);
            bool failed=false;
            var fail=new OriginalRewardGetFlow(user,()=>throw new InvalidOperationException("config"),()=>0,cb=>{},(cb,b)=>{},()=>0,v=>{},b=>{});
            user.NormalGetTimes=4;try{fail.GetCallback();}catch(InvalidOperationException){failed=true;}
            Check(failed&&user.NormalGetTimes==5,"Missing config does not undo native counter mutation");
            int baseClaims=0;
            var throwing=new OriginalRewardGetFlow(user,()=>1,()=>0,cb=>{},(cb,b)=>throw new InvalidOperationException("SDK"),()=>0,v=>{},b=>{Check(!b,"Provider exception is not a rewarded success");baseClaims++;});
            user.NormalGetTimes=4;user.InterAdTimes=2;failed=false;
            try{throwing.GetCallback();}catch(InvalidOperationException){failed=true;}
            Check(failed&&baseClaims==1&&user.NormalGetTimes==0&&user.InterAdTimes==3,"Provider exception still completes the earned base claim exactly once");
            failed=false;
            try{throwing.MoreGetCallback();}catch(InvalidOperationException){failed=true;}
            Check(failed&&baseClaims==1&&user.NormalGetTimes==0&&user.InterAdTimes==3,"Provider exception never grants an optional bonus");
            var success=new OriginalSuccessPanelFlow(user,()=>0,()=>{},id=>{});
            user.Level=3;user.NormalGetTimes=8;bool closed=false;
            success.GetCallback(()=>closed=true,flow.GetCallback);
            Check(closed&&user.NormalGetTimes==8,"Success early-level override bypasses base counters and ads");
            user.Level=4;normal=100;trace.Clear();success.GetCallback(()=>throw new InvalidOperationException("unexpected close"),flow.GetCallback);
            Equal(trace,"normal:9,normalReward");
            Debug.Log("NUT_REWARD_GET_FLOW_VALIDATION_PASS original thresholds, pending guard, normal claim completes on timeout/no-fill/exception, bonus requires success, duplicate/stale callbacks, lucky timestamp and early-level override.");
        }
        private static void Equal(List<string> trace,string expected){Check(string.Join(",",trace)==expected,"Expected "+expected+"; got "+string.Join(",",trace));}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
