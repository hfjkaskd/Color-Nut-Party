using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AdjustSdk;
using UnityEngine;

namespace AdjustIntegration
{
    public enum AdjustAttributionStatus
    {
        Unknown,
        Organic,
        Paid
    }

    public sealed class AdjustAttributionData
    {
        internal AdjustAttributionData(
            string trackerToken,
            string trackerName,
            string network,
            string campaign,
            string adgroup,
            string creative,
            string clickLabel,
            string costType,
            double? costAmount,
            string costCurrency,
            string fbInstallReferrer,
            IDictionary<string, object> jsonResponse)
        {
            TrackerToken = trackerToken ?? string.Empty;
            TrackerName = trackerName ?? string.Empty;
            Network = network ?? string.Empty;
            Campaign = campaign ?? string.Empty;
            Adgroup = adgroup ?? string.Empty;
            Creative = creative ?? string.Empty;
            ClickLabel = clickLabel ?? string.Empty;
            CostType = costType ?? string.Empty;
            CostAmount = costAmount;
            CostCurrency = costCurrency ?? string.Empty;
            FbInstallReferrer = fbInstallReferrer ?? string.Empty;
            Status = ResolveStatus(
                trackerToken,
                trackerName,
                network,
                campaign,
                adgroup,
                creative,
                clickLabel);

            Dictionary<string, object> snapshot =
                jsonResponse == null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(jsonResponse);
            JsonResponse =
                new ReadOnlyDictionary<string, object>(snapshot);
        }

        public string TrackerToken { get; private set; }

        public string TrackerName { get; private set; }

        public string Network { get; private set; }

        public string Campaign { get; private set; }

        public string Adgroup { get; private set; }

        public string Creative { get; private set; }

        public string ClickLabel { get; private set; }

        public string CostType { get; private set; }

        public double? CostAmount { get; private set; }

        public string CostCurrency { get; private set; }

        public string FbInstallReferrer { get; private set; }

        public AdjustAttributionStatus Status { get; private set; }

        public bool? IsOrganic
        {
            get
            {
                if (Status == AdjustAttributionStatus.Unknown)
                {
                    return null;
                }

                return Status == AdjustAttributionStatus.Organic;
            }
        }

        public IReadOnlyDictionary<string, object> JsonResponse
        {
            get;
            private set;
        }

        internal bool HasSameAttribution(AdjustAttributionData other)
        {
            return other != null &&
                   Status == other.Status &&
                   string.Equals(TrackerToken, other.TrackerToken,
                       StringComparison.Ordinal) &&
                   string.Equals(TrackerName, other.TrackerName,
                       StringComparison.Ordinal) &&
                   string.Equals(Network, other.Network,
                       StringComparison.Ordinal) &&
                   string.Equals(Campaign, other.Campaign,
                       StringComparison.Ordinal) &&
                   string.Equals(Adgroup, other.Adgroup,
                       StringComparison.Ordinal) &&
                   string.Equals(Creative, other.Creative,
                       StringComparison.Ordinal) &&
                   string.Equals(ClickLabel, other.ClickLabel,
                       StringComparison.Ordinal) &&
                   string.Equals(CostType, other.CostType,
                       StringComparison.Ordinal) &&
                   Nullable.Equals(CostAmount, other.CostAmount) &&
                   string.Equals(CostCurrency, other.CostCurrency,
                       StringComparison.Ordinal) &&
                   string.Equals(FbInstallReferrer,
                       other.FbInstallReferrer,
                       StringComparison.Ordinal);
        }

        private static AdjustAttributionStatus ResolveStatus(
            string trackerToken,
            string trackerName,
            string network,
            string campaign,
            string adgroup,
            string creative,
            string clickLabel)
        {
            bool organicTracker = IsOrganicValue(trackerName);
            bool organicNetwork = IsOrganicValue(network);
            bool hasOrganicSignal = organicTracker || organicNetwork;
            bool hasPaidSignal =
                HasNonOrganicValue(trackerName) ||
                HasNonOrganicValue(network) ||
                !string.IsNullOrWhiteSpace(campaign) ||
                !string.IsNullOrWhiteSpace(adgroup) ||
                !string.IsNullOrWhiteSpace(creative) ||
                !string.IsNullOrWhiteSpace(clickLabel);

            // Conflicting or incomplete data must remain unknown instead of
            // incorrectly classifying a user as organic.
            if (hasOrganicSignal && hasPaidSignal)
            {
                return AdjustAttributionStatus.Unknown;
            }

            if (hasOrganicSignal)
            {
                return AdjustAttributionStatus.Organic;
            }

            if (hasPaidSignal ||
                !string.IsNullOrWhiteSpace(trackerToken))
            {
                return AdjustAttributionStatus.Paid;
            }

            return AdjustAttributionStatus.Unknown;
        }

