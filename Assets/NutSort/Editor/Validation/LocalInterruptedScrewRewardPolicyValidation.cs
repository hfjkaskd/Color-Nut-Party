using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;

namespace NutSort.Validation
{
    public static class LocalInterruptedScrewRewardPolicyValidation
    {
        public static void Run()
        {
            var checks=new JArray();
            var report=new JObject{{"checks",checks},{"passed",false}};
            var defaults=Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults");
            var profile=Resources.Load<CountryRewardCatalog>("Configuration/CountryRewardCatalog").Load("BR");
            JObject rewards=profile.CreateRewardDocument();
            float cash=LocalLuckyRewardPolicy.Number(rewards["bear_torque"]),diamonds=LocalLuckyRewardPolicy.Number(rewards["zs_ea"]);
            void Check(bool pass,string name)
            {
                checks.Add(new JObject{{"name",name},{"passed",pass}});
                if(!pass)throw new InvalidOperationException(name);
            }
            OriginalUserLocalData Reload(OriginalUserLocalData user)=>OriginalUserDataJson.Read(OriginalUserDataJson.Write(user),defaults);
            OriginalUserLocalData Fixture(int level,int count,bool marker)
            {
                var user=new OriginalUserLocalData(defaults){Level=level,Gold=700,Coin=12,ScrewDoneCount=100+count,LuckyScrewDoneCount=3};
                if(marker)LocalLuckyRewardPolicy.State(user)["lastProcessedScrewDone"]=100;
                return Reload(user);
            }
            try
            {
                Check(cash>0&&diamonds>0,"Recovery uses positive per-rod amounts from the existing production country configuration");
                void RecoveryCase(int level,int count)
                {
                    var user=Fixture(level,count,true);
                    var pendingLucky=new JObject{{"amount",.12},{"multiplier",10},{"claimed",false}};
                    LocalLuckyRewardPolicy.State(user)["pendingLucky"]=pendingLucky;
                    LocalLuckyRewardPolicy.State(user)["lastLuckyTime"]=1000;
                    int restored=LocalLuckyRewardPolicy.RecoverScrewCompletions(user,level,cash,diamonds);
                    float expectedCash=700+cash*count;
                    double expectedCoin=12+(level>=4?diamonds*count:0);
                    Check(restored==count&&Mathf.Abs(user.Gold-expectedCash)<.001f&&Math.Abs(user.Coin-expectedCoin)<.0001,
                        "Restart restores exactly "+count+" interrupted rod rewards at display level "+level);
                    Check((int)LocalLuckyRewardPolicy.State(user)["lastProcessedScrewDone"]==user.ScrewDoneCount,
                        "Recovery updates the same durable receipt counter as live callbacks");
                    Check(ReferenceEquals(LocalLuckyRewardPolicy.Pending(user),pendingLucky)&&user.LuckyScrewDoneCount==3&&
                        (long)LocalLuckyRewardPolicy.State(user)["lastLuckyTime"]==1000&&user.LocalPendingReward==null,
                        "Recovery neither creates/replaces Lucky nor resets its thresholds nor creates a settlement");
                    user=Reload(user);
                    Check(LocalLuckyRewardPolicy.RecoverScrewCompletions(user,level,cash,diamonds)==0&&
                        Mathf.Abs(user.Gold-expectedCash)<.001f&&Math.Abs(user.Coin-expectedCoin)<.0001,
                        "Second persisted restart cannot repeat the restored cash or diamonds");
                    Check(!LocalLuckyRewardPolicy.TryConsumeScrewCompletion(user),
                        "A late duplicate done callback cannot pay an already restored rod");
                }
                RecoveryCase(3,1);RecoveryCase(4,2);

                var legacy=Fixture(4,200,false);
                Check(LocalLuckyRewardPolicy.RecoverScrewCompletions(legacy,4,cash,diamonds)==0&&legacy.Gold==700&&legacy.Coin==12,
                    "Legacy save without a receipt marker does not receive historical rod payouts");
                Check((int)LocalLuckyRewardPolicy.State(legacy)["lastProcessedScrewDone"]==legacy.ScrewDoneCount,
                    "Legacy save establishes its current counter as the migration baseline");
                legacy=Reload(legacy);
                Check(LocalLuckyRewardPolicy.RecoverScrewCompletions(legacy,4,cash,diamonds)==0&&legacy.Gold==700&&legacy.Coin==12,
                    "Persisted legacy migration remains idempotent");

                var pending=Fixture(4,1,true);
                pending.LocalPendingReward=new JObject{{"country","US"},{"amount",1.25},{"diamonds",0},{"nextLevel",4}};
                string settlement=pending.LocalPendingReward.ToString();
                Check(LocalLuckyRewardPolicy.RecoverScrewCompletions(pending,3,cash,diamonds)==1&&
                    Mathf.Abs(pending.Gold-(700+cash))<.001f&&pending.Coin==12,
                    "A pending third-level settlement uses the completed display level instead of its advanced user level");
                Check(pending.LocalPendingReward.ToString()==settlement,
                    "Rod recovery does not claim, scale, or replace an existing level settlement");
                report["passed"]=true;
                Debug.Log("[LocalInterruptedScrewRewardPolicyValidation] Passed "+checks.Count+" checks.");
            }
            catch(Exception exception){report["error"]=exception.ToString();throw;}
            finally
            {
                Directory.CreateDirectory("Docs/RewardFlowVideo/20261012");
                File.WriteAllText("Docs/RewardFlowVideo/20261012/interrupted-rod-policy-report.json",report.ToString());
            }
        }
    }
}
