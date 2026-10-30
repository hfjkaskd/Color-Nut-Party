using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine.Networking;

internal sealed partial class BizzaAnalyticsRuntime
{
  private readonly BizzaAnalyticsOptions _options;
  private bool _dirty;
  private float _saveElapsed;
  private long _saveTicket;
  private long _observedSaveTicket;
  private int _queuedRecordCount;
  private long _queuedBytes;
  private readonly Dictionary<string, int> _userPropertyBytes = new Dictionary<string, int>();
  private bool _paused;
  private bool _shutdown;
  private string _maintenanceDate = "";
  private string _nextDelayedDate = "";
  private string _oldestDelayedOrigin = "";
  private bool _delayedSortDirty = true;
  private DateTime _nextSendCheckUtc = DateTime.MinValue;
  private BizzaBatch _activeBatch;
  private UnityWebRequest _activeRequest;
  private Task<byte[]> _buildTask;

  public long DroppedRecordCount => _state == null ? 0 : _state.droppedRecordCount;
  public string LastDropReason => _state == null ? "" : _state.lastDropReason;
  public int QueuedRecordCount => _queuedRecordCount;

  private void ServicePersistence(float deltaTime)
  {
    _saveElapsed += deltaTime;
    string error;
    if (_saveTicket != _observedSaveTicket && _stateStore.GetSaveResult(_saveTicket, out error))
    {
      _observedSaveTicket = _saveTicket;
      if (!string.IsNullOrEmpty(error))
      {
        LastError = "state_save_failed:" + error;
        _dirty = true;
      }
    }
    if (_dirty && _saveElapsed >= _options.saveIntervalSeconds)
      StartCheckpoint();
  }

  private long StartCheckpoint()
  {
    if (!_dirty && _saveTicket != 0) return _saveTicket;
    _saveTicket = _stateStore.EnqueueSave(_state.Snapshot());
    _dirty = false;
    _saveElapsed = 0f;
    return _saveTicket;
  }

  private bool AdmitRecord(BizzaRecord record)
  {
    if (record.estimatedBytes <= 0 || record.estimatedBytes > _options.maxRecordBytes)
    {
      DropRecords(1, "record_size_limit");
      return false;
    }
    if (_queuedRecordCount >= _options.maxQueuedRecords || _queuedBytes + record.estimatedBytes > _options.maxQueuedBytes)
    {
      // Preserve accepted records and in-flight identity. New arrivals are rejected in O(1).
      DropRecords(1, "queue_capacity");
      return false;
    }
    if (record.timestampUtc == default(DateTime)) record.timestampUtc = ParseTimestamp(record.ts);
    _queuedRecordCount++;
    _queuedBytes += record.estimatedBytes;
    return true;
  }

  private void ReleaseRecords(List<BizzaRecord> records)
  {
    foreach (var record in records)
    {
      _queuedRecordCount--;
      _queuedBytes -= record.estimatedBytes;
    }
  }

  private void DropRecords(int count, string reason)
  {
    _state.droppedRecordCount += count;
    _state.lastDropReason = reason;
    LastError = "records_dropped:" + reason;
    SaveState();
  }

  private static DateTime ParseTimestamp(string value)
  {
    DateTime result;
    return DateTime.TryParse(value, CultureInfo.InvariantCulture,
      DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result) ? result : DateTime.MinValue;
  }

  private void SanitizeLoadedState()
  {
    // Replace legacy containers, never mutate payloads shared with an outstanding writer snapshot.
    var props = new Dictionary<string, object>();
    foreach (var pair in _state.userProps)
    {
      object normalized;
      int bytes;
      if (props.Count < 128 && pair.Key.Length <= 128 &&
          BizzaPayloadBudget.TryNormalize(pair.Value, Math.Min(8192, (_options.maxRecordBytes - 2048) / 2), out normalized, out bytes))
      {
        props[pair.Key] = normalized;
        _userPropertyBytes[pair.Key] = bytes;
      }
    }
    _state.userProps = props;
    _state.realtimeBuffer = LoadRecords(_state.realtimeBuffer);
    _state.userProfileBuffer = LoadRecords(_state.userProfileBuffer);
    _state.pendingBatches = LoadBatches(_state.pendingBatches);
    _state.delayedQueue = LoadBatches(_state.delayedQueue);
    RefreshDelayedSchedule();
    PruneExpiredState(UtcDateToday());
    _maintenanceDate = UtcDateToday();
  }

