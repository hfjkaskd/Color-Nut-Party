using System;
using Newtonsoft.Json.Linq;
using NutSort.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // One-time prefab authoring. This tool never changes a scene or an existing gameplay prefab.
    public static class LocalEarningPanelsBuilder
    {
        private const string Folder = "Assets/Resources/prefabs/panels/";
        private static readonly Color Ink = new Color32(83, 43, 16, 255);
        private static readonly Color SubtleInk = new Color32(136, 91, 50, 255);

        public static void RebuildMessages()
        {
            try
            {
                BuildMessages();
                Debug.Log("EARNING_MESSAGES_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        // Targeted authoring: preserve the existing bonus, receipt and cash-flight hierarchy.
        public static void BuildMessages()
        {
            ImportMessageArtwork();
            string path = Folder + "LocalEarningSequence.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureMessages(root.GetComponent<LocalEarningSequencePanel>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void ImportMessageArtwork()
        {
            foreach (string name in new[] { "cash-banner", "portrait-banner" })
            {
                string path = "Assets/Resources/atlas/localearningmessages/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing earning message artwork: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = Vector4.zero;
                importer.npotScale = TextureImporterNPOTScale.None;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureMessages(LocalEarningSequencePanel panel)
        {
            Transform old = panel.transform.Find("Message");
            int siblingIndex = old == null ? panel.transform.childCount : old.GetSiblingIndex();
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var message = Rect("Message", panel.transform, Vector2.zero, Vector2.zero);
            message.anchorMin = Vector2.zero; message.anchorMax = Vector2.one;
            message.SetSiblingIndex(siblingIndex);
            var configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Configuration/LocalEarningFlow.json");
            if (configAsset == null) throw new InvalidOperationException("Missing earning flow captions.");
            var config = JObject.Parse(configAsset.text);
            var layouts = new[]
            {
                BuildMessageLayout(message, LocalEarningMessageStyle.Assessment, "Assessment", "cash-banner",
                    (string)config["assessmentTitle"], (string)config["assessmentBody"], false, true),
                BuildMessageLayout(message, LocalEarningMessageStyle.Activating, "Activating", "portrait-banner",
                    (string)config["activatingTitle"], (string)config["activatingBody"], true, false),
                BuildMessageLayout(message, LocalEarningMessageStyle.Activated, "Activated", "portrait-banner",
                    (string)config["activatedTitle"], (string)config["activatedBody"], true, false),
                BuildMessageLayout(message, LocalEarningMessageStyle.Summary, "Summary", "cash-banner",
                    (string)config["summaryTitle"], (string)config["summaryBody"], false, false)
            };
            message.gameObject.SetActive(false);
            var serialized = new SerializedObject(panel);
            Object(serialized, "messageGroup", message.gameObject);
            Object(serialized, "messageTitle", layouts[3].Title);
            Object(serialized, "messageBody", layouts[3].Body);
            SerializedProperty rows = serialized.FindProperty("messageLayouts");
            rows.arraySize = layouts.Length;
            for (int i = 0; i < layouts.Length; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                LocalEarningSequencePanel.MessageLayout layout = layouts[i];
                row.FindPropertyRelative("Style").intValue = (int)layout.Style;
                row.FindPropertyRelative("Root").objectReferenceValue = layout.Root;
                row.FindPropertyRelative("Artwork").objectReferenceValue = layout.Artwork;
                row.FindPropertyRelative("ArtworkPath").stringValue = layout.ArtworkPath;
                row.FindPropertyRelative("Title").objectReferenceValue = layout.Title;
                row.FindPropertyRelative("Body").objectReferenceValue = layout.Body;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LocalEarningSequencePanel.MessageLayout BuildMessageLayout(Transform parent,
            LocalEarningMessageStyle style, string name, string artworkName, string title, string body,
            bool portrait, bool bodyOnly)
        {
            string resourcesPath = "atlas/localearningmessages/" + artworkName;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/" + resourcesPath + ".png");
            if (sprite == null) throw new InvalidOperationException("Message sprite has not been imported: " + resourcesPath);
            // Full-image aspect ratio and measured blank caption regions are authored once,
            // then remain independently editable as RectTransforms in the saved prefab.
            var size = new Vector2(1040, 1040 * sprite.rect.height / sprite.rect.width);
            var root = Rect(name, parent, new Vector2(0, portrait ? 190 : 90), size);
            var artwork = Icon("Artwork", root, Vector2.zero, size, null);
            var titleLabel = Label("Title", root, new Vector2(0, portrait ? -204 : -30),
                portrait ? new Vector2(650, 248) : new Vector2(810, 60), title, portrait ? 100 : 55, portrait);
            titleLabel.fontSizeMin = portrait ? 54 : 32;
            titleLabel.gameObject.SetActive(!bodyOnly);
            if (portrait)
            {
                var you = Label("You", root, new Vector2(0, 25), new Vector2(370, 78), "You", 66, false);
                you.fontSizeMin = 36;
            }
            Vector2 bodyPosition = new Vector2(0, portrait ? -390 : bodyOnly ? -106 : -139);
            Vector2 bodySize = portrait ? new Vector2(840, 100) : bodyOnly ? new Vector2(810, 200) : new Vector2(810, 126);
            var bodyLabel = Label("Body", root, bodyPosition, bodySize, body, portrait ? 40 : bodyOnly ? 64 : 42, true);
            bodyLabel.fontSizeMin = portrait || bodyOnly ? 36 : 30;
            root.gameObject.SetActive(false);
            return new LocalEarningSequencePanel.MessageLayout
            {
                Style = style, Root = root.gameObject, Artwork = artwork,
                ArtworkPath = resourcesPath, Title = titleLabel, Body = bodyLabel
            };
        }

        public static void RebuildLuckyHeader()
        {
            try
            {
                ImportLuckyScrews();
                var root = PrefabUtility.LoadPrefabContents(Folder + "LocalLuckyReward.prefab");
                try
                {
                    ConfigureLuckyHeader(root.GetComponent<OriginalSuccessPanel>());
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + "LocalLuckyReward.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.SaveAssets();
                Debug.Log("LUCKY_HEADER_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        private static void ImportLuckyScrews()
        {
            const string path = "Assets/Resources/atlas/locallucky/screw-decoration.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing original Lucky screw artwork.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            // Keep the original PNG intact; only expose its three screw stacks.
            importer.spritesheet = new[] { new SpriteMetaData {
                name = "screw-decoration", rect = new Rect(208, 148, 475, 182),
                alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
            } };
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureLuckyHeader(OriginalSuccessPanel panel)
        {
            RectTransform title = panel.Title;
            var label = title.GetComponentInChildren<TMP_Text>(true);
            if (label == null) throw new InvalidOperationException("Lucky title label is not configured.");
            title.GetComponent<Image>().enabled = false;
            title.sizeDelta = new Vector2(896, 372);
            title.anchoredPosition = new Vector2(0, 540);
            foreach (string name in new[] { "ScrewDecoration", "Ribbon" })
            {
                Transform old = title.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var screws = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/locallucky/screw-decoration.png");
            if (screws == null) throw new InvalidOperationException("Lucky screw sprite is not imported.");
            Icon("ScrewDecoration", title, new Vector2(0, 68), new Vector2(500, 500 * screws.rect.height / screws.rect.width), screws);
            // Draw the ribbon in front of the screw bases. The title tween moves
            // all three layers together, using the existing opening animation.
            Icon("Ribbon", title, new Vector2(0, -90), new Vector2(896, 180), Skin("wood-ribbon"));
            label.rectTransform.anchoredPosition = new Vector2(0, -90);
            label.rectTransform.sizeDelta = new Vector2(720, 105);
            label.text = "Lucky Reward";
            label.transform.SetAsLastSibling();
        }

        public static void RebuildHistory()
        {
            try
            {
                ImportHistoryCard();
                BuildHistory(BuildHistoryRow());
                AssetDatabase.SaveAssets();
                Debug.Log("CASH_HISTORY_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        private static void ImportHistoryCard()
        {
            const string path = "Assets/Resources/atlas/localhistory/record-card.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing history card artwork.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // Preserve the rounded corners and wood header when sizing the record rows.
            importer.spriteBorder = new Vector4(30, 30, 30, 82);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        public static void RebuildReceipt()
        {
            try
            {
                BuildSequence();
                AssetDatabase.SaveAssets();
                Debug.Log("CASH_RECEIPT_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void RebuildActivation()
        {
            try
            {
                ImportBonusIcons();
                BuildSequence();
                AssetDatabase.SaveAssets();
                Debug.Log("REWARD_ACTIVATION_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        private static void ImportBonusIcons()
        {
            for (int i = 1; i <= 5; i++)
            {
                string path = "Assets/Resources/atlas/rewardactivation/reward" + i + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing reward icon: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = i == 3 || i == 4 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                // The source PNGs remain intact. Sprite import rectangles exclude
                // excessive effect padding so the five reward subjects share a visual size.
                if (i == 3 || i == 4)
                    importer.spritesheet = new[] { new SpriteMetaData {
                        name = "reward" + i,
                        rect = i == 3 ? new Rect(6, 26, 177, 128) : new Rect(3, 17, 190, 135),
                        alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                    } };
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        public static void Run()
        {
            try
            {
                Build();
                Debug.Log("LOCAL_EARNING_PANELS_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void Build()
        {
            ImportBonusIcons();
            ImportHistoryCard();
            BuildSequence();
            LocalCashHistoryRow row = BuildHistoryRow();
            BuildHistory(row);
            BuildHistoryEntry();
            BuildLuckyReward();
            BuildLocalVerification();
            AssetDatabase.SaveAssets();
        }

        private static Sprite Skin(string name)
        {
            var result = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/octobergarden/" + name + ".png");
            if (result == null) throw new InvalidOperationException("Missing skin sprite: " + name);
            return result;
        }

        private static Sprite Cash(string name)
        {
            var result = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/golds/" + name + ".asset");
            if (result == null) throw new InvalidOperationException("Missing cash sprite: " + name);
            return result;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Root(string name)
        {
            RectTransform root = Rect(name, null, Vector2.zero, Vector2.zero);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0, 0, 0, .65f);
            backdrop.raycastTarget = true;
            return root;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static Image Icon(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var image = Image(name, parent, position, size, sprite);
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size,
            string text, float fontSize, bool wrap = false)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MonoBehaviour/zh_Custom SDF.asset");
            if (label.font == null) throw new InvalidOperationException("Missing UI font.");
            label.text = text;
            label.color = Ink;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Min(28, fontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableWordWrapping = wrap;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var image = Image(name, parent, position, size, sprite);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static void Object(SerializedObject target, string name, UnityEngine.Object value)
            => target.FindProperty(name).objectReferenceValue = value;

        private static void Objects(SerializedObject target, string name, UnityEngine.Object[] values)
        {
            var property = target.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void BuildSequence()
        {
            ImportMessageArtwork();
            RectTransform root = Root("LocalEarningSequence");
            try
            {
                var panel = root.gameObject.AddComponent<LocalEarningSequencePanel>();
                var bonuses = Image("Bonuses", root, Vector2.zero, new Vector2(1080, 1920), Skin("background")).rectTransform;
                bonuses.anchorMin = Vector2.zero; bonuses.anchorMax = Vector2.one; bonuses.sizeDelta = Vector2.zero;
                var avatarMask = Icon("AvatarMask", bonuses, new Vector2(0, 750), new Vector2(245, 245),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/localsettings/avatar-mask.png"));
                avatarMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                Icon("Avatar", avatarMask.transform, Vector2.zero, new Vector2(256, 256),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/localsettings/avatar.png"));
                Icon("CashDecorationLeft", bonuses, new Vector2(-186, 657), new Vector2(206, 148), Cash("US3"));
                Icon("CashDecorationRight", bonuses, new Vector2(186, 657), new Vector2(206, 148),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/rewardactivation/reward4.png"));
                var playerBand = Image("PlayerRibbon", bonuses, new Vector2(0, 618), new Vector2(574, 102), Skin("wood-ribbon"));
                Label("PlayerCaption", playerBand.transform, Vector2.zero, new Vector2(450, 70), "You", 46);
                var bonusHeading = Image("Heading", bonuses, new Vector2(0, 515), new Vector2(948, 78), Skin("cream-ribbon"));
                var bonusTitle = Label("Title", bonusHeading.transform, Vector2.zero, new Vector2(848, 62), "Your reward bonuses", 43);
                var groups = new CanvasGroup[5];
                var captions = new TMP_Text[5];
                for (int i = 0; i < 5; i++)
                {
                    var row = Image("Bonus" + (i + 1), bonuses, new Vector2(0, 320 - i * 250), new Vector2(920, 222), Skin("cream-card"));
                    groups[i] = row.gameObject.AddComponent<CanvasGroup>();
                    groups[i].blocksRaycasts = false;
                    groups[i].interactable = false;
                    var badge = Image("BonusBadge", row.transform, new Vector2(0, 105), new Vector2(430, 70), Skin("wood-ribbon"));
                    Label("Value", badge.transform, Vector2.zero, new Vector2(336, 56), "Bonus " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 37);
                    var icon = Icon("Icon", row.transform, new Vector2(0, 9), new Vector2(208, 112),
                        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/rewardactivation/reward" + (i + 1) + ".png"));
                    ConfigureLanguageNeutralBonusIcon(icon, i);
                    captions[i] = Label("Caption", row.transform, new Vector2(0, -78), new Vector2(820, 52), "Reward benefit", 38);
                }

                // The cash crest and scroll are authored as one image. Preserve its
                // proportions and keep the live amount inside the clear scroll face.
                var receiptBanner = Skin("success-title");
                var receiptSize = new Vector2(1000, 1000 * receiptBanner.rect.height / receiptBanner.rect.width);
                var receipt = Rect("Receipt", root, Vector2.zero, receiptSize);
                Icon("Banner", receipt, Vector2.zero, receiptSize, receiptBanner);
                var amount = Label("Amount", receipt, new Vector2(0, -58), new Vector2(740, 134), "$500", 112);
                amount.alignment = TextAlignmentOptions.MidlineGeoAligned;
                var icons = new RectTransform[5];
                for (int i = 0; i < icons.Length; i++)
                {
                    icons[i] = Icon("CashFlight" + (i + 1), root, new Vector2(-28 + i * 14, 92 + i * 10), new Vector2(88, 70), Cash("US1")).rectTransform;
                    icons[i].gameObject.SetActive(false);
                }
                bonuses.gameObject.SetActive(false);
                receipt.gameObject.SetActive(false);
                var serialized = new SerializedObject(panel);
                Object(serialized, "bonusesGroup", bonuses.gameObject);
                Object(serialized, "receiptGroup", receipt.gameObject);
                Object(serialized, "bonusesTitle", bonusTitle);
                Object(serialized, "receiptAmount", amount);
                Objects(serialized, "bonusRows", groups);
                Objects(serialized, "bonusCaptions", captions);
                Objects(serialized, "flightIcons", icons);
                serialized.FindProperty("bonusRevealInterval").floatValue = .35f;
                serialized.FindProperty("bonusPulseDuration").floatValue = .5f;
                serialized.FindProperty("bonusPulseCurve").animationCurveValue = new AnimationCurve(
                    new Keyframe(0, 1, 0, 0), new Keyframe(.35f, 1.035f, 0, 0), new Keyframe(1, 1, 0, 0));
                serialized.FindProperty("flightDuration").floatValue = .65f;
                serialized.FindProperty("flightStagger").floatValue = .075f;
                serialized.FindProperty("flightCurve").animationCurveValue = AnimationCurve.EaseInOut(0, 0, 1, 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ConfigureMessages(panel);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "LocalEarningSequence.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        public static void ConfigureLanguageNeutralBonusIcons(GameObject root)
        {
            foreach (int index in new[] { 0, 4 })
            {
                var icon = root.transform.Find("Bonuses/Bonus" + (index + 1) + "/Icon");
                if (icon != null) ConfigureLanguageNeutralBonusIcon(icon.GetComponent<Image>(), index);
            }
        }

        private static void ConfigureLanguageNeutralBonusIcon(Image icon, int index)
        {
            if (index == 0) icon.sprite = Cash("US3");
            if (index != 4) return;
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/mainpanel/hintlevel_bg4.png");
            icon.rectTransform.sizeDelta = new Vector2(96, 112);
            var old = icon.transform.Find("LevelNumber");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Label("LevelNumber", icon.transform, new Vector2(0, -13), new Vector2(66, 62), "5", 48);
        }

        private static LocalCashHistoryRow BuildHistoryRow()
        {
            var root = Rect("LocalCashHistoryRow", null, Vector2.zero, new Vector2(820, 246));
            try
            {
                var row = root.gameObject.AddComponent<LocalCashHistoryRow>();
                var layout = root.gameObject.AddComponent<LayoutElement>();
                layout.preferredWidth = 820;
                layout.preferredHeight = 246;
                var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/localhistory/record-card.png");
                if (background == null) throw new InvalidOperationException("Missing imported history card.");
                var readyVisual = Image("ReadyVisual", root, Vector2.zero, root.sizeDelta, background);
                var pendingVisual = Image("PendingVisual", root, Vector2.zero, root.sizeDelta, background);
                pendingVisual.color = new Color32(219, 225, 231, 255);
                pendingVisual.gameObject.SetActive(false);
                var title = Label("Title", root, new Vector2(-173, 82), new Vector2(400, 60), "Cash Reward #1", 38);
                title.alignment = TextAlignmentOptions.Left;
                var time = Label("Time", root, new Vector2(-173, -35), new Vector2(400, 76), "10/12/2026 7:24 AM", 29, true);
                time.alignment = TextAlignmentOptions.Left;
                time.color = SubtleInk;
                var amount = Label("Amount", root, new Vector2(240, -35), new Vector2(266, 100), "$0.40", 66);
                amount.alignment = TextAlignmentOptions.Right;
                amount.color = new Color32(28, 137, 55, 255);
                // A text Button owns both its rendered caption and hit rectangle.
                var caption = Label("Action", root, new Vector2(268, 82), new Vector2(210, 72), "Withdraw", 36);
                caption.color = new Color32(28, 115, 44, 255);
                caption.fontStyle = FontStyles.Underline;
                caption.alignment = TextAlignmentOptions.Right;
                caption.raycastTarget = true;
                var action = caption.gameObject.AddComponent<Button>();
                action.targetGraphic = caption;
                action.navigation = new Navigation { mode = Navigation.Mode.None };
                var status = Label("Status", root, new Vector2(219, 82), new Vector2(308, 60), "Pending - not paid", 29);
                status.alignment = TextAlignmentOptions.Right;
                status.color = new Color32(99, 89, 76, 255);
                status.gameObject.SetActive(false);
                var serialized = new SerializedObject(row);
                Object(serialized, "titleLabel", title);
                Object(serialized, "timeLabel", time);
                Object(serialized, "amountLabel", amount);
                Object(serialized, "actionLabel", caption);
                Object(serialized, "actionButton", action);
                Object(serialized, "statusLabel", status);
                Object(serialized, "readyVisual", readyVisual.gameObject);
                Object(serialized, "pendingVisual", pendingVisual.gameObject);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var asset = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "LocalCashHistoryRow.prefab");
                return asset.GetComponent<LocalCashHistoryRow>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        private static void BuildHistory(LocalCashHistoryRow rowPrefab)
        {
            RectTransform root = Root("LocalCashHistory");
            try
            {
                var panel = root.gameObject.AddComponent<LocalCashHistoryPanel>();
                var card = Image("Card", root, Vector2.zero, new Vector2(940, 1360), Skin("panel")).rectTransform;
                var heading = Image("Heading", card, new Vector2(0, 620), new Vector2(976, 186), Skin("wood-ribbon"));
                var title = Label("Title", heading.transform, new Vector2(-25, 8), new Vector2(720, 90), "Withdrawal History", 54);
                var close = Button("Close", card, new Vector2(418, 645), new Vector2(104, 104), Skin("close"));
                var balanceGroup = Image("Balance", card, new Vector2(0, 378), new Vector2(820, 260), Skin("cream-card"));
                var balanceCaption = Label("Caption", balanceGroup.transform, new Vector2(0, 76), new Vector2(710, 52), "Current cash balance", 34);
                balanceCaption.color = SubtleInk;
                Icon("Bank", balanceGroup.transform, new Vector2(-230, -32), new Vector2(136, 136),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/atlas/txrecordpanel/bg1.png"));
                var balance = Label("Value", balanceGroup.transform, new Vector2(83, -32), new Vector2(430, 142), "$500", 108);
                balance.color = new Color32(28, 137, 55, 255);
                balance.alignment = TextAlignmentOptions.MidlineGeoAligned;
                var parent = Rect("Rows", card, new Vector2(0, -172), new Vector2(820, 778));
                var layout = parent.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.spacing = 20;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                var serialized = new SerializedObject(panel);
                Object(serialized, "titleLabel", title);
                Object(serialized, "balanceLabel", balance);
                Object(serialized, "closeButton", close);
                Object(serialized, "rowParent", parent);
                Object(serialized, "rowPrefab", rowPrefab);
                serialized.FindProperty("maximumRows").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "LocalCashHistory.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        private static void BuildHistoryEntry()
        {
            // A separate local-only strip keeps the recovered native marquee unchanged.
            var root = PrefabUtility.LoadPrefabContents("Assets/Resources/prefabs/playerinfo/PMDSimple.prefab");
            try
            {
                root.name = "LocalCashHistoryEntry";
                var visual = root.transform.Find("Scroll View").GetComponent<Image>();
                var button = visual.GetComponent<Button>();
                if (button == null) button = visual.gameObject.AddComponent<Button>();
                visual.raycastTarget = true;
                button.targetGraphic = visual;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.interactable = false;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "LocalCashHistoryEntry.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string path = "Assets/Resources/prefabs/panels/TXPanel.prefab";
            root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<OriginalWithdrawalPanel>());
                serialized.FindProperty("localMarqueePath").stringValue = "Prefabs/Panels/LocalCashHistoryEntry";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildLuckyReward()
        {
            ImportLuckyScrews();
            var root = PrefabUtility.LoadPrefabContents(Folder + "SuccessPanel.prefab");
            try
            {
                root.name = "LocalLuckyReward";
                var panel = root.GetComponent<OriginalSuccessPanel>();
                var serialized = new SerializedObject(panel);
                var backdrop = (Image)serialized.FindProperty("backdrop").objectReferenceValue;
                backdrop.sprite = null;
                backdrop.color = new Color(0, 0, 0, .65f);
                root.transform.Find("bg").gameObject.SetActive(false);
                ConfigureLuckyHeader(panel);
                panel.MoreButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -430);
                panel.GetButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -630);
                panel.GetButton.GetComponentInChildren<TMP_Text>(true).color = Color.white;
                // Existing typed InitSettlement binds both actions from code.
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "LocalLuckyReward.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            root = PrefabUtility.LoadPrefabContents("Assets/Resources/prefabs/items/Item_Big.prefab");
            try
            {
                root.name = "LocalLuckyItem";
                foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))label.color=Color.white;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/prefabs/items/LocalLuckyItem.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            const string settingsPath="Assets/Resources/Configuration/LocalLuckyRewardItem.asset";
            var itemSettings=AssetDatabase.LoadAssetAtPath<OriginalRewardItemSettings>(settingsPath);
            if(itemSettings==null)
            {
                itemSettings=ScriptableObject.CreateInstance<OriginalRewardItemSettings>();
                AssetDatabase.CreateAsset(itemSettings,settingsPath);
            }
            EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<OriginalRewardItemSettings>("Assets/Resources/Configuration/OriginalRewardItem.asset"),itemSettings);
            itemSettings.name="LocalLuckyRewardItem";
            itemSettings.BigItemPath="Prefabs/Items/LocalLuckyItem";
            EditorUtility.SetDirty(itemSettings);
        }

        private static void BuildLocalVerification()
        {
            // Keep the recovered native verification prefab and all step bindings intact.
            var root = PrefabUtility.LoadPrefabContents(Folder + "TXLevelPanel.prefab");
            try
            {
                root.name = "LocalWithdrawalVerification";
                var panel = root.GetComponent<OriginalWithdrawalLevelPanel>();
                var serialized = new SerializedObject(panel);
                var main = (RectTransform)serialized.FindProperty("main").objectReferenceValue;
                var tip = (TMP_Text)serialized.FindProperty("Tip").objectReferenceValue;
                var banner = (RectTransform)serialized.FindProperty("title").objectReferenceValue;
                if (banner == null) banner = (RectTransform)tip.transform.parent;
                ApplySkin(banner.GetComponent<Image>(), "wood-ribbon");
                banner.anchorMin = banner.anchorMax = new Vector2(.5f, .5f);
                banner.anchoredPosition = new Vector2(0, 590);
                banner.sizeDelta = new Vector2(900, 180);
                tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(.5f, .5f);
                tip.rectTransform.anchoredPosition = Vector2.zero;
                tip.rectTransform.sizeDelta = new Vector2(650, 112);
                tip.enableAutoSizing = true;
                tip.fontSizeMin = 30;
                tip.fontSizeMax = 60;
                tip.alignment = TextAlignmentOptions.Center;
                tip.enableWordWrapping = false;
                tip.color = Ink;

                // This is the direct child amount-card Image, not Step/2/Image (1).
                // No text, bank icon, controls, or verification animations are replaced.
                var amountCard = main.Find("Image (1)").GetComponent<Image>();
                ApplySkin(amountCard, "cream-card");
                var amountHeading = main.Find("Title1").GetComponent<Image>();
                ApplySkin(amountHeading, "wood-card");
                amountHeading.rectTransform.anchoredPosition = new Vector2(0, 365.9f);
                amountHeading.rectTransform.sizeDelta = new Vector2(640, 96);
                var headingText = amountHeading.GetComponentInChildren<TMP_Text>(true);
                headingText.rectTransform.anchorMin = headingText.rectTransform.anchorMax = new Vector2(.5f, .5f);
                headingText.rectTransform.anchoredPosition = Vector2.zero;
                headingText.rectTransform.sizeDelta = new Vector2(570, 74);
                headingText.enableAutoSizing = true;
                headingText.fontSizeMin = 28;
                headingText.fontSizeMax = 48;
                headingText.alignment = TextAlignmentOptions.Center;
                headingText.enableWordWrapping = false;
                headingText.color = Ink;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "LocalWithdrawalVerification.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ApplySkin(Image image, string skin)
        {
            if (image == null) throw new InvalidOperationException("Verification prefab is missing its configured visual: " + skin);
            image.sprite = Skin(skin);
            image.color = Color.white;
            image.type = image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = false;
            image.fillCenter = true;
        }
    }
}
