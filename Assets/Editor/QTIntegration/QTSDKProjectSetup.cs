using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AdjustIntegration;
using MaxAdsIntegration;
using NutSort.Integration;
using ThinkingDataIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time Editor configuration; shipped initialization remains in the supplied SDK.</summary>
public static class QTSDKProjectSetup
{
    private const string InputPath = "Docs/SDKIntegration/QT-20261009/configuration-input.json";
    private const string ReportPath = "Docs/SDKIntegration/QT-20261009/configuration-validation.json";

    public static void Run()
    {
        var input = JsonUtility.FromJson<ConfigurationInput>(File.ReadAllText(InputPath));
        Require(input != null, "Configuration input is missing.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Setup requires Edit mode.");
        Require(!string.IsNullOrWhiteSpace(input.productName) &&
                !string.IsNullOrWhiteSpace(input.packageName) &&
                !string.IsNullOrWhiteSpace(input.version), "Application parameters are incomplete.");
        Require(input.versionCode > 0, "Android version code must be positive.");

        PlayerSettings.productName = input.productName;
        PlayerSettings.bundleVersion = input.version;
        PlayerSettings.Android.bundleVersionCode = input.versionCode;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, input.packageName);

        var max = new MaxAdsConfig
        {
            Android = new MaxAdsPlatformConfig
            {
                RewardedAdUnitId = input.rewardedAdUnitId,
                InterstitialAdUnitId = input.interstitialAdUnitId
            },
            // No iOS identifiers were supplied; never reuse Android units on iOS.
            Ios = new MaxAdsPlatformConfig()
        };
        Require(!string.IsNullOrWhiteSpace(max.Android.RewardedAdUnitId) &&
                !string.IsNullOrWhiteSpace(max.Android.InterstitialAdUnitId), "MAX ad units are incomplete.");
        WriteConfig(MaxAdsConfigCrypto.AssetPath, MaxAdsConfigCrypto.Encrypt(max));

        var adjust = new AdjustIntegrationConfig
        {
            AppToken = input.adjustAppToken,
            Ipu3EventToken = input.adjustIpu3,
            Ipu5EventToken = input.adjustIpu5,
            Ipu10EventToken = input.adjustIpu10
        };
        Require(adjust.TryValidate(out _), "Adjust parameters are incomplete.");
        WriteConfig(AdjustConfigCrypto.AssetPath, AdjustConfigCrypto.Encrypt(adjust));

        var thinking = new ThinkingDataConfig
        {
            AppCode = input.thinkingAppCode,
            AppId = input.thinkingAppId,
            ServerUrl = input.thinkingServerUrl
        };
        Require(thinking.TryValidate(out _), "ThinkingData parameters are incomplete.");
        WriteConfig(ThinkingDataConfigCrypto.AssetPath, ThinkingDataConfigCrypto.Encrypt(thinking));

        Require(!string.IsNullOrWhiteSpace(input.maxSdkKey) &&
                !string.IsNullOrWhiteSpace(input.adMobAndroidAppId), "MAX settings are incomplete.");
        var officialMax = AppLovinSettings.Instance;
        Require(officialMax != null, "Official MAX settings could not be created.");
        officialMax.SdkKey = input.maxSdkKey;
        officialMax.AdMobAndroidAppId = input.adMobAndroidAppId;
        officialMax.SaveAsync();
        EditorUtility.SetDirty(officialMax);

        string bizzaPath = string.Empty;
        BizzaDefaults bizzaDefaults = null;
        if (!string.IsNullOrWhiteSpace(input.bizzaAppId))
        {
            bizzaDefaults = ReadBizzaDefaults(input.bizzaDefaultsDocument, input.bizzaIngestUrl);
            Require(BizzaAnalyticsSecretConfig.TryCreateEncryptedText(
                input.bizzaAppId, bizzaDefaults.url, bizzaDefaults.token, 100f,
                bizzaDefaults.encryptionKey, bizzaDefaults.encryptionKid, true,
                out var encryptedText, out _), "Bizza encrypted configuration could not be generated.");
            bizzaPath = BizzaAnalyticsSecretConfig.GetResourceAssetPath(input.productName);
            WriteConfig(bizzaPath, new UTF8Encoding(false).GetBytes(encryptedText));
        }

