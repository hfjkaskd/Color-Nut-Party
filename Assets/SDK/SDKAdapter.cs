using System;
using System.Collections.Generic;
using AdjustIntegration;
using MaxAdsIntegration;
using ThinkingDataIntegration;
using UnityEngine;

/// <summary>
/// Consumer-facing entry point for analytics, attribution and fullscreen ads.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class SDKAdapter : MonoBehaviour
{
    private const string RewardedAdCountPlayerPrefsKey =
        "SDKAdapter.UserProperties.RewardedAdCount";
    private const string InterstitialAdCountPlayerPrefsKey =
        "SDKAdapter.UserProperties.InterstitialAdCount";

    private static SDKAdapter instance;

    [SerializeField]
    private bool autoInitialize = true;

    private bool initializationStarted;
    private bool identityApplied;
    private bool sdkEventsSubscribed;
    private bool advertisingIdRequested;
    private string currentCid = string.Empty;
    private string currentThinkingIdentity = string.Empty;
    private string currentAdvertisingId = string.Empty;
    private string lastAdjustAttributionSignature = string.Empty;

    internal bool IsRewardVideoReady =>
        initializationStarted && MaxAdsAdapter.IsRewardedReady;

    internal bool IsInterstitialReady =>
        initializationStarted && MaxAdsAdapter.IsInterstitialReady;

    /// <summary>
    /// Returns the persistent project SDK entry point, creating it when the
    /// scene does not contain one.
    /// </summary>
    internal static SDKAdapter GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<SDKAdapter>(true);
        if (instance != null)
        {
            return instance;
        }

        var host = new GameObject("[SDK Adapter]");
        return host.AddComponent<SDKAdapter>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (!autoInitialize || initializationStarted)
        {
            return;
        }

        if (!Initialize())
        {
            Debug.LogError(
                "[SDKAdapter] One or more SDK initialization requests failed.");
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (!sdkEventsSubscribed)
        {
            return;
        }

        MaxAdsAdapter.OnLifecycleChanged -= HandleMaxAdLifecycle;
        MaxAdsAdapter.OnRevenuePaid -= HandleMaxRevenuePaid;
        AdjustAdapter.OnAttributionChanged -=
            HandleAdjustAttributionChanged;
        sdkEventsSubscribed = false;
    }

    /// <summary>
    /// Explicitly starts every SDK without restarting initialized adapters.
    /// </summary>
    public bool Initialize()
    {
        if (initializationStarted &&
            ThinkingDataAdapter.IsInitialized &&
            AdjustAdapter.IsInitialized &&
            (MaxAdsAdapter.IsInitialized ||
             MaxAdsAdapter.IsInitializationInProgress))
        {
            return true;
        }

        initializationStarted = true;

        if (!sdkEventsSubscribed)
        {
            MaxAdsAdapter.OnLifecycleChanged += HandleMaxAdLifecycle;
            MaxAdsAdapter.OnRevenuePaid += HandleMaxRevenuePaid;
            AdjustAdapter.OnAttributionChanged +=
                HandleAdjustAttributionChanged;
            sdkEventsSubscribed = true;
        }

        if (!identityApplied)
        {
            identityApplied = ApplyIdentity();
            ThinkingDataAdapter.EnableAdjustSharing();
        }

        var thinkingStarted = ThinkingDataAdapter.IsInitialized ||
                              ThinkingDataAdapter.InitializeNow();

        var adjustStarted = AdjustAdapter.IsInitialized ||
                            AdjustAdapter.InitializeNow(
                                new Dictionary<string, string>
                                {
                                    { "ta_distinct_id", currentThinkingIdentity },
                                    { "ta_account_id", currentThinkingIdentity },
                                    { "cid", currentCid }
                                });
        if (adjustStarted)
        {
            RequestAdvertisingId();
        }

        var maxStarted = MaxAdsAdapter.IsInitialized ||
                         MaxAdsAdapter.IsInitializationInProgress;
        if (!maxStarted)
        {
            MaxAdsAdapter.SetUserId(currentThinkingIdentity);
            maxStarted = MaxAdsAdapter.InitializeNow();
        }

        var attribution = AdjustAdapter.Attribution;
        if (adjustStarted && attribution != null)
        {
            HandleAdjustAttributionChanged(attribution);
        }

        return thinkingStarted && adjustStarted && maxStarted;
    }

    public void Track(
        string eventName,
        Dictionary<string, object> properties = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            Debug.LogWarning(
                "[SDKAdapter] Ignored analytics event with an empty name.");
            return;
        }

        if (!initializationStarted)
        {
            Debug.LogWarning(
                "[SDKAdapter] Track ignored before Initialize was called.");
            return;
        }

        try
        {
            BizzaAnalyticsAgent.Instance?.BTrack(eventName, properties);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        ThinkingDataAdapter.Track(eventName, properties);
    }

    public void ShowRewardVideo(string placement, Action<bool> completed) =>
        ShowAd(MaxAdType.Rewarded, placement, completed);

    public void ShowInterstitial(string placement, Action<bool> completed) =>
        ShowAd(MaxAdType.Interstitial, placement, completed);

    private void SetUserProperties(Dictionary<string, object> properties)
    {
        if (properties == null || properties.Count == 0)
        {
            return;
        }

        if (!initializationStarted)
        {
            Debug.LogWarning(
                "[SDKAdapter] User properties ignored before Initialize was called.");
            return;
        }

        try
        {
            BizzaAnalyticsAgent.Instance?.BUserProp(properties);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        ThinkingDataAdapter.UserSet(properties);
    }

    private void ShowAd(
        MaxAdType adType,
        string placement,
        Action<bool> completed)
    {
        Action<bool> complete = success =>
        {
            try
            {
                completed?.Invoke(success);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };

        if (!initializationStarted)
        {
            Debug.LogWarning(
                "[SDKAdapter] Ad request rejected before Initialize was called.");
            complete(false);
            return;
        }

        Action<MaxAdResult> handleResult = result =>
        {
            var success = result != null &&
                          result.AdType == adType &&
                          (adType == MaxAdType.Rewarded
                              ? result.Status == MaxAdResultStatus.Rewarded
                              : result.Status == MaxAdResultStatus.Completed);
            complete(success);
        };

        if (adType == MaxAdType.Rewarded)
        {
            MaxAdsAdapter.ShowRewarded(placement, handleResult);
        }
        else
        {
            MaxAdsAdapter.ShowInterstitial(placement, handleResult);
        }
    }

    private bool ApplyIdentity()
    {
        currentCid = DeviceInfo.GetOrCreateCid();
        if (!ThinkingDataAdapter.TryGetAppCode(out var appCode))
        {
            currentThinkingIdentity = string.Empty;
            Debug.LogError(
                "[SDKAdapter] Identity was not applied because the " +
                "ThinkingData configuration is unavailable or invalid.");
            return false;
        }

        currentThinkingIdentity = appCode + "@" + currentCid;
        ThinkingDataAdapter.SetDistinctId(currentThinkingIdentity);
        ThinkingDataAdapter.Login(currentThinkingIdentity);
        ThinkingDataAdapter.ClearSuperProperties();
        ThinkingDataAdapter.SetSuperProperties(
            new Dictionary<string, object>
            {
                { "appcode", appCode }
            });
        SetUserProperties(BuildBaseUserProperties(appCode));

        return true;
    }

    private Dictionary<string, object> BuildBaseUserProperties(
        string appCode)
    {
        var properties = new Dictionary<string, object>
        {
            { "deviceId", currentCid },
            { "isVpnUser", DeviceInfo.GetIsVpnUser() },
            { "simState", DeviceInfo.GetSimState() },
            { "appcode", appCode }
        };

        AddAdCountProperties(properties);
        if (!string.IsNullOrEmpty(currentAdvertisingId))
        {
            properties["gaid"] = currentAdvertisingId;
        }

        return properties;
    }

    private void HandleAdjustAttributionChanged(
        AdjustAttributionData attribution)
    {
        if (attribution == null)
        {
            return;
        }

        var userNatural = attribution.Status switch
        {
            AdjustAttributionStatus.Organic => "organic",
            AdjustAttributionStatus.Paid => "paid",
            _ => string.Empty
        };
        var adNetworkSource = !string.IsNullOrWhiteSpace(attribution.Network)
            ? attribution.Network
            : attribution.TrackerName;
        var adNetwork = NormalizeText(adNetworkSource).ToLowerInvariant();
        var campaignName = NormalizeText(attribution.Campaign);
        var creativeName = NormalizeText(attribution.Creative);
        var trackerName = NormalizeText(attribution.TrackerName);
        var trackerToken = NormalizeText(attribution.TrackerToken);
        var signature = string.Join(
            "\u001f",
            new[]
            {
                currentThinkingIdentity,
                userNatural,
                adNetwork,
                campaignName,
                creativeName,
                trackerName,
                trackerToken
            });

        if (string.Equals(
                lastAdjustAttributionSignature,
                signature,
                StringComparison.Ordinal))
        {
            return;
        }

        lastAdjustAttributionSignature = signature;
        var properties = new Dictionary<string, object>();

        Add("usernatural", userNatural);
        Add("ad_network", adNetwork);
        Add("campaign_name", campaignName);
        Add("creative_name", creativeName);
        Add("tracker_name", trackerName);
        Add("tracker_token", trackerToken);

        SetUserProperties(properties);

        void Add(string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                properties[key] = value;
            }
        }
    }

    private static string NormalizeText(string value) =>
        (value ?? string.Empty).Trim();

    private void RequestAdvertisingId()
    {
        if (advertisingIdRequested)
        {
            return;
        }

        advertisingIdRequested = true;
        if (!DeviceInfo.TryRequestAdvertisingId(
                HandleAdvertisingIdChanged))
        {
            advertisingIdRequested = false;
        }
    }

    private void HandleAdvertisingIdChanged(string advertisingId)
    {
        var normalizedAdvertisingId = NormalizeText(advertisingId);
        if (string.IsNullOrEmpty(normalizedAdvertisingId))
        {
            // The GAID may be temporarily unavailable. Allow another request
            // when the app regains focus.
            advertisingIdRequested = false;
            return;
        }

        if (string.Equals(
                currentAdvertisingId,
                normalizedAdvertisingId,
                StringComparison.Ordinal))
        {
            return;
        }

        currentAdvertisingId = normalizedAdvertisingId;
        SetUserProperties(
            new Dictionary<string, object>
            {
                { "gaid", currentAdvertisingId }
            });
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus &&
            initializationStarted &&
            AdjustAdapter.IsInitialized &&
            string.IsNullOrEmpty(currentAdvertisingId))
        {
            RequestAdvertisingId();
        }
    }

    private static void AddAdCountProperties(
        Dictionary<string, object> properties)
    {
        var rewardedCount = GetStoredAdCount(
            RewardedAdCountPlayerPrefsKey);
        var interstitialCount = GetStoredAdCount(
            InterstitialAdCountPlayerPrefsKey);

        properties["ads_amout"] =
            (long)rewardedCount + interstitialCount;
        properties["rewardvideo_amout"] = (long)rewardedCount;
        properties["interstitial_amout"] = (long)interstitialCount;
    }

    private void RecordAdCount(MaxAdType adType)
    {
        var key = adType == MaxAdType.Rewarded
            ? RewardedAdCountPlayerPrefsKey
            : InterstitialAdCountPlayerPrefsKey;
        var currentCount = GetStoredAdCount(key);
        if (currentCount < int.MaxValue)
        {
            PlayerPrefs.SetInt(key, currentCount + 1);
            PlayerPrefs.Save();
        }

        var properties = new Dictionary<string, object>();
        AddAdCountProperties(properties);
        SetUserProperties(properties);
    }

    private static int GetStoredAdCount(string key) =>
        Math.Max(0, PlayerPrefs.GetInt(key, 0));

    private void HandleMaxAdLifecycle(MaxAdLifecycleInfo lifecycleInfo)
    {
        if (lifecycleInfo == null)
        {
            return;
        }

        var step = lifecycleInfo.Step switch
        {
            MaxAdLifecycleStep.Call => "ad_call",
            MaxAdLifecycleStep.Start => "ad_start",
            MaxAdLifecycleStep.Ready => "ad_ready",
            MaxAdLifecycleStep.Show => "ad_show",
            MaxAdLifecycleStep.Complete => "ad_complete",
            MaxAdLifecycleStep.Fail => "ad_fail",
            MaxAdLifecycleStep.Closed => "ad_closed",
            _ => "ad_fail"
        };

        Track(
            "MaxTrack",
            new Dictionary<string, object>
            {
                { "step", step },
                {
                    "adtype",
                    lifecycleInfo.AdType == MaxAdType.Rewarded
                        ? "rv"
                        : "iv"
                },
                { "from", lifecycleInfo.Placement },
                { "errorCode", lifecycleInfo.ErrorCode },
                { "networkName", lifecycleInfo.NetworkName }
            });
    }

    private void HandleMaxRevenuePaid(MaxRevenueInfo revenueInfo)
    {
        if (revenueInfo == null)
        {
            Debug.LogWarning(
                "[SDKAdapter] Ignored a MAX paid-impression callback " +
                "without ad data.");
            return;
        }

        AdjustAdapter.RecordAdImpression();
        RecordAdCount(revenueInfo.AdType);

        if (revenueInfo.Revenue < 0d ||
            double.IsNaN(revenueInfo.Revenue) ||
            double.IsInfinity(revenueInfo.Revenue))
        {
            Debug.LogWarning(
                "[SDKAdapter] Ignored invalid MAX paid-impression revenue.");
            return;
        }

        AdjustAdapter.TrackAdRevenue(
            revenueInfo.Revenue,
            revenueInfo.NetworkName,
            revenueInfo.AdUnitId,
            revenueInfo.Placement,
            "USD");

        Track(
            "ad_revenue",
            new Dictionary<string, object>
            {
                { "countryCode", revenueInfo.CountryCode },
                { "revenue", revenueInfo.Revenue },
                { "networkName", revenueInfo.NetworkName },
                { "adUnitId", revenueInfo.AdUnitId },
                {
                    "adFormat",
                    revenueInfo.AdType == MaxAdType.Rewarded
                        ? "REWARDED"
                        : "INTER"
                },
                { "mediation", "MAX" },
                { "networkfirmid", 0 },
                { "placement", revenueInfo.Placement },
                { "ispvn", DeviceInfo.GetIsVpnUser() },
                { "adsource", revenueInfo.Placement }
            });
    }

}
