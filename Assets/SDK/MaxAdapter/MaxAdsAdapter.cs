using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace MaxAdsIntegration
{
    public enum MaxAdType
    {
        Rewarded = 0,
        Interstitial = 1
    }

    public enum MaxAdResultStatus
    {
        Rewarded = 0,
        Completed = 1,
        Skipped = 2,
        Unavailable = 3,
        Busy = 4,
        Failed = 5,
        TimedOut = 6
    }

    public enum MaxAdLifecycleStep
    {
        Call = 0,
        Start = 1,
        Ready = 2,
        Show = 3,
        Complete = 4,
        Fail = 5,
        Closed = 6
    }

    public sealed class MaxAdLifecycleInfo
    {
        public MaxAdLifecycleStep Step { get; }
        public MaxAdType AdType { get; }
        public string Placement { get; }
        public string ErrorCode { get; }
        public string NetworkName { get; }

        internal MaxAdLifecycleInfo(
            MaxAdLifecycleStep step,
            MaxAdType adType,
            string placement,
            string errorCode = "",
            string networkName = "")
        {
            Step = step;
            AdType = adType;
            Placement = placement ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty;
            NetworkName = networkName ?? string.Empty;
        }
    }

    public sealed class MaxAdResult
    {
        public MaxAdType AdType { get; }
        public MaxAdResultStatus Status { get; }
        public string Placement { get; }
        public string Error { get; }

        internal MaxAdResult(
            MaxAdType adType,
            MaxAdResultStatus status,
            string placement,
            string error = "")
        {
            AdType = adType;
            Status = status;
            Placement = placement ?? string.Empty;
            Error = error ?? string.Empty;
        }
    }

    public sealed class MaxRewardInfo
    {
        public string Label { get; }
        public int Amount { get; }
        public string Placement { get; }

        internal MaxRewardInfo(string label, int amount, string placement)
        {
            Label = label ?? string.Empty;
            Amount = amount;
            Placement = placement ?? string.Empty;
        }
    }

    public sealed class MaxRevenueInfo
    {
        /// <summary>
        /// 广告类型：激励视频或插屏
        /// </summary>
        public MaxAdType AdType { get; }
        /// <summary>
        /// MAX 返回的本次广告展示收入值
        /// </summary>
        public double Revenue { get; }
        /// <summary>
        /// 收入数据的精度/可信类型
        /// </summary>
        public string RevenuePrecision { get; }
        /// <summary>
        /// MAX 广告位 ID
        /// </summary>
        public string AdUnitId { get; }
        /// <summary>
        /// MAX 返回的广告格式。
        /// </summary>
        public string AdFormat { get; }
        /// <summary>
        /// 游戏内展示场景
        /// </summary>
        public string Placement { get; }
        /// <summary>
        /// 实际填充广告的广告网络
        /// </summary>
        public string NetworkName { get; }
        /// <summary>
        /// DSP that served an AppLovin Exchange impression, when available.
        /// </summary>
        public string DspName { get; }
        /// <summary>
        /// 该广告网络内部的广告位
        /// </summary>
        public string NetworkPlacement { get; }
        /// <summary>
        /// 广告素材 ID
        /// </summary>
        public string CreativeId { get; }
        /// <summary>
        /// MAX SDK 初始化配置返回的 ISO 3166-1 alpha-2 国家码。
        /// </summary>
        public string CountryCode { get; }

        internal MaxRevenueInfo(
            MaxAdType adType,
            MaxSdkBase.AdInfo adInfo,
            string countryCode,
            string fallbackAdUnitId)
        {
            AdType = adType;
            Revenue = adInfo?.Revenue ?? -1d;
            RevenuePrecision = adInfo?.RevenuePrecision ?? string.Empty;
            AdUnitId = string.IsNullOrEmpty(adInfo?.AdUnitIdentifier)
                ? fallbackAdUnitId ?? string.Empty
                : adInfo.AdUnitIdentifier;
            AdFormat = string.IsNullOrEmpty(adInfo?.AdFormat)
                ? (adType == MaxAdType.Rewarded
                    ? "REWARDED"
                    : "INTERSTITIAL")
                : adInfo.AdFormat;
            Placement = adInfo?.Placement ?? string.Empty;
            NetworkName = adInfo?.NetworkName ?? string.Empty;
            DspName = adInfo?.DspName ?? string.Empty;
            NetworkPlacement = adInfo?.NetworkPlacement ?? string.Empty;
            CreativeId = adInfo?.CreativeIdentifier ?? string.Empty;
            CountryCode = countryCode ?? string.Empty;
        }
    }

    public sealed class MaxAdsAdapter : MonoBehaviour
    {
        private const float ShowTimeoutSeconds = 180f;
        private const float ResumeCompletionGraceSeconds = 1f;
        private const int MaximumRetryDelaySeconds = 64;

        private static MaxAdsAdapter instance;

        private MaxAdsPlatformConfig platformConfig;
        private bool initialized;
        private bool initializationInProgress;
        private bool initializationReported;
        private bool callbacksSubscribed;
        private bool fullScreenBusy;
        private bool rewardReceived;
        private bool adDisplayedReported;
        private bool applicationInterruptedCurrentShow;
        private int rewardedRetryAttempt;
        private int interstitialRetryAttempt;
        private int currentShowSequence;
        private string countryCode = string.Empty;
        private string rewardedReadyNetworkName = string.Empty;
        private string interstitialReadyNetworkName = string.Empty;
        private string currentNetworkName = string.Empty;
        private MaxAdType currentAdType;
        private string currentPlacement = string.Empty;
        private TaskCompletionSource<MaxAdResult> currentCompletion;
        private Coroutine showTimeoutCoroutine;
        private Coroutine resumeCompletionCoroutine;
        private Coroutine rewardedRetryCoroutine;
        private Coroutine interstitialRetryCoroutine;

        public static bool IsInitialized => instance != null && instance.initialized;

        public static bool IsInitializationInProgress =>
            instance != null && instance.initializationInProgress;

        public static bool IsRewardedReady =>
            instance != null && instance.GetRewardedReady();

        public static bool IsInterstitialReady =>
            instance != null && instance.GetInterstitialReady();

        public static event Action<bool, string> OnInitialized;
        public static event Action<MaxAdLifecycleInfo> OnLifecycleChanged;
        public static event Action<MaxRewardInfo> OnRewardGranted;
        public static event Action<MaxRevenueInfo> OnRevenuePaid;

        /// <summary>
        /// Creates the persistent adapter and initializes MAX using the
        /// encrypted Resources configuration. The application must call this
        /// method explicitly.
        /// </summary>
        public static bool InitializeNow()
        {
            return EnsureInstance().Initialize();
        }

        private static MaxAdsAdapter EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            var host = new GameObject("[MAX Ads Adapter]");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<MaxAdsAdapter>();
            return instance;
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
        }

        private void OnDestroy()
        {
            UnsubscribeCallbacks();

            if (showTimeoutCoroutine != null)
            {
                StopCoroutine(showTimeoutCoroutine);
            }

            if (resumeCompletionCoroutine != null)
            {
                StopCoroutine(resumeCompletionCoroutine);
            }

            if (rewardedRetryCoroutine != null)
            {
                StopCoroutine(rewardedRetryCoroutine);
            }

            if (interstitialRetryCoroutine != null)
            {
                StopCoroutine(interstitialRetryCoroutine);
            }

            currentCompletion?.TrySetResult(
                Result(currentAdType, MaxAdResultStatus.Failed, currentPlacement, "MAX adapter was destroyed."));
            currentCompletion = null;
            fullScreenBusy = false;

            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            HandleApplicationVisibilityChanged(paused);
        }

        private void OnApplicationFocus(bool focused)
        {
            HandleApplicationVisibilityChanged(!focused);
        }

        private void HandleApplicationVisibilityChanged(bool inactive)
        {
            if (!fullScreenBusy)
            {
                return;
            }

            if (inactive)
            {
                applicationInterruptedCurrentShow = true;
                CancelResumeCompletion();
                return;
            }

            if (!applicationInterruptedCurrentShow ||
                resumeCompletionCoroutine != null)
            {
                return;
            }

            var showSequence = currentShowSequence;
            resumeCompletionCoroutine = StartCoroutine(
                CompleteShowAfterApplicationResume(showSequence));
        }

        private IEnumerator CompleteShowAfterApplicationResume(int showSequence)
        {
            // Native hidden callbacks normally arrive as Unity regains focus.
            // Give MAX one second to deliver that callback before recovering a
            // request that would otherwise leave every calling UI permanently locked.
            yield return new WaitForSecondsRealtime(ResumeCompletionGraceSeconds);
            resumeCompletionCoroutine = null;

            if (!fullScreenBusy ||
                currentShowSequence != showSequence ||
                !applicationInterruptedCurrentShow)
            {
                yield break;
            }

            var status = currentAdType == MaxAdType.Rewarded
                ? (rewardReceived
                    ? MaxAdResultStatus.Rewarded
                    : MaxAdResultStatus.Skipped)
                : MaxAdResultStatus.Completed;

            Debug.LogWarning(
                "[MAX Ads] Native close callback was not received after the app resumed; " +
                "completing the fullscreen ad from the application lifecycle.");
            FinishCurrentShow(status, "Recovered after application resume.");
        }

        public static Task<MaxAdResult> ShowRewardedAsync(
            string placement = null,
            string customData = null)
        {
            return instance != null
                ? instance.ShowAsync(MaxAdType.Rewarded, placement, customData)
                : Task.FromResult(
                    Result(
                        MaxAdType.Rewarded,
                        MaxAdResultStatus.Unavailable,
                        placement,
                        "MAX adapter is not available."));
        }

        public static Task<MaxAdResult> ShowInterstitialAsync(
            string placement = null,
            string customData = null)
        {
            return instance != null
                ? instance.ShowAsync(MaxAdType.Interstitial, placement, customData)
                : Task.FromResult(
                    Result(
                        MaxAdType.Interstitial,
                        MaxAdResultStatus.Unavailable,
                        placement,
                        "MAX adapter is not available."));
        }

        public static void ShowRewarded(
            string placement,
            Action<MaxAdResult> callback,
            string customData = null)
        {
            CompleteWithCallback(ShowRewardedAsync(placement, customData), callback);
        }

        public static void ShowInterstitial(
            string placement,
            Action<MaxAdResult> callback,
            string customData = null)
        {
            CompleteWithCallback(ShowInterstitialAsync(placement, customData), callback);
        }

        public static void SetUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            try
            {
                MaxSdk.SetUserId(userId.Trim());
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MAX Ads] SetUserId failed: {exception.Message}");
            }
        }

        public static void ShowMediationDebugger()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[MAX Ads] Mediation debugger is unavailable before initialization.");
                return;
            }

            try
            {
                MaxSdk.ShowMediationDebugger();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MAX Ads] Mediation debugger failed: {exception.Message}");
            }
        }

        private bool Initialize()
        {
            if (initialized)
            {
                return true;
            }

            if (initializationInProgress)
            {
                return false;
            }

            initializationReported = false;
            if (!MaxAdsConfigCrypto.TryLoadFromResources(out var config, out var error))
            {
                Debug.LogError($"[MAX Ads] Initialization skipped: {error}");
                ReportInitialization(false, error);
                return false;
            }

            platformConfig = config.GetPlatformConfig(UseIosConfig());
            MaxSdkBase.InvokeEventsOnUnityMainThread = true;
            SubscribeCallbacks();
            initializationInProgress = true;

            try
            {
                // Use MAX production defaults: audio enabled, verbose logging off,
                // and no test-device identifiers.
                if (MaxSdk.IsInitialized())
                {
                    CaptureSdkConfiguration(MaxSdk.GetSdkConfiguration());
                    CompleteInitialization(true, string.Empty);
                    return true;
                }

                var adUnitIds = new List<string>(2);
                AddIfNotEmpty(adUnitIds, platformConfig.RewardedAdUnitId);
                AddIfNotEmpty(adUnitIds, platformConfig.InterstitialAdUnitId);
                MaxSdk.InitializeSdk(adUnitIds.Count > 0 ? adUnitIds.ToArray() : null);
                return true;
            }
            catch (Exception exception)
            {
                initializationInProgress = false;
                Debug.LogError($"[MAX Ads] Initialization failed: {exception.Message}");
                ReportInitialization(false, exception.Message);
                return false;
            }
        }

        private void SubscribeCallbacks()
        {
            if (callbacksSubscribed)
            {
                return;
            }

            callbacksSubscribed = true;
            MaxSdkCallbacks.OnSdkInitializedEvent += HandleSdkInitialized;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += HandleRewardedLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += HandleRewardedLoadFailed;
            MaxSdkCallbacks.Rewarded.OnExpiredAdReloadedEvent +=
                HandleRewardedExpiredAdReloaded;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += HandleRewardedDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += HandleRewardedDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += HandleRewardReceived;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += HandleRewardedHidden;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += HandleRewardedRevenue;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += HandleInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += HandleInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnExpiredAdReloadedEvent +=
                HandleInterstitialExpiredAdReloaded;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += HandleInterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += HandleInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += HandleInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += HandleInterstitialRevenue;
        }

        private void UnsubscribeCallbacks()
        {
            if (!callbacksSubscribed)
            {
                return;
            }

            callbacksSubscribed = false;
            MaxSdkCallbacks.OnSdkInitializedEvent -= HandleSdkInitialized;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= HandleRewardedLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= HandleRewardedLoadFailed;
            MaxSdkCallbacks.Rewarded.OnExpiredAdReloadedEvent -=
                HandleRewardedExpiredAdReloaded;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= HandleRewardedDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= HandleRewardedDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= HandleRewardReceived;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= HandleRewardedHidden;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= HandleRewardedRevenue;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= HandleInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= HandleInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnExpiredAdReloadedEvent -=
                HandleInterstitialExpiredAdReloaded;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= HandleInterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= HandleInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= HandleInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= HandleInterstitialRevenue;
        }

        private void HandleSdkInitialized(MaxSdkBase.SdkConfiguration configuration)
        {
            CaptureSdkConfiguration(configuration);
            var success = configuration != null && configuration.IsSuccessfullyInitialized;
            CompleteInitialization(
                success,
                success ? string.Empty : "MAX SDK reported initialization failure.");
        }

        private void CompleteInitialization(bool success, string error)
        {
            initializationInProgress = false;
            initialized = success;
            ReportInitialization(success, error);
            if (!success)
            {
                return;
            }

            LoadRewarded();
            LoadInterstitial();
        }

        private void ReportInitialization(bool success, string error)
        {
            if (initializationReported)
            {
                return;
            }

            initializationReported = true;
            var handlers = OnInitialized;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<bool, string> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(success, error ?? string.Empty);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private Task<MaxAdResult> ShowAsync(
            MaxAdType adType,
            string placement,
            string customData)
        {
            placement = placement ?? string.Empty;
            InvokeLifecycle(
                new MaxAdLifecycleInfo(
                    MaxAdLifecycleStep.Call,
                    adType,
                    placement));
            InvokeLifecycle(
                new MaxAdLifecycleInfo(
                    MaxAdLifecycleStep.Start,
                    adType,
                    placement));

            if (!initialized)
            {
                ReportFailure(
                    adType,
                    placement,
                    "not_initialized");
                return Task.FromResult(
                    Result(adType, MaxAdResultStatus.Unavailable, placement, "MAX is not initialized."));
            }

            if (fullScreenBusy)
            {
                ReportFailure(adType, placement, "busy");
                return Task.FromResult(
                    Result(adType, MaxAdResultStatus.Busy, placement, "Another fullscreen ad is active."));
            }

            var ready = adType == MaxAdType.Rewarded
                ? GetRewardedReady()
                : GetInterstitialReady();
            if (!ready)
            {
                ReportFailure(adType, placement, "not_ready");
                return Task.FromResult(
                    Result(adType, MaxAdResultStatus.Unavailable, placement, "Ad is not ready."));
            }

            var readyNetworkName = adType == MaxAdType.Rewarded
                ? rewardedReadyNetworkName
                : interstitialReadyNetworkName;
            InvokeLifecycle(
                new MaxAdLifecycleInfo(
                    MaxAdLifecycleStep.Ready,
                    adType,
                    placement,
                    networkName: readyNetworkName));

            fullScreenBusy = true;
            rewardReceived = false;
            adDisplayedReported = false;
            applicationInterruptedCurrentShow = false;
            currentShowSequence++;
            CancelResumeCompletion();
            currentAdType = adType;
            currentPlacement = placement;
            currentNetworkName = readyNetworkName;
            currentCompletion = new TaskCompletionSource<MaxAdResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = currentCompletion;
            showTimeoutCoroutine = StartCoroutine(
                WaitForShowTimeout(
                    adType,
                    placement,
                    currentShowSequence));

            try
            {
                if (adType == MaxAdType.Rewarded)
                {
                    MaxSdk.ShowRewardedAd(
                        platformConfig.RewardedAdUnitId,
                        placement,
                        customData);
                }
                else
                {
                    MaxSdk.ShowInterstitial(
                        platformConfig.InterstitialAdUnitId,
                        placement,
                        customData);
                }
            }
            catch (Exception exception)
            {
                ReportFailure(
                    adType,
                    placement,
                    "show_exception",
                    currentNetworkName);
                FinishCurrentShow(MaxAdResultStatus.Failed, exception.Message);
            }

            return completion.Task;
        }

        private IEnumerator WaitForShowTimeout(
            MaxAdType adType,
            string placement,
            int showSequence)
        {
            yield return new WaitForSecondsRealtime(ShowTimeoutSeconds);
            showTimeoutCoroutine = null;

            if (!fullScreenBusy ||
                currentAdType != adType ||
                currentShowSequence != showSequence)
            {
                yield break;
            }

            Debug.LogWarning("[MAX Ads] Fullscreen ad timed out; releasing the fullscreen lock.");
            ReportFailure(
                adType,
                placement,
                "timeout",
                currentNetworkName);
            FinishCurrentShow(
                MaxAdResultStatus.TimedOut,
                "MAX did not finish the fullscreen ad within 180 seconds.");
        }

        private void HandleRewardedLoaded(
            string adUnitId,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId))
            {
                return;
            }

            rewardedRetryAttempt = 0;
            rewardedReadyNetworkName = GetNetworkName(adInfo);
        }

        private void HandleRewardedLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo error)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId))
            {
                return;
            }

            ScheduleRewardedRetry();
            rewardedReadyNetworkName = string.Empty;
            Debug.LogWarning($"[MAX Ads] Rewarded load failed: {FormatError(error)}");
        }

        private void HandleRewardedExpiredAdReloaded(
            string adUnitId,
            MaxSdkBase.AdInfo _,
            MaxSdkBase.AdInfo newAdInfo)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId))
            {
                return;
            }

            rewardedRetryAttempt = 0;
            rewardedReadyNetworkName = GetNetworkName(newAdInfo);
        }

        private void HandleRewardedDisplayed(
            string adUnitId,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId) ||
                !IsCurrentShow(MaxAdType.Rewarded, adInfo))
            {
                return;
            }

            ReportDisplayedIfNeeded(MaxAdType.Rewarded, adInfo);
        }

        private void HandleRewardedDisplayFailed(
            string adUnitId,
            MaxSdkBase.ErrorInfo error,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId))
            {
                return;
            }

            if (fullScreenBusy && currentAdType == MaxAdType.Rewarded)
            {
                if (!IsCurrentShow(MaxAdType.Rewarded, adInfo))
                {
                    return;
                }

                CaptureCurrentNetwork(adInfo);
                ReportFailure(
                    MaxAdType.Rewarded,
                    ResolveCurrentPlacement(),
                    GetErrorCode(error),
                    currentNetworkName);
                FinishCurrentShow(MaxAdResultStatus.Failed, FormatError(error));
            }
            else
            {
                LoadRewarded();
            }
        }

        private void HandleRewardReceived(
            string adUnitId,
            MaxSdkBase.Reward reward,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId) ||
                !IsCurrentShow(MaxAdType.Rewarded, adInfo) ||
                rewardReceived)
            {
                return;
            }

            rewardReceived = true;
            ReportDisplayedIfNeeded(MaxAdType.Rewarded, adInfo);
            var placement = ResolveCurrentPlacement();
            InvokeLifecycle(
                new MaxAdLifecycleInfo(
                    MaxAdLifecycleStep.Complete,
                    MaxAdType.Rewarded,
                    placement,
                    networkName: currentNetworkName));
            try
            {
                OnRewardGranted?.Invoke(
                    new MaxRewardInfo(reward.Label, reward.Amount, placement));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void HandleRewardedHidden(
            string adUnitId,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.RewardedAdUnitId))
            {
                return;
            }

            if (fullScreenBusy && currentAdType == MaxAdType.Rewarded)
            {
                if (!IsCurrentShow(MaxAdType.Rewarded, adInfo))
                {
                    return;
                }

                ReportDisplayedIfNeeded(MaxAdType.Rewarded, adInfo);
                InvokeLifecycle(
                    new MaxAdLifecycleInfo(
                        MaxAdLifecycleStep.Closed,
                        MaxAdType.Rewarded,
                        ResolveCurrentPlacement(),
                        networkName: currentNetworkName));
                FinishCurrentShow(
                    rewardReceived
                        ? MaxAdResultStatus.Rewarded
                        : MaxAdResultStatus.Skipped,
                    string.Empty);
            }
            else
            {
                LoadRewarded();
            }
        }

        private void HandleRewardedRevenue(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (Matches(adUnitId, platformConfig?.RewardedAdUnitId))
            {
                if (IsCurrentShow(MaxAdType.Rewarded, adInfo))
                {
                    ReportDisplayedIfNeeded(MaxAdType.Rewarded, adInfo);
                }

                InvokeRevenue(
                    new MaxRevenueInfo(
                        MaxAdType.Rewarded,
                        adInfo,
                        countryCode,
                        adUnitId));
            }
        }

        private void HandleInterstitialLoaded(
            string adUnitId,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.InterstitialAdUnitId))
            {
                return;
            }

            interstitialRetryAttempt = 0;
            interstitialReadyNetworkName = GetNetworkName(adInfo);
        }

        private void HandleInterstitialLoadFailed(
            string adUnitId,
            MaxSdkBase.ErrorInfo error)
        {
            if (!Matches(adUnitId, platformConfig?.InterstitialAdUnitId))
            {
                return;
            }

            ScheduleInterstitialRetry();
            interstitialReadyNetworkName = string.Empty;
            Debug.LogWarning($"[MAX Ads] Interstitial load failed: {FormatError(error)}");
        }

        private void HandleInterstitialExpiredAdReloaded(
            string adUnitId,
            MaxSdkBase.AdInfo _,
            MaxSdkBase.AdInfo newAdInfo)
        {
            if (!Matches(adUnitId, platformConfig?.InterstitialAdUnitId))
            {
                return;
            }

            interstitialRetryAttempt = 0;
            interstitialReadyNetworkName = GetNetworkName(newAdInfo);
        }

        private void HandleInterstitialDisplayed(
            string adUnitId,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.InterstitialAdUnitId) ||
                !IsCurrentShow(MaxAdType.Interstitial, adInfo))
            {
                return;
            }

            ReportDisplayedIfNeeded(MaxAdType.Interstitial, adInfo);
        }

        private void HandleInterstitialDisplayFailed(
            string adUnitId,
            MaxSdkBase.ErrorInfo error,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.InterstitialAdUnitId))
            {
                return;
            }

            if (fullScreenBusy && currentAdType == MaxAdType.Interstitial)
            {
                if (!IsCurrentShow(MaxAdType.Interstitial, adInfo))
                {
                    return;
                }

                CaptureCurrentNetwork(adInfo);
                ReportFailure(
                    MaxAdType.Interstitial,
                    ResolveCurrentPlacement(),
                    GetErrorCode(error),
                    currentNetworkName);
                FinishCurrentShow(MaxAdResultStatus.Failed, FormatError(error));
            }
            else
            {
                LoadInterstitial();
            }
        }

        private void HandleInterstitialHidden(
            string adUnitId,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!Matches(adUnitId, platformConfig?.InterstitialAdUnitId))
            {
                return;
            }

            if (fullScreenBusy && currentAdType == MaxAdType.Interstitial)
            {
                if (!IsCurrentShow(MaxAdType.Interstitial, adInfo))
                {
                    return;
                }

                ReportDisplayedIfNeeded(MaxAdType.Interstitial, adInfo);
                InvokeLifecycle(
                    new MaxAdLifecycleInfo(
                        MaxAdLifecycleStep.Closed,
                        MaxAdType.Interstitial,
                        ResolveCurrentPlacement(),
                        networkName: currentNetworkName));
                FinishCurrentShow(MaxAdResultStatus.Completed, string.Empty);
            }
            else
            {
                LoadInterstitial();
            }
        }

        private void HandleInterstitialRevenue(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (Matches(adUnitId, platformConfig?.InterstitialAdUnitId))
            {
                if (IsCurrentShow(MaxAdType.Interstitial, adInfo))
                {
                    ReportDisplayedIfNeeded(MaxAdType.Interstitial, adInfo);
                }

                InvokeRevenue(
                    new MaxRevenueInfo(
                        MaxAdType.Interstitial,
                        adInfo,
                        countryCode,
                        adUnitId));
            }
        }

        private void CaptureSdkConfiguration(
            MaxSdkBase.SdkConfiguration configuration)
        {
            if (configuration != null)
            {
                countryCode = configuration.CountryCode ?? string.Empty;
            }
        }

        private void FinishCurrentShow(MaxAdResultStatus status, string error)
        {
            if (showTimeoutCoroutine != null)
            {
                StopCoroutine(showTimeoutCoroutine);
                showTimeoutCoroutine = null;
            }

            CancelResumeCompletion();

            var adType = currentAdType;
            var placement = currentPlacement;
            var completion = currentCompletion;

            currentCompletion = null;
            currentPlacement = string.Empty;
            currentNetworkName = string.Empty;
            rewardReceived = false;
            adDisplayedReported = false;
            applicationInterruptedCurrentShow = false;
            fullScreenBusy = false;

            completion?.TrySetResult(Result(adType, status, placement, error));

            if (adType == MaxAdType.Rewarded)
            {
                LoadRewarded();
            }
            else
            {
                LoadInterstitial();
            }
        }

        private void CancelResumeCompletion()
        {
            if (resumeCompletionCoroutine == null)
            {
                return;
            }

            StopCoroutine(resumeCompletionCoroutine);
            resumeCompletionCoroutine = null;
        }

        private bool GetRewardedReady()
        {
            if (!initialized || string.IsNullOrEmpty(platformConfig?.RewardedAdUnitId))
            {
                return false;
            }

            try
            {
                return MaxSdk.IsRewardedAdReady(platformConfig.RewardedAdUnitId);
            }
            catch
            {
                return false;
            }
        }

        private bool GetInterstitialReady()
        {
            if (!initialized || string.IsNullOrEmpty(platformConfig?.InterstitialAdUnitId))
            {
                return false;
            }

            try
            {
                return MaxSdk.IsInterstitialReady(platformConfig.InterstitialAdUnitId);
            }
            catch
            {
                return false;
            }
        }

        private void LoadRewarded()
        {
            if (!initialized || string.IsNullOrEmpty(platformConfig?.RewardedAdUnitId))
            {
                return;
            }

            try
            {
                rewardedReadyNetworkName = string.Empty;
                MaxSdk.LoadRewardedAd(platformConfig.RewardedAdUnitId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MAX Ads] Rewarded load request failed: {exception.Message}");
                ScheduleRewardedRetry();
            }
        }

        private void LoadInterstitial()
        {
            if (!initialized || string.IsNullOrEmpty(platformConfig?.InterstitialAdUnitId))
            {
                return;
            }

            try
            {
                interstitialReadyNetworkName = string.Empty;
                MaxSdk.LoadInterstitial(platformConfig.InterstitialAdUnitId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[MAX Ads] Interstitial load request failed: {exception.Message}");
                ScheduleInterstitialRetry();
            }
        }

        private void ScheduleRewardedRetry()
        {
            rewardedRetryAttempt++;
            if (rewardedRetryCoroutine != null)
            {
                StopCoroutine(rewardedRetryCoroutine);
            }

            rewardedRetryCoroutine = StartCoroutine(
                RetryLoad(MaxAdType.Rewarded, RetryDelay(rewardedRetryAttempt)));
        }

        private void ScheduleInterstitialRetry()
        {
            interstitialRetryAttempt++;
            if (interstitialRetryCoroutine != null)
            {
                StopCoroutine(interstitialRetryCoroutine);
            }

            interstitialRetryCoroutine = StartCoroutine(
                RetryLoad(MaxAdType.Interstitial, RetryDelay(interstitialRetryAttempt)));
        }

        private IEnumerator RetryLoad(MaxAdType adType, int delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);

            if (adType == MaxAdType.Rewarded)
            {
                rewardedRetryCoroutine = null;
                LoadRewarded();
            }
            else
            {
                interstitialRetryCoroutine = null;
                LoadInterstitial();
            }
        }

        private static int RetryDelay(int attempt)
        {
            var exponent = Mathf.Clamp(attempt, 1, 6);
            return Mathf.Min(1 << exponent, MaximumRetryDelaySeconds);
        }

        private static bool UseIosConfig()
        {
#if UNITY_IOS || UNITY_IPHONE
            return true;
#else
            return false;
#endif
        }

        private static bool Matches(string actual, string expected)
        {
            return !string.IsNullOrEmpty(expected) &&
                   string.Equals(actual, expected, StringComparison.Ordinal);
        }

        private static string FormatError(MaxSdkBase.ErrorInfo error)
        {
            return error == null
                ? "Unknown MAX error."
                : $"{error.Code}: {error.Message}";
        }

        private void ReportDisplayedIfNeeded(
            MaxAdType adType,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!IsCurrentShow(adType, adInfo))
            {
                return;
            }

            CaptureCurrentNetwork(adInfo);
            if (adDisplayedReported)
            {
                return;
            }

            adDisplayedReported = true;
            InvokeLifecycle(
                new MaxAdLifecycleInfo(
                    MaxAdLifecycleStep.Show,
                    adType,
                    ResolveCurrentPlacement(),
                    networkName: currentNetworkName));
        }

        private bool IsCurrentShow(
            MaxAdType adType,
            MaxSdkBase.AdInfo adInfo)
        {
            if (!fullScreenBusy || currentAdType != adType)
            {
                return false;
            }

            var callbackPlacement = adInfo?.Placement;
            return string.IsNullOrEmpty(currentPlacement) ||
                   string.IsNullOrEmpty(callbackPlacement) ||
                   string.Equals(
                       currentPlacement,
                       callbackPlacement,
                       StringComparison.Ordinal);
        }

        private void CaptureCurrentNetwork(MaxSdkBase.AdInfo adInfo)
        {
            var networkName = GetNetworkName(adInfo);
            if (!string.IsNullOrEmpty(networkName))
            {
                currentNetworkName = networkName;
            }
        }

        private string ResolveCurrentPlacement()
        {
            // `from` must be the business placement passed into ShowAsync.
            return currentPlacement;
        }

        private static string GetNetworkName(MaxSdkBase.AdInfo adInfo)
        {
            return (adInfo?.NetworkName ?? string.Empty).Trim();
        }

        private static string GetErrorCode(MaxSdkBase.ErrorInfo error)
        {
            return error == null
                ? "unknown"
                : ((int) error.Code).ToString(CultureInfo.InvariantCulture);
        }

        private static void ReportFailure(
            MaxAdType adType,
            string placement,
            string errorCode,
            string networkName = "")
        {
            InvokeLifecycle(
                new MaxAdLifecycleInfo(
                    MaxAdLifecycleStep.Fail,
                    adType,
                    placement,
                    string.IsNullOrEmpty(errorCode)
                        ? "unknown"
                        : errorCode,
                    networkName));
        }

        private static void InvokeLifecycle(MaxAdLifecycleInfo lifecycle)
        {
            var handlers = OnLifecycleChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<MaxAdLifecycleInfo> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(lifecycle);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void InvokeRevenue(MaxRevenueInfo revenue)
        {
            var handlers = OnRevenuePaid;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<MaxRevenueInfo> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(revenue);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static MaxAdResult Result(
            MaxAdType adType,
            MaxAdResultStatus status,
            string placement,
            string error = "")
        {
            return new MaxAdResult(adType, status, placement, error);
        }

        private static void AddIfNotEmpty(ICollection<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static async void CompleteWithCallback(
            Task<MaxAdResult> task,
            Action<MaxAdResult> callback)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(await task);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
