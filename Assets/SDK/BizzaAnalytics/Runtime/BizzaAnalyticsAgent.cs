using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BizzaAnalyticsAgent : MonoBehaviour
{
  private string appId = "";
  private string ingestUrl = "";
  private string ingestToken = "";

  [Header("Optional")]
  [SerializeField] private BizzaMessageFrequency messageFrequency = BizzaMessageFrequency.Default;
  [SerializeField, Range(0f, 100f)]
  [Tooltip("Percentage of users that report analytics. 100 means all users. Sampling is stable per app and user. Export Encrypted Text to save this value into the application config.")]
  private float reportingPercentage = 100f;
  [SerializeField] private bool encryptionEnabled = true;
  [SerializeField] private bool dontDestroyOnLoad = true;
  [SerializeField] private bool autoInitialize = true;
  [SerializeField] private bool logMode = false;
  [SerializeField, HideInInspector] private bool verboseLog = false; // Legacy field, keep for old scenes.
  [Header("Performance")]
  [SerializeField] private BizzaAnalyticsOptions performance = new BizzaAnalyticsOptions();

  private static BizzaAnalyticsAgent _instance;
  private BizzaAnalyticsRuntime _runtime;
  private bool _isPaused;

  public static BizzaAnalyticsAgent Instance => _instance;
  public bool IsReady => _runtime != null && _runtime.IsReady;
  public bool IsReportingEnabled => _runtime != null && _runtime.IsReportingEnabled;
  public float ReportingPercentage => reportingPercentage;
  public string LastError => _runtime?.LastError ?? string.Empty;
  public long DroppedRecordCount => _runtime?.DroppedRecordCount ?? 0;
  public string LastDropReason => _runtime?.LastDropReason ?? string.Empty;
  public int QueuedRecordCount => _runtime?.QueuedRecordCount ?? 0;

  private void Awake()
  {
    if (_instance != null && _instance != this)
    {
      Debug.LogWarning("[BizzaAnalytics] Duplicate BizzaAnalyticsAgent detected and destroyed.");
      Destroy(gameObject);
      return;
    }

    _instance = this;
    if (dontDestroyOnLoad)
    {
      DontDestroyOnLoad(gameObject);
    }

    if (autoInitialize && IsProductionRuntime())
    {
      Initialize();
    }
  }

  private void Update()
  {
    _runtime?.Tick(Time.unscaledDeltaTime);
  }

  private void OnApplicationPause(bool pauseStatus)
  {
    _isPaused = pauseStatus;
    if (pauseStatus)
    {
      _runtime?.FlushLifecycleNow("application_pause");
    }
    else
    {
      _runtime?.ResumeGameDuration();
    }
  }

  private void OnApplicationQuit()
  {
    _runtime?.FlushLifecycleNow("application_quit");
  }

  private void OnDestroy()
  {
    if (_instance == this)
    {
      _runtime?.FlushLifecycleNow("agent_destroy");
      _runtime?.Shutdown();
      _instance = null;
    }
  }

  public bool Initialize()
  {
    if (_runtime != null)
    {
      return _runtime.IsReady;
    }

    BizzaAnalyticsSecretConfig secretConfig;
    string secretConfigError;
    if (BizzaAnalyticsSecretConfig.TryLoadFromResources(out secretConfig, out secretConfigError))
    {
      appId = secretConfig.appId;
      ingestUrl = secretConfig.ingestUrl;
      ingestToken = secretConfig.ingestToken;
      if (secretConfig.hasReportingPercentage)
      {
        reportingPercentage = secretConfig.reportingPercentage;
      }
    }
    else
    {
      appId = "";
      ingestUrl = "";
      ingestToken = "";
      secretConfig = null;
      Debug.LogError("[BizzaAnalytics] Failed to load encrypted config text from Resources. " + secretConfigError);
    }

    var config = new BizzaAnalyticsConfig
    {
      appId = appId,
      ingestUrl = ingestUrl,
      ingestToken = ingestToken,
      release = secretConfig?.release ?? false,
      messageFrequency = messageFrequency,
      reportingPercentage = reportingPercentage,
      encryptionEnabled = encryptionEnabled,
      encryptionKeyBase64 = secretConfig?.encryptionKeyBase64 ?? "",
      encryptionKid = secretConfig?.encryptionKid ?? "",
      verboseLog = logMode || verboseLog,
      performance = performance,
    };

    _runtime = new BizzaAnalyticsRuntime(this, config);
    if (!_isPaused)
    {
      _runtime.ResumeGameDuration();
    }
    return _runtime.IsReady;
  }

  public void BTrack(string eventName, Dictionary<string, object> data)
  {
    EnsureRuntime();
    _runtime?.Track(eventName, data);
  }

  public void BUserProp(Dictionary<string, object> data)
  {
    EnsureRuntime();
    _runtime?.UpdateUserProperties(data);
  }

  public static void Track(string eventName, Dictionary<string, object> data = null)
  {
    if (_instance == null)
    {
      Debug.LogWarning("[BizzaAnalytics] BizzaAnalyticsAgent not found in scene.");
      return;
    }
    _instance.BTrack(eventName, data);
  }

  public static void UserProp(Dictionary<string, object> data)
  {
    if (_instance == null)
    {
      Debug.LogWarning("[BizzaAnalytics] BizzaAnalyticsAgent not found in scene.");
      return;
    }
    _instance.BUserProp(data);
  }

  internal Coroutine RunCoroutine(System.Collections.IEnumerator routine)
  {
    return StartCoroutine(routine);
  }

  internal void StopAnalyticsCoroutine(Coroutine routine)
  {
    if (routine != null) StopCoroutine(routine);
  }

  private void EnsureRuntime()
  {
    if (_runtime == null)
    {
      Initialize();
    }
  }

  private void OnValidate()
  {
    reportingPercentage = BizzaAnalyticsSampling.NormalizePercentage(reportingPercentage);
  }

  private static bool IsProductionRuntime()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
    return true;
#else
    return false;
#endif
  }
}
[Serializable]
public sealed class BizzaAnalyticsSecretConfig
{
  public static string ResourceLoadPath => GetResourceLoadPath(Application.productName);
  public static string ResourceAssetPath => GetResourceAssetPath(Application.productName);

