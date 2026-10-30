using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    public sealed class LocalEarningRecord
    {
        public int Stage { get; }
        public float Amount { get; }
        public long CreatedUtcSeconds { get; }
        public long SubmittedUtcSeconds { get; }
        public bool Pending { get; }
        public bool Archived { get; }

        internal LocalEarningRecord(JObject row)
        {
            Stage=(int)row["stage"];
            Amount=(float)row["amount"];
            CreatedUtcSeconds=(long?)row["createdUtcSeconds"]??0;
            SubmittedUtcSeconds=(long?)row["submittedUtcSeconds"]??0;
            Pending=(bool?)row["pending"]??false;
            Archived=(bool?)row["archived"]??false;
        }
    }

    // Local reward accounting only. Pending is a saved request state, never a
    // payment result. Callers persist user data after mutations; no SDK is invoked.
    public static class LocalEarningFlowPolicy
    {
        public static IReadOnlyList<LocalEarningRecord> GetRecords(OriginalUserLocalData user)
        {
            MigrateLegacyRecords(user);
            var result=new List<LocalEarningRecord>(2);
            for(int stage=1;stage<=2;stage++)
            {
                JObject row=FindRecord(State(user,false),stage);
                if(row!=null)result.Add(new LocalEarningRecord(row));
            }
            return result;
        }

        // Invoke only after the actual settlement amount was claimed. This
        // records that claim without granting it a second time.
        public static bool EnsureEarlyReward(OriginalUserLocalData user,int stage,float amount,long utcSeconds)
        {
            ValidateStage(stage);
            if(!ValidAmount(amount))throw new ArgumentOutOfRangeException(nameof(amount));
            if(utcSeconds<=0)throw new ArgumentOutOfRangeException(nameof(utcSeconds));
            bool changed=MigrateLegacyRecords(user);
            JObject state=State(user,true),row=FindRecord(state,stage);
            if(row==null)
            {
                Records(state).Add(NewRecord(stage,amount,utcSeconds,false));
                changed=true;
            }
            else if((float)row["amount"]!=amount)
                throw new InvalidOperationException("An earned reward record cannot change its amount.");
            if(EarlyAmount(user,stage)!=amount){SetEarlyAmount(user,stage,amount);changed=true;}
            return changed;
        }

        // Transfer the earned amount from the cash balance to its local record.
        // Insufficient balance leaves both sides unchanged instead of creating money.
        public static bool ArchiveEarlyReward(OriginalUserLocalData user,int stage)
        {
            ValidateStage(stage);
            MigrateLegacyRecords(user);
            JObject row=FindRecord(State(user,false),stage);
            if(row==null)return false;
            if((bool?)row["archived"]??false)
            {
                if(!WasReduced(user,stage)){SetReduced(user,stage);return true;}
                return false;
            }
            float amount=(float)row["amount"];
            if(!ValidAmount(amount)||float.IsNaN(user.Gold)||float.IsInfinity(user.Gold))return false;
            float remaining=user.Gold-amount;
            float roundingTolerance=Math.Max(0.00001f,amount*0.000001f);
            if(remaining < -roundingTolerance)return false;
            user.Gold=Math.Max(0f,remaining);
            row["archived"]=true;
            SetReduced(user,stage);
            return true;
        }

        public static bool SubmitEarlyRecord(OriginalUserLocalData user,int stage,long utcSeconds)
        {
            ValidateStage(stage);
            if(utcSeconds<=0)throw new ArgumentOutOfRangeException(nameof(utcSeconds));
            MigrateLegacyRecords(user);
            JObject row=FindRecord(State(user,false),stage);
            if(row==null||!((bool?)row["archived"]??false)||((bool?)row["pending"]??false))return false;
            row["pending"]=true;
            row["submittedUtcSeconds"]=utcSeconds;
            return true;
        }

        public static int NeedsEarlyGuide(OriginalUserLocalData user)
        {
            MigrateLegacyRecords(user);
            JObject state=State(user,false);
            for(int stage=1;stage<=2;stage++)
            {
                JObject row=FindRecord(state,stage);
                if(row!=null&&!((bool?)row["archived"]??false))return stage;
            }
            return 0;
        }

        public static bool EligibleForActivation(OriginalUserLocalData user)
        {
            MigrateLegacyRecords(user);
            JObject state=State(user,false);
            if(state==null||user.Level<3||((bool?)state["activationComplete"]??false))return false;
            for(int stage=1;stage<=2;stage++)
                if(!((bool?)FindRecord(state,stage)?["archived"]??false))return false;
            return true;
        }

        public static int GetActivationStep(OriginalUserLocalData user)
            =>(int?)State(user,false)?["activationStep"]??0;

        public static bool SetActivationStep(OriginalUserLocalData user,int step)
        {
            if(step<0)throw new ArgumentOutOfRangeException(nameof(step));
            JObject state=State(user,true);
            if(((bool?)state["activationComplete"]??false)||((int?)state["activationStep"]??0)==step)return false;
            state["activationStep"]=step;
            return true;
        }

        public static bool CompleteActivation(OriginalUserLocalData user)
        {
            if(!EligibleForActivation(user))return false;
            State(user,true)["activationComplete"]=true;
            user.IsCompleteExtraGoldGuide=true;
            return true;
        }

        // Read APIs also recover only previously deducted, explicitly valued
        // rewards. Missing UTC evidence remains zero. Level alone grants nothing.
        public static bool MigrateLegacyRecords(OriginalUserLocalData user)
        {
            if(user==null)throw new ArgumentNullException(nameof(user));
            bool changed=false;
            // The native completed-guide flag proves the sequence already ended,
            // but does not prove a balance amount or an acknowledged receipt.
            if(user.IsCompleteExtraGoldGuide)
            {
                JObject completedState=State(user,true);
                if(!((bool?)completedState["activationComplete"]??false))
                {
                    completedState["activationComplete"]=true;
                    changed=true;
                }
            }
            for(int stage=1;stage<=2;stage++)
            {
                float amount=EarlyAmount(user,stage);
                if(!WasReduced(user,stage)||!ValidAmount(amount))continue;
                JObject state=State(user,true),row=FindRecord(state,stage);
                if(row==null){Records(state).Add(NewRecord(stage,amount,0,true));changed=true;}
                else if(!((bool?)row["archived"]??false)){row["archived"]=true;changed=true;}
            }
            return changed;
        }

        private static JObject State(OriginalUserLocalData user,bool create)
        {
            if(user==null)throw new ArgumentNullException(nameof(user));
            if(user.LocalWithdrawalData==null)
            {
                if(!create)return null;
                user.LocalWithdrawalData=new JObject();
            }
            JToken value=user.LocalWithdrawalData["earningFlow"];
            if(value==null||value.Type==JTokenType.Null)
            {
                if(!create)return null;
                var state=new JObject{{"records",new JArray()}};
                user.LocalWithdrawalData["earningFlow"]=state;
                return state;
            }
            return (JObject)value;
        }

        private static JArray Records(JObject state)
        {
            JToken value=state["records"];
            if(value==null||value.Type==JTokenType.Null){var rows=new JArray();state["records"]=rows;return rows;}
            return (JArray)value;
        }
        private static JObject FindRecord(JObject state,int stage)
        {
            if(state==null||state["records"]==null||state["records"].Type==JTokenType.Null)return null;
            foreach(JObject row in (JArray)state["records"])
                if((int?)row["stage"]==stage)return row;
            return null;
        }
        private static JObject NewRecord(int stage,float amount,long created,bool archived)
            =>new JObject{{"stage",stage},{"amount",amount},{"createdUtcSeconds",created},
                {"submittedUtcSeconds",0},{"pending",false},{"archived",archived}};
        private static bool ValidAmount(float value)=>value>0&&!float.IsNaN(value)&&!float.IsInfinity(value);
        private static void ValidateStage(int stage){if(stage<1||stage>2)throw new ArgumentOutOfRangeException(nameof(stage));}
        private static float EarlyAmount(OriginalUserLocalData user,int stage)=>stage==1?user.Level1Gold:user.Level2Gold;
        private static void SetEarlyAmount(OriginalUserLocalData user,int stage,float value){if(stage==1)user.Level1Gold=value;else user.Level2Gold=value;}
        private static bool WasReduced(OriginalUserLocalData user,int stage)=>stage==1?user.IsGoldReduceLevel1:user.IsGoldReduceLevel2;
        private static void SetReduced(OriginalUserLocalData user,int stage){if(stage==1)user.IsGoldReduceLevel1=true;else user.IsGoldReduceLevel2=true;}
    }
}
