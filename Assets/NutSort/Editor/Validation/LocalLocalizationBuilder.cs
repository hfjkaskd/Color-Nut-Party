using System;
using System.IO;
using NutSort.Content;
using NutSort.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace NutSort.Validation
{
    public static class LocalLocalizationBuilder
    {
        public static void Run()
        {
            try { Apply(); Debug.Log("LOCALIZATION_PREFABS_PASS"); EditorApplication.Exit(0); }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void Apply()
        {
            LocalLocalizationFontBuilder.Build();
            var settings = Resources.Load<OriginalTableSettings>("Configuration/OriginalTables");
            var tables = new OriginalTables(settings);
            LocalUiLocalization.Configure("en", tables.Text);
            LocalEarningPanelsBuilder.BuildMessages();
            int prefabs = 0, labels = 0;
            foreach (string path in Directory.GetFiles("Assets/Resources/prefabs", "*.prefab", SearchOption.AllDirectories))
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<LocalEarningSequencePanel>() != null)
                        LocalEarningPanelsBuilder.ConfigureLanguageNeutralBonusIcons(root);
                    TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                    if (texts.Length == 0) continue;
                    foreach (TMP_Text label in texts)
                    {
                        var binding = label.GetComponent<LocalLocalizedText>();
                        if (binding == null) binding = label.gameObject.AddComponent<LocalLocalizedText>();
                        var serialized = new SerializedObject(binding);
                        serialized.FindProperty("label").objectReferenceValue = label;
                        string source = label.text;
                        // Formatted/dynamic values remain owned by the runtime panel, never freeze a sample value.
                        bool isStatic = !string.IsNullOrEmpty(source) && source.IndexOf('{') < 0 && LocalUiLocalization.HasTranslation(source);
                        serialized.FindProperty("englishSource").stringValue = isStatic ? source : string.Empty;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        // Bound typography lives in the prefab and may be tuned by art/design.
                        if (label.rectTransform.rect.width > 0 && label.rectTransform.rect.height > 0)
                        {
                            float maximum = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
                            if (maximum > 0)
                            {
                                label.enableAutoSizing = true;
                                label.fontSizeMax = maximum;
                                label.fontSizeMin = Mathf.Min(label.fontSizeMin > 0 ? label.fontSizeMin : maximum, maximum * .55f);
                                label.overflowMode = TextOverflowModes.Overflow;
                            }
                        }
                        labels++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Localized prefab count=" + prefabs + "; text bindings=" + labels);
        }
    }
}
