using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    // Original UserLocalData top-level fields. Nested server/payment documents
    // stay opaque until their non-SDK consumers are recovered. No CLR activation.
    public sealed class OriginalUserLocalData
    {
        public bool IsVPN;
        public bool IsTestedVPN;
        public string UserId;
        public string UserName;
        public int UserLevel;
        public int Level;
        public int LevelId;
        public int LevelSeed;
        public float PassLevelTime;
        public bool IsRandomLevelSeed;
        public int LoginDay;
        public int TodayPassLevelCount;
        public int LoginDayCoin;
        public float Gold;
        public double Coin;
        public int AddScrewCount;
        public int RevokeCount;
        public int ExchangeCount;
        public int CurrentLevelAddScrewCount;
        public bool IsAudio;
        public bool IsVibrate;
        public long RegisterTime;
        public long LoginTime;
        public JObject UserLssInfo;
        public JObject ServerConfigData;
        public JObject GoldRewardTargetS2CData;
        public JObject LocalPendingReward;
        public JObject LocalWithdrawalData;
        public JObject LocalAnalyticsData;
        public int ScrewDoneCount;
        public int ScrewMoveCount;
        public int LuckyDrawScrewDoneTimes;
        public int LuckyDrawShowTimes;
        public int NewGameplayUnlockIndex;
        public int GuideIndex;
        public string LevelInfo;
        public int LuckyScrewDoneCount;
        public string TXTargetGold;
        public string ComeOnGold;
        public string ComeOnAddGold;
        public long Level1TXTime;
        public bool IsTXLevel1;
        public bool IsGoldReduceLevel1;
        public bool IsGoldReduceLevel2;
        public bool IsTXLevel2;
        public bool IsCompleteGuidePassStage2Level;
        public float Level1Gold;
        public float Level2Gold;
        public long Level2TXTime;
        public bool IsSyncSuccess;
        public bool IsGuideGold;
        public bool IsGuideGoldComplete;
        public bool IsGuideGoldTargetComplete;
        public long LastGetEveryDayGift;
        public int VideoToolRewardIndex;
        public int TodayChallengeTimes;
        public string StatsDisplayUtcDate;
        public bool IsCompleteAppRatingPanel;
        public int NormalGetTimes;
        public int InterAdTimes;
        public bool IsCompleteRecordGuide;
        public bool IsCompleteShowCoinRewardHint;
        public bool IsCompleteExtraGoldGuide;
        public bool IsCompleteCoinNewPeopleReward;
        public bool IsShowCoin;
        public bool IsCompleteSignIn;

        public OriginalUserLocalData(OriginalUserDefaults defaults)
        {
            Level=defaults.Level;LevelId=defaults.LevelId;RevokeCount=defaults.RevokeCount;
            IsAudio=defaults.IsAudio;IsVibrate=defaults.IsVibrate;
        }
        public void Init() { CurrentLevelAddScrewCount=0; }
    }
}
