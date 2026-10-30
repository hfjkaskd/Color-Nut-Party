using System;

/// <summary>Local resource budgets. These do not change the ingest protocol or daily partition quota.</summary>
[Serializable]
public sealed class BizzaAnalyticsOptions
{
  public float saveIntervalSeconds = 2f;
  public int maxQueuedRecords = 10000;
  public int maxQueuedBytes = 8 * 1024 * 1024;
  public int maxBatchRecords = 500;
  public int maxBatchBytes = 256 * 1024;
  public int maxRecordBytes = 32 * 1024;
  public int retentionDays = 7;
  public int requestTimeoutSeconds = 15;
  public int lifecycleSaveBudgetMilliseconds = 100;

  internal BizzaAnalyticsOptions ValidatedCopy()
  {
    var result = (BizzaAnalyticsOptions)MemberwiseClone();
    result.saveIntervalSeconds = float.IsNaN(saveIntervalSeconds) || float.IsInfinity(saveIntervalSeconds)
      ? 2f : Math.Max(0.25f, Math.Min(30f, saveIntervalSeconds));
    result.maxQueuedRecords = Math.Max(100, Math.Min(50000, maxQueuedRecords));
    result.maxQueuedBytes = Math.Max(256 * 1024, Math.Min(32 * 1024 * 1024, maxQueuedBytes));
    result.maxBatchRecords = Math.Max(1, Math.Min(500, maxBatchRecords));
    result.maxBatchBytes = Math.Max(16 * 1024, Math.Min(1024 * 1024, maxBatchBytes));
    result.maxRecordBytes = Math.Max(1024, Math.Min(Math.Min(64 * 1024, (result.maxBatchBytes - 4096 - 512) / 2), maxRecordBytes));
    result.retentionDays = Math.Max(1, Math.Min(30, retentionDays));
    result.requestTimeoutSeconds = Math.Max(5, Math.Min(120, requestTimeoutSeconds));
    result.lifecycleSaveBudgetMilliseconds = Math.Max(0, Math.Min(250, lifecycleSaveBudgetMilliseconds));
    return result;
  }
}
