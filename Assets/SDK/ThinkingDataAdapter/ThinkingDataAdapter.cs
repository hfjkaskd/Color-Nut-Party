using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThinkingDataIntegration
{
    public static class ThinkingDataAdapter
    {
        private const int MaximumPendingOperations = 512;

        private static readonly object Sync = new object();
        private static readonly Queue<PendingOperation> PendingOperations =
            new Queue<PendingOperation>(MaximumPendingOperations);

        private static AdapterState state = AdapterState.Buffering;
        private static IThinkingDataSdkBridge sdkBridge =
            new ThinkingDataSdkBridge();
        private static ThinkingDataConfig cachedConfig;
        private static bool initialized;
        private static bool autoTrackEnabled;
        private static bool queueOverflowLogged;

        public static bool IsInitialized
        {
            get
            {
                lock (Sync)
                {
                    return initialized;
                }
            }
        }

        public static bool IsAutoTrackEnabled
        {
            get
            {
                lock (Sync)
                {
                    return autoTrackEnabled;
                }
            }
        }

        /// <summary>
        /// Reads the project App Code from the encrypted ThinkingData
        /// configuration. This is available before SDK initialization so the
        /// caller can queue its identity before automatic tracking starts.
        /// The first lookup should be made from Unity's main thread.
        /// </summary>
        public static bool TryGetAppCode(out string appCode)
        {
            try
            {
                if (!TryGetConfiguration(out var config, out _))
                {
                    appCode = string.Empty;
                    return false;
                }

                appCode = config.AppCode;
                return true;
            }
            catch (Exception exception)
            {
                appCode = string.Empty;
                return LogAdapterFailure(
                    nameof(TryGetAppCode),
                    exception);
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lock (Sync)
            {
                PendingOperations.Clear();
                state = AdapterState.Buffering;
                sdkBridge = new ThinkingDataSdkBridge();
                cachedConfig = null;
                initialized = false;
                autoTrackEnabled = false;
                queueOverflowLogged = false;
            }
        }

        /// <summary>
        /// Initializes ThinkingData using the encrypted Resources
        /// configuration. The application must call this method explicitly.
        /// </summary>
        public static bool InitializeNow()
        {
            if (!TryGetConfiguration(
                    out var config,
                    out var error))
            {
                Debug.LogError($"[ThinkingData] Initialization skipped: {error}");
                return false;
            }

            return Initialize(config);
        }

        public static bool Login(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                Debug.LogError("[ThinkingData] Login rejected: account ID is empty.");
                return false;
            }

            try
            {
                return SubmitOrQueue(PendingOperation.Login(accountId));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(nameof(Login), exception);
            }
        }

        public static bool EnableAppsFlyerSharing()
        {
            try
            {
                return SubmitOrQueue(
                    PendingOperation.EnableAppsFlyerSharing());
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    nameof(EnableAppsFlyerSharing),
                    exception);
            }
        }

        public static bool EnableAdjustSharing()
        {
            try
            {
                return SubmitOrQueue(
                    PendingOperation.EnableAdjustSharing());
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    nameof(EnableAdjustSharing),
                    exception);
            }
        }

        public static bool SetDistinctId(string distinctId)
        {
            if (string.IsNullOrWhiteSpace(distinctId))
            {
                Debug.LogError(
                    "[ThinkingData] SetDistinctId rejected: distinct ID is empty.");
                return false;
            }

            try
            {
                return SubmitOrQueue(
                    PendingOperation.SetDistinctId(distinctId.Trim()));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    nameof(SetDistinctId),
                    exception);
            }
        }

        public static string GetDistinctId()
        {
            lock (Sync)
            {
                if (state != AdapterState.Ready)
                {
                    return string.Empty;
                }
            }

            try
            {
                return sdkBridge.GetDistinctId() ?? string.Empty;
            }
            catch (Exception exception)
            {
                LogAdapterFailure(nameof(GetDistinctId), exception);
                return string.Empty;
            }
        }

        public static string GetDeviceId()
        {
            lock (Sync)
            {
                if (state != AdapterState.Ready)
                {
                    return string.Empty;
                }
            }

            try
            {
                return sdkBridge.GetDeviceId() ?? string.Empty;
            }
            catch (Exception exception)
            {
                LogAdapterFailure(nameof(GetDeviceId), exception);
                return string.Empty;
            }
        }

        public static bool SetSuperProperties(
            Dictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                Debug.LogError(
                    "[ThinkingData] SetSuperProperties rejected: properties are empty.");
                return false;
            }

            try
            {
                return SubmitOrQueue(
                    PendingOperation.SetSuperProperties(
                        Snapshot(properties)));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    nameof(SetSuperProperties),
                    exception);
            }
        }

        public static bool ClearSuperProperties()
        {
            try
            {
                return SubmitOrQueue(
                    PendingOperation.ClearSuperProperties());
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    nameof(ClearSuperProperties),
                    exception);
            }
        }

        public static bool SetDynamicSuperProperties(
            Func<Dictionary<string, object>> propertiesProvider)
        {
            if (propertiesProvider == null)
            {
                Debug.LogError(
                    "[ThinkingData] SetDynamicSuperProperties rejected: provider is null.");
                return false;
            }

            try
            {
                return SubmitOrQueue(
                    PendingOperation.SetDynamicSuperProperties(
                        propertiesProvider));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(
                    nameof(SetDynamicSuperProperties),
                    exception);
            }
        }

        public static bool Logout()
        {
            try
            {
                return SubmitOrQueue(PendingOperation.Logout());
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(nameof(Logout), exception);
            }
        }

        public static bool Track(
            string eventName,
            Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogError("[ThinkingData] Track rejected: event name is empty.");
                return false;
            }

            try
            {
                return SubmitOrQueue(
                    PendingOperation.Track(
                        eventName,
                        Snapshot(properties),
                        DateTime.Now,
                        TimeZoneInfo.Local));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(nameof(Track), exception);
            }
        }

        public static bool UserSet(Dictionary<string, object> properties)
        {
            if (properties == null)
            {
                Debug.LogError("[ThinkingData] UserSet rejected: properties are null.");
                return false;
            }

            try
            {
                return SubmitOrQueue(
                    PendingOperation.UserSet(
                        Snapshot(properties),
                        DateTime.Now));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(nameof(UserSet), exception);
            }
        }

        public static bool UserSetOnce(Dictionary<string, object> properties)
        {
            if (properties == null)
            {
                Debug.LogError("[ThinkingData] UserSetOnce rejected: properties are null.");
                return false;
            }

            try
            {
                return SubmitOrQueue(
                    PendingOperation.UserSetOnce(
                        Snapshot(properties),
                        DateTime.Now));
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(nameof(UserSetOnce), exception);
            }
        }

        public static bool Flush()
        {
            try
            {
                return SubmitOrQueue(PendingOperation.Flush());
            }
            catch (Exception exception)
            {
                return LogAdapterFailure(nameof(Flush), exception);
            }
        }

        private static bool SubmitOrQueue(PendingOperation operation)
        {
            var executeImmediately = false;
            var logOverflow = false;

            lock (Sync)
            {
                if (state == AdapterState.Ready)
                {
                    executeImmediately = true;
                }
                else if (PendingOperations.Count >= MaximumPendingOperations)
                {
                    if (!queueOverflowLogged)
                    {
                        queueOverflowLogged = true;
                        logOverflow = true;
                    }
                }
                else
                {
                    PendingOperations.Enqueue(operation);
                    return true;
                }
            }

            if (logOverflow)
            {
                Debug.LogError(
                    $"[ThinkingData] Pending operation queue reached its {MaximumPendingOperations}-item limit. New operations will be rejected.");
            }

            return executeImmediately && TryExecute(operation, false);
        }

        private static bool TryGetConfiguration(
            out ThinkingDataConfig config,
            out string error)
        {
            lock (Sync)
            {
                config = cachedConfig;
            }

            if (config != null)
            {
                error = string.Empty;
                return true;
            }

            if (!ThinkingDataConfigCrypto.TryLoadFromResources(
                    out var loadedConfig,
                    out error))
            {
                return false;
            }

            // The crypto layer can read the previous schema so the Editor can
            // recover App ID and Server URL. Runtime use requires the current
            // schema, including App Code.
            if (!loadedConfig.TryValidate(out error))
            {
                return false;
            }

            lock (Sync)
            {
                if (cachedConfig == null)
                {
                    cachedConfig = loadedConfig;
                }

                config = cachedConfig;
            }

            return true;
        }

        private static bool Initialize(ThinkingDataConfig config)
        {
            if (config == null)
            {
                Debug.LogError(
                    "[ThinkingData] Initialization skipped: encrypted config is invalid.");
                return false;
            }

            if (!config.TryValidate(out var validationError))
            {
                Debug.LogError(
                    $"[ThinkingData] Initialization skipped: {validationError}");
                return false;
            }

            IThinkingDataSdkBridge bridge;
            lock (Sync)
            {
                if (state != AdapterState.Buffering)
                {
                    return initialized;
                }

                cachedConfig = config;
                state = AdapterState.Initializing;
                bridge = sdkBridge;
            }

            try
            {
                bridge.Initialize(config.AppId, config.ServerUrl);
            }
            catch (Exception exception)
            {
                lock (Sync)
                {
                    // Keep queued identity/events intact and allow a later
                    // facade call to retry a transient SDK initialization
                    // failure.
                    state = AdapterState.Buffering;
                    initialized = false;
                }

                Debug.LogError(
                    $"[ThinkingData] SDK initialization failed ({exception.GetType().Name}).");
                return false;
            }

            lock (Sync)
            {
                initialized = true;
                state = AdapterState.DrainingBeforeAutoTrack;
            }

            DrainPendingOperations(AdapterState.EnablingAutoTrack);

            var enabledAutoTrack = false;
            try
            {
                bridge.EnableAllAutoTrack();
                enabledAutoTrack = true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ThinkingData] Automatic tracking could not be enabled ({exception.GetType().Name}).");
            }

            lock (Sync)
            {
                autoTrackEnabled = enabledAutoTrack;
                state = AdapterState.DrainingAfterAutoTrack;
            }

            DrainPendingOperations(AdapterState.Ready);
            return true;
        }

        private static void DrainPendingOperations(AdapterState stateWhenEmpty)
        {
            while (true)
            {
                PendingOperation operation;
                lock (Sync)
                {
                    if (PendingOperations.Count == 0)
                    {
                        state = stateWhenEmpty;
                        return;
                    }

                    operation = PendingOperations.Dequeue();
                }

                TryExecute(operation, true);
            }
        }

        private static bool TryExecute(
            PendingOperation operation,
            bool preserveCapturedTime)
        {
            IThinkingDataSdkBridge bridge;
            lock (Sync)
            {
                bridge = sdkBridge;
            }

            try
            {
                switch (operation.Kind)
                {
                    case PendingOperationKind.EnableAdjustSharing:
                        bridge.EnableAdjustSharing();
                        break;

                    case PendingOperationKind.EnableAppsFlyerSharing:
                        bridge.EnableAppsFlyerSharing();
                        break;

                    case PendingOperationKind.SetDistinctId:
                        bridge.SetDistinctId(operation.Text);
                        break;

                    case PendingOperationKind.SetSuperProperties:
                        bridge.SetSuperProperties(operation.Properties);
                        break;

                    case PendingOperationKind.ClearSuperProperties:
                        bridge.ClearSuperProperties();
                        break;

                    case PendingOperationKind.SetDynamicSuperProperties:
                        bridge.SetDynamicSuperProperties(
                            operation.PropertiesProvider);
                        break;

                    case PendingOperationKind.Login:
                        bridge.Login(operation.Text);
                        break;

                    case PendingOperationKind.Logout:
                        bridge.Logout();
                        break;

                    case PendingOperationKind.Track:
                        if (preserveCapturedTime)
                        {
                            bridge.Track(
                                operation.Text,
                                operation.Properties,
                                operation.CapturedAt,
                                operation.TimeZone);
                        }
                        else
                        {
                            bridge.Track(operation.Text, operation.Properties);
                        }

                        break;

                    case PendingOperationKind.UserSet:
                        if (preserveCapturedTime)
                        {
                            bridge.UserSet(
                                operation.Properties,
                                operation.CapturedAt);
                        }
                        else
                        {
                            bridge.UserSet(operation.Properties);
                        }

                        break;

                    case PendingOperationKind.UserSetOnce:
                        if (preserveCapturedTime)
                        {
                            bridge.UserSetOnce(
                                operation.Properties,
                                operation.CapturedAt);
                        }
                        else
                        {
                            bridge.UserSetOnce(operation.Properties);
                        }

                        break;

                    case PendingOperationKind.Flush:
                        bridge.Flush();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ThinkingData] {operation.Kind} failed ({exception.GetType().Name}).");
                return false;
            }
        }

        private static Dictionary<string, object> Snapshot(
            Dictionary<string, object> properties)
        {
            return properties == null
                ? null
                : new Dictionary<string, object>(properties);
        }

        private static bool LogAdapterFailure(
            string operation,
            Exception exception)
        {
            Debug.LogError(
                $"[ThinkingData] {operation} was rejected by the adapter ({exception.GetType().Name}).");
            return false;
        }

        internal static void ResetForTests()
        {
            ResetStatics();
        }

        internal static void SetSdkBridgeForTests(
            IThinkingDataSdkBridge bridge)
        {
            if (bridge == null)
            {
                throw new ArgumentNullException(nameof(bridge));
            }

            lock (Sync)
            {
                sdkBridge = bridge;
            }
        }

        internal static bool InitializeForTests(ThinkingDataConfig config)
        {
            return Initialize(config);
        }

        internal static int PendingCountForTests
        {
            get
            {
                lock (Sync)
                {
                    return PendingOperations.Count;
                }
            }
        }

        private enum AdapterState
        {
            Buffering,
            Initializing,
            DrainingBeforeAutoTrack,
            EnablingAutoTrack,
            DrainingAfterAutoTrack,
            Ready
        }

        private enum PendingOperationKind
        {
            EnableAdjustSharing,
            EnableAppsFlyerSharing,
            SetDistinctId,
            SetSuperProperties,
            ClearSuperProperties,
            SetDynamicSuperProperties,
            Login,
            Logout,
            Track,
            UserSet,
            UserSetOnce,
            Flush
        }

        private sealed class PendingOperation
        {
            private PendingOperation(
                PendingOperationKind kind,
                string text,
                Dictionary<string, object> properties,
                DateTime capturedAt,
                TimeZoneInfo timeZone,
                Func<Dictionary<string, object>> propertiesProvider = null)
            {
                Kind = kind;
                Text = text ?? string.Empty;
                Properties = properties;
                CapturedAt = capturedAt;
                TimeZone = timeZone;
                PropertiesProvider = propertiesProvider;
            }

            internal PendingOperationKind Kind { get; }

            internal string Text { get; }

            internal Dictionary<string, object> Properties { get; }

            internal DateTime CapturedAt { get; }

            internal TimeZoneInfo TimeZone { get; }

            internal Func<Dictionary<string, object>> PropertiesProvider
            {
                get;
            }

            internal static PendingOperation EnableAppsFlyerSharing()
            {
                return new PendingOperation(
                    PendingOperationKind.EnableAppsFlyerSharing,
                    string.Empty,
                    null,
                    default,
                    null);
            }

            internal static PendingOperation EnableAdjustSharing()
            {
                return new PendingOperation(
                    PendingOperationKind.EnableAdjustSharing,
                    string.Empty,
                    null,
                    default,
                    null);
            }

            internal static PendingOperation SetDistinctId(string distinctId)
            {
                return new PendingOperation(
                    PendingOperationKind.SetDistinctId,
                    distinctId,
                    null,
                    default,
                    null);
            }

            internal static PendingOperation SetSuperProperties(
                Dictionary<string, object> properties)
            {
                return new PendingOperation(
                    PendingOperationKind.SetSuperProperties,
                    string.Empty,
                    properties,
                    default,
                    null);
            }

            internal static PendingOperation ClearSuperProperties()
            {
                return new PendingOperation(
                    PendingOperationKind.ClearSuperProperties,
                    null,
                    null,
                    default,
                    null);
            }

            internal static PendingOperation SetDynamicSuperProperties(
                Func<Dictionary<string, object>> propertiesProvider)
            {
                return new PendingOperation(
                    PendingOperationKind.SetDynamicSuperProperties,
                    string.Empty,
                    null,
                    default,
                    null,
                    propertiesProvider);
            }

            internal static PendingOperation Login(string accountId)
            {
                return new PendingOperation(
                    PendingOperationKind.Login,
                    accountId,
                    null,
                    default,
                    null);
            }

            internal static PendingOperation Logout()
            {
                return new PendingOperation(
                    PendingOperationKind.Logout,
                    string.Empty,
                    null,
                    default,
                    null);
            }

            internal static PendingOperation Track(
                string eventName,
                Dictionary<string, object> properties,
                DateTime capturedAt,
                TimeZoneInfo timeZone)
            {
                return new PendingOperation(
                    PendingOperationKind.Track,
                    eventName,
                    properties,
                    capturedAt,
                    timeZone);
            }

            internal static PendingOperation UserSet(
                Dictionary<string, object> properties,
                DateTime capturedAt)
            {
                return new PendingOperation(
                    PendingOperationKind.UserSet,
                    string.Empty,
                    properties,
                    capturedAt,
                    null);
            }

            internal static PendingOperation UserSetOnce(
                Dictionary<string, object> properties,
                DateTime capturedAt)
            {
                return new PendingOperation(
                    PendingOperationKind.UserSetOnce,
                    string.Empty,
                    properties,
                    capturedAt,
                    null);
            }

            internal static PendingOperation Flush()
            {
                return new PendingOperation(
                    PendingOperationKind.Flush,
                    string.Empty,
                    null,
                    default,
                    null);
            }
        }
    }
}