        private static bool IsOrganicValue(string value)
        {
            return string.Equals(
                value?.Trim(),
                "Organic",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasNonOrganicValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !IsOrganicValue(value);
        }
    }

    public static class AdjustAdapter
    {
        private const string AppLovinMaxRevenueSource =
            "applovin_max_sdk";
        private const string IpuCountPlayerPrefsKey =
            "AdjustIntegration.IPU.ImpressionCount.v1";
        private const int MaximumIpuMilestone = 10;
        private const int DefaultAttributionReadTimeoutMilliseconds = 5000;

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, string>
            GlobalCallbackParameters =
                new Dictionary<string, string>(StringComparer.Ordinal);

        private static InitializationState initializationState =
            InitializationState.NotInitialized;
        private static AdjustIntegrationConfig activeConfig;
        private static string cachedAdid = string.Empty;
        private static AdjustAttributionData cachedAttribution;

        public static event Action<string> OnAdidChanged;

        public static event Action<AdjustAttributionData>
            OnAttributionChanged;

        public static bool IsInitialized
        {
            get
            {
                lock (Sync)
                {
                    return initializationState ==
                           InitializationState.Initialized;
                }
            }
        }

        public static string Adid
        {
            get
            {
                lock (Sync)
                {
                    return cachedAdid;
                }
            }
        }