  private BizzaRecord NormalizeLoadedRecord(BizzaRecord source)
  {
    if (source.eventName.Length > 256 || source.key.Length > 128 || source.ts.Length > 128) return null;
    BizzaRecord record;
    if (source.type == "event")
    {
      object payload;
      int bytes;
      if (!BizzaPayloadBudget.TryNormalize(source.payload, _options.maxRecordBytes - 2048, out payload, out bytes)) return null;
      record = BizzaRecord.NewEvent(source.eventName, (Dictionary<string, object>)payload);
      record.estimatedBytes = bytes + 256 + source.eventName.Length * 6;
    }
    else if (source.type == "user_profile_change")
    {
      object oldValue, newValue, meta;
      int oldBytes, newBytes, metaBytes;
      var budget = Math.Min(8192, (_options.maxRecordBytes - 2048) / 2);
      if (!BizzaPayloadBudget.TryNormalize(source.oldValue, budget, out oldValue, out oldBytes) ||
          !BizzaPayloadBudget.TryNormalize(source.newValue, budget, out newValue, out newBytes)) return null;
      record = BizzaRecord.NewUserProfileChange(source.key, oldValue, newValue, source.ts);
      record.estimatedBytes = 256 + source.key.Length * 6 + oldBytes + newBytes;
      if (source.meta.Count > 0)
      {
        if (!BizzaPayloadBudget.TryNormalize(source.meta, _options.maxRecordBytes - record.estimatedBytes - 64, out meta, out metaBytes)) return null;
        record.meta = (Dictionary<string, object>)meta;
        record.estimatedBytes += metaBytes + 64;
      }
    }
    else return null;
    record.ts = source.ts;
    record.timestampUtc = ParseTimestamp(record.ts);
    return record;
  }

  private List<BizzaRecord> LoadRecords(List<BizzaRecord> source)
  {
    var result = new List<BizzaRecord>(Math.Min(source.Count, _options.maxQueuedRecords));
    var cutoff = DateTime.UtcNow.AddDays(-_options.retentionDays);
    foreach (var item in source)
    {
      var record = NormalizeLoadedRecord(item);
      if (record == null) { DropRecords(1, "legacy_record_limit"); continue; }
      if (record.timestampUtc < cutoff) { DropRecords(1, "retention_expired"); continue; }
      if (AdmitRecord(record)) result.Add(record);
    }
    return result;
  }

  private List<BizzaBatch> LoadBatches(List<BizzaBatch> source)
  {
    var result = new List<BizzaBatch>();
    var cutoff = DateTime.UtcNow.AddDays(-_options.retentionDays);
    foreach (var item in source)
    {
      var records = new List<BizzaRecord>(Math.Min(item.records.Count, _options.maxBatchRecords));
      long size = 0;
      var latest = DateTime.MinValue;
      var valid = item.records.Count > 0 && item.records.Count <= _options.maxBatchRecords && item.batchId.Length <= 128;
      if (valid)
      {
        foreach (var raw in item.records)
        {
          var record = NormalizeLoadedRecord(raw);
          if (record == null) { valid = false; break; }
          records.Add(record);
          size += record.estimatedBytes;
          if (record.timestampUtc > latest) latest = record.timestampUtc;
        }
      }
      // A previously allocated batch keeps its exact identity/content, or is dropped as a whole.
      // Silently splitting it could duplicate an already accepted request after a crash.
      if (!valid || BatchWireBudget(records) > _options.maxBatchBytes)
      { DropRecords(item.records.Count, "legacy_batch_limit"); continue; }
      if (latest < cutoff) { DropRecords(records.Count, "retention_expired"); continue; }
      if (_queuedRecordCount + records.Count > _options.maxQueuedRecords || _queuedBytes + size > _options.maxQueuedBytes)
      { DropRecords(records.Count, "queue_capacity"); continue; }
      var batch = item.Snapshot();
      batch.records = records;
      batch.retryAtUtc = string.IsNullOrEmpty(batch.nextRetryAt) ? DateTime.MinValue : ParseTimestamp(batch.nextRetryAt);
      result.Add(batch);
      _queuedRecordCount += records.Count;
      _queuedBytes += size;
    }
    return result;
  }

  private long BatchWireBudget(List<BizzaRecord> records)
  {
    long bytes = 4096;
    foreach (var record in records) bytes += record.estimatedBytes * 2L + 512;
    return bytes;
  }

