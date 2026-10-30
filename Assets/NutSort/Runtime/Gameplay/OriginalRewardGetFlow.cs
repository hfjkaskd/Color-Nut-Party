using System;
using NutSort.Content;
namespace NutSort.Gameplay
{
    // Native RewardGetPanel button dispatch. SDK delegates must supply their own
    // real completion; this adapter never fabricates an ad result or a reward.
    public sealed class OriginalRewardGetFlow
    {
        private readonly OriginalUserLocalData user;
        private readonly Func<int> normalThreshold;
        private readonly Func<int> interstitialThreshold;
        private readonly Action<Action<bool>> interstitial;
        private readonly Action<Action<bool>,bool> rewarded;
        private readonly Func<long> timeSeconds;
        private readonly Action<long> setLuckyLastGetTime;
        private readonly Action<bool> getReward;
        private bool adPending;
        public OriginalRewardGetFlow(OriginalUserLocalData user,Func<int> normalThreshold,
            Func<int> interstitialThreshold,Action<Action<bool>> interstitial,
            Action<Action<bool>,bool> rewarded,Func<long> timeSeconds,
            Action<long> setLuckyLastGetTime,Action<bool> getReward)
        {
            this.user=user;this.normalThreshold=normalThreshold;
            this.interstitialThreshold=interstitialThreshold;this.interstitial=interstitial;
            this.rewarded=rewarded;this.timeSeconds=timeSeconds;
            this.setLuckyLastGetTime=setLuckyLastGetTime;this.getReward=getReward;
        }
        public void GetCallback()
        {
            if(adPending)return;
            int previousNormal=user.NormalGetTimes,previousInter=user.InterAdTimes;
            user.NormalGetTimes=unchecked(user.NormalGetTimes+1);
            if(user.NormalGetTimes<normalThreshold()){getReward(false);return;}
            user.InterAdTimes=unchecked(user.InterAdTimes+1);
            user.NormalGetTimes=0;
            bool useInterstitial=user.InterAdTimes<=interstitialThreshold();
            RequestAd(useInterstitial,false,previousNormal,previousInter);
        }
        public void MoreGetCallback()
        {
            if(adPending)return;
            int previousNormal=user.NormalGetTimes,previousInter=user.InterAdTimes;
            user.NormalGetTimes=0;
            RequestAd(false,true,previousNormal,previousInter);
        }
        private void RequestAd(bool useInterstitial,bool more,int previousNormal,int previousInter)
        {
            adPending=true;
            bool completed=false;
            Action<bool> finish=success=>
            {
                if(completed)return;
                completed=true;
                adPending=false;
                if(!success)
                {
                    // A normal claim already earned its base reward by completing
                    // the level. Timeout/no fill must finish that claim, not leave
                    // the same settlement open to request another ad later.
                    if(!more){getReward(false);return;}
                    // Optional bonus claims still require a real rewarded result.
                    user.NormalGetTimes=previousNormal;
                    user.InterAdTimes=previousInter;
                    return;
                }
                if(more)
                {
                    user.LuckyScrewDoneCount=0;
                    setLuckyLastGetTime(timeSeconds());
                }
                getReward(true);
            };
            try
            {
                if(useInterstitial)interstitial(finish);
                else rewarded(finish,false);
            }
            catch
            {
                finish(false);
                throw;
            }
        }
    }
}
