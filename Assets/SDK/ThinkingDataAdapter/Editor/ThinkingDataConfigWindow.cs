using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ThinkingDataIntegration.Editor
{
    public sealed class ThinkingDataConfigWindow : EditorWindow
    {
        private ThinkingDataConfig config;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("Tools/ThinkingData Adapter/Configuration")]
        public static void Open()
        {
            var window = GetWindow<ThinkingDataConfigWindow>(
                "ThinkingData Adapter");
            window.minSize = new Vector2(520f, 245f);
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
                config = new ThinkingDataConfig();
            }

            EditorGUILayout.LabelField(
                "ThinkingData Analytics",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            config.AppCode = EditorGUILayout.TextField(
                "App Code",
                config.AppCode);
            config.AppId = EditorGUILayout.TextField("App ID", config.AppId);
            config.ServerUrl = EditorGUILayout.TextField(
                "Server URL",
                config.ServerUrl);

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "Call ThinkingDataAdapter.InitializeNow() explicitly to initialize. " +
                "Initialization enables all ThinkingData automatic tracking events.",
                MessageType.Info);
            EditorGUILayout.HelpBox(statusMessage, statusType);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload", GUILayout.Height(30f)))
                {
                    Reload();
                }

                if (GUILayout.Button(
                        "Save Encrypted Config",
                        GUILayout.Height(30f)))
                {
                    SaveEncryptedConfig();
                }
            }
        }

        private void Reload()
        {
            var fullPath = AssetPathToFullPath(
                ThinkingDataConfigCrypto.AssetPath);
            var status = ThinkingDataConfigStorage.Load(
                fullPath,
                out config,
                out var error);

            switch (status)
            {
                case ThinkingDataConfigLoadStatus.Loaded:
                    statusMessage =
                        $"Loaded encrypted config from {ThinkingDataConfigCrypto.AssetPath}.";
                    statusType = MessageType.Info;
                    break;

                case ThinkingDataConfigLoadStatus.Invalid:
                    statusMessage =
                        $"Encrypted config could not be read: {error}";
                    statusType = MessageType.Error;
                    break;

                default:
                    config = new ThinkingDataConfig();
                    statusMessage =
                        $"No encrypted config exists yet. Saving will create {ThinkingDataConfigCrypto.AssetPath}.";
                    statusType = MessageType.Info;
                    break;
            }

            Repaint();
        }

        private void SaveEncryptedConfig()
        {
            config = config ?? new ThinkingDataConfig();

            try
            {
                var fullPath = AssetPathToFullPath(
                    ThinkingDataConfigCrypto.AssetPath);
                var writeStatus = ThinkingDataConfigStorage.Apply(
                    fullPath,
                    config,
                    out var error);

                switch (writeStatus)
                {
                    case ThinkingDataConfigWriteStatus.Saved:
                        AssetDatabase.ImportAsset(
                            ThinkingDataConfigCrypto.AssetPath,
                            ImportAssetOptions.ForceUpdate);
                        AssetDatabase.SaveAssets();
                        statusMessage =
                            $"Encrypted ThinkingData config saved to {ThinkingDataConfigCrypto.AssetPath}.";
                        statusType = MessageType.Info;
                        break;

                    case ThinkingDataConfigWriteStatus.Deleted:
                        config = new ThinkingDataConfig();
                        AssetDatabase.Refresh(
                            ImportAssetOptions.ForceUpdate);
                        statusMessage =
                            "Encrypted ThinkingData config was cleared.";
                        statusType = MessageType.Info;
                        break;

                    default:
                        statusMessage = error;
                        statusType = MessageType.Error;
                        break;
                }
            }
            catch (Exception exception)
            {
                statusMessage =
                    $"Failed to save encrypted ThinkingData config: {exception.Message}";
                statusType = MessageType.Error;
                Debug.LogError(
                    $"[ThinkingData] Config save failed ({exception.GetType().Name}).");
            }
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            var relativePath = assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, relativePath));
        }
    }
}
