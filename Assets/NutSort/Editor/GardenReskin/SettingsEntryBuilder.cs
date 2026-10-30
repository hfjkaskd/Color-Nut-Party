using System;
using NutSort.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // One-time asset authoring. Runtime only instantiates the saved, editable prefab.
    public static class SettingsEntryBuilder
    {
        const string PanelPath = "Assets/Resources/prefabs/panels/LocalSettings.prefab";
        const string UserLevelPath = "Assets/Resources/prefabs/panels/LocalUserLevel.prefab";
        const string LocalPath = "Assets/Resources/prefabs/panels/LocalGameplay.prefab";
        const string BottomPath = "Assets/Resources/prefabs/panels/MainPanelBottom.prefab";
        static readonly Color Ink = new Color32(83, 43, 16, 255);

        public static void RestoreFullPanel()
        {
            try
            {
                ImportSettingsIcons();
                BuildPanel();
                BuildUserLevelPanel();
                ConfigureLocal();
                AssetDatabase.SaveAssets();
                Debug.Log("SETTINGS_FULL_PANEL_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        static void ImportSettingsIcons()
        {
            foreach (string name in new[] { "audio", "vibrate", "avatar", "avatar-mask" })
            {
                string path = "Assets/Resources/atlas/localsettings/" + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing settings artwork: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        static Sprite SettingsSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/atlas/localsettings/" + name + ".png");

        public static void RefreshButtonVisual()
        {
            try
            {
                RestoreBottom();
                AssetDatabase.SaveAssets();
                Debug.Log("SETTINGS_BUTTON_VISUAL_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        public static void Run()
        {
            try
            {
                BuildPanel();
                BuildUserLevelPanel();
                RestoreBottom();
                ConfigureLocal();
                AssetDatabase.SaveAssets();
                Debug.Log("SETTINGS_ENTRY_BUILD_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
        }

        static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/atlas/octobergarden/" + name + ".png");

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var rect = Rect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text, float fontSize)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/MonoBehaviour/zh_Custom SDF.asset");
            label.text = text; label.color = Ink; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            label.enableWordWrapping = false;
            return label;
        }

        static Button Button(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var image = Image(name, parent, position, size, sprite); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        static void BuildPanel()
        {
            var root = Rect("LocalSettings", null, Vector2.zero, Vector2.zero);
            try
            {
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                var backdrop = root.gameObject.AddComponent<Image>();
                backdrop.color = new Color(0, 0, 0, .65f); backdrop.raycastTarget = true;
                var panel = root.gameObject.AddComponent<LocalSettingsPanel>();
                Image("Card", root, new Vector2(0, -70), new Vector2(940, 1080), Sprite("cream-card"));
                Image("HeadingArtwork", root, new Vector2(0, 588), new Vector2(950, 488), Sprite("replay-title"));
                Label("Title", root, new Vector2(0, 423), new Vector2(720, 105), "Settings", 72);
                var close = Button("Close", root, new Vector2(423, 423), new Vector2(100, 100), Sprite("close"));
                var profile = Image("ProfileCard", root, new Vector2(0, 125), new Vector2(850, 415), Sprite("flat-cream"));
                profile.color = new Color32(249, 228, 192, 255);
                var avatarMask = Image("AvatarMask", root, new Vector2(-285, 157), new Vector2(222, 222),
                    SettingsSprite("avatar-mask"));
                avatarMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var avatar = Image("Avatar", avatarMask.transform, Vector2.zero, new Vector2(232, 232), SettingsSprite("avatar"));
                avatar.preserveAspect = true;
                var userName = Label("UserName", root, new Vector2(123, 250), new Vector2(505, 70), "Player", 44);
                userName.alignment = TextAlignmentOptions.MidlineLeft;
                userName.enableAutoSizing = true; userName.fontSizeMin = 24; userName.fontSizeMax = 44;
                var userId = Label("UserId", root, new Vector2(123, 172), new Vector2(505, 65), "Id:", 36);
                userId.alignment = TextAlignmentOptions.MidlineLeft;
                userId.enableAutoSizing = true; userId.fontSizeMin = 18; userId.fontSizeMax = 36;
                Image("LevelBand", root, new Vector2(123, 96), new Vector2(505, 57), Sprite("primary-button"));
                var userLevel = Label("UserLevel", root, new Vector2(123, 96), new Vector2(460, 54), "LV.0", 38);
                userLevel.color = Color.white;
                var levelLink = Label("UserLevelLink", root, new Vector2(202, -22), new Vector2(380, 76), "<u>User Level</u>", 48);
                levelLink.raycastTarget = true;
                var levelButton = levelLink.gameObject.AddComponent<Button>(); levelButton.targetGraphic = levelLink;
                levelButton.navigation = new Navigation { mode = Navigation.Mode.None };
                var sound = Button("Sound", root, new Vector2(-185, -222), new Vector2(180, 175), Sprite("primary-button"));
                var vibration = Button("Vibration", root, new Vector2(185, -222), new Vector2(180, 175), Sprite("primary-button"));
                var soundIcon = Image("Icon", sound.transform, Vector2.zero, new Vector2(92, 107), SettingsSprite("audio"));
                var vibrationIcon = Image("Icon", vibration.transform, Vector2.zero, new Vector2(112, 106), SettingsSprite("vibrate"));
                soundIcon.preserveAspect = vibrationIcon.preserveAspect = true;
                Label("SoundCaption", root, new Vector2(-185, -341), new Vector2(280, 55), "Sound", 37);
                Label("VibrationCaption", root, new Vector2(185, -341), new Vector2(280, 55), "Vibration", 37);
                var state = Label("SoundState", root, new Vector2(-185, -387), new Vector2(200, 44), "ON", 30);
                var vibrationState = Label("VibrationState", root, new Vector2(185, -387), new Vector2(200, 44), "ON", 30);
                var history = Button("WithdrawalHistory", root, new Vector2(0, -499), new Vector2(416, 111), Sprite("wood-card"));
                Label("Caption", history.transform, Vector2.zero, new Vector2(370, 93), "Withdrawal\nHistory", 36).enableWordWrapping = true;
                var replay = Button("Replay", root, new Vector2(0, -755), new Vector2(880, 174), Sprite("primary-button"));
                var replayText = Label("Caption", replay.transform, Vector2.zero, new Vector2(800, 140), "Replay", 72);
                replayText.color = Color.white;
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("soundButton").objectReferenceValue = sound;
                serialized.FindProperty("vibrateButton").objectReferenceValue = vibration;
                serialized.FindProperty("closeButton").objectReferenceValue = close;
                serialized.FindProperty("userLevelButton").objectReferenceValue = levelButton;
                serialized.FindProperty("historyButton").objectReferenceValue = history;
                serialized.FindProperty("replayButton").objectReferenceValue = replay;
                serialized.FindProperty("soundStateLabel").objectReferenceValue = state;
                serialized.FindProperty("vibrateStateLabel").objectReferenceValue = vibrationState;
                serialized.FindProperty("userNameLabel").objectReferenceValue = userName;
                serialized.FindProperty("userIdLabel").objectReferenceValue = userId;
                serialized.FindProperty("userLevelLabel").objectReferenceValue = userLevel;
                serialized.FindProperty("userIdFormat").stringValue = "Id:{0}";
                serialized.FindProperty("userLevelFormat").stringValue = "LV.{0}";
                serialized.FindProperty("soundIcon").objectReferenceValue = soundIcon;
                serialized.FindProperty("vibrateIcon").objectReferenceValue = vibrationIcon;
                serialized.FindProperty("soundOnIcon").objectReferenceValue = SettingsSprite("audio");
                serialized.FindProperty("soundOffIcon").objectReferenceValue = SettingsSprite("audio");
                serialized.FindProperty("vibrateOnIcon").objectReferenceValue = SettingsSprite("vibrate");
                serialized.FindProperty("vibrateOffIcon").objectReferenceValue = SettingsSprite("vibrate");
                serialized.FindProperty("soundOnText").stringValue = "ON";
                serialized.FindProperty("soundOffText").stringValue = "OFF";
                serialized.FindProperty("soundOnSprite").objectReferenceValue = Sprite("primary-button");
                serialized.FindProperty("soundOffSprite").objectReferenceValue = Sprite("amount-normal");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, PanelPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        static void BuildUserLevelPanel()
        {
            var root = Rect("LocalUserLevel", null, Vector2.zero, Vector2.zero);
            try
            {
                root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
                var backdrop = root.gameObject.AddComponent<Image>();
                backdrop.color = new Color(0, 0, 0, .65f); backdrop.raycastTarget = true;
                var panel = root.gameObject.AddComponent<LocalUserLevelPanel>();
                Image("Card", root, Vector2.zero, new Vector2(910, 770), Sprite("cream-card"));
                Image("Heading", root, new Vector2(0, 320), new Vector2(850, 156), Sprite("wood-ribbon"));
                Label("Title", root, new Vector2(0, 320), new Vector2(680, 120), "User Level", 60);
                var close = Button("Close", root, new Vector2(385, 320), new Vector2(94, 94), Sprite("close"));
                var level = Label("Level", root, new Vector2(0, 150), new Vector2(790, 100), "Current Level: 0", 58);
                Label("ProgressCaption", root, new Vector2(0, 35), new Vector2(780, 65), "Level progress", 40);
                Image("ProgressTrack", root, new Vector2(0, -62), new Vector2(766, 78), Sprite("progress-track"));
                var fill = Image("ProgressFill", root, new Vector2(0, -62), new Vector2(752, 62), Sprite("progress-fill"));
                fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.fillOrigin = 0; fill.fillAmount = 0;
                var progress = Label("ProgressText", root, new Vector2(0, -62), new Vector2(700, 60), "Progress unavailable", 36);
                var done = Button("Continue", root, new Vector2(0, -250), new Vector2(600, 130), Sprite("primary-button"));
                Label("Caption", done.transform, Vector2.zero, new Vector2(540, 110), "Continue", 54).color = Color.white;
                var data = new SerializedObject(panel);
                data.FindProperty("closeButton").objectReferenceValue = close;
                data.FindProperty("continueButton").objectReferenceValue = done;
                data.FindProperty("levelLabel").objectReferenceValue = level;
                data.FindProperty("progressLabel").objectReferenceValue = progress;
                data.FindProperty("progressFill").objectReferenceValue = fill;
                data.FindProperty("levelFormat").stringValue = "Current Level: {0}";
                data.FindProperty("progressFormat").stringValue = "{0:F2}%";
                data.FindProperty("unavailableProgressText").stringValue = "Progress unavailable";
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, UserLevelPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }

        static void RestoreBottom()
        {
            var root = PrefabUtility.LoadPrefabContents(BottomPath);
            try
            {
                var button = root.GetComponent<OriginalMainBottomView>().Setting;
                button.transform.parent.gameObject.SetActive(true);
                button.image.sprite = Sprite("settings-round-blue");
                if(button.image.sprite == null) throw new InvalidOperationException("Missing round Settings button sprite.");
                button.image.type = UnityEngine.UI.Image.Type.Simple;
                button.image.preserveAspect = true;
                button.image.color = Color.white;
                button.image.raycastTarget = true;
                var rect = (RectTransform)button.transform;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(88,88);
                // The whole circular face belongs to the real Button. Its old
                // panel-frame backing must not show through the transparent rim.
                var backing = button.transform.parent.GetComponent<Image>();
                backing.enabled = false;
                backing.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, BottomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ConfigureLocal()
        {
            var root = PrefabUtility.LoadPrefabContents(LocalPath);
            try
            {
                var controller = root.GetComponent<LocalGameplayController>();
                controller.Bottom.Setting.transform.parent.gameObject.SetActive(true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller.Bottom.Setting.transform.parent.gameObject);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("settingsPrefabPath").stringValue = "Prefabs/Panels/LocalSettings";
                serialized.FindProperty("userLevelPrefabPath").stringValue = "Prefabs/Panels/LocalUserLevel";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LocalPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
