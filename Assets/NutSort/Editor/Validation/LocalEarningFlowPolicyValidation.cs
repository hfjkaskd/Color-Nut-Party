using System;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEditor;
using UnityEngine;

namespace NutSort.Validation
{
    public static class LocalEarningFlowPolicyValidation
    {
        public static void Run()
        {
            try{Validate();EditorApplication.Exit(0);}
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        }
        public static void Validate()
        {
            var defaults=ScriptableObject.CreateInstance<OriginalUserDefaults>();
            try
            {
                var user=new OriginalUserLocalData(defaults){Level=2,Gold=9.27f,Coin=31.5};
                Check(LocalEarningFlowPolicy.GetRecords(user).Count==0&&LocalEarningFlowPolicy.NeedsEarlyGuide(user)==0,"Level alone creates no reward");
                Check(!LocalEarningFlowPolicy.EligibleForActivation(user)&&!LocalEarningFlowPolicy.CompleteActivation(user),"Unearned rewards cannot activate");
                Check(LocalEarningFlowPolicy.EnsureEarlyReward(user,1,1.27f,100),"Record actual non-default first amount");
                var first=LocalEarningFlowPolicy.GetRecords(user)[0];
                Check(first.Stage==1&&first.Amount==1.27f&&first.CreatedUtcSeconds==100&&!first.Archived&&!first.Pending,"New record stores amount and UTC but no submission");
                Check(user.Gold==9.27f&&user.Level1Gold==1.27f&&user.Coin==31.5,"Recording grants no money");
                Check(!LocalEarningFlowPolicy.EnsureEarlyReward(user,1,1.27f,150)&&LocalEarningFlowPolicy.GetRecords(user)[0].CreatedUtcSeconds==100,"Repeat claim preserves first record");
                Check(LocalEarningFlowPolicy.NeedsEarlyGuide(user)==1&&!LocalEarningFlowPolicy.SubmitEarlyRecord(user,1,160),"Unarchived reward needs guide and cannot submit");
                Check(LocalEarningFlowPolicy.ArchiveEarlyReward(user,1)&&Near(user.Gold,8f),"Archive transfers first reward and preserves unrelated cash");
                Check(user.IsGoldReduceLevel1&&!user.IsTXLevel1&&LocalEarningFlowPolicy.NeedsEarlyGuide(user)==0,"Archive is not a payment");
                Check(!LocalEarningFlowPolicy.ArchiveEarlyReward(user,1)&&Near(user.Gold,8f),"Archive is once only");
                Check(LocalEarningFlowPolicy.SubmitEarlyRecord(user,1,200),"Archived record can become pending");
                Check(!LocalEarningFlowPolicy.SubmitEarlyRecord(user,1,250),"Submission is idempotent");
                first=LocalEarningFlowPolicy.GetRecords(user)[0];
                Check(first.Pending&&first.SubmittedUtcSeconds==200&&Near(user.Gold,8f)&&!user.IsTXLevel1,"Pending does not pay or debit again");

                user.Level=3;user.Gold+=2.13f;
                LocalEarningFlowPolicy.EnsureEarlyReward(user,2,2.13f,300);
                Check(LocalEarningFlowPolicy.NeedsEarlyGuide(user)==2&&!LocalEarningFlowPolicy.EligibleForActivation(user),"Second reward has independent guide");
                Check(LocalEarningFlowPolicy.ArchiveEarlyReward(user,2)&&Near(user.Gold,8f),"Second archive preserves additional cash");
                Check(LocalEarningFlowPolicy.EligibleForActivation(user),"Both archived permit activation without pretending payment");
                Check(LocalEarningFlowPolicy.GetActivationStep(user)==0&&LocalEarningFlowPolicy.SetActivationStep(user,3),"Persist animation step");
                var restored=OriginalUserDataJson.Read(OriginalUserDataJson.Write(user),defaults);
                Check(LocalEarningFlowPolicy.GetActivationStep(restored)==3&&LocalEarningFlowPolicy.GetRecords(restored).Count==2,"Records and activation step survive full user serialization");
                Check(LocalEarningFlowPolicy.GetRecords(restored)[0].Pending&&!LocalEarningFlowPolicy.GetRecords(restored)[1].Pending,"Separate Withdraw and Withdrawing states survive reload");
                Check(LocalEarningFlowPolicy.CompleteActivation(restored)&&restored.IsCompleteExtraGoldGuide&&Near(restored.Gold,8f),"Activation completion grants no money");
                Check(!LocalEarningFlowPolicy.CompleteActivation(restored)&&!LocalEarningFlowPolicy.EligibleForActivation(restored)&&!LocalEarningFlowPolicy.SetActivationStep(restored,1),"Completed activation cannot repeat");

                var legacy=new OriginalUserLocalData(defaults){Level=15,Gold=500.58f,Level1Gold=1.27f,Level2Gold=2.13f,IsGoldReduceLevel1=true};
                Check(LocalEarningFlowPolicy.MigrateLegacyRecords(legacy)&&!LocalEarningFlowPolicy.MigrateLegacyRecords(legacy),"Legacy migration uses existing deducted amount once");
                var old=LocalEarningFlowPolicy.GetRecords(legacy);
                Check(old.Count==1&&old[0].Archived&&old[0].Amount==1.27f&&old[0].CreatedUtcSeconds==0&&old[0].SubmittedUtcSeconds==0&&!old[0].Pending,"Legacy time or payment is not fabricated");
                Check(!LocalEarningFlowPolicy.ArchiveEarlyReward(legacy,1)&&legacy.Gold==500.58f&&!LocalEarningFlowPolicy.EligibleForActivation(legacy),"Legacy deducted reward is not deducted twice; unreduced stage is not invented");
                legacy.IsGoldReduceLevel2=true;
                Check(LocalEarningFlowPolicy.GetRecords(legacy).Count==2&&legacy.Gold==500.58f&&LocalEarningFlowPolicy.EligibleForActivation(legacy),"Second explicit legacy deduction migrates safely");

                var completedLegacy=new OriginalUserLocalData(defaults){Level=3,Gold=12.5f,Level1Gold=1.27f,Level2Gold=2.13f,
                    IsGoldReduceLevel1=true,IsGoldReduceLevel2=true,IsCompleteExtraGoldGuide=true};
                Check(LocalEarningFlowPolicy.MigrateLegacyRecords(completedLegacy)&&!LocalEarningFlowPolicy.MigrateLegacyRecords(completedLegacy),"Native completed activation migrates once");
                Check(!LocalEarningFlowPolicy.EligibleForActivation(completedLegacy)&&!LocalEarningFlowPolicy.CompleteActivation(completedLegacy)&&
                    !LocalEarningFlowPolicy.SetActivationStep(completedLegacy,3),"Legacy completion prevents replaying activation and its balance grant");
                Check(completedLegacy.Gold==12.5f&&LocalEarningFlowPolicy.GetRecords(completedLegacy).Count==2&&
                    completedLegacy.LocalWithdrawalData["tutorialBalanceApplied"]==null&&completedLegacy.LocalWithdrawalData["tutorialBalanceAmount"]==null&&
                    completedLegacy.LocalWithdrawalData["tutorialBalanceNoticeAcknowledged"]==null,"Activation migration invents no amount or receipt acknowledgement");
                var completedRestored=OriginalUserDataJson.Read(OriginalUserDataJson.Write(completedLegacy),defaults);
                Check(!LocalEarningFlowPolicy.EligibleForActivation(completedRestored)&&completedRestored.Gold==12.5f,"Legacy completion survives full serialization");
                var legacyReceipt=new OriginalUserLocalData(defaults){Level=3,Gold=27,IsCompleteExtraGoldGuide=true,
                    LocalWithdrawalData=new JObject{{"tutorialBalanceApplied",true},{"tutorialBalanceAmount",500f},{"tutorialBalanceNoticeAcknowledged",false}}};
                LocalEarningFlowPolicy.MigrateLegacyRecords(legacyReceipt);
                Check(legacyReceipt.Gold==27&&(bool)legacyReceipt.LocalWithdrawalData["tutorialBalanceApplied"]&&
                    (float)legacyReceipt.LocalWithdrawalData["tutorialBalanceAmount"]==500&&!(bool)legacyReceipt.LocalWithdrawalData["tutorialBalanceNoticeAcknowledged"]&&
                    LocalEarningFlowPolicy.GetRecords(legacyReceipt).Count==0,"Existing receipt markers remain untouched; no early rewards inferred from activation");

                var insufficient=new OriginalUserLocalData(defaults){Level=3,Gold=.1f};
                LocalEarningFlowPolicy.EnsureEarlyReward(insufficient,1,1.27f,400);
                Check(!LocalEarningFlowPolicy.ArchiveEarlyReward(insufficient,1)&&insufficient.Gold==.1f&&!insufficient.IsGoldReduceLevel1,"Insufficient balance does not create funded record");
                Check(!LocalEarningFlowPolicy.SubmitEarlyRecord(insufficient,2,500),"Missing record cannot be submitted");
                Check(Throws(()=>LocalEarningFlowPolicy.EnsureEarlyReward(insufficient,1,9,500)),"Existing amount cannot be rewritten");
                Check(Throws(()=>LocalEarningFlowPolicy.EnsureEarlyReward(insufficient,3,1,500))&&Throws(()=>LocalEarningFlowPolicy.EnsureEarlyReward(insufficient,2,float.NaN,500)),"Invalid stages and amounts rejected");
                Check(Throws(()=>LocalEarningFlowPolicy.SubmitEarlyRecord(insufficient,1,0))&&Throws(()=>LocalEarningFlowPolicy.SetActivationStep(insufficient,-1)),"Invalid UTC and steps rejected");
                Check(user.Coin==31.5&&!user.IsTXLevel1&&!user.IsTXLevel2,"All operations avoid currency side grants and paid markers");
            }
            finally{UnityEngine.Object.DestroyImmediate(defaults);}
            Debug.Log("NUT_LOCAL_EARNING_FLOW_POLICY_PASS earned amount recording; once-only transfer; pending submissions; restart; legacy migration; activation state; no payment or grant");
        }
        private static bool Near(float a,float b)=>Math.Abs(a-b)<.00001f;
        private static bool Throws(Action action){try{action();return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
