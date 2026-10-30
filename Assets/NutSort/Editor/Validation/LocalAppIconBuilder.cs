using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace NutSort.Validation
{
    // Authoring only: Android launcher resources are generated from these Player Settings.
    public static class LocalAppIconBuilder
    {
        private const string MainIconPath = "Assets/Art/Branding/ColorNutParty-AppIcon.png";
        private const string AdaptiveBackgroundPath = "Assets/Art/Branding/ColorNutParty-AdaptiveBackground.png";
        private const string AdaptiveForegroundPath = "Assets/Art/Branding/ColorNutParty-AdaptiveForeground.png";
        private const string ReportDirectory = "Docs/APKBuild/20261013-app-icon";

        [MenuItem("Tools/Color Nut Party/Configure App Icons")]
        public static void Configure()
        {
            Texture2D mainIcon = ImportIcon(MainIconPath, false);
            Texture2D adaptiveBackground = ImportIcon(AdaptiveBackgroundPath, false);
            RequireSquare(mainIcon, MainIconPath);
            RequireSquare(adaptiveBackground, AdaptiveBackgroundPath);
            WriteClearForeground();
            Texture2D adaptiveForeground = ImportIcon(AdaptiveForegroundPath, true);

            PlayerSettings.bundleVersion = "1.0.1";
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { mainIcon }, IconKind.Any);
            var slots = new List<IconSlotReport>();
            ConfigureKind(AndroidPlatformIconKind.Legacy, "legacy", new[] { mainIcon }, slots);
            ConfigureKind(AndroidPlatformIconKind.Round, "round", new[] { mainIcon }, slots);
            // Unity 2022.3 exports layer 0 as background and layer 1 as foreground.
            // This ordering is verified from the APK's adaptive XML and PNG resources.
            // The complete padded illustration lives in the background; the clear layer
            // supplies the required foreground without covering or cropping the artwork.
            ConfigureKind(AndroidPlatformIconKind.Adaptive, "adaptive",
                new[] { adaptiveBackground, adaptiveForeground }, slots);

            Texture2D[] defaultIcons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (defaultIcons.Length == 0 || defaultIcons[0] != mainIcon)
                throw new InvalidOperationException("Default app icon was not persisted in Player Settings.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(ReportDirectory);
            var report = new IconConfigurationReport
            {
                passed = true,
                version = PlayerSettings.bundleVersion,
                packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                defaultIcon = MainIconPath,
                defaultIconWidth = mainIcon.width,
                defaultIconHeight = mainIcon.height,
                adaptiveBackground = AdaptiveBackgroundPath,
                adaptiveBackgroundWidth = adaptiveBackground.width,
                adaptiveBackgroundHeight = adaptiveBackground.height,
                adaptiveForeground = AdaptiveForegroundPath,
                slots = slots.ToArray()
            };
            File.WriteAllText(Path.Combine(ReportDirectory, "icon-configuration.json"),
                JsonUtility.ToJson(report, true));
            Debug.Log("NUT_APP_ICON_CONFIGURATION_PASS slots=" + slots.Count
                + " default=" + MainIconPath + " version=" + PlayerSettings.bundleVersion);
        }

        public static void ConfigureAndBuild()
        {
            try
            {
                Configure();
                // Preserve the configured icon settings when the build temporarily assigns signing settings.
                File.Copy("ProjectSettings/ProjectSettings.asset",
                    Path.Combine(ReportDirectory, "ProjectSettings.before.asset"), true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            LocalAndroidBuild.RunSignedRelease();
        }

        public static void RunOnly()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureKind(PlatformIconKind kind, string kindName,
            Texture2D[] layers, List<IconSlotReport> reports)
        {
            PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots.Length == 0)
                throw new InvalidOperationException("Android icon slots were not available: " + kindName);
            foreach (PlatformIcon slot in slots)
            {
                if (layers.Length < slot.minLayerCount || layers.Length > slot.maxLayerCount)
                    throw new InvalidOperationException("Unexpected Android icon layer count: " + kindName);
                slot.SetTextures(layers);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            PlatformIcon[] savedSlots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (savedSlots.Length != slots.Length)
                throw new InvalidOperationException("Android icon slot count changed: " + kindName);
            foreach (PlatformIcon slot in savedSlots)
            {
                var layerPaths = new string[layers.Length];
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    if (slot.GetTexture(layer) != layers[layer])
                        throw new InvalidOperationException("Android icon layer was not saved: " + kindName);
                    layerPaths[layer] = AssetDatabase.GetAssetPath(slot.GetTexture(layer));
                }
                reports.Add(new IconSlotReport
                {
                    kind = kindName, width = slot.width, height = slot.height, layers = layerPaths
                });
            }
        }

        private static Texture2D ImportIcon(string path, bool hasAlpha)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("App icon asset is missing.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("App icon TextureImporter is missing: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = hasAlpha;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("App icon texture did not import: " + path);
            return texture;
        }

        private static void RequireSquare(Texture2D texture, string path)
        {
            if (texture.width != texture.height || texture.width < 512)
                throw new InvalidOperationException("App icon must be square and at least 512 pixels: " + path);
        }

        private static void WriteClearForeground()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AdaptiveForegroundPath));
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                // This mechanical transparent layer contains no visual design or generated artwork.
                texture.SetPixels32(new Color32[16]);
                texture.Apply();
                File.WriteAllBytes(AdaptiveForegroundPath, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        [Serializable]
        private sealed class IconConfigurationReport
        {
            public bool passed;
            public string version;
            public string packageName;
            public string defaultIcon;
            public int defaultIconWidth;
            public int defaultIconHeight;
            public string adaptiveBackground;
            public int adaptiveBackgroundWidth;
            public int adaptiveBackgroundHeight;
            public string adaptiveForeground;
            public IconSlotReport[] slots;
        }

        [Serializable]
        private sealed class IconSlotReport
        {
            public string kind;
            public int width;
            public int height;
            public string[] layers;
        }
    }
}