        public static AdjustAttributionData Attribution
        {
            get
            {
                lock (Sync)
                {
                    return cachedAttribution;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lock (Sync)
            {
                GlobalCallbackParameters.Clear();
                initializationState =
                    InitializationState.NotInitialized;
                activeConfig = null;
                cachedAdid = string.Empty;
                cachedAttribution = null;
                OnAdidChanged = null;
                OnAttributionChanged = null;
            }
        }

        /// <summary>
        /// Initializes Adjust in Production using the encrypted Resources
        /// configuration. Callback parameters are applied before InitSdk.
        /// </summary>
        public static bool InitializeNow(
            IDictionary<string, string> callbackParameters = null)
        {
            try
            {
                AdjustIntegrationConfig config;
                string configError;
                if (!AdjustConfigCrypto.TryLoadFromResources(
                        out config,
                        out configError))
                {
                    Debug.LogError(
                        "[AdjustIntegration] Initialization skipped: " +
                        configError);
                    return false;
                }

                List<KeyValuePair<string, string>> suppliedParameters;
                if (!TrySnapshotParameters(
                        callbackParameters,
                        "global callback",
                        out suppliedParameters))
                {
                    return false;
                }

                bool alreadyInitialized;
                List<KeyValuePair<string, string>> parametersForStartup =
                    null;

                lock (Sync)
                {
                    if (initializationState ==
                        InitializationState.Initializing)
                    {
                        Debug.LogWarning(
                            "[AdjustIntegration] Initialization is already in progress.");
                        return false;
                    }

                    for (int index = 0;
                         index < suppliedParameters.Count;
                         index++)
                    {
                        KeyValuePair<string, string> parameter =
                            suppliedParameters[index];
                        GlobalCallbackParameters[parameter.Key] =
                            parameter.Value;
                    }

                    alreadyInitialized =
                        initializationState ==
                        InitializationState.Initialized;
                    if (!alreadyInitialized)
                    {
                        initializationState =
                            InitializationState.Initializing;
                        parametersForStartup =
                            new List<KeyValuePair<string, string>>(
                                GlobalCallbackParameters);
                    }
                }

                if (alreadyInitialized)
                {
                    return ApplyGlobalCallbackParameters(
                        suppliedParameters);
                }

                try
                {
                    if (!ApplyGlobalCallbackParameters(
                            parametersForStartup))
                    {
                        SetInitializationFailed();
                        return false;
                    }

                    AdjustConfig sdkConfig = new AdjustConfig(
                        config.AppToken,
                        AdjustEnvironment.Production);
                    sdkConfig.AttributionChangedDelegate =
                        HandleAttributionChanged;

                    Adjust.InitSdk(sdkConfig);

                    lock (Sync)
                    {
                        activeConfig = config;
                        initializationState =
                            InitializationState.Initialized;
                    }

                    RequestAdidWithoutWaiting();
                    RequestAttribution(
                        DefaultAttributionReadTimeoutMilliseconds);
                    return true;
                }
                catch (Exception exception)
                {
                    SetInitializationFailed();
                    return LogSdkFailure(
                        "InitializeNow",
                        exception);
                }
            }
            catch (Exception exception)
            {
                SetInitializationFailed();
                return LogAdapterFailure(
                    "InitializeNow",
                    exception);
            }
        }

        /// <summary>
        /// Stores a callback parameter before initialization, or applies it
        /// immediately after initialization.
        /// </summary>
        public static bool SetGlobalCallbackParameter(
            string key,
            string value)
        {
            try
            {
                string normalizedKey = (key ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(normalizedKey))
                {
                    Debug.LogError(
                        "[AdjustIntegration] Global callback parameter key is empty.");
                    return false;
                }

                if (value == null)
                {
                    Debug.LogError(
                        "[AdjustIntegration] Global callback parameter value is null.");
                    return false;
                }

                bool applyImmediately;
                lock (Sync)
                {
                    GlobalCallbackParameters[normalizedKey] = value;
                    applyImmediately =
                        initializationState ==
                        InitializationState.Initialized;
                }

                if (!applyImmediately)
                {
                    return true;
                }

                try
                {
                    Adjust.AddGlobalCallbackParameter(
                        normalizedKey,
                        value);
                    return true;
                }
                catch (Exception exception)
                {
                    return LogSdkFailure(
                        "SetGlobalCallbackParameter",
                        exception);
                }
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    "SetGlobalCallbackParameter",
                    exception);
            }
        }

        /// <summary>
        /// Asynchronously refreshes the device's current Adjust attribution.
        /// A later native attribution-change callback can still update it.
        /// </summary>
        public static bool RequestAttribution(
            int timeoutInMilliseconds =
                DefaultAttributionReadTimeoutMilliseconds)
        {
            try
            {
                if (!EnsureInitialized("RequestAttribution"))
                {
                    return false;
                }

                if (timeoutInMilliseconds <= 0)
                {
                    Debug.LogError(
                        "[AdjustIntegration] Attribution timeout must be greater than zero.");
                    return false;
                }

                Adjust.GetAttributionWithTimeout(
                    timeoutInMilliseconds,
                    HandleAttributionChanged);
                return true;
            }
            catch (Exception exception)
            {
                return LogSdkFailure(
                    "RequestAttribution",
                    exception);
            }
        }

        public static bool TrackEvent(
            string eventToken,
            IDictionary<string, string> callbackParameters = null,
            IDictionary<string, string> partnerParameters = null)
        {
            return TrackEventInternal(
                eventToken,
                null,
                string.Empty,
                callbackParameters,
                partnerParameters);
        }

        public static bool TrackRevenueEvent(
            string eventToken,
            double revenue,
            string currency = "USD",
            IDictionary<string, string> callbackParameters = null,
            IDictionary<string, string> partnerParameters = null)
        {
            return TrackEventInternal(
                eventToken,
                revenue,
                currency,
                callbackParameters,
                partnerParameters);
        }

        /// <summary>
        /// Reports AppLovin MAX impression-level ad revenue to Adjust.
        /// </summary>
        public static bool TrackAdRevenue(
            double revenue,
            string networkName,
            string adUnitId,
            string placement,
            string currency = "USD")
        {
            try
            {
                if (!EnsureInitialized("TrackAdRevenue") ||
                    !TryValidateRevenue(revenue, currency))
                {
                    return false;
                }

                AdjustAdRevenue adRevenue =
                    new AdjustAdRevenue(AppLovinMaxRevenueSource);
                adRevenue.SetRevenue(
                    revenue,
                    currency.Trim().ToUpperInvariant());
                adRevenue.AdImpressionsCount = 1;
                adRevenue.AdRevenueNetwork = networkName ?? string.Empty;
                adRevenue.AdRevenueUnit = adUnitId ?? string.Empty;
                adRevenue.AdRevenuePlacement = placement ?? string.Empty;

                Adjust.TrackAdRevenue(adRevenue);
                return true;
            }
            catch (Exception exception)
            {
                return LogSdkFailure("TrackAdRevenue", exception);
            }
        }

        /// <summary>
        /// Records one displayed ad for this local installation and reports
        /// the configured Adjust event when cumulative count reaches 3, 5,
        /// or 10. Calls before initialization do not advance the counter.
        /// </summary>
        public static bool RecordAdImpression()
        {
#if UNITY_EDITOR || (!UNITY_ANDROID && !UNITY_IOS)
            Debug.LogWarning(
                "[AdjustIntegration] IPU impressions are only recorded on Android/iOS players.");
            return false;
#else
            try
            {
                if (!EnsureInitialized("RecordAdImpression"))
                {
                    return false;
                }

                int previousCount = PlayerPrefs.GetInt(
                    IpuCountPlayerPrefsKey,
                    0);
                if (previousCount < 0)
                {
                    previousCount = 0;
                }

                if (previousCount >= MaximumIpuMilestone)
                {
                    return true;
                }

                int nextCount = previousCount + 1;
                AdjustIntegrationConfig config;
                lock (Sync)
                {
                    config = activeConfig;
                }

                if (config == null)
                {
                    Debug.LogError(
                        "[AdjustIntegration] IPU tracking has no active config.");
                    return false;
                }

                string milestoneToken =
                    config.GetIpuEventToken(nextCount);
                if (!string.IsNullOrEmpty(milestoneToken) &&
                    !TrackEvent(milestoneToken))
                {
                    return false;
                }

                PlayerPrefs.SetInt(IpuCountPlayerPrefsKey, nextCount);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    "RecordAdImpression",
                    exception);
            }
#endif
        }

