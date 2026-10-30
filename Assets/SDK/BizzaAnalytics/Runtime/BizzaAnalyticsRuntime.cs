using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public enum BizzaMessageFrequency
{
  Default = 0,
  High = 1,
}

internal sealed class BizzaAnalyticsConfig
{
  public string appId = "";
  public string ingestUrl = "";
  public string ingestToken = "";
  public bool release = false;
  public BizzaMessageFrequency messageFrequency = BizzaMessageFrequency.Default;
  public float reportingPercentage = 100f;
  public bool encryptionEnabled = true;
  public string encryptionKeyBase64 = "";
  public string encryptionKid = "";
  public bool verboseLog;
  public BizzaAnalyticsOptions performance = new BizzaAnalyticsOptions();
}

internal static class BizzaAnalyticsSampling
{
  private const int BucketCount = 10000;

  public static float NormalizePercentage(float percentage)
  {
    if (float.IsNaN(percentage) || float.IsInfinity(percentage))
    {
      return 100f;
    }

    if (percentage <= 0f) return 0f;
    if (percentage >= 100f) return 100f;
    return percentage;
  }

  public static int ResolveBucket(string appId, string uid)
  {
    var key = (appId ?? string.Empty) + ":" + (uid ?? string.Empty);
    unchecked
    {
      var hash = 2166136261u;
      for (var i = 0; i < key.Length; i++)
      {
        hash ^= key[i];
        hash *= 16777619u;
      }
      return (int)(hash % BucketCount);
    }
  }

  public static bool ShouldReport(float percentage, int bucket)
  {
    percentage = NormalizePercentage(percentage);
    if (percentage >= 100f) return true;
    if (percentage <= 0f) return false;

    var threshold = (int)Math.Round(percentage * 100d, MidpointRounding.AwayFromZero);
    return bucket >= 0 && bucket < threshold;
  }
}

