using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class BizzaAnalyticsWindow : EditorWindow
{
  private const string AppIdPrefsKey = "BizzaAnalytics.Editor.AppId";
  private const string IngestUrlPrefsKey = "BizzaAnalytics.Editor.IngestUrl";
  private const string IngestTokenPrefsKey = "BizzaAnalytics.Editor.IngestToken";

  [SerializeField] private Vector2 _scrollPosition;

  // Config drafts are reloaded from the encrypted text after a domain reload.
  [NonSerialized] private string _appId = "";
  [NonSerialized] private string _ingestUrl = "";
  [NonSerialized] private string _ingestToken = "";
  [NonSerialized] private string _encryptionKeyBase64 = "";
  [NonSerialized] private string _encryptionKid = "";
  [NonSerialized] private bool _release;
  [NonSerialized] private float _reportingPercentage = 100f;

  [MenuItem("Tools/BizzaAnalytics")]
  private static void ShowWindow()
  {
    var window = GetWindow<BizzaAnalyticsWindow>("BizzaAnalytics");
    window.minSize = new Vector2(480f, 420f);
    window.Show();
  }

  private void OnEnable()
  {
    LoadEditorValues();
    EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
  }

  private void OnDisable()
  {
    EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
  }

  private void OnPlayModeStateChanged(PlayModeStateChange state)
  {
    Repaint();
  }

  private void OnGUI()
  {
    if (EditorApplication.isPlayingOrWillChangePlaymode)
    {
      EditorGUILayout.HelpBox(
        "Configuration is available in Edit Mode. Runtime reads the exported encrypted config.",
        MessageType.Info);
      return;
    }

    using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
    {
      _scrollPosition = scroll.scrollPosition;
      DrawConfigFields();
    }
  }

  private void DrawConfigFields()
  {
    EditorGUILayout.LabelField("Application Config", EditorStyles.boldLabel);
    EditorGUILayout.HelpBox(
      "Configure and export the application here. A scene Agent is not required to edit this config.",
      MessageType.Info);

    EditorGUI.BeginChangeCheck();
    _appId = EditorGUILayout.TextField("App Id", _appId);
    _ingestUrl = EditorGUILayout.TextField("Ingest Url", _ingestUrl);
    _ingestToken = EditorGUILayout.TextField("Ingest Token", _ingestToken);
    if (EditorGUI.EndChangeCheck()) SaveEditorValues();

    _reportingPercentage = EditorGUILayout.Slider("Reporting Percentage", _reportingPercentage, 0f, 100f);
    _release = EditorGUILayout.Toggle(
      new GUIContent("Release", "Enable analytics reporting. Disabled by default."), _release);
    _encryptionKeyBase64 = EditorGUILayout.TextField("Encryption Key Base 64", _encryptionKeyBase64);
    _encryptionKid = EditorGUILayout.TextField("Encryption Kid", _encryptionKid);

    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
    {
      EditorGUILayout.LabelField("Encrypted Config Text", EditorStyles.boldLabel);
      EditorGUILayout.SelectableLabel(BizzaAnalyticsSecretConfig.ResourceAssetPath,
        EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
      EditorGUILayout.HelpBox(
        "Click Export Encrypted Text to apply config changes. Unexported Release, reporting percentage and encryption settings are lost when this window closes or scripts reload.",
        MessageType.Info);

      using (new EditorGUILayout.HorizontalScope())
      {
        if (GUILayout.Button("Export Encrypted Text")) ExportEncryptedConfig();
        if (GUILayout.Button("Reload Text")) LoadConfigAssetValues(true);
      }
      if (GUILayout.Button("Ping Config Asset")) PingConfigAsset();
    }
  }

  private static float ResolveLegacyReportingPercentage()
  {
    // Older configs without reporting_percentage use the component's saved value.
    var selected = Selection.activeGameObject;
    var agent = selected != null ? selected.GetComponent<BizzaAnalyticsAgent>() : null;
    if (agent != null) return agent.ReportingPercentage;
    var agents = UnityEngine.Object.FindObjectsOfType<BizzaAnalyticsAgent>(true);
    return agents.Length == 1 ? agents[0].ReportingPercentage : 100f;
  }

  private void ExportEncryptedConfig()
  {
    SaveEditorValues();
    var config = new BizzaAnalyticsSecretConfig
    {
      appId = _appId,
      ingestUrl = _ingestUrl,
      ingestToken = _ingestToken,
    };
    if (!config.HasRequiredValues)
    {
      EditorUtility.DisplayDialog("BizzaAnalytics", "App Id, Ingest Url and Ingest Token are required.", "OK");
      return;
    }

    string text;
    string error;
    if (!BizzaAnalyticsSecretConfig.TryCreateEncryptedText(
          _appId, _ingestUrl, _ingestToken, _reportingPercentage,
          _encryptionKeyBase64, _encryptionKid, _release, out text, out error))
    {
      EditorUtility.DisplayDialog("BizzaAnalytics", "Failed to export encrypted text: " + error, "OK");
      return;
    }

    var path = BizzaAnalyticsSecretConfig.ResourceAssetPath;
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllText(path, text, new UTF8Encoding(false));
      AssetDatabase.ImportAsset(path);
      AssetDatabase.Refresh();
    }
    catch (Exception ex)
    {
      EditorUtility.DisplayDialog("BizzaAnalytics", "Failed to write encrypted config: " + ex.Message, "OK");
      return;
    }
    PingConfigAsset();
    EditorUtility.DisplayDialog("BizzaAnalytics", "Encrypted text exported to:\n" + path, "OK");
  }

  private void LoadEditorValues()
  {
    if (LoadConfigAssetValues(false)) return;
    _appId = EditorPrefs.GetString(AppIdPrefsKey, "");
    _ingestUrl = EditorPrefs.GetString(IngestUrlPrefsKey, "");
    _ingestToken = EditorPrefs.GetString(IngestTokenPrefsKey, "");
    _reportingPercentage = ResolveLegacyReportingPercentage();
  }

  private void SaveEditorValues()
  {
    EditorPrefs.SetString(AppIdPrefsKey, _appId ?? "");
    EditorPrefs.SetString(IngestUrlPrefsKey, _ingestUrl ?? "");
    EditorPrefs.SetString(IngestTokenPrefsKey, _ingestToken ?? "");
  }

  private bool LoadConfigAssetValues(bool showDialog)
  {
    var path = BizzaAnalyticsSecretConfig.ResourceAssetPath;
    if (!File.Exists(path))
    {
      if (showDialog)
        EditorUtility.DisplayDialog("BizzaAnalytics", "Encrypted config text was not found:\n" + path, "OK");
      return false;
    }

    string text;
    try
    {
      text = File.ReadAllText(path, Encoding.UTF8);
    }
    catch (Exception ex)
    {
      if (showDialog)
        EditorUtility.DisplayDialog("BizzaAnalytics", "Failed to read encrypted config text: " + ex.Message, "OK");
      return false;
    }

    string error;
    BizzaAnalyticsSecretConfig config;
    if (!BizzaAnalyticsSecretConfig.TryReadEncryptedText(text, out config, out error))
    {
      if (showDialog)
        EditorUtility.DisplayDialog("BizzaAnalytics", "Failed to read encrypted config text: " + error, "OK");
      return false;
    }

    _appId = config.appId;
    _ingestUrl = config.ingestUrl;
    _ingestToken = config.ingestToken;
    _encryptionKeyBase64 = config.encryptionKeyBase64;
    _encryptionKid = config.encryptionKid;
    _release = config.release;
    _reportingPercentage = config.hasReportingPercentage
      ? config.reportingPercentage
      : ResolveLegacyReportingPercentage();
    SaveEditorValues();

    if (showDialog)
      EditorUtility.DisplayDialog("BizzaAnalytics", "Encrypted config text loaded.", "OK");
    return true;
  }

  private static void PingConfigAsset()
  {
    var path = BizzaAnalyticsSecretConfig.ResourceAssetPath;
    var configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
    if (configAsset != null)
    {
      EditorGUIUtility.PingObject(configAsset);
      Selection.activeObject = configAsset;
      return;
    }
    EditorUtility.DisplayDialog("BizzaAnalytics",
      "Encrypted config text was not found. Export it first:\n" + path, "OK");
  }
}