  private const string ConfigEncryptionKeyBase64 = "8qB1h6eCT7iQ7BlXwolU8Jgm6bHSJ8nqmkLcJijrd6k=";
  private const string ConfigEncryptionKid = "bizza-config-k1";
  private const string ConfigEnvelopeVersion = "config-v1";

  public string appId = "";
  public string ingestUrl = "";
  public string ingestToken = "";
  public string encryptionKeyBase64 = "";
  public string encryptionKid = "";
  public bool release = false;
  public float reportingPercentage = 100f;
  public bool hasReportingPercentage;

  public static string GetResourceLoadPath(string applicationName)
  {
    return GetSafeApplicationName(applicationName);
  }

  public static string GetResourceAssetPath(string applicationName)
  {
    return "Assets/Resources/" + GetResourceLoadPath(applicationName) + ".txt";
  }

  public static string GetSafeApplicationName(string applicationName)
  {
    const string invalidFileNameChars = "<>:\"/\\|?*";
    var source = string.IsNullOrWhiteSpace(applicationName) ? "Application" : applicationName.Trim();
    var builder = new StringBuilder(source.Length);

    for (var i = 0; i < source.Length; i++)
    {
      var c = source[i];
      builder.Append(char.IsControl(c) || invalidFileNameChars.IndexOf(c) >= 0 ? '_' : c);
    }

    var safeName = builder.ToString().Trim(' ', '.');
    return string.IsNullOrEmpty(safeName) ? "Application" : safeName;
  }

  public bool HasRequiredValues
  {
    get
    {
      return !string.IsNullOrWhiteSpace(appId) &&
             !string.IsNullOrWhiteSpace(ingestUrl) &&
             !string.IsNullOrWhiteSpace(ingestToken);
    }
  }

  public static bool TryCreateEncryptedText(string appId, string ingestUrl, string ingestToken, out string text, out string error)
  {
    return TryCreateEncryptedText(appId, ingestUrl, ingestToken, 100f, out text, out error);
  }

  public static bool TryCreateEncryptedText(
    string appId,
    string ingestUrl,
    string ingestToken,
    float reportingPercentage,
    out string text,
    out string error)
  {
    return TryCreateEncryptedText(appId, ingestUrl, ingestToken, reportingPercentage, "", "", out text, out error);
  }

  public static bool TryCreateEncryptedText(
    string appId,
    string ingestUrl,
    string ingestToken,
    float reportingPercentage,
    string encryptionKeyBase64,
    string encryptionKid,
    out string text,
    out string error)
  {
    return TryCreateEncryptedText(appId, ingestUrl, ingestToken, reportingPercentage, encryptionKeyBase64, encryptionKid, false, out text, out error);
  }