        private static bool TrackEventInternal(
            string eventToken,
            double? revenue,
            string currency,
            IDictionary<string, string> callbackParameters,
            IDictionary<string, string> partnerParameters)
        {
            try
            {
                string normalizedToken =
                    (eventToken ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(normalizedToken))
                {
                    Debug.LogError(
                        "[AdjustIntegration] Event token is empty.");
                    return false;
                }

                if (!EnsureInitialized("TrackEvent"))
                {
                    return false;
                }

                if (revenue.HasValue &&
                    !TryValidateRevenue(revenue.Value, currency))
                {
                    return false;
                }

                List<KeyValuePair<string, string>> callbackSnapshot;
                if (!TrySnapshotParameters(
                        callbackParameters,
                        "event callback",
                        out callbackSnapshot))
                {
                    return false;
                }

                List<KeyValuePair<string, string>> partnerSnapshot;
                if (!TrySnapshotParameters(
                        partnerParameters,
                        "event partner",
                        out partnerSnapshot))
                {
                    return false;
                }

                AdjustEvent adjustEvent =
                    new AdjustEvent(normalizedToken);
                if (revenue.HasValue)
                {
                    adjustEvent.SetRevenue(
                        revenue.Value,
                        currency.Trim().ToUpperInvariant());
                }

                for (int index = 0;
                     index < callbackSnapshot.Count;
                     index++)
                {
                    KeyValuePair<string, string> parameter =
                        callbackSnapshot[index];
                    adjustEvent.AddCallbackParameter(
                        parameter.Key,
                        parameter.Value);
                }

                for (int index = 0;
                     index < partnerSnapshot.Count;
                     index++)
                {
                    KeyValuePair<string, string> parameter =
                        partnerSnapshot[index];
                    adjustEvent.AddPartnerParameter(
                        parameter.Key,
                        parameter.Value);
                }

                Adjust.TrackEvent(adjustEvent);
                return true;
            }
            catch (Exception exception)
            {
                return LogSdkFailure("TrackEvent", exception);
            }
        }

        private static bool ApplyGlobalCallbackParameters(
            IList<KeyValuePair<string, string>> parameters)
        {
            if (parameters == null)
            {
                return true;
            }

            try
            {
                for (int index = 0; index < parameters.Count; index++)
                {
                    KeyValuePair<string, string> parameter =
                        parameters[index];
                    Adjust.AddGlobalCallbackParameter(
                        parameter.Key,
                        parameter.Value);
                }

                return true;
            }
            catch (Exception exception)
            {
                return LogSdkFailure(
                    "ApplyGlobalCallbackParameters",
                    exception);
            }
        }

        private static bool TrySnapshotParameters(
            IDictionary<string, string> parameters,
            string parameterKind,
            out List<KeyValuePair<string, string>> snapshot)
        {
            snapshot = new List<KeyValuePair<string, string>>();
            if (parameters == null)
            {
                return true;
            }

            HashSet<string> normalizedKeys =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> parameter in parameters)
            {
                string key = (parameter.Key ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogError(
                        "[AdjustIntegration] " + parameterKind +
                        " parameter key is empty.");
                    return false;
                }

                if (parameter.Value == null)
                {
                    Debug.LogError(
                        "[AdjustIntegration] " + parameterKind +
                        " parameter '" + key + "' has a null value.");
                    return false;
                }

                if (!normalizedKeys.Add(key))
                {
                    Debug.LogError(
                        "[AdjustIntegration] " + parameterKind +
                        " parameters contain duplicate key '" + key + "'.");
                    return false;
                }

                snapshot.Add(
                    new KeyValuePair<string, string>(
                        key,
                        parameter.Value));
            }

