using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // One-time prefab authoring; the runtime only displays its configured guide.
    public static class LocalGuideScrewBuilder
    {
        private const string PrefabPath = "Assets/Resources/prefabs/panels/NewbieGuidePanel.prefab";
        private const string IconPath = "Assets/Resources/atlas/newbieguidepanel/yellow-screw.png";

        public static void Rebuild()
        {
            try
            {
                AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Guide screw artwork is missing.");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                // Keep the generated PNG intact; the import rectangle removes
                // transparent padding so the icon uses its reserved UI space.
                importer.spritesheet = new[] { new SpriteMetaData {
                    name = "yellow-screw", rect = new Rect(276, 70, 658, 1111),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                } };
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
                if (sprite == null) throw new InvalidOperationException("Guide screw sprite did not import.");

                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    var tip = (RectTransform)root.transform.Find("Pos/Tip");
                    var label = tip.Find("Value").GetComponent<TextMeshProUGUI>();
                    tip.sizeDelta = new Vector2(900, 220);
                    var fitter = label.GetComponent<ContentSizeFitter>();
                    if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                    label.rectTransform.pivot = new Vector2(.5f, .5f);
                    label.rectTransform.anchoredPosition = new Vector2(65, 0);
                    label.rectTransform.sizeDelta = new Vector2(720, 174);
                    label.enableWordWrapping = true;
                    label.enableAutoSizing = true;
                    label.fontSize = label.fontSizeMax = 42;
                    label.fontSizeMin = 30;
                    label.alignment = TextAlignmentOptions.MidlineGeoAligned;
                    label.raycastTarget = false;

                    Transform old = tip.Find("YellowScrewDecoration");
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    var icon = new GameObject("YellowScrewDecoration", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    icon.layer = tip.gameObject.layer;
                    icon.transform.SetParent(tip, false);
                    var rect = (RectTransform)icon.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(-375, 0);
                    rect.sizeDelta = new Vector2(112, 184);
                    var image = icon.GetComponent<Image>();
                    image.sprite = sprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.SaveAssets();
                Debug.Log("LOCAL_GUIDE_SCREW_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
