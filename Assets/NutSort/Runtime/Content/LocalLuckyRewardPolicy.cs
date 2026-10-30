using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    // Authorized offline policy: reuse the project's existing cash bands. This is
    // not represented as the original server's unknown Lucky formula.
    public static class LocalLuckyRewardPolicy
    {
        public static JObject State(OriginalUserLocalData user)
        {
            if(user.LocalWithdrawalData==null)user.LocalWithdrawalData=new JObject();
            var state=user.LocalWithdrawalData["earningRewards"] as JObject;
            if(state==null){state=new JObject();user.LocalWithdrawalData["earningRewards"]=state;}
            return state;
        }
        public static JObject Pending(OriginalUserLocalData user)=>State(user)["pendingLucky"] as JObject;
        public static void BeginScrewCompletionTracking(OriginalUserLocalData user)
        {
            var state=State(user);
            if(state["lastProcessedScrewDone"]==null)state["lastProcessedScrewDone"]=user.ScrewDoneCount;
        }
        public static bool TryConsumeScrewCompletion(OriginalUserLocalData user)
        {
            var state=State(user);
            // A move increments the original counter before its delayed callback.
            // If this is our first initialization, that first completion is still owed.
            int previous=(int?)state["lastProcessedScrewDone"]??unchecked(user.ScrewDoneCount-1);
            if(previous==user.ScrewDoneCount)return false;
            // Other moves can finish before the first delayed callback arrives.
            // Each callback consumes one completion, not the entire current counter.
            state["lastProcessedScrewDone"]=unchecked(previous+1);
            return true;
        }

        // A completed move is saved before its delayed reward callback. At the
        // first restored board only, settle that persisted gap without replaying
        // live completion callbacks, Lucky requests, or level-success actions.
        public static int RecoverScrewCompletions(OriginalUserLocalData user,int completedShowLevel,
            float cashPerScrew,float diamondsPerScrew)
        {
            var state=State(user);
            if(state["lastProcessedScrewDone"]==null)
            {
                // Pre-feature saves do not prove any unpaid historical rewards.
                state["lastProcessedScrewDone"]=user.ScrewDoneCount;
                return 0;
            }
            int count=unchecked(user.ScrewDoneCount-(int)state["lastProcessedScrewDone"]);
            if(count<=0)return 0;
            if(cashPerScrew<0||float.IsNaN(cashPerScrew)||float.IsInfinity(cashPerScrew)||
                diamondsPerScrew<0||float.IsNaN(diamondsPerScrew)||float.IsInfinity(diamondsPerScrew))
                throw new ArgumentOutOfRangeException(nameof(cashPerScrew));
            float cash=user.Gold+(completedShowLevel>=3?cashPerScrew*count:0);
            double diamonds=user.Coin+(completedShowLevel>=4?(double)diamondsPerScrew*count:0);
            if(float.IsNaN(cash)||float.IsInfinity(cash)||double.IsNaN(diamonds)||double.IsInfinity(diamonds))
                throw new InvalidOperationException("Invalid recovered rod reward balance.");
            user.Gold=cash;user.Coin=diamonds;
            state["lastProcessedScrewDone"]=user.ScrewDoneCount;
            return count;
        }
        public static float Number(JToken value)
        {
            if(value==null||value.Type==JTokenType.Null)return 0;
            float result=float.Parse((string)value,CultureInfo.InvariantCulture);
            if(float.IsNaN(result)||float.IsInfinity(result)||result<0)throw new InvalidOperationException("Invalid local reward value.");
            return result;
        }
        public static float Multiplier(CountryRewardConfiguration config,bool lucky)
        {
            float result=Number(config.CreateRulesDocument()[lucky?"shuaiLkVideo":"shuaiGtVideo"]);
            if(result<1)throw new InvalidOperationException("Missing configured reward multiplier.");
            return result;
        }
        public static JObject Prepare(OriginalUserLocalData user,CountryRewardConfiguration profile,JObject config,Func<float,float,float> random)
        {
            var state=State(user);var existing=Pending(user);if(existing!=null)return existing;
            if((string)config["luckyAmountPolicy"]!="existing-local-cash-bands")
                throw new InvalidOperationException("Unsupported local Lucky reward policy.");
            JObject reward=profile.CreateRewardDocument(),rules=profile.CreateRulesDocument();
            float amount=Number(reward["bear_torque"]);
            foreach(JObject band in (JArray)rules["shuaijianList"])
                if(user.Gold>=Number(band["le"])&&user.Gold<Number(band["lh"]))
                {amount=Number(band["value"]);break;}
            string[] range=((string)rules["shuaiXcRatio"]).Split('_');
            float lower=float.Parse(range[0],CultureInfo.InvariantCulture),upper=float.Parse(range[1],CultureInfo.InvariantCulture);
            amount=(float)Math.Round(amount*random(lower,upper),profile.CountryCode=="ID"?0:2,MidpointRounding.AwayFromZero);
            if(amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))throw new InvalidOperationException("Invalid Lucky reward amount.");
            var pending=new JObject{{"amount",amount},{"multiplier",Multiplier(profile,true)},
                {"country",profile.CountryCode},{"source",(string)config["source"]},{"claimed",false}};
            LocalAnalytics.ClaimId(pending);state["pendingLucky"]=pending;return pending;
        }
        // Persist the complete user immediately afterward. Reopening a claimed
        // receipt may replay its animation but cannot grant its balance twice.
        public static bool Claim(OriginalUserLocalData user,bool more,out float amount)
        {
            amount=0;var pending=Pending(user);
            if(pending==null||((bool?)pending["claimed"]??false))return false;
            amount=Number(pending["amount"])*(more?Number(pending["multiplier"]):1);
            if(amount<=0||float.IsInfinity(amount)||float.IsInfinity(user.Gold+amount))throw new InvalidOperationException("Invalid Lucky claim.");
            user.Gold+=amount;pending["claimed"]=true;pending["award"]=amount;pending["bonus"]=more;
            return true;
        }
        public static void CompleteReceipt(OriginalUserLocalData user)
        {
            var pending=Pending(user);
            if(pending!=null&&((bool?)pending["claimed"]??false))State(user).Remove("pendingLucky");
        }
    }
}