  private void PruneExpiredState(string today)
  {
    var cutoff = DateTime.UtcNow.AddDays(-_options.retentionDays);
    PruneBuffer(_state.realtimeBuffer, cutoff);
    PruneBuffer(_state.userProfileBuffer, cutoff);
    PruneBatches(_state.pendingBatches, cutoff);
    PruneBatches(_state.delayedQueue, cutoff);
    var keepSince = DateTime.UtcNow.AddDays(-Math.Max(14, _options.retentionDays)).ToString("yyyy-MM-dd");
    PruneDates(_state.dailyRecordCountByDt, keepSince);
    PruneDates(_state.dailyActiveSecondsByDt, keepSince);
    PruneDates(_state.dailyFirstPhaseFlushElapsedByDt, keepSince);
    PruneDates(_state.dailyFallbackFlushElapsedByDt, keepSince);
    PruneDates(_state.realtimeSeqByDt, keepSince);
    PruneDates(_state.delayedSeqByDt, keepSince);
    RefreshDelayedSchedule();
    _nextSendCheckUtc = DateTime.MinValue;
    SaveState();
  }

  private void PruneBuffer(List<BizzaRecord> buffer, DateTime cutoff)
  {
    var write = 0;
    for (var read = 0; read < buffer.Count; read++)
    {
      var record = buffer[read];
      if (record.timestampUtc < cutoff)
      {
        _queuedRecordCount--;
        _queuedBytes -= record.estimatedBytes;
        DropRecords(1, "retention_expired");
      }
      else buffer[write++] = record;
    }
    if (write < buffer.Count) buffer.RemoveRange(write, buffer.Count - write);
  }

  private void PruneBatches(List<BizzaBatch> batches, DateTime cutoff)
  {
    for (var i = batches.Count - 1; i >= 0; i--)
    {
      var batch = batches[i];
      if (ReferenceEquals(batch, _activeBatch)) continue;
      var expired = true;
      foreach (var record in batch.records) if (record.timestampUtc >= cutoff) { expired = false; break; }
      if (!expired) continue;
      ReleaseRecords(batch.records);
      DropRecords(batch.records.Count, "retention_expired");
      batches.RemoveAt(i);
    }
  }

  private static void PruneDates<T>(Dictionary<string, T> map, string keepSince)
  {
    var expired = new List<string>();
    foreach (var key in map.Keys) if (string.CompareOrdinal(key, keepSince) < 0) expired.Add(key);
    foreach (var key in expired) map.Remove(key);
  }

  private void RefreshDelayedSchedule()
  {
    _nextDelayedDate = "";
    _oldestDelayedOrigin = "";
    foreach (var batch in _state.delayedQueue) IncludeDelayedDate(batch);
    _delayedSortDirty = true;
    if (_state.delayedQueue.Count == 0) _state.delayedQueueNonEmptySince = "";
  }

  private void IncludeDelayedDate(BizzaBatch batch)
  {
    var earliest = string.IsNullOrEmpty(batch.earliestSendDt) ? UtcDateToday() : batch.earliestSendDt;
    if (string.IsNullOrEmpty(_nextDelayedDate) || string.CompareOrdinal(earliest, _nextDelayedDate) < 0) _nextDelayedDate = earliest;
    if (string.IsNullOrEmpty(_oldestDelayedOrigin) || string.CompareOrdinal(batch.originDt, _oldestDelayedOrigin) < 0)
      _oldestDelayedOrigin = batch.originDt;
    _delayedSortDirty = true;
  }

  public void Shutdown()
  {
    if (_shutdown) return;
    CancelLoginIpLookup();
    if (_startupLoginPending)
    {
      _paused = true;
      CompleteStartupLogin("unknown");
      StartCheckpoint();
    }
    _shutdown = true;
    if (_activeRequest != null) _activeRequest.Abort();
    _host.StopAnalyticsCoroutine(_sendCoroutine);
    // Do not depend on an engine coroutine stop invoking iterator finally blocks.
    if (_activeRequest != null) { _activeRequest.Dispose(); _activeRequest = null; }
    _activeBatch = null;
    _sendCoroutine = null;
    _isSending = false;
    var crypto = _crypto;
    _crypto = null;
    if (crypto != null)
    {
      if (_buildTask != null && !_buildTask.IsCompleted)
        _buildTask.ContinueWith(task => { var ignored = task.Exception; crypto.Dispose(); }, TaskScheduler.Default);
      else
      {
        if (_buildTask != null) { var ignored = _buildTask.Exception; }
        crypto.Dispose();
      }
    }
  }
}