            return true;
        }

        private static bool TryValidateRevenue(
            double revenue,
            string currency)
        {
            if (double.IsNaN(revenue) ||
                double.IsInfinity(revenue) ||
                revenue < 0d)
            {
                Debug.LogError(
                    "[AdjustIntegration] Revenue must be a finite, non-negative value.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(currency))
            {
                Debug.LogError(
                    "[AdjustIntegration] Revenue currency is empty.");
                return false;
            }

            return true;
        }

        private static bool EnsureInitialized(string operation)
        {
            lock (Sync)
            {
                if (initializationState ==
                    InitializationState.Initialized)
                {
                    return true;
                }
            }

            Debug.LogWarning(
                "[AdjustIntegration] " + operation +
                " rejected because Adjust is not initialized.");
            return false;
        }

        private static void RequestAdidWithoutWaiting()
        {
            try
            {
                // Editor and unsupported platforms intentionally do not invoke
                // this callback. Initialization never waits for it.
                Adjust.GetAdid(HandleAdidResult);
            }
            catch (Exception exception)
            {
                LogSdkFailure("GetAdid", exception);
            }
        }

        private static void HandleAdidResult(string adid)
        {
            try
            {
                string normalizedAdid =
                    (adid ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(normalizedAdid))
                {
                    return;
                }

                Action<string> handlers;
                lock (Sync)
                {
                    if (string.Equals(
                            cachedAdid,
                            normalizedAdid,
                            StringComparison.Ordinal))
                    {
                        return;
                    }

                    cachedAdid = normalizedAdid;
                    handlers = OnAdidChanged;
                }

                InvokeSafely(handlers, normalizedAdid, "OnAdidChanged");
            }
            catch (Exception exception)
            {
                LogAdapterFailure("HandleAdidResult", exception);
            }
        }

        private static void HandleAttributionChanged(
            AdjustAttribution attribution)
        {
            try
            {
                if (attribution == null)
                {
                    return;
                }

                AdjustAttributionData data =
                    new AdjustAttributionData(
                        attribution.TrackerToken,
                        attribution.TrackerName,
                        attribution.Network,
                        attribution.Campaign,
                        attribution.Adgroup,
                        attribution.Creative,
                        attribution.ClickLabel,
                        attribution.CostType,
                        attribution.CostAmount,
                        attribution.CostCurrency,
                        attribution.FbInstallReferrer,
                        attribution.JsonResponse);

                Action<AdjustAttributionData> handlers;
                lock (Sync)
                {
                    if (data.HasSameAttribution(cachedAttribution))
                    {
                        return;
                    }

                    cachedAttribution = data;
                    handlers = OnAttributionChanged;
                }

                InvokeSafely(
                    handlers,
                    data,
                    "OnAttributionChanged");
            }
            catch (Exception exception)
            {
                LogAdapterFailure(
                    "HandleAttributionChanged",
                    exception);
            }
        }

        private static void InvokeSafely(
            Action<string> handlers,
            string value,
            string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<string>)invocationList[index])(value);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure(eventName, exception);
                }
            }
        }

        private static void InvokeSafely(
            Action<AdjustAttributionData> handlers,
            AdjustAttributionData value,
            string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<AdjustAttributionData>)invocationList[index])(
                        value);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailure(eventName, exception);
                }
            }
        }

        private static void SetInitializationFailed()
        {
            lock (Sync)
            {
                activeConfig = null;
                initializationState =
                    InitializationState.NotInitialized;
            }
        }

        private static bool LogAdapterFailure(
            string operation,
            Exception exception)
        {
            Debug.LogError(
                "[AdjustIntegration] " + operation +
                " failed in the adapter (" +
                exception.GetType().Name + ").");
            return false;
        }

        private static bool LogSdkFailure(
            string operation,
            Exception exception)
        {
            Debug.LogError(
                "[AdjustIntegration] Adjust SDK call " + operation +
                " failed (" + exception.GetType().Name + ").");
            return false;
        }

        private static void LogSubscriberFailure(
            string eventName,
            Exception exception)
        {
            Debug.LogError(
                "[AdjustIntegration] Subscriber of " + eventName +
                " failed (" + exception.GetType().Name + ").");
        }

        private enum InitializationState
        {
            NotInitialized,
            Initializing,
            Initialized
        }
    }
}