internal sealed partial class BizzaAnalyticsRuntime
{
  private const string LogPrefix = "[BizzaAnalytics]";
  private const string LogColorInfo = "#66CCFF";
  private const string LogColorSuccess = "#58D68D";
  private const string LogColorWarn = "#F5B041";
  private const string LogColorError = "#EC7063";
  private const int MaxLogTextLength = 600;
  private const int Partitions = 200;
  private const int NormalRealtimeCap = 120;
  private const int OverloadRealtimeCap = 100;
  private const float UserProfileFlushSeconds = 60f;
  private const float FirstPhaseSeconds = 300f;
  private const float FirstPhaseFlushSeconds = 60f;
  private const float FallbackFlushSeconds = 240f;
  private const int MaxRetryCount = 8;
  private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9._-]{1,128}$");
  private static string cachedUtcDateToday;
  private static int cachedUtcDateNumber;

  private readonly BizzaAnalyticsAgent _host;
  private readonly BizzaAnalyticsConfig _config;
  private readonly BizzaStateStore _stateStore;
  private readonly BizzaState _state;
  private readonly string _ingestEndpoint;
  private readonly string _authorizationHeaderValue;
  private BizzaCrypto _crypto;

  private readonly System.Diagnostics.Stopwatch _foregroundTimer = new System.Diagnostics.Stopwatch();
  private bool _isSending;
  private Coroutine _sendCoroutine;

  public bool IsReady { get; private set; }
  public bool IsReportingEnabled { get; private set; }
  public string LastError { get; private set; }

  public BizzaAnalyticsRuntime(BizzaAnalyticsAgent host, BizzaAnalyticsConfig config)
  {
    _host = host;
    _config = config;
    LastError = string.Empty;

    if (!ValidateConfig())
    {
      IsReady = false;
      return;
    }

    _options = (_config.performance ?? new BizzaAnalyticsOptions()).ValidatedCopy();
    _stateStore = BizzaStateStore.Open(Application.persistentDataPath, _config.verboseLog);
    _state = _stateStore.Load();
    SanitizeLoadedState();
    _ingestEndpoint = _config.ingestUrl + "/v1/ingest";
    _authorizationHeaderValue = "Bearer " + _config.ingestToken;
    EnsureStableIds();
    var samplingBucket = BizzaAnalyticsSampling.ResolveBucket(_config.appId, _state.uid);
    IsReportingEnabled = _config.release && BizzaAnalyticsSampling.ShouldReport(_config.reportingPercentage, samplingBucket);
    if (IsReportingEnabled)
    {
      BootstrapAutoSignals();
    }
    SaveState();

    IsReady = true;
    StartCheckpoint(); // Stable IDs must also survive for sampled-out and release-disabled users.
    LogStartupSnapshot();
    if (IsReportingEnabled)
    {
      BeginStartupLogin();
      FlushNow("bootstrap");
    }
  }

  public void Tick(float deltaTime)
  {
    if (!IsReady || !IsReportingEnabled || _shutdown || _paused)
    {
      return;
    }

    if (deltaTime < 0f)
    {
      deltaTime = 0f;
    }

    var today = UtcDateToday();
    AddDailyElapsed(today, deltaTime);
    AddDailyFirstPhaseFlushElapsed(today, deltaTime);
    AddDailyFallbackFlushElapsed(today, deltaTime);
    if (_state.userProfileBuffer.Count > 0)
    {
      _state.userProfileFlushElapsedSeconds += deltaTime;
      if (_state.userProfileFlushElapsedSeconds >= UserProfileFlushSeconds)
      {
        FlushUserProfileNow("user_profile_minute_interval");
      }
    }
    else if (_state.userProfileFlushElapsedSeconds > 0d)
    {
      _state.userProfileFlushElapsedSeconds = 0d;
      SaveState();
    }

    if (GetDailyElapsed(today) <= FirstPhaseSeconds)
    {
      if (GetDailyRecordCount(today) >= 4 && GetDailyFirstPhaseFlushElapsed(today) >= FirstPhaseFlushSeconds)
      {
        _state.dailyFirstPhaseFlushElapsedByDt[today] = 0d;
        SaveState();
        FlushNow("first_five_minutes_of_day");
      }
    }
    else
    {
      if (GetDailyFallbackFlushElapsed(today) >= FallbackFlushSeconds)
      {
        _state.dailyFallbackFlushElapsedByDt[today] = 0d;
        SaveState();
        FlushNow("fallback_four_minutes_of_day");
      }
    }

    ServicePersistence(deltaTime);
    if (_maintenanceDate != today)
    {
      PruneExpiredState(today);
      _maintenanceDate = today;
    }
    PromoteDelayedQueue(today);
    EnsureSenderRunning();
  }

  public void Track(string eventName, Dictionary<string, object> data)
  {
    if (!IsReady || !IsReportingEnabled || _shutdown)
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(eventName))
    {
      LogWarn("BTrack eventName is empty, ignored.");
      return;
    }

    eventName = eventName.Trim();
    if (eventName.Length > 256) { DropRecords(1, "event_name_too_long"); return; }
    object normalized;
    int payloadBytes;
    if (!BizzaPayloadBudget.TryNormalize(data ?? new Dictionary<string, object>(), _options.maxRecordBytes - 2048, out normalized, out payloadBytes))
    {
      DropRecords(1, "event_payload_limit");
      return;
    }
    var payload = (Dictionary<string, object>)normalized;
    if (_config.verboseLog) LogVerbose("track(user) event=" + eventName + " payload=" + SerializeForLog(payload));
    var record = BizzaRecord.NewEvent(eventName, payload);
    record.estimatedBytes = payloadBytes + 256 + eventName.Length * 6;
    if (!EnqueueEventRealtimeRecord(record)) return;
    IncrementDailyRecordCount(UtcDateToday());
    OnEventRecordEnqueued();
  }

  public void UpdateUserProperties(Dictionary<string, object> data)
  {
    if (!IsReady || !IsReportingEnabled || _shutdown || data == null || data.Count == 0)
    {
      return;
    }

    var changed = 0;
    foreach (var kv in data)
    {
      if (string.IsNullOrWhiteSpace(kv.Key))
      {
        continue;
      }
      if (ApplyUserProperty(kv.Key.Trim(), kv.Value, false, "user"))
      {
        changed += 1;
      }
    }

    if (changed > 0)
    {
      OnUserProfileRecordEnqueued();
    }
  }

  public void FlushNow(string reason)
  {
    if (!IsReady || !IsReportingEnabled || _shutdown)
    {
      return;
    }

    if (_config.verboseLog)
    {
      var today = UtcDateToday();
      LogInfo(
        "FlushNow reason=" + reason +
        " uid=" + _state.uid +
        " player_id=" + _state.playerId +
        " today_records=" + GetDailyRecordCount(today) +
        " today_active_seconds=" + Math.Floor(GetDailyElapsed(today)) +
        " event_realtime_records=" + _state.realtimeBuffer.Count +
        " user_profile_records=" + _state.userProfileBuffer.Count +
        " pending_batches=" + _state.pendingBatches.Count +
        " pending_records=" + CountQueuedRecords(_state.pendingBatches) +
        " delayed_batches=" + _state.delayedQueue.Count +
        " delayed_records=" + CountQueuedRecords(_state.delayedQueue));
    }

    MoveEventRealtimeToPendingBatches(true);
    PromoteDelayedQueue(UtcDateToday());
    EnsureSenderRunning();
  }

  public void FlushLifecycleNow(string reason)
  {
    if (!IsReady || !IsReportingEnabled || _shutdown)
    {
      return;
    }

    _paused = true;
    CancelLoginIpLookup();
    CompleteStartupLogin("unknown");
    FinalizeGameDuration();
    FlushNow(reason);
    FlushUserProfileNow(reason + "_user_profile");
    var ticket = StartCheckpoint();
    if (!_stateStore.WaitForSave(ticket, _options.lifecycleSaveBudgetMilliseconds))
      LastError = "lifecycle_save_pending";
  }

  private bool ValidateConfig()
  {
    if (_config == null)
    {
      LastError = "config_is_null";
      LogError(LastError);
      return false;
    }

    _config.appId = (_config.appId ?? string.Empty).Trim();
    if (!IdPattern.IsMatch(_config.appId))
    {
      LastError = "app_id_must_match_^[A-Za-z0-9._-]{1,128}$";
      LogError(LastError);
      return false;
    }

    _config.ingestUrl = (_config.ingestUrl ?? string.Empty).Trim().TrimEnd('/');
    Uri endpoint;
    if (!Uri.TryCreate(_config.ingestUrl, UriKind.Absolute, out endpoint) ||
        (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
    {
      LastError = "ingest_url_is_invalid";
      LogError(LastError);
      return false;
    }

    _config.ingestToken = (_config.ingestToken ?? string.Empty).Trim();
    if (string.IsNullOrEmpty(_config.ingestToken) || _config.ingestToken.IndexOfAny(new[] { '\r', '\n' }) >= 0)
    {
      LastError = "ingest_token_is_empty";
      LogError(LastError);
      return false;
    }

    _config.reportingPercentage = BizzaAnalyticsSampling.NormalizePercentage(_config.reportingPercentage);

    _config.encryptionKeyBase64 = (_config.encryptionKeyBase64 ?? string.Empty).Trim();
    _config.encryptionKid = (_config.encryptionKid ?? string.Empty).Trim();
    if (_config.encryptionKid.Length > 128)
    {
      LastError = "encryption_kid_too_long";
      return false;
    }

    if (_config.encryptionEnabled)
    {
      string cryptoError;
      if (!BizzaCrypto.TryCreate(_config.encryptionKeyBase64, _config.encryptionKid, out _crypto, out cryptoError))
      {
        LastError = "encryption_config_invalid:" + cryptoError;
        LogError(LastError);
        return false;
      }
    }
    else
    {
      _crypto = null;
    }

    return true;
  }

  private void EnsureStableIds()
  {
    if (!IdPattern.IsMatch(_state.uid))
    {
      _state.uid = GenerateId("u");
    }

    if (string.IsNullOrEmpty(_state.playerId))
    {
      _state.playerId = GenerateId("player");
    }
  }

  private void BootstrapAutoSignals()
  {
    ApplyUserProperty("playerID", _state.playerId, false, "auto_bootstrap");
    ApplyUserProperty("device_model", string.IsNullOrEmpty(SystemInfo.deviceModel) ? "unknown" : SystemInfo.deviceModel, false, "auto_bootstrap");

    ApplyUserProperty("language", Application.systemLanguage.ToString(), false, "auto_bootstrap");
    ApplyUserProperty("country", ResolveDeviceCountry(), false, "auto_bootstrap");
    ApplyUserProperty("os_version", SystemInfo.operatingSystem, false, "auto_bootstrap");
    ApplyUserProperty("app_version", Application.version, false, "auto_bootstrap");
    ApplyUserProperty("platform", Application.platform.ToString(), false, "auto_bootstrap");

    var duration = (long)Math.Floor(_state.gameDurationSeconds);
    ApplyUserProperty("game_duration", duration, false, "auto_bootstrap");
  }

  private bool ApplyUserProperty(string key, object value, bool countAsDailyRecord, string source, bool force = false)
  {
    if (key.Length > 128 || (!_state.userProps.ContainsKey(key) && _state.userProps.Count >= 128))
    { DropRecords(1, "user_property_key_limit"); return false; }
    object normalizedValue;
    int valueBytes;
    var valueBudget = Math.Min(8192, (_options.maxRecordBytes - 2048) / 2);
    if (!BizzaPayloadBudget.TryNormalize(value, valueBudget, out normalizedValue, out valueBytes))
    { DropRecords(1, "user_property_value_limit"); return false; }
    object oldValue;
    var exists = _state.userProps.TryGetValue(key, out oldValue);
    if (!force && exists && BizzaValueUtil.AreEqual(oldValue, normalizedValue)) return false;
    var record = BizzaRecord.NewUserProfileChange(key, exists ? oldValue : null, normalizedValue, DateTime.UtcNow.ToString("o"));
    int oldBytes;
    if (!_userPropertyBytes.TryGetValue(key, out oldBytes)) oldBytes = exists ? valueBudget : 4;
    record.estimatedBytes = 256 + key.Length * 6 + valueBytes + oldBytes;
    if (!EnqueueUserProfileRecord(record)) return false;
    _state.userProps[key] = normalizedValue;
    _userPropertyBytes[key] = valueBytes;
    if (_config.verboseLog)
      LogVerbose("user_prop(" + source + ") key=" + key + " old=" + SerializeForLog(oldValue) + " new=" + SerializeForLog(normalizedValue));
    if (countAsDailyRecord) IncrementDailyRecordCount(UtcDateToday());
    return true;
  }

  // Device locale region; this is not IP geolocation.
  private static string ResolveDeviceCountry()
  {
    try
    {
#if UNITY_ANDROID && !UNITY_EDITOR
      using (var localeClass = new AndroidJavaClass("java.util.Locale"))
      using (var locale = localeClass.CallStatic<AndroidJavaObject>("getDefault"))
      {
        var country = locale.Call<string>("getCountry");
        return string.IsNullOrEmpty(country) ? "unknown" : country.ToUpperInvariant();
      }
#else
      var culture = CultureInfo.CurrentCulture;
      if (!culture.IsNeutralCulture && !string.IsNullOrEmpty(culture.Name))
      {
        return new RegionInfo(culture.Name).TwoLetterISORegionName;
      }
#endif
    }
    catch (Exception)
    {
      // Locale data may be unavailable on stripped or invariant runtimes.
    }
    return "unknown";
  }

  public void ResumeGameDuration()
  {
    _paused = false;
    _nextSendCheckUtc = DateTime.MinValue;
    if (!IsReady || !IsReportingEnabled || _shutdown || _foregroundTimer.IsRunning) return;
    _foregroundTimer.Restart();
  }

  private void FinalizeGameDuration()
  {
    if (!_foregroundTimer.IsRunning) return;
    _foregroundTimer.Stop();
    var oldDuration = (long)Math.Floor(_state.gameDurationSeconds);
    _state.gameDurationSeconds += _foregroundTimer.Elapsed.TotalSeconds;
    _foregroundTimer.Reset();
    var newDuration = (long)Math.Floor(_state.gameDurationSeconds);
    if (newDuration != oldDuration)
    {
      ApplyUserProperty("game_duration", newDuration, false, "lifecycle_flush");
    }
    SaveState();
  }

  private void OnEventRecordEnqueued()
  {
    SaveState();

    var today = UtcDateToday();
    var dailyRecordCount = GetDailyRecordCount(today);
    if (dailyRecordCount <= 4)
    {
      if ((dailyRecordCount % 2) == 0)
      {
        FlushNow("first_four_records_of_day");
      }
      return;
    }

    if (GetDailyElapsed(today) > FirstPhaseSeconds)
    {
      var threshold = ResolveEffectiveBatchSize();
      if (_state.realtimeBuffer.Count >= threshold)
      {
        FlushNow("count_threshold");
      }
    }
  }

  private void OnUserProfileRecordEnqueued()
  {
    SaveState();
  }

  private bool EnqueueEventRealtimeRecord(BizzaRecord record)
  {
    if (record.estimatedBytes == 0) record.estimatedBytes = 258 + record.eventName.Length * 6; // Empty events; nonempty payloads supply their size.
    if (!AdmitRecord(record)) return false;
    _state.realtimeBuffer.Add(record);
    return true;
  }

  private bool EnqueueUserProfileRecord(BizzaRecord record)
  {
    if (!AdmitRecord(record)) return false;
    _state.userProfileBuffer.Add(record);
    return true;
  }

  private void MoveEventRealtimeToPendingBatches(bool flushAll)
  {
    MoveBufferToBatches(_state.realtimeBuffer, "realtime", ResolveEffectiveBatchSize(), flushAll);
  }

  private void FlushUserProfileNow(string reason)
  {
    if (!IsReady || !IsReportingEnabled || _shutdown)
    {
      return;
    }

    if (_config.verboseLog)
    {
      LogInfo(
        "FlushUserProfileNow reason=" + reason +
        " uid=" + _state.uid +
        " player_id=" + _state.playerId +
        " user_profile_records=" + _state.userProfileBuffer.Count +
        " pending_batches=" + _state.pendingBatches.Count +
        " pending_records=" + CountQueuedRecords(_state.pendingBatches) +
        " delayed_batches=" + _state.delayedQueue.Count +
        " delayed_records=" + CountQueuedRecords(_state.delayedQueue));
    }

    MoveUserProfileToPendingBatches();
    PromoteDelayedQueue(UtcDateToday());
    EnsureSenderRunning();
  }

  private void MoveUserProfileToPendingBatches()
  {
    MoveBufferToBatches(_state.userProfileBuffer, "user_profile_realtime", _options.maxBatchRecords, true);
    if (_state.userProfileFlushElapsedSeconds != 0d)
    {
      _state.userProfileFlushElapsedSeconds = 0d;
      SaveState();
    }
  }

  private void MoveBufferToBatches(List<BizzaRecord> source, string channel, int batchSize, bool flushAll)
  {
    if (source.Count == 0) return;
    batchSize = Math.Min(batchSize, _options.maxBatchRecords);
    var offset = 0;
    var today = UtcDateToday();
    while (offset < source.Count)
    {
      var count = 0;
      long bytes = 4096;
      while (offset + count < source.Count && count < batchSize)
      {
        var recordBytes = source[offset + count].estimatedBytes * 2L + 512;
        if (bytes + recordBytes > _options.maxBatchBytes) break;
        bytes += recordBytes;
        count++;
      }
      if (count == 0) throw new InvalidOperationException("Admitted record exceeds batch budget.");
      if (!flushAll && count < batchSize && offset + count == source.Count) break;
      _state.pendingBatches.Add(new BizzaBatch
      {
        batchId = GenerateId("b"), channel = channel, dt = today, earliestSendDt = today,
        records = source.GetRange(offset, count)
      });
      offset += count;
    }
    if (offset == 0) return;
    // Shift the remaining buffer once, rather than once per batch.
    source.RemoveRange(0, offset);
    _nextSendCheckUtc = DateTime.MinValue;
    SaveState();
  }

  private int ResolveEffectiveBatchSize()
  {
    var today = UtcDateToday();
    var baseSize = 20;
    if (GetDailyRecordCount(today) < 4)
    {
      baseSize = 2;
    }
    else if (GetDailyElapsed(today) > FirstPhaseSeconds)
    {
      baseSize = _config.messageFrequency == BizzaMessageFrequency.High ? 100 : 20;
    }

    if (IsOverloadMode(today))
    {
      baseSize = Math.Max(baseSize, 100);
    }

    var usedPartitions = GetUsedPartitions(today);
    if (usedPartitions >= 175)
    {
      baseSize *= 4;
    }
    else if (usedPartitions >= 160)
    {
      baseSize *= 2;
    }

    if (baseSize > 500) baseSize = 500;
    if (baseSize < 2) baseSize = 2;
    return baseSize;
  }

  private int GetUsedPartitions(string dt)
  {
    return GetCounter(_state.realtimeSeqByDt, dt) + GetCounter(_state.delayedSeqByDt, dt);
  }

  private void PromoteDelayedQueue(string today)
  {
    if (_state.delayedQueue.Count == 0 || (!string.IsNullOrEmpty(_nextDelayedDate) && string.CompareOrdinal(_nextDelayedDate, today) > 0)) return;
    if (_delayedSortDirty)
    {
      _state.delayedQueue.Sort((a, b) =>
      {
        var comparison = string.CompareOrdinal(a.earliestSendDt, b.earliestSendDt);
        return comparison != 0 ? comparison : string.CompareOrdinal(a.originDt, b.originDt);
      });
      _delayedSortDirty = false;
    }
    var moved = 0;
    var removedOldestOrigin = false;
    // Promotion also has a fixed budget; a day rollover cannot drain an arbitrary queue in one frame.
    while (moved < _state.delayedQueue.Count && moved < 16)
    {
      var batch = _state.delayedQueue[moved];
      if (string.CompareOrdinal(batch.earliestSendDt, today) > 0) break;
      if (batch.originDt == _oldestDelayedOrigin) removedOldestOrigin = true;
      _state.pendingBatches.Add(batch);
      moved++;
    }
    if (moved > 0)
    {
      _state.delayedQueue.RemoveRange(0, moved);
      _nextSendCheckUtc = DateTime.MinValue;
      SaveState();
    }
    _nextDelayedDate = _state.delayedQueue.Count == 0 ? "" : _state.delayedQueue[0].earliestSendDt;
    if (_state.delayedQueue.Count == 0)
    {
      _state.delayedQueueNonEmptySince = "";
      _oldestDelayedOrigin = "";
    }
    else if (removedOldestOrigin)
    {
      _oldestDelayedOrigin = _state.delayedQueue[0].originDt;
      foreach (var batch in _state.delayedQueue)
        if (string.CompareOrdinal(batch.originDt, _oldestDelayedOrigin) < 0) _oldestDelayedOrigin = batch.originDt;
    }
  }

  private void EnsureSenderRunning()
  {
    if (_isSending || !IsReady || !IsReportingEnabled || _paused || _shutdown || DateTime.UtcNow < _nextSendCheckUtc) return;
    if (!HasReadyBatch()) return;
    _isSending = true;
    try { _sendCoroutine = _host.RunCoroutine(SendLoop()); }
    catch { _isSending = false; throw; }
  }

  private bool HasReadyBatch()
  {
    return FindNextReadyBatchIndex(DateTime.UtcNow) >= 0;
  }

  private IEnumerator SendLoop()
  {
    _isSending = true;
    var movedWithoutRequest = 0;
    try
    {
      while (!_shutdown && !_paused)
      {
        var today = UtcDateToday();
        var index = FindNextReadyBatchIndex(DateTime.UtcNow);
        if (index < 0) break;
        var batch = _state.pendingBatches[index];
        if (batch.part < 0)
        {
          int part;
          if (!TryAllocatePart(batch, today, out part))
          {
            _state.pendingBatches.RemoveAt(index);
            MoveBatchToDelayedQueue(batch, today);
            SaveState();
            if (++movedWithoutRequest >= 16)
            {
              movedWithoutRequest = 0;
              yield return null;
            }
            continue;
          }
          batch.part = part;
          batch.dt = today;
          SaveState();
        }

        _activeBatch = batch;
        // Persist the batch identity and partition counters before the server can accept it.
        var ticket = StartCheckpoint();
        string saveError;
        while (!_stateStore.GetSaveResult(ticket, out saveError))
        {
          if (_shutdown || _paused) yield break;
          yield return null;
        }
        if (!string.IsNullOrEmpty(saveError))
        {
          LastError = "state_save_failed:" + saveError;
          _dirty = true;
          _nextSendCheckUtc = DateTime.UtcNow.AddSeconds(5);
          break;
        }
        if (_shutdown || _paused) yield break;

        var sent = false;
        var statusCode = 0L;
        var errorText = "";
        yield return SendBatchRequest(batch, (ok, code, error) => { sent = ok; statusCode = code; errorText = error; });
        // Other main-thread maintenance may remove batches before this one while we yield.
        index = _state.pendingBatches.IndexOf(batch);
        if (index >= 0)
        {
          if (sent || statusCode == 413)
          {
            _state.pendingBatches.RemoveAt(index);
            ReleaseRecords(batch.records);
            if (!sent) DropRecords(batch.records.Count, "request_size_limit");
          }
          else HandleSendFailure(index, batch, today, statusCode, errorText);
          SaveState();
        }
        _activeBatch = null;
        movedWithoutRequest = 0;
        yield return null;
      }
    }
    finally
    {
      _activeBatch = null;
      _isSending = false;
      _sendCoroutine = null;
    }
  }

  private IEnumerator SendBatchRequest(BizzaBatch batch, Action<bool, long, string> onDone)
  {
    var snapshot = batch.Snapshot();
    var appId = _config.appId;
    var uid = _state.uid;
    var limit = _options.maxBatchBytes;
    byte[] bodyRaw = null;
    while (bodyRaw == null)
    {
      var crypto = _config.encryptionEnabled ? _crypto : null;
      _buildTask = Task.Run(() =>
      {
        var payload = BuildRequestPayload(snapshot, appId, uid, crypto);
        var bytes = Encoding.UTF8.GetBytes(BizzaJson.Serialize(payload));
        if (bytes.Length > limit) throw new System.IO.InvalidDataException("request_size_limit");
        return bytes;
      });
      while (!_buildTask.IsCompleted)
      {
        if (_shutdown) yield break;
        yield return null;
      }
      if (_buildTask.IsFaulted)
      {
        var error = _buildTask.Exception.GetBaseException();
        _buildTask = null;
        if (crypto != null && (error is PlatformNotSupportedException || error is NotSupportedException))
        {
          DisableEncryptionFallback(error);
          continue;
        }
        onDone(false, error is System.IO.InvalidDataException ? 413L : 0L, "build_payload_failed:" + error.Message);
        yield break;
      }
      bodyRaw = _buildTask.Result;
      _buildTask = null;
    }
    if (_shutdown || _paused) { onDone(false, 0L, "application_paused"); yield break; }
    UnityWebRequest request = null;
    UnityWebRequestAsyncOperation operation = null;
    var setupError = "";
    try
    {
      request = new UnityWebRequest(_ingestEndpoint, "POST");
      _activeRequest = request;
      request.timeout = _options.requestTimeoutSeconds;
      request.uploadHandler = new UploadHandlerRaw(bodyRaw);
      request.downloadHandler = new DownloadHandlerBuffer();
      request.SetRequestHeader("Authorization", _authorizationHeaderValue);
      request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
      if (_config.verboseLog)
        LogInfo("send(start) batch=" + batch.batchId + " records=" + batch.records.Count + " bytes=" + bodyRaw.Length);
      operation = request.SendWebRequest();
    }
    catch (Exception ex)
    {
      setupError = ex.Message;
      if (request != null) request.Dispose();
      _activeRequest = null;
    }
    if (operation == null)
    {
      onDone(false, 0L, "request_setup_failed:" + setupError);
      yield break;
    }
    using (request)
    {
      try
      {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var timedOut = false;
        while (!operation.isDone)
        {
          if (_shutdown || timer.Elapsed.TotalSeconds >= _options.requestTimeoutSeconds)
          {
            timedOut = !_shutdown;
            request.Abort();
            break;
          }
          yield return null;
        }
        var ok = !timedOut && request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
        var responseText = (_config.verboseLog || !ok) && request.downloadHandler != null ? request.downloadHandler.text : "";
        var error = timedOut ? "request_timeout" : ok ? "" : (!string.IsNullOrEmpty(request.error) ? request.error : responseText);
        if (_config.verboseLog)
          LogInfo("send(result) batch=" + batch.batchId + " ok=" + ok + " status=" + request.responseCode + " error=" + LimitForLog(error));
        onDone(ok, request.responseCode, error);
      }
      finally { _activeRequest = null; }
    }
  }

  // Kept as a pure helper for profiling; production calls the static overload on a worker.
  private Dictionary<string, object> BuildRequestPayload(BizzaBatch batch)
  {
    return BuildRequestPayload(batch, _config.appId, _state.uid, _config.encryptionEnabled ? _crypto : null);
  }

  private static Dictionary<string, object> BuildRequestPayload(BizzaBatch batch, string appId, string uid, BizzaCrypto crypto)
  {
    var records = new List<object>(batch.records.Count);
    var originDt = batch.channel == "delayed" ? batch.originDt : "";
    foreach (var record in batch.records)
    {
      var wire = record.ToWireRecord(originDt);
      if (crypto != null) EncryptWireRecordInPlace(wire, crypto);
      records.Add(wire);
    }
    return new Dictionary<string, object>(6)
    {
      { "app_id", appId }, { "uid", uid }, { "batch_id", batch.batchId },
      { "dt", batch.dt }, { "part", batch.part }, { "records", records }
    };
  }

  private void DisableEncryptionFallback(Exception ex)
  {
    _config.encryptionEnabled = false;
    if (_crypto != null) _crypto.Dispose();
    _crypto = null;
    LogWarn("encryption(disabled) reason=" + ex.Message + " fallback=plaintext platform=" + Application.platform);
  }

  private static void EncryptWireRecordInPlace(Dictionary<string, object> wireRecord, BizzaCrypto crypto)
  {
    if ((string)wireRecord["type"] == "event")
      wireRecord["payload"] = crypto.EncryptNormalizedValue(wireRecord["payload"]);
    else
    {
      if (wireRecord["old"] != null) wireRecord["old"] = crypto.EncryptNormalizedValue(wireRecord["old"]);
      if (wireRecord["new"] != null) wireRecord["new"] = crypto.EncryptNormalizedValue(wireRecord["new"]);
    }
  }

  private void HandleSendFailure(int pendingIndex, BizzaBatch batch, string today, long statusCode, string errorText)
  {
    if (statusCode == 409)
    {
      _state.pendingBatches.RemoveAt(pendingIndex);
      MoveBatchToDelayedQueue(batch, today);
      return;
    }

    batch.retryCount += 1;

    if (batch.retryCount > MaxRetryCount)
    {
      _state.pendingBatches.RemoveAt(pendingIndex);
      MoveBatchToDelayedQueue(batch, today);
      return;
    }

    var backoff = Math.Min(300d, Math.Pow(2d, Math.Min(8, batch.retryCount)) * 2d);
    batch.retryAtUtc = DateTime.UtcNow.AddSeconds(backoff);
    batch.nextRetryAt = batch.retryAtUtc.ToString("o");
    _state.pendingBatches[pendingIndex] = batch;

    if (_config.verboseLog)
    {
      LogWarn(
        "send(retry) batch=" + batch.batchId +
        " status=" + statusCode + " retry=" + batch.retryCount +
        " next_retry_at=" + batch.nextRetryAt + " error=" + LimitForLog(errorText)
      );
    }
  }

  private void MoveBatchToDelayedQueue(BizzaBatch batch, string today)
  {
    batch.channel = "delayed";
    if (string.IsNullOrEmpty(batch.originDt))
    {
      batch.originDt = string.IsNullOrEmpty(batch.dt) ? today : batch.dt;
    }
    batch.earliestSendDt = NextUtcDate(today);
    batch.dt = batch.earliestSendDt;
    batch.part = -1;
    batch.retryCount = 0;
    batch.nextRetryAt = "";

    batch.retryAtUtc = DateTime.MinValue;
    _state.delayedQueue.Add(batch);
    IncludeDelayedDate(batch);
    if (string.IsNullOrEmpty(_state.delayedQueueNonEmptySince))
    {
      _state.delayedQueueNonEmptySince = today;
    }
  }

  private bool TryAllocatePart(BizzaBatch batch, string dt, out int part)
  {
    var overloadMode = IsOverloadMode(dt);
    var realtimeCap = overloadMode ? OverloadRealtimeCap : NormalRealtimeCap;
    var delayedStart = realtimeCap;
    var delayedCap = Partitions - delayedStart;

    if (batch.channel == "realtime" || batch.channel == "user_profile_realtime")
    {
      var seq = GetCounter(_state.realtimeSeqByDt, dt);
      if (seq >= realtimeCap)
      {
        part = -1;
        return false;
      }

      part = seq;
      _state.realtimeSeqByDt[dt] = seq + 1;
      return true;
    }

    var delayedSeq = GetCounter(_state.delayedSeqByDt, dt);
    if (delayedSeq >= delayedCap)
    {
      part = -1;
      return false;
    }

    part = delayedStart + delayedSeq;
    _state.delayedSeqByDt[dt] = delayedSeq + 1;
    return true;
  }

  private bool IsOverloadMode(string today)
  {
    if (_state.delayedQueue.Count == 0)
    {
      return false;
    }

    var oldestOrigin = _oldestDelayedOrigin;
    DateTime oldestOriginDate;
    DateTime todayDate;
    if (TryParseDate(oldestOrigin, out oldestOriginDate) && TryParseDate(today, out todayDate))
    {
      if ((todayDate - oldestOriginDate).TotalDays > 2d)
      {
        return true;
      }
    }

    if (!string.IsNullOrEmpty(_state.delayedQueueNonEmptySince))
    {
      DateTime sinceDate;
      if (TryParseDate(_state.delayedQueueNonEmptySince, out sinceDate) && TryParseDate(today, out todayDate))
      {
        if ((todayDate - sinceDate).TotalDays >= 2d)
        {
          return true;
        }
      }
    }

    return false;
  }

  private int FindNextReadyBatchIndex(DateTime nowUtc)
  {
    var earliest = DateTime.MaxValue;
    for (var i = 0; i < _state.pendingBatches.Count; i++)
    {
      var batch = _state.pendingBatches[i];
      if (batch.part < 0 || batch.retryAtUtc <= nowUtc) return i;
      if (batch.retryAtUtc < earliest) earliest = batch.retryAtUtc;
    }
    _nextSendCheckUtc = earliest;
    return -1;
  }

  private void IncrementDailyRecordCount(string dt)
  {
    _state.dailyRecordCountByDt[dt] = GetCounter(_state.dailyRecordCountByDt, dt) + 1;
  }

  private void AddDailyElapsed(string dt, float deltaTime)
  {
    if (deltaTime <= 0f)
    {
      return;
    }

    _state.dailyActiveSecondsByDt[dt] = GetDoubleCounter(_state.dailyActiveSecondsByDt, dt) + deltaTime;
  }

  private void AddDailyFirstPhaseFlushElapsed(string dt, float deltaTime)
  {
    if (deltaTime <= 0f)
    {
      return;
    }

    _state.dailyFirstPhaseFlushElapsedByDt[dt] = GetDoubleCounter(_state.dailyFirstPhaseFlushElapsedByDt, dt) + deltaTime;
  }

  private void AddDailyFallbackFlushElapsed(string dt, float deltaTime)
  {
    if (deltaTime <= 0f)
    {
      return;
    }

    _state.dailyFallbackFlushElapsedByDt[dt] = GetDoubleCounter(_state.dailyFallbackFlushElapsedByDt, dt) + deltaTime;
  }

  private int GetDailyRecordCount(string dt)
  {
    return GetCounter(_state.dailyRecordCountByDt, dt);
  }

  private double GetDailyElapsed(string dt)
  {
    return GetDoubleCounter(_state.dailyActiveSecondsByDt, dt);
  }

  private double GetDailyFirstPhaseFlushElapsed(string dt)
  {
    return GetDoubleCounter(_state.dailyFirstPhaseFlushElapsedByDt, dt);
  }

  private double GetDailyFallbackFlushElapsed(string dt)
  {
    return GetDoubleCounter(_state.dailyFallbackFlushElapsedByDt, dt);
  }

  private int GetCounter(Dictionary<string, int> map, string key)
  {
    int value;
    if (!map.TryGetValue(key, out value))
    {
      return 0;
    }
    return value;
  }

  private double GetDoubleCounter(Dictionary<string, double> map, string key)
  {
    double value;
    if (!map.TryGetValue(key, out value))
    {
      return 0d;
    }
    return value;
  }

  private void LogVerbose(string message)
  {
    if (!_config.verboseLog)
    {
      return;
    }

    LogInfo(message);
  }

  private string SerializeForLog(object value)
  {
    try
    {
      return LimitForLog(BizzaJson.Serialize(value));
    }
    catch
    {
      return value == null ? "null" : value.ToString();
    }
  }

  private static string Colorize(string color, string message)
  {
    return "<color=" + color + ">" + LogPrefix + " " + message + "</color>";
  }

  private void LogInfo(string message)
  {
    if (!_config.verboseLog)
    {
      return;
    }

    Debug.Log(Colorize(LogColorInfo, message));
  }

  private void LogSuccess(string message)
  {
    if (!_config.verboseLog)
    {
      return;
    }

    Debug.Log(Colorize(LogColorSuccess, message));
  }

  private void LogWarn(string message)
  {
    if (!_config.verboseLog)
    {
      return;
    }

    Debug.LogWarning(Colorize(LogColorWarn, message));
  }

  private void LogError(string message)
  {
    Debug.LogError(Colorize(LogColorError, message));
  }

  private static string LimitForLog(string text)
  {
    if (string.IsNullOrEmpty(text))
    {
      return "";
    }

    if (text.Length <= MaxLogTextLength)
    {
      return text;
    }

    return text.Substring(0, MaxLogTextLength) + "...(truncated)";
  }

  private void SaveState()
  {
    _dirty = true;
  }

  private void LogStartupSnapshot()
  {
    if (!_config.verboseLog)
    {
      return;
    }

    LogSuccess("startup(app_id) " + _config.appId);
    LogSuccess("startup(current_uid) " + _state.uid);
    LogSuccess("startup(current_player_id) " + _state.playerId);
    LogSuccess(
      "startup(reporting) percentage=" +
      _config.reportingPercentage.ToString("0.##", CultureInfo.InvariantCulture) +
      " enabled=" + IsReportingEnabled);
    var today = UtcDateToday();
    LogSuccess(
      "startup(buffer_state) today_records=" + GetDailyRecordCount(today) +
      " today_active_seconds=" + Math.Floor(GetDailyElapsed(today)) +
      " event_realtime_records=" + _state.realtimeBuffer.Count +
      " user_profile_records=" + _state.userProfileBuffer.Count +
      " pending_batches=" + _state.pendingBatches.Count +
      " pending_records=" + CountQueuedRecords(_state.pendingBatches) +
      " delayed_batches=" + _state.delayedQueue.Count +
      " delayed_records=" + CountQueuedRecords(_state.delayedQueue));
    LogSuccess("startup(user_props) " + SerializeForLog(_state.userProps));
  }

  private static int CountQueuedRecords(List<BizzaBatch> batches)
  {
    if (batches == null || batches.Count == 0)
    {
      return 0;
    }

    var total = 0;
    for (var i = 0; i < batches.Count; i++)
    {
      total += batches[i].records == null ? 0 : batches[i].records.Count;
    }
    return total;
  }

  private static string GenerateId(string prefix)
  {
    var core = Guid.NewGuid().ToString("N");
    var id = prefix + "_" + core.Substring(0, 20);
    return id;
  }

  private static string UtcDateToday()
  {
    var now = DateTime.UtcNow;
    var dateNumber = now.Year * 10000 + now.Month * 100 + now.Day;
    if (cachedUtcDateNumber != dateNumber || string.IsNullOrEmpty(cachedUtcDateToday))
    {
      cachedUtcDateNumber = dateNumber;
      cachedUtcDateToday = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    return cachedUtcDateToday;
  }

  private static string NextUtcDate(string utcDate)
  {
    DateTime date;
    if (!TryParseDate(utcDate, out date))
    {
      date = DateTime.UtcNow.Date;
    }
    return date.AddDays(1).ToString("yyyy-MM-dd");
  }

  private static bool TryParseDate(string dateText, out DateTime result)
  {
    return DateTime.TryParseExact(
      dateText,
      "yyyy-MM-dd",
      CultureInfo.InvariantCulture,
      DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
      out result
    );
  }
}
