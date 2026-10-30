using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    // Client requirements only. Eligibility never constitutes a payment result.
    public sealed class LocalWithdrawalRequirement
    {
        public string Key;
        public double Current, Target;
        public bool Known = true;
        public bool Waived;
        public bool Complete => Known && (Waived || Current >= Target);
    }
    public sealed class LocalWithdrawalEligibility
    {
        public readonly List<LocalWithdrawalRequirement> Requirements = new List<LocalWithdrawalRequirement>(6);
        public bool Eligible { get { foreach(var item in Requirements)if(!item.Complete)return false;return true; } }
        public string FirstIncomplete { get { foreach(var item in Requirements)if(!item.Complete)return item.Key;return "ready"; } }
    }
    public static class LocalWithdrawalPolicy
    {
        public static LocalWithdrawalEligibility Evaluate(OriginalUserLocalData user, OriginalTables tables, bool diamonds, int tier)
        {
            var result = new LocalWithdrawalEligibility();
            JObject document = user.GoldRewardTargetS2CData;
            JObject row = (JObject)document[diamonds ? "bear_zs_list" : "bear_list"][diamonds ? tier : 2];
            if(!diamonds)
            {
                Add(result,"firstLevel",user.Level-1,(int)row["RealLevel"]);
                Add(result,"secondLevel",user.Level-1,(int)row["Stage2RealLevel"]);
            }
            double target;
            bool known = diamonds || double.TryParse(user.TXTargetGold,NumberStyles.Float,CultureInfo.InvariantCulture,out target);
            target = diamonds ? (double)row["caliper_psi"] : (known ? double.Parse(user.TXTargetGold,CultureInfo.InvariantCulture) : 0);
            Add(result,"balance",diamonds ? user.Coin : user.Gold,target,known && target > 0 && !double.IsInfinity(target));
            if(!diamonds)Add(result,"guide",user.IsGuideGold ? 0 : 1,1);
            Add(result,"daily",user.TodayPassLevelCount,(int)document["cal_cfg"]);
            // Native stage selection advances straight to rank once login is complete.
            // Daily play is an intermediate stage, not a permanent daily payment gate.
            result.Requirements[result.Requirements.Count-1].Waived=(diamonds ? user.LoginDayCoin : user.LoginDay)>=(int)row["caliper_logs"];
            Add(result,"login",diamonds ? user.LoginDayCoin : user.LoginDay,(int)row["caliper_logs"]);
            Add(result,"rank",user.UserLevel,(int)row["caliper_rank"]);
            return result;
        }
        private static void Add(LocalWithdrawalEligibility result,string key,double current,double target,bool known=true)
        {result.Requirements.Add(new LocalWithdrawalRequirement{Key=key,Current=current,Target=target,Known=known});}

        // Local calendar tracking is distinct from remote authoritative statistics.
        // Backwards clock changes never grant extra days or reset today's count.
        public static bool RefreshCalendar(OriginalUserLocalData user,DateTime today)
        {
            if(user.LocalWithdrawalData==null)user.LocalWithdrawalData=new JObject();
            string date=today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            string previous=(string)user.LocalWithdrawalData["lastLoginDate"];
            if(string.IsNullOrEmpty(previous))
            {
                user.LocalWithdrawalData["lastLoginDate"]=date;
                user.LoginDay=Math.Max(1,user.LoginDay);user.LoginDayCoin=Math.Max(1,user.LoginDayCoin);
                return true;
            }
            if(string.CompareOrdinal(date,previous)<=0)return false;
            user.LocalWithdrawalData["lastLoginDate"]=date;
            user.TodayPassLevelCount=0;
            user.LoginDay=checked(user.LoginDay+1);user.LoginDayCoin=checked(user.LoginDayCoin+1);
            return true;
        }
        // The recorded US response is an absolute balance of 500, not +500.
        // Apply at the tutorial boundary only; no invented migration of old accounts.
        public static bool ApplyTutorialBalance(OriginalUserLocalData user,string country,JObject config)
        {
            if(user.Level!=3||user.LocalPendingReward!=null)return false;
            if(user.LocalWithdrawalData==null)user.LocalWithdrawalData=new JObject();
            if((bool?)user.LocalWithdrawalData["tutorialBalanceApplied"]??false)return false;
            JToken amount=config["tutorialBalances"]?[country];
            if(amount==null||amount.Type==JTokenType.Null)return false;
            user.Gold=Math.Max(user.Gold,(float)amount);
            user.LocalWithdrawalData["tutorialBalanceApplied"]=true;
            user.LocalWithdrawalData["tutorialBalanceAmount"]=(float)amount;
            return true;
        }

        // A receipt is independent of granting the balance. Older saves have the
        // applied marker but no receipt marker, so they can see it without a regrant.
        public static bool TryGetTutorialBalanceNotice(OriginalUserLocalData user,string country,JObject config,out float amount)
        {
            amount=0;
            var state=user.LocalWithdrawalData;
            if(state==null||!((bool?)state["tutorialBalanceApplied"]??false)||
                ((bool?)state["tutorialBalanceNoticeAcknowledged"]??false))return false;
            JToken recorded=state["tutorialBalanceAmount"]??config["tutorialBalances"]?[country];
            if(recorded==null||recorded.Type==JTokenType.Null)return false;
            amount=(float)recorded;
            return amount>0&&!float.IsNaN(amount)&&!float.IsInfinity(amount);
        }

        public static void AcknowledgeTutorialBalanceNotice(OriginalUserLocalData user)
        {
            if(user.LocalWithdrawalData==null||!((bool?)user.LocalWithdrawalData["tutorialBalanceApplied"]??false))return;
            user.LocalWithdrawalData["tutorialBalanceNoticeAcknowledged"]=true;
        }
    }
}
