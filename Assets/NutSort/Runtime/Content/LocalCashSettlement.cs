using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    // Offline settlement policy using captured bands; not a remote server response.
    public static class LocalCashSettlement
    {
        public static JObject Prepare(OriginalUserLocalData user,CountryRewardConfiguration config,Func<float,float,float> random)
        {
            if(user.LocalPendingReward!=null)
            {
                AddConfiguredDiamonds(user,random);
                return user.LocalPendingReward;
            }
            JObject reward=config.CreateRewardDocument();
            double amount;
            if(user.Level==2||user.Level==3)
                amount=Number(reward["bear_list"][user.Level-2]["psi_value"]);
            else
            {
                JObject rules=config.CreateRulesDocument();
                amount=Number(reward["bear_torque"]);
                foreach(JObject band in (JArray)rules["shuaijianList"])
                    if(user.Gold>=Number(band["le"])&&user.Gold<Number(band["lh"]))
                    {amount=Number(band["value"]);break;}
                string[] range=((string)rules["shuaiXcRatio"]).Split('_');
                amount*=random((float)Number(range[0]),(float)Number(range[1]));
            }
            amount=Math.Round(amount,config.CountryCode=="ID"?0:2,MidpointRounding.AwayFromZero);
            if(amount<=0||double.IsNaN(amount)||double.IsInfinity(amount))throw new InvalidOperationException("Invalid local cash settlement.");
            user.LocalPendingReward=new JObject{{"country",config.CountryCode},{"amount",amount},{"nextLevel",user.Level}};
            LocalAnalytics.ClaimId(user.LocalPendingReward);
            AddConfiguredDiamonds(user,random);
            return user.LocalPendingReward;
        }
        // Attach the independently supplied diamond award before presentation and persistence.
        // Missing/empty server values mean no award; they are never withdrawal thresholds.
        public static void AddConfiguredDiamonds(OriginalUserLocalData user,Func<float,float,float> random=null)
        {
            if(user.LocalPendingReward==null)return;
            string source=(string)user.LocalPendingReward["diamondSource"];
            bool temporary=source=="temporary-reference-screenshot-not-server-rule"||source=="awaiting-original-server-settlement";
            if(user.LocalPendingReward["diamonds"]!=null&&!temporary)return;
            var asset=UnityEngine.Resources.Load<UnityEngine.TextAsset>("Configuration/LocalDiamondSettlement");
            if(asset==null)throw new InvalidOperationException("Missing local diamond settlement configuration.");
            JObject config=JObject.Parse(asset.text);
            int completed=((int?)user.LocalPendingReward["nextLevel"]??user.Level)-1;
            double bandBalance;
            if(random==null)random=UnityEngine.Random.Range;
            double amount=LocalDiamondRewardPolicy.Calculate(config,(string)user.LocalPendingReward["country"],
                completed,user.Coin,random,out bandBalance);
            user.LocalPendingReward["diamondBandBalance"]=bandBalance;
            SetDiamonds(user,amount);
            user.LocalPendingReward["diamondSource"]=(string)config["source"];
        }
        // Use only the response for this pending settlement; account totals and
        // withdrawal tier configuration are not per-level reward amounts.
        public static void ApplyOriginalDiamonds(OriginalUserLocalData user,JObject response)
        {
            if(user.LocalPendingReward==null)throw new InvalidOperationException("No pending settlement.");
            if((string)response?["kinetic_state"]!="ERR-OK"||!(response["kinetic_data"] is JObject data))
                throw new InvalidOperationException("Invalid original settlement response.");
            string value=(string)data["hg_zs_amt"];
            SetDiamonds(user,string.IsNullOrEmpty(value)?0:Number(value));
            user.LocalPendingReward["diamondSource"]="original-server-settlement";
        }
        public static void SetDiamonds(OriginalUserLocalData user, double amount)
        {
            if(user.LocalPendingReward==null)throw new InvalidOperationException("No pending settlement.");
            if(amount<0||double.IsNaN(amount)||double.IsInfinity(amount))throw new InvalidOperationException("Invalid diamond settlement.");
            user.LocalPendingReward["diamonds"]=amount;
        }
        public static bool Claim(OriginalUserLocalData user)
        {
            JObject pending=user.LocalPendingReward;
            if(pending==null)return false;
            float amount=(float)Number(pending["amount"]);
            if(amount<=0||float.IsNaN(amount)||float.IsInfinity(amount))throw new InvalidOperationException("Invalid pending reward.");
            double diamonds=pending["diamonds"]==null?0:Number(pending["diamonds"]);
            if(diamonds<0||double.IsNaN(diamonds)||double.IsInfinity(diamonds)||double.IsInfinity(user.Coin+diamonds))
                throw new InvalidOperationException("Invalid pending diamonds.");
            user.Gold+=amount;
            user.Coin+=diamonds;
            user.LocalPendingReward=null;
            return true;
        }
        private static double Number(JToken value)=>Number((string)value);
        private static double Number(string value)=>double.Parse(value,CultureInfo.InvariantCulture);
    }
}
