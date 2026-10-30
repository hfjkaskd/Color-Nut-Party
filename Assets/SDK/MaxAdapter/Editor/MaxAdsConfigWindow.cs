using System;
using System.IO;
using MaxAdsIntegration;
using UnityEditor;
using UnityEngine;

namespace MaxAdsIntegrationEditor
{
    // Keeps production ad-unit configuration separate from official MAX SDK settings.
    public sealed class MaxAdsConfigWindow : EditorWindow
    {
        private MaxAdsConfig config;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;
        private Vector2 scrollPosition;

        [MenuItem("Tools/MAX Adapter/Configuration")]
        public static void Open()
        {
            var window = GetWindow<MaxAdsConfigWindow>("MAX Adapter");
            window.minSize = new Vector2(480f, 280f);
            window.Show();
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnGUI()
        {
            if (config == null)
            {
                config = new MaxAdsConfig();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("MAX Rewarded & Interstitial", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            DrawPlatform("Android", config.Android);
            EditorGUILayout.Space(10f);
            DrawPlatform("iOS", config.Ios);

            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "Call MaxAdsAdapter.InitializeNow() explicitly to initialize.",
                MessageType.Info);
            EditorGUILayout.HelpBox(statusMessage, statusType);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload", GUILayout.Height(30f)))
                {
                    Reload();
                }

                if (GUILayout.Button("Save Encrypted Config", GUILayout.Height(30f)))
                {
                    SaveEncryptedConfig();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawPlatform(string label, MaxAdsPlatformConfig platform)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            platform.RewardedAdUnitId = EditorGUILayout.TextField(
                "Rewarded Ad Unit ID",
                platform.RewardedAdUnitId);
            platform.InterstitialAdUnitId = EditorGUILayout.TextField(
                "Interstitial Ad Unit ID",
                platform.InterstitialAdUnitId);
        }

        private void Reload()
        {
            var fullPath = AssetPathToFullPath(MaxAdsConfigCrypto.AssetPath);
            if (!File.Exists(fullPath))
            {
                config = new MaxAdsConfig();
                statusMessage =
                    $"No encrypted config exists yet. Saving will create {MaxAdsConfigCrypto.AssetPath}.";
                statusType = MessageType.Info;
                Repaint();
                return;
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                if (!MaxAdsConfigCrypto.TryDecrypt(bytes, out config, out var error))
                {
                    config = new MaxAdsConfig();
                    statusMessage = $"Encrypted config could not be read: {error}";
                    statusType = MessageType.Error;
                    Repaint();
                    return;
                }

                statusMessage = $"Loaded encrypted config from {MaxAdsConfigCrypto.AssetPath}.";
                statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                config = new MaxAdsConfig();
                statusMessage = $"Encrypted config could not be read: {exception.Message}";
                statusType = MessageType.Error;
            }

            Repaint();
        }

        private void SaveEncryptedConfig()
        {
            try
            {
                var encryptedBytes = MaxAdsConfigCrypto.Encrypt(config);
                var fullPath = AssetPathToFullPath(MaxAdsConfigCrypto.AssetPath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                WriteAtomically(fullPath, encryptedBytes);

                AssetDatabase.ImportAsset(
                    MaxAdsConfigCrypto.AssetPath,
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                statusMessage =
                    $"Encrypted MAX config saved to {MaxAdsConfigCrypto.AssetPath}.";
                statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                statusMessage = $"Failed to save encrypted MAX config: {exception.Message}";
                statusType = MessageType.Error;
                Debug.LogError($"[MAX Ads] Config save failed: {exception.Message}");
            }
        }

        private static void WriteAtomically(string fullPath, byte[] bytes)
        {
            var temporaryPath = fullPath + ".tmp";
            File.WriteAllBytes(temporaryPath, bytes);

            if (!File.Exists(fullPath))
            {
                File.Move(temporaryPath, fullPath);
                return;
            }

            try
            {
                File.Replace(temporaryPath, fullPath, null);
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(fullPath);
                File.Move(temporaryPath, fullPath);
            }
            catch (IOException)
            {
                File.Delete(fullPath);
                File.Move(temporaryPath, fullPath);
            }
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            var relativePath = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
        }
    }
}
