using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    [Serializable]
    public sealed class OriginalSimulatedStatsSettings
    {
        public string TitleFormat;
        public string DateFormat;
        public int PlayersPerMinute;
        public int DailyAttemptsMinimum;
        public int DailyAttemptsMaximumExclusive;
    }

    public sealed class OriginalSimulatedStatsSnapshot
    {
        public readonly string Title;
        public readonly int PassCount,ChallengeTimes;
        public readonly float CashAmount;
        public readonly bool DisplayStateChanged;

        public OriginalSimulatedStatsSnapshot(string title,int passCount,int challengeTimes,float cashAmount,bool displayStateChanged)
        {
            Title=title;PassCount=passCount;ChallengeTimes=challengeTimes;
            CashAmount=cashAmount;DisplayStateChanged=displayStateChanged;
        }
    }

    // Original client display formulas, explicitly presented as simulated statistics.
    // Only the daily display sample is persisted; no login, reward or queue initialization runs here.
    public static class OriginalSimulatedStats
    {
        public static OriginalSimulatedStatsSnapshot Create(int level,OriginalUserLocalData user,
            OriginalMarqueeData marquee,OriginalSimulatedStatsSettings settings,string culture,
            DateTime localNow,DateTime utcNow,Func<int,int,int> randomAttempts=null,
            Func<float,float,float> randomCash=null)
        {
            if(user==null)throw new ArgumentNullException(nameof(user));
            if(settings==null)throw new ArgumentNullException(nameof(settings));
            if(settings.PlayersPerMinute<0||settings.DailyAttemptsMinimum<=0||
                settings.DailyAttemptsMaximumExclusive<=settings.DailyAttemptsMinimum)
                throw new InvalidOperationException("Invalid simulated statistics configuration.");
            var displayCulture=CultureInfo.GetCultureInfo(culture);
            string title=string.Format(displayCulture,LocalUiLocalization.Text(settings.TitleFormat),utcNow.ToString(settings.DateFormat,displayCulture));
            int minutes=(int)(localNow-localNow.Date).TotalSeconds/60;
            int attempts=1;
            float amount;
            bool changed=false;
            string day=utcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
            if(level>2)
            {
                if(marquee==null)throw new ArgumentNullException(nameof(marquee));
                amount=randomCash==null?marquee.SampleCashValue():marquee.SampleCashValue(randomCash);
                attempts=user.TodayChallengeTimes;
                changed=!string.Equals(user.StatsDisplayUtcDate,day,StringComparison.Ordinal)||attempts<=0;
                if(changed)
                {
                    attempts=randomAttempts==null
                        ?UnityEngine.Random.Range(settings.DailyAttemptsMinimum,settings.DailyAttemptsMaximumExclusive)
                        :randomAttempts(settings.DailyAttemptsMinimum,settings.DailyAttemptsMaximumExclusive);
                    user.TodayChallengeTimes=attempts;
                    user.StatsDisplayUtcDate=day;
                }
            }
            else
            {
                if(user.GoldRewardTargetS2CData==null)throw new InvalidOperationException("Missing reward configuration for statistics.");
                var rows=user.GoldRewardTargetS2CData.GetValue("bear_list",StringComparison.OrdinalIgnoreCase) as JArray;
                if(rows==null)throw new InvalidOperationException("Missing bear_list for statistics.");
                var row=(JObject)rows[level==1?0:1];
                amount=OriginalRewardProgress.ToFloat((string)row.GetValue("psi_value",StringComparison.OrdinalIgnoreCase));
            }
            return new OriginalSimulatedStatsSnapshot(title,minutes*settings.PlayersPerMinute,attempts,amount,changed);
        }
    }
}
