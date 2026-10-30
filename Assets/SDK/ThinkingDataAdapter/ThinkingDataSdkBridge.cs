using System;
using System.Collections.Generic;
using ThinkingData.Analytics;
using ThinkingData.Analytics.Utils;
using UnityEngine;

namespace ThinkingDataIntegration
{
    internal interface IThinkingDataSdkBridge
    {
        void Initialize(string appId, string serverUrl);

        void EnableAllAutoTrack();

        void EnableAppsFlyerSharing();

        void EnableAdjustSharing();

        void SetDistinctId(string distinctId);

        string GetDistinctId();

        string GetDeviceId();

        void SetSuperProperties(Dictionary<string, object> properties);

        void ClearSuperProperties();

        void SetDynamicSuperProperties(
            Func<Dictionary<string, object>> propertiesProvider);

        void Login(string accountId);

        void Logout();

        void Track(string eventName, Dictionary<string, object> properties);

        void Track(
            string eventName,
            Dictionary<string, object> properties,
            DateTime time,
            TimeZoneInfo timeZone);

        void UserSet(Dictionary<string, object> properties);

        void UserSet(
            Dictionary<string, object> properties,
            DateTime time);

        void UserSetOnce(Dictionary<string, object> properties);

        void UserSetOnce(
            Dictionary<string, object> properties,
            DateTime time);

        void Flush();
    }

    internal sealed class ThinkingDataSdkBridge : IThinkingDataSdkBridge
    {
        public void Initialize(string appId, string serverUrl)
        {
            // SDK 3.5.0 enables verbose logging by default and may include
            // credentials and event JSON. Establish its host first so the
            // official logging switch is effective before initialization.
            new GameObject("ThinkingData", typeof(TDAnalytics));
            TDAnalytics.EnableLog(false);
            TDAnalytics.Init(new[] { new TDConfig(appId, serverUrl) });
            TDAnalytics.EnableLog(false);
        }

        public void EnableAllAutoTrack()
        {
            TDAnalytics.EnableAutoTrack(TDAutoTrackEventType.All);
        }

        public void EnableAppsFlyerSharing()
        {
            TDAnalytics.EnableThirdPartySharing(
                TDThirdPartyType.APPSFLYER);
        }

        public void EnableAdjustSharing()
        {
            TDAnalytics.EnableThirdPartySharing(
                TDThirdPartyType.ADJUST);
        }

        public void SetDistinctId(string distinctId)
        {
            TDAnalytics.SetDistinctId(distinctId);
        }

        public string GetDistinctId()
        {
            return TDAnalytics.GetDistinctId() ?? string.Empty;
        }

        public string GetDeviceId()
        {
            return TDAnalytics.GetDeviceId() ?? string.Empty;
        }

        public void SetSuperProperties(
            Dictionary<string, object> properties)
        {
            TDAnalytics.SetSuperProperties(properties);
        }

        public void ClearSuperProperties()
        {
            TDAnalytics.ClearSuperProperties();
        }

        public void SetDynamicSuperProperties(
            Func<Dictionary<string, object>> propertiesProvider)
        {
            TDAnalytics.SetDynamicSuperProperties(
                new DynamicSuperPropertiesHandler(propertiesProvider));
        }

        public void Login(string accountId)
        {
            TDAnalytics.Login(accountId);
        }

        public void Logout()
        {
            TDAnalytics.Logout();
        }

        public void Track(
            string eventName,
            Dictionary<string, object> properties)
        {
            TDAnalytics.Track(eventName, properties);
        }

        public void Track(
            string eventName,
            Dictionary<string, object> properties,
            DateTime time,
            TimeZoneInfo timeZone)
        {
            TDAnalytics.Track(eventName, properties, time, timeZone);
        }

        public void UserSet(Dictionary<string, object> properties)
        {
            TDAnalytics.UserSet(properties);
        }

        public void UserSet(
            Dictionary<string, object> properties,
            DateTime time)
        {
            TDAnalytics.UserSet(properties, time);
        }

        public void UserSetOnce(Dictionary<string, object> properties)
        {
            TDAnalytics.UserSetOnce(properties);
        }

        public void UserSetOnce(
            Dictionary<string, object> properties,
            DateTime time)
        {
            TDAnalytics.UserSetOnce(properties, time);
        }

        public void Flush()
        {
            TDAnalytics.Flush();
        }

        private sealed class DynamicSuperPropertiesHandler :
            TDDynamicSuperPropertiesHandler
        {
            private readonly Func<Dictionary<string, object>> provider;

            internal DynamicSuperPropertiesHandler(
                Func<Dictionary<string, object>> provider)
            {
                this.provider = provider ??
                    throw new ArgumentNullException(nameof(provider));
            }

            public Dictionary<string, object> GetDynamicSuperProperties()
            {
                try
                {
                    var properties = provider();
                    return properties == null
                        ? new Dictionary<string, object>()
                        : new Dictionary<string, object>(properties);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[ThinkingData] Dynamic super-property provider failed (" +
                        exception.GetType().Name + ").");
                    return new Dictionary<string, object>();
                }
            }
        }
    }
}
