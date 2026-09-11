using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NutSort.Content
{
    [CreateAssetMenu(menuName="Nut Sort/Country reward catalog")]
    public sealed class CountryRewardCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Profile
        {
            public string RewardPath;
            public string RulesPath;
            public string DisplayPath;
        }

        [SerializeField] private Profile brazil, indonesia;

        public CountryRewardConfiguration Load(string countryCode)
        {
            string code=ResolveCountry(countryCode);
            Profile profile=code=="ID"?indonesia:brazil;
            if(profile==null)throw new InvalidOperationException("Missing country reward profile: "+code);
            return new CountryRewardConfiguration(code,Read(profile.RewardPath),Read(profile.RulesPath),Read(profile.DisplayPath));
        }

        public static string ResolveCountry(string countryCode)
        {
            string code=countryCode==null?string.Empty:countryCode.Trim();
            return string.Equals(code,"ID",StringComparison.OrdinalIgnoreCase)||
                string.Equals(code,"IDN",StringComparison.OrdinalIgnoreCase)?"ID":"BR";
        }

        private static JObject Read(string path)
        {
            var asset=Resources.Load<TextAsset>(path);
            if(asset==null)throw new InvalidOperationException("Missing reward configuration: "+path);
            return JObject.Parse(asset.text);
        }
    }

    // Captured configuration only. Loading never represents a settlement or grants inventory.
    public sealed class CountryRewardConfiguration
    {
        public string CountryCode { get; }
        private readonly JObject reward, rules, display;
        internal CountryRewardConfiguration(string country,JObject reward,JObject rules,JObject display)
        {
            CountryCode=country;
            this.reward=reward;this.rules=rules;this.display=display;
        }
        public JObject CreateRewardDocument()=>(JObject)reward.DeepClone();
        public JObject CreateRulesDocument()=>(JObject)rules.DeepClone();
        public JObject CreateDisplayDocument()=>(JObject)display.DeepClone();

        public void ApplyTo(OriginalUserLocalData user)
        {
            if(user==null)throw new ArgumentNullException(nameof(user));
            JObject previous=user.GoldRewardTargetS2CData;
            JObject next=CreateRewardDocument();
            if(previous!=null)
            {
                Preserve(previous,next,"ext_gap_logs");
                Preserve(previous,next,"gap_day_gates");
                Preserve(previous,next,"gap_all_gates");
                PreserveCounts(previous,next,"bear_list");
                PreserveCounts(previous,next,"bear_zs_list");
            }
            user.GoldRewardTargetS2CData=next;
        }
        private static void Preserve(JObject previous,JObject next,string key)
        {
            JToken value=previous.GetValue(key,StringComparison.OrdinalIgnoreCase);
            if(value!=null)next[key]=value.DeepClone();
        }
        private static void PreserveCounts(JObject previous,JObject next,string key)
        {
            var oldRows=previous.GetValue(key,StringComparison.OrdinalIgnoreCase) as JArray;
            var newRows=next[key] as JArray;
            if(oldRows==null||newRows==null)return;
            foreach(JObject row in newRows)
                foreach(JObject oldRow in oldRows)
                    if(JToken.DeepEquals(row["bear_id"],oldRow.GetValue("bear_id",StringComparison.OrdinalIgnoreCase)))
                    {Preserve(oldRow,row,"caliper_count");break;}
        }
    }
}