  public static bool TryCreateEncryptedText(
    string appId,
    string ingestUrl,
    string ingestToken,
    float reportingPercentage,
    string encryptionKeyBase64,
    string encryptionKid,
    bool release,
    out string text,
    out string error)
  {
    text = "";
    error = "";

    BizzaCrypto crypto;
    if (!BizzaCrypto.TryCreate(ConfigEncryptionKeyBase64, ConfigEncryptionKid, out crypto, out error))
    {
      error = "create_config_crypto_failed:" + error;
      return false;
    }

    var plainConfig = new Dictionary<string, object>(6);
    plainConfig["app_id"] = appId ?? "";
    plainConfig["ingest_url"] = ingestUrl ?? "";
    plainConfig["ingest_token"] = ingestToken ?? "";
    plainConfig["encryption_key_base64"] = encryptionKeyBase64 ?? "";
    plainConfig["encryption_kid"] = encryptionKid ?? "";
    plainConfig["release"] = release;

    var root = new Dictionary<string, object>(3);
    root["v"] = ConfigEnvelopeVersion;
    root["reporting_percentage"] = BizzaAnalyticsSampling.NormalizePercentage(reportingPercentage);
    using (crypto)
    {
      root["payload"] = crypto.EncryptValue(plainConfig);
    }

    text = BizzaJson.Serialize(root);
    return true;
  }

  public static bool TryLoadFromResources(out BizzaAnalyticsSecretConfig config, out string error)
  {
    config = null;
    error = "";

    var resourceLoadPath = ResourceLoadPath;
    var asset = Resources.Load<TextAsset>(resourceLoadPath);
    if (asset == null)
    {
      error = "config_resource_missing:" + resourceLoadPath;
      return false;
    }

    return TryReadEncryptedText(asset.text, out config, out error);
  }

  public static bool TryReadEncryptedText(string text, out BizzaAnalyticsSecretConfig config, out string error)
  {
    config = null;
    error = "";

    if (string.IsNullOrWhiteSpace(text))
    {
      error = "config_text_empty";
      return false;
    }

    object parsed;
    try
    {
      parsed = BizzaJson.Deserialize(text);
    }
    catch (Exception ex)
    {
      error = "config_text_invalid:" + ex.Message;
      return false;
    }

    var root = BizzaValueUtil.AsStringObjectDictionary(parsed);
    var version = BizzaValueUtil.AsString(root.ContainsKey("v") ? root["v"] : null, "");
    if (version != ConfigEnvelopeVersion)
    {
      error = "config_version_invalid";
      return false;
    }

    if (!root.ContainsKey("payload"))
    {
      error = "config_payload_missing";
      return false;
    }

    BizzaCrypto crypto;
    if (!BizzaCrypto.TryCreate(ConfigEncryptionKeyBase64, ConfigEncryptionKid, out crypto, out error))
    {
      error = "create_config_crypto_failed:" + error;
      return false;
    }

    object decrypted;
    using (crypto)
    {
      if (!crypto.TryDecryptValue(root["payload"], out decrypted, out error))
      {
        error = "config_decrypt_failed:" + error;
        return false;
      }
    }

    var payload = BizzaValueUtil.AsStringObjectDictionary(decrypted);
    var hasReportingPercentage = root.ContainsKey("reporting_percentage");
    config = new BizzaAnalyticsSecretConfig
    {
      appId = BizzaValueUtil.AsString(payload.ContainsKey("app_id") ? payload["app_id"] : null, ""),
      ingestUrl = BizzaValueUtil.AsString(payload.ContainsKey("ingest_url") ? payload["ingest_url"] : null, ""),
      ingestToken = BizzaValueUtil.AsString(payload.ContainsKey("ingest_token") ? payload["ingest_token"] : null, ""),
      encryptionKeyBase64 = BizzaValueUtil.AsString(payload.ContainsKey("encryption_key_base64") ? payload["encryption_key_base64"] : null, ""),
      encryptionKid = BizzaValueUtil.AsString(payload.ContainsKey("encryption_kid") ? payload["encryption_kid"] : null, ""),
      release = BizzaValueUtil.AsBool(payload.ContainsKey("release") ? payload["release"] : null, false),
      reportingPercentage = BizzaAnalyticsSampling.NormalizePercentage(
        (float)BizzaValueUtil.AsDouble(
          hasReportingPercentage ? root["reporting_percentage"] : null,
          100d)),
      hasReportingPercentage = hasReportingPercentage,
    };

    if (!config.HasRequiredValues)
    {
      error = "config_required_value_missing";
      return false;
    }

    return true;
  }
}