        string scenePath = GetEntryScenePath();
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject sdkNode = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "SDK") continue;
            Require(sdkNode == null, "Multiple root SDK objects exist in the entry scene.");
            sdkNode = root;
        }
        if (sdkNode == null)
        {
            sdkNode = new GameObject("SDK");
            SceneManager.MoveGameObjectToScene(sdkNode, scene);
        }
        sdkNode.SetActive(true);
        var adapter = GetOrAdd<SDKAdapter>(sdkNode);
        adapter.enabled = true;
        SetBoolean(adapter, "autoInitialize", true);

        BizzaAnalyticsAgent bizza = null;
        if (!string.IsNullOrWhiteSpace(input.bizzaAppId))
        {
            bizza = GetOrAdd<BizzaAnalyticsAgent>(sdkNode);
            bizza.enabled = true;
            var serializedBizza = new SerializedObject(bizza);
            Property(serializedBizza, "autoInitialize").boolValue = true;
            Property(serializedBizza, "dontDestroyOnLoad").boolValue = true;
            Property(serializedBizza, "encryptionEnabled").boolValue = true;
            Property(serializedBizza, "reportingPercentage").floatValue = 100f;
            Property(serializedBizza, "logMode").boolValue = false;
            Property(serializedBizza, "verboseLog").boolValue = false;
            serializedBizza.ApplyModifiedPropertiesWithoutUndo();
        }

        var bridge = GetOrAdd<QTGameAdsBridge>(sdkNode);
        bridge.enabled = true;
        var serializedBridge = new SerializedObject(bridge);
        Property(serializedBridge, "sdkAdapter").objectReferenceValue = adapter;
        SetPlacement(serializedBridge, "undoPlacement", input.undoPlacement);
        SetPlacement(serializedBridge, "refreshPlacement", input.refreshPlacement);
        SetPlacement(serializedBridge, "addScrewPlacement", input.addScrewPlacement);
        SetPlacement(serializedBridge, "settlementInterstitialPlacement", input.settlementInterstitialPlacement);
        SetPlacement(serializedBridge, "settlementRewardedPlacement", input.settlementRewardedPlacement);
        serializedBridge.ApplyModifiedPropertiesWithoutUndo();

        Require(CountComponents<SDKAdapter>(scene) == 1, "Entry scene must contain exactly one SDKAdapter.");
        Require(CountComponents<QTGameAdsBridge>(scene) == 1, "Entry scene must contain exactly one QTGameAdsBridge.");
        Require(CountComponents<BizzaAnalyticsAgent>(scene) == (bizza != null ? 1 : 0),
            "Bizza scene component count does not match the supplied configuration.");

        EditorUtility.SetDirty(adapter);
        if (bizza != null) EditorUtility.SetDirty(bizza);
        EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Entry scene could not be saved.");
        AssetDatabase.SaveAssets();

        Require(MaxAdsConfigCrypto.TryDecrypt(File.ReadAllBytes(MaxAdsConfigCrypto.AssetPath),
                out var savedMax, out _) &&
                savedMax.Android.RewardedAdUnitId == input.rewardedAdUnitId &&
                savedMax.Android.InterstitialAdUnitId == input.interstitialAdUnitId &&
                string.IsNullOrEmpty(savedMax.Ios.RewardedAdUnitId) &&
                string.IsNullOrEmpty(savedMax.Ios.InterstitialAdUnitId), "MAX config round trip failed.");
        Require(AdjustConfigCrypto.TryDecrypt(File.ReadAllBytes(AdjustConfigCrypto.AssetPath),
                out var savedAdjust, out _) &&
                savedAdjust.AppToken == input.adjustAppToken && savedAdjust.Ipu3EventToken == input.adjustIpu3 &&
                savedAdjust.Ipu5EventToken == input.adjustIpu5 && savedAdjust.Ipu10EventToken == input.adjustIpu10,
            "Adjust config round trip failed.");
        Require(ThinkingDataConfigCrypto.TryDecrypt(File.ReadAllBytes(ThinkingDataConfigCrypto.AssetPath),
                out var savedThinking, out _) && savedThinking.AppCode == input.thinkingAppCode &&
                savedThinking.AppId == input.thinkingAppId && savedThinking.ServerUrl == input.thinkingServerUrl,
            "ThinkingData config round trip failed.");
        if (bizza != null)
        {
            Require(BizzaAnalyticsSecretConfig.TryReadEncryptedText(File.ReadAllText(bizzaPath),
                    out var savedBizza, out _) && savedBizza.appId == input.bizzaAppId &&
                    savedBizza.ingestUrl == bizzaDefaults.url && savedBizza.ingestToken == bizzaDefaults.token &&
                    savedBizza.encryptionKeyBase64 == bizzaDefaults.encryptionKey &&
                    savedBizza.encryptionKid == bizzaDefaults.encryptionKid && savedBizza.release &&
                    savedBizza.hasReportingPercentage && Mathf.Approximately(savedBizza.reportingPercentage, 100f),
                "Bizza config round trip failed.");
        }
        Require(officialMax.SdkKey == input.maxSdkKey && officialMax.AdMobAndroidAppId == input.adMobAndroidAppId,
            "Official MAX settings verification failed.");
        Require(PlayerSettings.productName == input.productName &&
                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android) == input.packageName &&
                PlayerSettings.bundleVersion == input.version &&
                PlayerSettings.Android.bundleVersionCode == input.versionCode,
            "Saved application identity or version differs from the supplied parameters.");

        // Reopen the saved scene so verification covers the serialized result.
        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        sdkNode = FindSdkRoot(scene);
        Require(sdkNode != null && sdkNode.activeSelf, "Saved SDK root is missing or inactive.");
        adapter = sdkNode.GetComponent<SDKAdapter>();
        bizza = sdkNode.GetComponent<BizzaAnalyticsAgent>();
        bridge = sdkNode.GetComponent<QTGameAdsBridge>();
        Require(adapter != null && adapter.enabled && GetBoolean(adapter, "autoInitialize"),
            "Saved SDKAdapter is not enabled with automatic initialization.");
        Require(bridge != null && bridge.enabled, "Saved ad bridge is missing or disabled.");
        serializedBridge = new SerializedObject(bridge);
        Require(Property(serializedBridge, "sdkAdapter").objectReferenceValue == adapter,
            "Saved ad bridge reference is not the entry SDKAdapter.");
        CheckPlacement(serializedBridge, "undoPlacement", input.undoPlacement);
        CheckPlacement(serializedBridge, "refreshPlacement", input.refreshPlacement);
        CheckPlacement(serializedBridge, "addScrewPlacement", input.addScrewPlacement);
        CheckPlacement(serializedBridge, "settlementInterstitialPlacement", input.settlementInterstitialPlacement);
        CheckPlacement(serializedBridge, "settlementRewardedPlacement", input.settlementRewardedPlacement);
        if (!string.IsNullOrWhiteSpace(input.bizzaAppId))
            Require(bizza != null && bizza.enabled && GetBoolean(bizza, "autoInitialize") &&
                    GetBoolean(bizza, "dontDestroyOnLoad") && GetBoolean(bizza, "encryptionEnabled"),
                "Saved Bizza component settings do not match the request.");

        var report = new ValidationReport
        {
            result = "PASS",
            validatedUtc = DateTime.UtcNow.ToString("O"),
            entryScene = scenePath,
            sdkNode = "SDK",
            productName = PlayerSettings.productName,
            packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
            version = PlayerSettings.bundleVersion,
            versionCode = PlayerSettings.Android.bundleVersionCode,
            components = bizza != null
                ? new[] { "SDKAdapter", "BizzaAnalyticsAgent", "QTGameAdsBridge" }
                : new[] { "SDKAdapter", "QTGameAdsBridge" },
            automaticInitialization = true,
            bridgeReferenceVerified = true,
            placementTags = new[] { input.undoPlacement, input.refreshPlacement, input.addScrewPlacement,
                input.settlementInterstitialPlacement, input.settlementRewardedPlacement },
            encryptedConfigs = new[] { MaxAdsConfigCrypto.AssetPath, AdjustConfigCrypto.AssetPath,
                ThinkingDataConfigCrypto.AssetPath, bizzaPath },
            allSuppliedParametersMatchedAfterDecrypt = true,
            officialMaxSettings = AssetDatabase.GetAssetPath(officialMax),
            maxKeyAndAdMobAppIdMatched = true,
            bizzaReleaseReporting = bizza != null,
            bizzaIngestHost = bizzaDefaults == null ? string.Empty : new Uri(bizzaDefaults.url).Host,
            iosConfigured = false,
            sdkSourceModifiedByThisTool = false,
            verificationScope = "Saved scene, Editor configuration and encrypted resource round trips; no ad or analytics network requests. SDK source hashes are verified separately."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        Debug.Log("QT_SDK_PROJECT_SETUP_PASS: " + ReportPath);
    }

    private static void WriteConfig(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static T GetOrAdd<T>(GameObject host) where T : Component
    {
        var existing = host.GetComponent<T>();
        return existing != null ? existing : host.AddComponent<T>();
    }

    private static SerializedProperty Property(SerializedObject target, string name)
    {
        var property = target.FindProperty(name);
        Require(property != null, "Required serialized SDK property is missing: " + name);
        return property;
    }

    private static void SetBoolean(UnityEngine.Object target, string name, bool value)
    {
        var serialized = new SerializedObject(target);
        Property(serialized, name).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool GetBoolean(UnityEngine.Object target, string name) =>
        Property(new SerializedObject(target), name).boolValue;

    private static void SetPlacement(SerializedObject bridge, string field, string value)
    {
        Require(!string.IsNullOrWhiteSpace(value), "An ad placement tag is missing.");
        Property(bridge, field).stringValue = value;
    }

    private static void CheckPlacement(SerializedObject bridge, string field, string value) =>
        Require(Property(bridge, field).stringValue == value, "Saved placement tag differs: " + field);

    private static int CountComponents<T>(Scene scene) where T : Component
    {
        int count = 0;
        foreach (var root in scene.GetRootGameObjects()) count += root.GetComponentsInChildren<T>(true).Length;
        return count;
    }

    private static GameObject FindSdkRoot(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects()) if (root.name == "SDK") return root;
        return null;
    }

    private static string GetEntryScenePath()
    {
        foreach (var entry in EditorBuildSettings.scenes)
            if (entry.enabled && File.Exists(entry.path)) return entry.path;
        throw new InvalidOperationException("There is no enabled entry scene in Build Settings.");
    }

    private static BizzaDefaults ReadBizzaDefaults(string path, string selectedUrl)
    {
        Require(!string.IsNullOrWhiteSpace(path) && File.Exists(path), "Provided Bizza defaults document is missing.");
        Require(Uri.TryCreate(selectedUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps,
            "Bizza ingest URL must be explicit HTTPS configuration.");
        // Read only fixed parameter labels. Prose in the supplied file is never executed.
        string document = File.ReadAllText(path);
        var token = Regex.Match(document, @"(?m)^ingestUrl\s*[:：]\s*" + Regex.Escape(selectedUrl) +
            @"\s*\r?\n\s*ingestToken\s*[:：]\s*([^\r\n]+)");
        var key = Regex.Match(document, @"(?m)^encryptionKeyBase64\s*[:：]\s*([^\r\n]+)");
        var kid = Regex.Match(document, @"(?m)^encryptionKid\s*[:：]\s*([^\r\n]+)");
        Require(token.Success && key.Success && kid.Success, "Bizza defaults document lacks required values.");
        return new BizzaDefaults
        {
            url = selectedUrl,
            token = token.Groups[1].Value.Trim(),
            encryptionKey = key.Groups[1].Value.Trim(),
            encryptionKid = kid.Groups[1].Value.Trim()
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class BizzaDefaults
    {
        public string url;
        public string token;
        public string encryptionKey;
        public string encryptionKid;
    }

    [Serializable]
    private sealed class ConfigurationInput
    {
        public string productName;
        public string packageName;
        public string version;
        public int versionCode;
        public string maxSdkKey;
        public string rewardedAdUnitId;
        public string interstitialAdUnitId;
        public string adMobAndroidAppId;
        public string thinkingAppId;
        public string thinkingServerUrl;
        public string thinkingAppCode;
        public string adjustAppToken;
        public string adjustIpu3;
        public string adjustIpu5;
        public string adjustIpu10;
        public string bizzaAppId;
        public string bizzaDefaultsDocument;
        public string bizzaIngestUrl;
        public string undoPlacement;
        public string refreshPlacement;
        public string addScrewPlacement;
        public string settlementInterstitialPlacement;
        public string settlementRewardedPlacement;
    }

    [Serializable]
    private sealed class ValidationReport
    {
        public string result;
        public string validatedUtc;
        public string entryScene;
        public string sdkNode;
        public string productName;
        public string packageName;
        public string version;
        public int versionCode;
        public string[] components;
        public bool automaticInitialization;
        public bool bridgeReferenceVerified;
        public string[] placementTags;
        public string[] encryptedConfigs;
        public bool allSuppliedParametersMatchedAfterDecrypt;
        public string officialMaxSettings;
        public bool maxKeyAndAdMobAppIdMatched;
        public bool bizzaReleaseReporting;
        public string bizzaIngestHost;
        public bool iosConfigured;
        public bool sdkSourceModifiedByThisTool;
        public string verificationScope;
    }
}
