using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    public static class OriginalUserDataJson
    {
        public static OriginalUserLocalData Read(string json, OriginalUserDefaults defaults)
        {
            using(var reader=new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling=DateParseHandling.None;
                while(reader.Read() && reader.TokenType==JsonToken.Comment) { }
                if(reader.TokenType==JsonToken.None)return null;
                if(reader.TokenType==JsonToken.Null)
                {
                    while(reader.Read())if(reader.TokenType!=JsonToken.Comment)throw new JsonSerializationException("Additional user data content.");
                    return null;
                }
                if(reader.TokenType!=JsonToken.StartObject)throw new JsonSerializationException("UserLocalData must be a JSON object.");
                var data=new OriginalUserLocalData(defaults);
                while(reader.Read())
                {
                    if(reader.TokenType==JsonToken.Comment)continue;
                    if(reader.TokenType==JsonToken.EndObject)
                    {
                        while(reader.Read())if(reader.TokenType!=JsonToken.Comment)throw new JsonSerializationException("Additional user data content.");
                        return data;
                    }
                    if(reader.TokenType!=JsonToken.PropertyName)throw new JsonSerializationException("Expected user field.");
                    string name=(string)reader.Value;
                    if(!reader.Read())throw new JsonSerializationException("Missing user field value.");
                    while(reader.TokenType==JsonToken.Comment)if(!reader.Read())throw new JsonSerializationException("Missing user field value.");
                    if(string.Equals(name,"ServerConfigData",StringComparison.OrdinalIgnoreCase))
                    {data.ServerConfigData=OriginalServerConfigJson.ReadValue(reader,defaults.ServerConfig);continue;}
                    JToken value=JToken.ReadFrom(reader);
                    // Process in source order: duplicate and case-varied field names
                    // overwrite earlier values just as Json.NET field deserialization.
                    switch(name.ToUpperInvariant())
                    {
                        case "ISVPN": data.IsVPN=(bool)value;break;
                        case "ISTESTEDVPN": data.IsTestedVPN=(bool)value;break;
                        case "USERID": data.UserId=(string)value;break;
                        case "USERNAME": data.UserName=(string)value;break;
                        case "USERLEVEL": data.UserLevel=(int)value;break;
                        case "LEVEL": data.Level=(int)value;break;
                        case "LEVELID": data.LevelId=(int)value;break;
                        case "LEVELSEED": data.LevelSeed=(int)value;break;
                        case "PASSLEVELTIME": data.PassLevelTime=(float)value;break;
                        case "ISRANDOMLEVELSEED": data.IsRandomLevelSeed=(bool)value;break;
                        case "LOGINDAY": data.LoginDay=(int)value;break;
                        case "TODAYPASSLEVELCOUNT": data.TodayPassLevelCount=(int)value;break;
                        case "LOGINDAYCOIN": data.LoginDayCoin=(int)value;break;
                        case "GOLD": data.Gold=(float)value;break;
                        case "COIN": data.Coin=(double)value;break;
                        case "ADDSCREWCOUNT": data.AddScrewCount=(int)value;break;
                        case "REVOKECOUNT": data.RevokeCount=(int)value;break;
                        case "EXCHANGECOUNT": data.ExchangeCount=(int)value;break;
                        case "CURRENTLEVELADDSCREWCOUNT": data.CurrentLevelAddScrewCount=(int)value;break;
                        case "ISAUDIO": data.IsAudio=(bool)value;break;
                        case "ISVIBRATE": data.IsVibrate=(bool)value;break;
                        case "REGISTERTIME": data.RegisterTime=(long)value;break;
                        case "LOGINTIME": data.LoginTime=(long)value;break;
                        case "USERLSSINFO": data.UserLssInfo=Document(value);break;
                        case "GOLDREWARDTARGETS2CDATA": data.GoldRewardTargetS2CData=Document(value);break;
                        case "LOCALPENDINGREWARD": data.LocalPendingReward=Document(value);break;
                        case "SCREWDONECOUNT": data.ScrewDoneCount=(int)value;break;
                        case "SCREWMOVECOUNT": data.ScrewMoveCount=(int)value;break;
                        case "LUCKYDRAWSCREWDONETIMES": data.LuckyDrawScrewDoneTimes=(int)value;break;
                        case "LUCKYDRAWSHOWTIMES": data.LuckyDrawShowTimes=(int)value;break;
                        case "NEWGAMEPLAYUNLOCKINDEX": data.NewGameplayUnlockIndex=(int)value;break;
                        case "GUIDEINDEX": data.GuideIndex=(int)value;break;
                        case "LEVELINFO": data.LevelInfo=(string)value;break;
                        case "LUCKYSCREWDONECOUNT": data.LuckyScrewDoneCount=(int)value;break;
                        case "TXTARGETGOLD": data.TXTargetGold=(string)value;break;
                        case "COMEONGOLD": data.ComeOnGold=(string)value;break;
                        case "COMEONADDGOLD": data.ComeOnAddGold=(string)value;break;
                        case "LEVEL1TXTIME": data.Level1TXTime=(long)value;break;
                        case "ISTXLEVEL1": data.IsTXLevel1=(bool)value;break;
                        case "ISGOLDREDUCELEVEL1": data.IsGoldReduceLevel1=(bool)value;break;
                        case "ISGOLDREDUCELEVEL2": data.IsGoldReduceLevel2=(bool)value;break;
                        case "ISTXLEVEL2": data.IsTXLevel2=(bool)value;break;
                        case "ISCOMPLETEGUIDEPASSSTAGE2LEVEL": data.IsCompleteGuidePassStage2Level=(bool)value;break;
                        case "LEVEL1GOLD": data.Level1Gold=(float)value;break;
                        case "LEVEL2GOLD": data.Level2Gold=(float)value;break;
                        case "LEVEL2TXTIME": data.Level2TXTime=(long)value;break;
                        case "ISSYNCSUCCESS": data.IsSyncSuccess=(bool)value;break;
                        case "ISGUIDEGOLD": data.IsGuideGold=(bool)value;break;
                        case "ISGUIDEGOLDCOMPLETE": data.IsGuideGoldComplete=(bool)value;break;
                        case "ISGUIDEGOLDTARGETCOMPLETE": data.IsGuideGoldTargetComplete=(bool)value;break;
                        case "LASTGETEVERYDAYGIFT": data.LastGetEveryDayGift=(long)value;break;
                        case "VIDEOTOOLREWARDINDEX": data.VideoToolRewardIndex=(int)value;break;
                        case "TODAYCHALLENGETIMES": data.TodayChallengeTimes=(int)value;break;
                        case "ISCOMPLETEAPPRATINGPANEL": data.IsCompleteAppRatingPanel=(bool)value;break;
                        case "NORMALGETTIMES": data.NormalGetTimes=(int)value;break;
                        case "INTERADTIMES": data.InterAdTimes=(int)value;break;
                        case "ISCOMPLETERECORDGUIDE": data.IsCompleteRecordGuide=(bool)value;break;
                        case "ISCOMPLETESHOWCOINREWARDHINT": data.IsCompleteShowCoinRewardHint=(bool)value;break;
                        case "ISCOMPLETEEXTRAGOLDGUIDE": data.IsCompleteExtraGoldGuide=(bool)value;break;
                        case "ISCOMPLETECOINNEWPEOPLEREWARD": data.IsCompleteCoinNewPeopleReward=(bool)value;break;
                        case "ISSHOWCOIN": data.IsShowCoin=(bool)value;break;
                        case "ISCOMPLETESIGNIN": data.IsCompleteSignIn=(bool)value;break;
                    }
                }
                throw new JsonSerializationException("Incomplete UserLocalData object.");
            }
        }
        private static JObject Document(JToken token)
        {
            if(token.Type==JTokenType.Null)return null;
            if(!(token is JObject document))throw new JsonSerializationException("Nested user document must be an object.");
            return (JObject)document.DeepClone();
        }
        public static string Write(OriginalUserLocalData data)
        {
            if(data==null)return "null";
            var root=new JObject();
            root["IsVPN"]=data.IsVPN;
            root["IsTestedVPN"]=data.IsTestedVPN;
            root["UserId"]=data.UserId;
            root["UserName"]=data.UserName;
            root["UserLevel"]=data.UserLevel;
            root["Level"]=data.Level;
            root["LevelId"]=data.LevelId;
            root["LevelSeed"]=data.LevelSeed;
            root["PassLevelTime"]=data.PassLevelTime;
            root["IsRandomLevelSeed"]=data.IsRandomLevelSeed;
            root["LoginDay"]=data.LoginDay;
            root["TodayPassLevelCount"]=data.TodayPassLevelCount;
            root["LoginDayCoin"]=data.LoginDayCoin;
            root["Gold"]=data.Gold;
            root["Coin"]=data.Coin;
            root["AddScrewCount"]=data.AddScrewCount;
            root["RevokeCount"]=data.RevokeCount;
            root["ExchangeCount"]=data.ExchangeCount;
            root["CurrentLevelAddScrewCount"]=data.CurrentLevelAddScrewCount;
            root["IsAudio"]=data.IsAudio;
            root["IsVibrate"]=data.IsVibrate;
            root["RegisterTime"]=data.RegisterTime;
            root["LoginTime"]=data.LoginTime;
            root["UserLssInfo"]=data.UserLssInfo==null?JValue.CreateNull():data.UserLssInfo.DeepClone();
            root["ServerConfigData"]=data.ServerConfigData==null?JValue.CreateNull():data.ServerConfigData.DeepClone();
            root["GoldRewardTargetS2CData"]=data.GoldRewardTargetS2CData==null?JValue.CreateNull():data.GoldRewardTargetS2CData.DeepClone();
            root["LocalPendingReward"]=data.LocalPendingReward==null?JValue.CreateNull():data.LocalPendingReward.DeepClone();
            root["ScrewDoneCount"]=data.ScrewDoneCount;
            root["ScrewMoveCount"]=data.ScrewMoveCount;
            root["LuckyDrawScrewDoneTimes"]=data.LuckyDrawScrewDoneTimes;
            root["LuckyDrawShowTimes"]=data.LuckyDrawShowTimes;
            root["NewGameplayUnlockIndex"]=data.NewGameplayUnlockIndex;
            root["GuideIndex"]=data.GuideIndex;
            root["LevelInfo"]=data.LevelInfo;
            root["LuckyScrewDoneCount"]=data.LuckyScrewDoneCount;
            root["TXTargetGold"]=data.TXTargetGold;
            root["ComeOnGold"]=data.ComeOnGold;
            root["ComeOnAddGold"]=data.ComeOnAddGold;
            root["Level1TXTime"]=data.Level1TXTime;
            root["IsTXLevel1"]=data.IsTXLevel1;
            root["IsGoldReduceLevel1"]=data.IsGoldReduceLevel1;
            root["IsGoldReduceLevel2"]=data.IsGoldReduceLevel2;
            root["IsTXLevel2"]=data.IsTXLevel2;
            root["IsCompleteGuidePassStage2Level"]=data.IsCompleteGuidePassStage2Level;
            root["Level1Gold"]=data.Level1Gold;
            root["Level2Gold"]=data.Level2Gold;
            root["Level2TXTime"]=data.Level2TXTime;
            root["IsSyncSuccess"]=data.IsSyncSuccess;
            root["IsGuideGold"]=data.IsGuideGold;
            root["IsGuideGoldComplete"]=data.IsGuideGoldComplete;
            root["IsGuideGoldTargetComplete"]=data.IsGuideGoldTargetComplete;
            root["LastGetEveryDayGift"]=data.LastGetEveryDayGift;
            root["VideoToolRewardIndex"]=data.VideoToolRewardIndex;
            root["TodayChallengeTimes"]=data.TodayChallengeTimes;
            root["IsCompleteAppRatingPanel"]=data.IsCompleteAppRatingPanel;
            root["NormalGetTimes"]=data.NormalGetTimes;
            root["InterAdTimes"]=data.InterAdTimes;
            root["IsCompleteRecordGuide"]=data.IsCompleteRecordGuide;
            root["IsCompleteShowCoinRewardHint"]=data.IsCompleteShowCoinRewardHint;
            root["IsCompleteExtraGoldGuide"]=data.IsCompleteExtraGoldGuide;
            root["IsCompleteCoinNewPeopleReward"]=data.IsCompleteCoinNewPeopleReward;
            root["IsShowCoin"]=data.IsShowCoin;
            root["IsCompleteSignIn"]=data.IsCompleteSignIn;
            return root.ToString(Formatting.None);
        }
    }
}
