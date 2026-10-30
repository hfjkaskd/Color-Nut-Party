using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using UnityEngine;

namespace NutSort.Validation
{
    // Pure production policies/flows; no save fixture, live SDK, or ad network.
    public static class LocalLuckyRewardPolicyValidation
    {
        public static void Run()
        {
            var checks=new JArray();
            var defaults=Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults");
            var catalog=Resources.Load<CountryRewardCatalog>("Configuration/CountryRewardCatalog");
            var config=JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningRewards").text);
            void Check(bool pass,string name)
            {
                checks.Add(new JObject{{"name",name},{"passed",pass}});
                if(!pass)throw new InvalidOperationException(name);
            }
            OriginalUserLocalData User()=>new OriginalUserLocalData(defaults){Level=3,Gold=500};
            OriginalUserLocalData Reload(OriginalUserLocalData user)=>OriginalUserDataJson.Read(OriginalUserDataJson.Write(user),defaults);
            var report=new JObject{{"checks",checks},{"passed",false}};
            try
            {
                var profile=catalog.Load("BR");
                var user=User();
                var pending=LocalLuckyRewardPolicy.Prepare(user,profile,config,(lo,hi)=>lo);
                float low=(float)pending["amount"];
                Check(low>0&&user.Gold==500,"Preparing Lucky chooses configured cash without crediting inventory");
                Check(ReferenceEquals(pending,LocalLuckyRewardPolicy.Prepare(user,profile,config,(lo,hi)=>hi)),"Repeated preparation retains the pending amount");
                user=Reload(user);
                Check((float)LocalLuckyRewardPolicy.Pending(user)["amount"]==low,"Unclaimed Lucky reward survives a save/reload");
                Check(LocalLuckyRewardPolicy.Claim(user,false,out float baseAward)&&Mathf.Abs(baseAward-low)<.0001f,"Normal claim grants the base amount");
                float balance=user.Gold;
                Check(!LocalLuckyRewardPolicy.Claim(user,true,out _)&&user.Gold==balance,"Duplicate claim cannot upgrade or repeat a granted reward");
                user=Reload(user);
                Check(!LocalLuckyRewardPolicy.Claim(user,false,out _)&&user.Gold==balance,"Reload before receipt completion cannot grant twice");
                Check((float)LocalLuckyRewardPolicy.Pending(user)["award"]==baseAward,"Claimed receipt retains its actual amount after reload");
                LocalLuckyRewardPolicy.CompleteReceipt(user);
                Check(LocalLuckyRewardPolicy.Pending(user)==null,"Only completed claimed receipt removes its pending state");

                var higher=User();
                float high=(float)LocalLuckyRewardPolicy.Prepare(higher,profile,config,(lo,hi)=>hi)["amount"];
                Check(high>low,"Lucky amount follows configured local sampling, not a fixed video amount");
                LocalLuckyRewardPolicy.CompleteReceipt(higher);
                Check(LocalLuckyRewardPolicy.Pending(higher)!=null,"Completing an unclaimed receipt never discards the reward");

                void VerifyAd(bool more,bool success)
                {
                    var sample=User();
                    var reward=LocalLuckyRewardPolicy.Prepare(sample,profile,config,(lo,hi)=>(lo+hi)*.5f);
                    float amount=(float)reward["amount"],multiplier=(float)reward["multiplier"];
                    Action<bool> ad=null;int grants=0;
                    var flow=new OriginalRewardGetFlow(sample,()=>1,()=>1,
                        done=>ad=done,(done,option)=>ad=done,()=>1000,value=>{},
                        bonus=>{if(LocalLuckyRewardPolicy.Claim(sample,more&&bonus,out _))grants++;});
                    if(more)flow.MoreGetCallback();else flow.GetCallback();
                    Check(ad!=null&&sample.Gold==500,"Ad request does not credit early: more="+more+", success="+success);
                    ad(success);ad(success);
                    bool earned=!more||success;
                    float expected=500+(earned?amount*(more?multiplier:1):0);
                    Check(Mathf.Abs(sample.Gold-expected)<.001f&&grants==(earned?1:0),"Ad result credits exactly once with correct multiplier: more="+more+", success="+success);
                    if(more&&!success)
                        Check(sample.NormalGetTimes==0&&sample.InterAdTimes==0&&!((bool)LocalLuckyRewardPolicy.Pending(sample)["claimed"]),"Failed optional bonus restores counters and remains claimable");
                }
                VerifyAd(false,false);VerifyAd(false,true);VerifyAd(true,false);VerifyAd(true,true);

                var first=User();first.ScrewDoneCount=1;
                Check(LocalLuckyRewardPolicy.TryConsumeScrewCompletion(first),"The first completion is consumed when initialization first runs inside its callback");
                Check(!LocalLuckyRewardPolicy.TryConsumeScrewCompletion(first),"Repeated first callback does not credit another rod");
                var queued=User();queued.ScrewDoneCount=10;
                LocalLuckyRewardPolicy.BeginScrewCompletionTracking(queued);
                Check(!LocalLuckyRewardPolicy.TryConsumeScrewCompletion(queued),"Skip/landing with unchanged counter owes no rod reward");
                queued.ScrewDoneCount=13;
                Check(LocalLuckyRewardPolicy.TryConsumeScrewCompletion(queued)&&LocalLuckyRewardPolicy.TryConsumeScrewCompletion(queued)&&LocalLuckyRewardPolicy.TryConsumeScrewCompletion(queued)&&!LocalLuckyRewardPolicy.TryConsumeScrewCompletion(queued),"Three delayed rod callbacks observing the same counter consume exactly three rewards");
                queued=Reload(queued);
                Check(!LocalLuckyRewardPolicy.TryConsumeScrewCompletion(queued),"Saved rod receipts survive reload without repeating inventory");

                var rodUser=User();rodUser.LuckyScrewDoneCount=4;
                int successes=0,showLevel=3;
                var effects=new Effects();
                var screwFlow=new OriginalScrewDoneFlow(()=>rodUser,()=>showLevel,()=>successes++,effects);
                screwFlow.Run(null,false);
                Check(effects.Gold==.1f&&effects.Coin==0&&effects.LuckyRequests==1,"Level three rod grants cash and opens the configured Lucky threshold without diamonds");
                showLevel=4;effects.Gold=0;effects.Coin=0;effects.LuckyRequests=0;
                rodUser.LuckyScrewDoneCount=4;
                screwFlow.Run(null,true);
                Check(successes==1&&effects.Gold==.1f&&effects.Coin==1&&effects.LuckyRequests==0,"Board completion settles once and takes priority over a simultaneous Lucky threshold");
                report["passed"]=true;
                Debug.Log("[LocalLuckyRewardPolicyValidation] Passed "+checks.Count+" checks.");
            }
            catch(Exception exception){report["error"]=exception.ToString();throw;}
            finally
            {
                Directory.CreateDirectory("Docs/RewardFlowVideo/20261012");
                File.WriteAllText("Docs/RewardFlowVideo/20261012/lucky-policy-report.json",report.ToString());
            }
        }
        private sealed class Effects:IOriginalScrewDoneEffects
        {
            public float Gold,Coin;
            public int LuckyRequests;
            public float RemoveGold=>.1f;
            public float RemoveCoin=>1;
            public bool HasPanel=>false;
            public long TimeSeconds=>1000;
            public long LuckyRewardTime{get;set;}=1000;
            public long LuckyDrawTime{get;set;}=1000;
            public int Config(string field)=>field=="LSSLR2"?4:int.MaxValue;
            public void FlyGold(ScrewState target){}
            public void FlyCoin(ScrewState target){}
            public void AddGold(float amount,bool refreshUI,bool synchronize){Gold+=amount;}
            public void AddCoin(float amount,bool refreshUI){Coin+=amount;}
            public void RequestLuckyReward(){LuckyRequests++;}
            public void ShowPanel(int id){throw new InvalidOperationException("Unrelated feature opened: "+id);}
        }
    }
}
