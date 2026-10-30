using System;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using TMPro;
using UnityEngine;
namespace NutSort.UI
{
    public sealed class OriginalPlayerInfo:MonoBehaviour
    {
        [SerializeField] private TMP_Text Time,PassCount,ChallengeTimes,GoldGetCount;
        [SerializeField] private Transform PMDPos;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private OriginalSimulatedStatsSettings simulatedStats;
        public OriginalSimulatedStatsSettings SimulatedStatsSettings=>simulatedStats;
        public TMP_Text DateLabel=>Time;
        public TMP_Text PeopleLabel=>PassCount;
        public TMP_Text ChallengeLabel=>ChallengeTimes;
        public TMP_Text GoldLabel=>GoldGetCount;
        public Transform MarqueeParent=>PMDPos;
        // The supplied PMD readers share the transport's current response. No default response is created.
        public void Init(int level,OriginalUserLocalData user,OriginalTables tables,string language,
            Func<bool> hasPMD,Func<string> date,Func<int> passPeople,Func<float> txRandomValue,
            Func<float,string> formatGold,Action<Transform,int> createPMD)
        {
            transform.localPosition=Vector3.zero;
            gameObject.SetActive(false);
            if((bool?)Field(user.ServerConfigData,"LSS260820")??false)return;
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            if(!hasPMD())return;
            if(this==null)return;
            Time.text=tables.Text.GetText(31,language,date()??string.Empty);
            PassCount.text=passPeople().ToString();
            float amount;
            if(level>2)
            {
                ChallengeTimes.text=user.TodayChallengeTimes.ToString();
                amount=txRandomValue();
            }
            else
            {
                ChallengeTimes.text="1";
                JToken list=Field(user.GoldRewardTargetS2CData,"bear_list");
                if(list==null||list.Type==JTokenType.Null)throw new NullReferenceException("bear_list");
                JObject row=(JObject)((JArray)list)[level==1?0:1];
                amount=OriginalRewardProgress.ToFloat((string)Field(row,"psi_value"));
            }
            GoldGetCount.text=formatGold(amount);
            gameObject.SetActive(true);
            createPMD(PMDPos,level);
        }
        public void InitSimulated(OriginalSimulatedStatsSnapshot snapshot,OriginalTables tables,string language,Func<float,string> formatGold)
        {
            if(snapshot==null)throw new ArgumentNullException(nameof(snapshot));
            transform.localPosition=Vector3.zero;
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            Time.text=snapshot.Title;
            PassCount.text=snapshot.PassCount.ToString();
            ChallengeTimes.text=snapshot.ChallengeTimes.ToString();
            GoldGetCount.text=formatGold(snapshot.CashAmount);
            gameObject.SetActive(true);
        }
        public void InitUnavailable(OriginalTables tables,string language,string placeholder)
        {
            transform.localPosition=Vector3.zero;
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            Time.text=tables.Text.GetText(31,language,placeholder);
            PassCount.text=placeholder;ChallengeTimes.text=placeholder;GoldGetCount.text=placeholder;
            gameObject.SetActive(true);
        }
        private static JToken Field(JObject document,string name)
        {
            if(document==null)throw new NullReferenceException(name);
            JToken value=null;
            foreach(var property in document.Properties())
                if(string.Equals(property.Name,name,StringComparison.OrdinalIgnoreCase))value=property.Value;
            return value;
        }
    }
}
