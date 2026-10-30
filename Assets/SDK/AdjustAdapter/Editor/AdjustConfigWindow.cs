using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdjustIntegration.Editor
{
    public sealed class AdjustConfigWindow : EditorWindow
    {
        private AdjustIntegrationConfig config;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("Tools/Adjust Adapter/Configuration")]
        public static void Open()
        {
            AdjustConfigWindow window =
                GetWindow<AdjustConfigWindow>("Adjust Adapter");
            window.minSize = new Vector2(540f, 300f);
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
                config = new AdjustIntegrationConfig();
            }

            EditorGUILayout.LabelField(
                "Adjust Production Configuration",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            config.AppToken = EditorGUILayout.TextField(
                "App Token",
                config.AppToken);
            config.Ipu3EventToken = EditorGUILayout.TextField(
                "IPU 3 Event Token",
                config.Ipu3EventToken);
            config.Ipu5EventToken = EditorGUILayout.TextField(
                "IPU 5 Event Token",
                config.Ipu5EventToken);
            config.Ipu10EventToken = EditorGUILayout.TextField(
                "IPU 10 Event Token",
                config.Ipu10EventToken);

            EditorGUILayout.Space(10f);
            EditorGUILayout.HelpBox(
                "The runtime always initializes Adjust in Production. " +
                "Pass global callback parameters to " +
                "AdjustAdapter.InitializeNow(parameters), or call " +
                "SetGlobalCallbackParameter before InitializeNow().",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "RecordAdImpression() persists a per-install display count " +
                "and sends the configured milestone events at 3, 5, and 10.",
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
            string fullPath = AssetPathToFullPath(
                AdjustConfigCrypto.AssetPath);
            string error;
            AdjustConfigLoadStatus status = AdjustConfigStorage.Load(
                fullPath,
                out config,
                out error);

            switch (status)
            {
                case AdjustConfigLoadStatus.Loaded:
                    statusMessage =
                        "Loaded encrypted config from " +
                        AdjustConfigCrypto.AssetPath + ".";
                    statusType = MessageType.Info;
                    break;

                case AdjustConfigLoadStatus.Invalid:
                    statusMessage =
                        "Encrypted config could not be read: " + error;
                    statusType = MessageType.Error;
                    break;

                default:
                    config = new AdjustIntegrationConfig();
                    statusMessage =
                        "No encrypted config exists yet. Saving will create " +
                        AdjustConfigCrypto.AssetPath + ".";
                    statusType = MessageType.Info;
                    break;
            }

            Repaint();
        }

        private void SaveEncryptedConfig()
        {
            config = config ?? new AdjustIntegrationConfig();

            try
            {
                string fullPath = AssetPathToFullPath(
                    AdjustConfigCrypto.AssetPath);
                string error;
                AdjustConfigWriteStatus writeStatus =
                    AdjustConfigStorage.Apply(
                        fullPath,
                        config,
                        out error);

                switch (writeStatus)
                {
                    case AdjustConfigWriteStatus.Saved:
                        AssetDatabase.ImportAsset(
                            AdjustConfigCrypto.AssetPath,
                            ImportAssetOptions.ForceUpdate);
                        AssetDatabase.SaveAssets();
                        statusMessage =
                            "Encrypted Adjust config saved to " +
                            AdjustConfigCrypto.AssetPath + ".";
                        statusType = MessageType.Info;
                        break;

                    case AdjustConfigWriteStatus.Deleted:
                        config = new AdjustIntegrationConfig();
                        AssetDatabase.Refresh(
                            ImportAssetOptions.ForceUpdate);
                        statusMessage =
                            "Encrypted Adjust config was cleared.";
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
                    "Failed to save encrypted Adjust config: " +
                    exception.Message;
                statusType = MessageType.Error;
                Debug.LogError(
                    "[AdjustIntegration] Config save failed (" +
                    exception.GetType().Name + ").");
            }
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string relativePath =
                assetPath.Substring("Assets/".Length);
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, relativePath));
        }
    }
}
