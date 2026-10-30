using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Acceptance tooling only. Exercises the production scene/prefabs through standard Unity UI events.
    [InitializeOnLoad]
    public static class SettingsEntryPlayValidation
    {
        const string Key = "NutSort.SettingsEntryPlayValidation";
        const string Output = "Docs/SettingsPanelRestore/20261012";
        const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        const string Fixture = "{\"Level\":11,\"LevelSeed\":0,\"Gold\":500.58,\"Coin\":486.73," +
            "\"RevokeCount\":5,\"IsAudio\":true,\"IsVibrate\":true,\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true," +
            "\"UserName\":\"Player_C3f1\",\"UserId\":\"90703fb2c51f383a0123456789abcdef\",\"UserLevel\":7," +
            "\"IsGoldReduceLevel1\":true,\"IsGoldReduceLevel2\":true,\"Level1Gold\":0.4,\"Level2Gold\":0.6," +
            "\"IsCompleteExtraGoldGuide\":true,\"IsCompleteCoinNewPeopleReward\":true,\"IsShowCoin\":true,\"IsCompleteShowCoinRewardHint\":true}";
        static readonly JArray Captures = new JArray();
        static readonly JArray Checks = new JArray();
        static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static OriginalAudioPlayer audio;
        static LocalGameplayController local;
        static OriginalBoardState board;
        static string boardJson, pendingCapture, error;
        static int phase, revoke, exchange, addScrew, vibrationRequests;
        static double readyAt, deadline, nextProbe;
        static DateTime begun;

        static SettingsEntryPlayValidation()
        {
            if (SessionState.GetBool(Key + ".Finish", false))
            {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                EditorApplication.delayCall += CompleteFinish;
            }
            else if (SessionState.GetBool(Key, false)) StartPolling();
        }

        public static void Run()
        {
            try
            {
                Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in Edit Mode");
                Directory.CreateDirectory(Output);
                ValidateIdentityPolicy();
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetBool(Key + ".Finish", false);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Settings entry: isolated acceptance fixture");
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void StartPolling()
        {
            phase = 0; game = null; startup = null; local = null; audio = null; board = null;
            boardJson = pendingCapture = error = null; Captures.Clear(); Checks.Clear();
            begun = DateTime.UtcNow;
            vibrationRequests = 0;
            readyAt = nextProbe = 0; deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= ConfigureCountry;
            SceneManager.sceneLoaded += ConfigureCountry;
        }

        static void ConfigureCountry(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "LuoSiSortGame") return;
            var session = UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
            if (session != null) session.ConfigureRewardCountry("US");
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                Check(now < deadline, "Settings validation timeout, phase " + phase, false);
                if (phase == 0)
                {
                    if (now < nextProbe) return;
                    nextProbe = now + .2;
                    if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                    if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                    if (audio == null) audio = UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
                    if (game == null || startup == null || audio == null || startup.LocalGameplay == null || game.Level == null ||
                        !game.IsInitDone || !game.Level.AreNutsInitialized || game.IsRestarting || game.InputBlocked ||
                        game.ModalInputBlocked || startup.Replay.IsClickMasked) return;
                    local = startup.LocalGameplay;
                    audio.VibrationRequested += CountVibration;
                    Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Uses production US reward configuration");
                    CheckSessionIdentityMigration();
                    local.RefreshRewardsTop();
                    board = game.Level.Board; boardJson = OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot());
                    revoke = game.User.RevokeCount; exchange = game.User.ExchangeCount; addScrew = game.User.AddScrewCount;
                    Check(audio.AudioEnabled && game.User.IsAudio && audio.VibrationEnabled && game.User.IsVibrate,
                        "Fixture begins with independent sound and vibration ON");
                    Check(Screen.width == 720 && Screen.height == 1600, "Tall viewport is actually 720x1600");
                    // AreNutsInitialized is reached before their entry tween finishes visually.
                    // Wait for the real production animation before taking the main-screen evidence.
                    Advance(1, now, 3); return;
                }
                if (now < readyAt || startup.Replay.IsClickMasked) return;
                switch (phase)
                {
                    case 1:
                        CheckEntry(); Capture("main-after"); Advance(2, now); break;
                    case 2:
                        FreshCapture(); Click(local.Bottom.Setting); Advance(3, now); break;
                    case 3:
                        CheckPanel(true, true); CheckModalGuards(); Capture("settings-tall"); Advance(4, now); break;
                    case 4:
                        FreshCapture(); Click(local.SettingsPanel.SoundButton); Advance(5, now); break;
                    case 5:
                        CheckPanel(false, true); CheckSavedSettings(false, true);
                        Check(!audio.Background.isPlaying, "OFF pauses production background AudioSource");
                        int cached = audio.CachedClipCount;
                        Check(audio.PlaySound("Select") == null && audio.CachedClipCount == cached,
                            "OFF blocks new sound without loading another clip");
                        int beforeVibration = vibrationRequests;
                        audio.PlayVibration(); audio.PlayVibration();
                        Check(vibrationRequests == beforeVibration + 1, "Sound OFF preserves enabled vibration; duplicate request in one frame is coalesced");
                        Click(local.SettingsPanel.VibrateButton); Advance(6, now); break;
                    case 6:
                        CheckPanel(false, false); CheckSavedSettings(false, false);
                        int disabledVibration = vibrationRequests;
                        audio.PlayVibration();
                        Check(vibrationRequests == disabledVibration, "Vibration OFF blocks production platform vibration requests");
                        Capture("settings-both-off"); Advance(7, now); break;
                    case 7:
                        FreshCapture(); Click(local.SettingsPanel.SoundButton); Advance(8, now); break;
                    case 8:
                        CheckPanel(true, false); CheckSavedSettings(true, false);
                        Check(audio.Background.isPlaying, "ON resumes production background AudioSource");
                        Click(local.SettingsPanel.CloseButton); Advance(9, now); break;
                    case 9:
                        CheckClosed(); CheckEntry(); Click(local.Bottom.Setting); Advance(10, now); break;
                    case 10:
                        CheckPanel(true, false); CheckSavedSettings(true, false);
                        Click(local.SettingsPanel.VibrateButton); Advance(11, now); break;
                    case 11:
                        CheckPanel(true, true); CheckSavedSettings(true, true);
                        Click(local.SettingsPanel.UserLevelButton); Advance(12, now); break;
                    case 12:
                        CheckUserLevel(); Capture("userinfo-tall"); Advance(13, now); break;
                    case 13:
                        FreshCapture(); Click(local.UserLevelPanel.ContinueButton); Advance(14, now); break;
                    case 14:
                        CheckClosed(); Check(local.UserLevelPanel == null, "User Level Continue closes detail");
                        Click(local.Bottom.Setting); Advance(15, now); break;
                    case 15:
                        CheckPanel(true, true); Click(local.SettingsPanel.HistoryButton); Advance(16, now); break;
                    case 16:
                        CheckHistory(); Capture("history-tall"); Advance(17, now); break;
                    case 17:
                        FreshCapture(); Click(local.CashHistoryPanel.CloseButton); Advance(18, now); break;
                    case 18:
                        CheckClosed(); Check(local.CashHistoryPanel == null, "History Close completes navigation loop");
                        Click(local.Bottom.Setting); Advance(19, now); break;
                    case 19:
                        CheckPanel(true, true); Click(local.SettingsPanel.ReplayButton); Advance(20, now); break;
                    case 20:
                        CheckReplay(); Capture("replay-tall"); Advance(21, now); break;
                    case 21:
                        FreshCapture(); Click(startup.Replay.Panel.ContinueButton); Advance(22, now); break;
                    case 22:
                        CheckClosed(); Check(startup.Replay.Panel == null, "Replay cancellation closes confirmation");
                        PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Settings: short viewport fixture");
                        Advance(23, now, 1); break;
                    case 23:
                        if (Screen.width != 720 || Screen.height != 1280) return;
                        CheckEntry(); Capture("main-short"); Advance(24, now); break;
                    case 24:
                        FreshCapture(); Click(local.Bottom.Setting); Advance(25, now); break;
                    case 25:
                        CheckPanel(true, true); Capture("settings-short"); Advance(26, now); break;
                    case 26:
                        FreshCapture(); Click(local.SettingsPanel.UserLevelButton); Advance(27, now); break;
                    case 27:
                        CheckUserLevel(); Capture("userinfo-short"); Advance(28, now); break;
                    case 28:
                        FreshCapture(); Click(local.UserLevelPanel.CloseButton); Advance(29, now); break;
                    case 29:
                        CheckClosed(); Check(local.UserLevelPanel == null, "User Level X closes detail");
                        Click(local.Bottom.Setting); Advance(30, now); break;
                    case 30:
                        CheckPanel(true, true); Click(local.SettingsPanel.HistoryButton); Advance(31, now); break;
                    case 31:
                        CheckHistory(); Capture("history-short"); Advance(32, now); break;
                    case 32:
                        FreshCapture(); Click(local.CashHistoryPanel.CloseButton); Advance(33, now); break;
                    case 33:
                        CheckClosed(); Click(local.Bottom.Setting); Advance(34, now); break;
                    case 34:
                        CheckPanel(true, true); Click(local.SettingsPanel.ReplayButton); Advance(35, now); break;
                    case 35:
                        CheckReplay(); Capture("replay-short"); Advance(36, now); break;
                    case 36:
                        FreshCapture(); Click(startup.Replay.Panel.ReplayButton); Advance(37, now, 3); break;
                    case 37:
                        if (game.IsRestarting || !game.IsInitDone || !game.Level.AreNutsInitialized || startup.Replay.Panel != null) return;
                        Check(!ReferenceEquals(board, game.Level.Board), "Confirm Replay reconstructs the actual puzzle board once");
                        CheckResourcesUnchanged(); CheckSavedSettings(true, true);
                        Check(!game.ModalInputBlocked, "Confirmed Replay releases modal input");
                        Capture("replayed-short"); Advance(38, now); break;
                    case 38:
                        FreshCapture(); Finish(0); break;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void Advance(int next, double now, double delay = .6) { phase = next; readyAt = now + delay; }

        static void CheckEntry()
        {
            var button = local.Bottom.Setting;
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Settings Button is active and interactable");
            Check(button.image != null && button.image.sprite != null && button.image.enabled && button.image.color.a > 0,
                "Settings Button has a visible configured Image and Sprite");
            Rect screen = ScreenBounds((RectTransform)button.transform);
            Check(screen.xMin >= -.1f && screen.yMin >= -.1f && screen.xMax <= Screen.width + .1f && screen.yMax <= Screen.height + .1f,
                "Settings Button stays fully inside " + Screen.width + "x" + Screen.height + " viewport");
            Check(screen.width >= 36 && screen.height >= 36, "Settings touch target has at least 36 screen pixels in both axes");
            Check(HitHandler(screen.center) == button.gameObject, "Settings Button is the frontmost actual Unity UI hit");
        }

        static void CountVibration() { vibrationRequests++; }

        // Uses the same production session initialization that Android executes.
        // Missing fields are introduced only inside the isolated acceptance save.
        static void CheckSessionIdentityMigration()
        {
            string fixtureName = game.User.UserName, fixtureId = game.User.UserId;
            Check(fixtureName == "Player_C3f1" && fixtureId == "90703fb2c51f383a0123456789abcdef",
                "Production session initialization preserves an existing identity exactly");
            JObject before = BusinessState(game.User);
            game.User.UserName = null; game.User.UserId = "  ";
            audio.UserState.Initialize();
            string createdId = game.User.UserId, createdName = game.User.UserName;
            Check(IsHex(createdId, 32) && createdName.StartsWith("Player_", StringComparison.Ordinal) && IsHex(createdName.Substring(7), 4),
                "Production session migrates missing local ID/name into persistent generated identity");
            JObject saved = JObject.Parse(PlayerPrefs.GetString(OriginalUserStore.Key));
            Check((string)saved["UserId"] == createdId && (string)saved["UserName"] == createdName,
                "Production session saves generated identity immediately");
            Check(JToken.DeepEquals(before, BusinessState(game.User)), "Session identity migration preserves board, rewards, sync state and all other user fields");
            audio.UserState.Initialize();
            Check(game.User.UserId == createdId && game.User.UserName == createdName, "Repeated session initialization does not regenerate local identity");
            game.User.UserName = fixtureName; game.User.UserId = fixtureId;
            audio.UserState.Store.SaveData(false, null);
            Check(JToken.DeepEquals(before, BusinessState(game.User)), "Restoring disclosed profile fixture preserves all business fields");
        }

        static bool IsHex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            for (int i = 0; i < value.Length; i++)
                if (!(value[i] >= '0' && value[i] <= '9') && !(value[i] >= 'a' && value[i] <= 'f')) return false;
            return true;
        }

        static JObject BusinessState(OriginalUserLocalData user)
        {
            JObject state = JObject.Parse(OriginalUserDataJson.Write(user));
            state.Remove("UserId"); state.Remove("UserName");
            return state;
        }

        sealed class MemoryPreferences : IOriginalUserPreferences
        {
            public string Json;
            public string GetString(string key, string fallback) => Json ?? fallback;
            public void SetString(string key, string value) { Json = value; }
            public void Save() { }
        }

        static void ValidateIdentityPolicy()
        {
            var checks = new JArray();
            Action<bool, string> verify = (value, message) =>
            { if (!value) throw new InvalidOperationException("Identity validation: " + message); checks.Add(message); };
            var defaults = Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults");
            verify(defaults != null, "Uses actual production user defaults");
            string[] ids = { null, "existing-id-with-Original-Case", null, "existing-id-only", " \t " };
            string[] names = { null, "Original Name", "Original Name Only", null, "\n " };
            for (int test = 0; test < ids.Length; test++)
            {
                var preferences = new MemoryPreferences();
                var store = new OriginalUserStore(defaults, preferences);
                var user = store.Data;
                user.UserId = ids[test]; user.UserName = names[test];
                user.Level = 11; user.UserLevel = 7; user.Gold = 500.58f; user.Coin = 486.73;
                user.LevelInfo = "opaque-existing-board-snapshot";
                user.IsAudio = false; user.IsVibrate = true;
                user.LocalWithdrawalData = new JObject { ["probe"] = "keep-original-withdrawal-state" };
                JObject before = BusinessState(user);
                bool missingId = string.IsNullOrWhiteSpace(ids[test]), missingName = string.IsNullOrWhiteSpace(names[test]);
                verify(LocalPlayerIdentity.Ensure(user) == (missingId || missingName), "Ensure reports only actual missing-field changes, case " + test);
                verify(missingId ? IsHex(user.UserId, 32) : user.UserId == ids[test], "ID generated only when missing; existing value preserved exactly, case " + test);
                verify(missingName ? user.UserName.StartsWith("Player_", StringComparison.Ordinal) && IsHex(user.UserName.Substring(7), 4) : user.UserName == names[test],
                    "Name generated only when missing; existing value preserved exactly, case " + test);
                verify(JToken.DeepEquals(before, BusinessState(user)), "All non-identity fields remain unchanged, case " + test);
                string stableId = user.UserId, stableName = user.UserName;
                verify(!LocalPlayerIdentity.Ensure(user) && user.UserId == stableId && user.UserName == stableName, "Repeated Ensure is idempotent, case " + test);
                store.SaveData(false, null);
                var reloaded = new OriginalUserStore(defaults, preferences).Data;
                verify(reloaded.UserId == stableId && reloaded.UserName == stableName && !LocalPlayerIdentity.Ensure(reloaded), "Production JSON save/reload retains identity, case " + test);
                verify(JToken.DeepEquals(before, BusinessState(reloaded)), "Production save/reload preserves all business data, case " + test);
            }
            bool rejectedNull = false;
            try { LocalPlayerIdentity.Ensure(null); } catch (ArgumentNullException) { rejectedNull = true; }
            verify(rejectedNull, "Null user is rejected explicitly");
            File.WriteAllText(Output + "/identity-report.json", new JObject { ["status"] = "PASS", ["utc"] = DateTime.UtcNow.ToString("o"),
                ["checks"] = checks, ["scope"] = "Actual production identity policy and JSON store; isolated in-memory storage; no SDK IDs or remote registration" }.ToString());
        }

        static void CheckPanel(bool enabled, bool vibrate)
        {
            var panel = local.SettingsPanel;
            Check(panel != null && !panel.Closing && panel.gameObject.activeInHierarchy, "Production settings panel is open");
            Check(UnityEngine.Object.FindObjectsOfType<LocalSettingsPanel>().Length == 1, "Exactly one production settings panel exists");
            Check(game.ModalInputBlocked, "Settings panel blocks world gameplay input");
            Check(audio.AudioEnabled == enabled && game.User.IsAudio == enabled, "Shared user audio setting matches switch");
            Check(panel.StateLabel != null && panel.StateLabel.text == (enabled ? "ON" : "OFF"), "Visible sound label matches ON/OFF state");
            Check(panel.SoundButton.image.sprite != null, "Sound switch has a configured state Sprite");
            Check(audio.VibrationEnabled == vibrate && game.User.IsVibrate == vibrate,
                "Independent vibration state matches shared user preference");
            Check(panel.VibrateStateLabel != null && panel.VibrateStateLabel.text == (vibrate ? "ON" : "OFF"),
                "Visible vibration label matches ON/OFF state");
            Check(panel.SoundIcon != null && panel.SoundIcon.sprite != null && panel.VibrateIcon != null && panel.VibrateIcon.sprite != null,
                "Audio and vibration have configured icon sprites");
            Check(panel.UserNameLabel.text == game.User.UserName && !string.IsNullOrEmpty(game.User.UserName), "Profile displays actual saved player name");
            Check(panel.UserIdLabel.text.Contains(game.User.UserId) && game.User.UserId.Length == 32,
                "Profile displays complete saved 32-character player ID");
            Check(panel.UserLevelLabel.text == "LV.7" && game.User.UserLevel == 7 && game.User.Level == 11,
                "Profile shows actual UserLevel, distinct from puzzle Level");
            CheckTextFits(panel.UserNameLabel); CheckTextFits(panel.UserIdLabel); CheckTextFits(panel.UserLevelLabel);
            bool avatarPresent = false;
            foreach (var image in panel.GetComponentsInChildren<Image>(false))
                if (image.name.IndexOf("Avatar", StringComparison.OrdinalIgnoreCase) >= 0 && image.enabled && image.sprite != null && image.color.a > 0)
                { avatarPresent = true; CheckRectInsideViewport((RectTransform)image.transform, "Player avatar"); }
            Check(avatarPresent, "Configured player avatar is present and visible");
            var controls = new[] { panel.SoundButton, panel.VibrateButton, panel.CloseButton, panel.UserLevelButton, panel.HistoryButton, panel.ReplayButton };
            for (int i = 0; i < controls.Length; i++)
            {
                CheckInsideViewport(controls[i]);
                Check(controls[i].onClick.GetPersistentEventCount() == 0, "Settings Button uses code listeners, no serialized Unity events: " + controls[i].name);
                for (int j = i + 1; j < controls.Length; j++)
                    Check(!ScreenBounds((RectTransform)controls[i].transform).Overlaps(ScreenBounds((RectTransform)controls[j].transform)),
                        "Settings controls do not overlap: " + controls[i].name + " / " + controls[j].name);
            }
            CheckUnchanged();
        }

        static void CheckUserLevel()
        {
            var panel = local.UserLevelPanel;
            Check(local.SettingsPanel == null && panel != null && !panel.Closing && game.ModalInputBlocked,
                "User Level replaces settings and retains gameplay modal blocking");
            Check(UnityEngine.Object.FindObjectsOfType<LocalUserLevelPanel>().Length == 1, "Exactly one User Level detail exists");
            Check(panel.LevelLabel.text.Contains("7") && game.User.UserLevel == 7 && game.User.Level == 11,
                "User Level detail uses user rank instead of puzzle number");
            var rows = game.User.GoldRewardTargetS2CData?["bear_list"] as JArray;
            var target = rows != null && rows.Count > 2 ? (int?)rows[2]["caliper_rank"] ?? 0 : 0;
            if (target > 0)
            {
                float expected = Mathf.Clamp01((float)game.User.UserLevel / target);
                Check(Mathf.Abs(panel.ProgressFill.fillAmount - expected) < .0001f, "User Level fill derives from production configured rank target");
                Check(panel.ProgressLabel.text.Contains((expected * 100f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    "User Level percentage matches configured rank target");
            }
            else Check(panel.ProgressFill.fillAmount == 0 && !string.IsNullOrEmpty(panel.ProgressLabel.text),
                "Missing rank target uses unavailable state without invented percentage");
            CheckTextFits(panel.LevelLabel); CheckTextFits(panel.ProgressLabel);
            CheckInsideViewport(panel.CloseButton); CheckInsideViewport(panel.ContinueButton);
            CheckUnchanged();
        }

        static void CheckHistory()
        {
            var panel = local.CashHistoryPanel;
            Check(local.SettingsPanel == null && panel != null && !panel.Closing && game.ModalInputBlocked,
                "Withdrawal History replaces settings and retains modal blocking");
            Check(UnityEngine.Object.FindObjectsOfType<LocalCashHistoryPanel>().Length == 1, "Exactly one history panel exists");
            Check(panel.Rows.Count == 3, "History shows two real archived fixture records and current balance");
            Check(panel.Rows[0].AmountLabel.text.Contains("0.40") && panel.Rows[1].AmountLabel.text.Contains("0.60") &&
                panel.Rows[2].AmountLabel.text.Contains("500.58"), "History uses actual saved record amounts and current balance");
            CheckInsideViewport(panel.CloseButton);
            foreach (var row in panel.Rows)
            { CheckTextFits(row.AmountLabel); CheckTextFits(row.TitleLabel); CheckTextFits(row.ActionLabel); }
            CheckUnchanged();
        }

        static void CheckReplay()
        {
            var panel = startup.Replay.Panel;
            Check(local.SettingsPanel == null && panel != null && !panel.Closing && game.ModalInputBlocked,
                "Replay opens original confirmation and retains modal blocking");
            Check(UnityEngine.Object.FindObjectsOfType<OriginalReplayPanel>().Length == 1, "Exactly one Replay confirmation exists");
            CheckInsideViewport(panel.ContinueButton); CheckInsideViewport(panel.ReplayButton);
            CheckUnchanged();
        }

        static void CheckTextFits(TMP_Text label)
        {
            Check(label != null && label.gameObject.activeInHierarchy, "Configured text is active", false);
            Canvas.ForceUpdateCanvases(); label.ForceMeshUpdate();
            CheckRectInsideViewport(label.rectTransform, "Text " + label.name);
            Check(!label.isTextOverflowing, "Text fits assigned area without overflow: " + label.name);
            Rect rect = label.rectTransform.rect;
            const float tolerance = 2f;
            for (int i = 0; i < label.textInfo.characterCount; i++)
            {
                var character = label.textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                Check(character.bottomLeft.x >= rect.xMin - tolerance && character.topRight.x <= rect.xMax + tolerance &&
                    character.bottomLeft.y >= rect.yMin - tolerance && character.topRight.y <= rect.yMax + tolerance,
                    "Visible text geometry remains inside bounds: " + label.name + " character " + i, false);
            }
        }

        static void CheckRectInsideViewport(RectTransform transform, string name)
        {
            Rect rect = ScreenBounds(transform);
            Check(rect.xMin >= -.1f && rect.yMin >= -.1f && rect.xMax <= Screen.width + .1f && rect.yMax <= Screen.height + .1f,
                name + " remains inside " + Screen.width + "x" + Screen.height + " viewport");
        }

        static void CheckInsideViewport(Button button)
        {
            var rect = ScreenBounds((RectTransform)button.transform);
            Check(rect.xMin >= -.1f && rect.yMin >= -.1f && rect.xMax <= Screen.width + .1f && rect.yMax <= Screen.height + .1f,
                "Settings control remains inside viewport: " + button.name);
            Check(HitHandler(rect.center) == button.gameObject, "Settings control receives real Unity UI hit: " + button.name);
        }

        static void CheckModalGuards()
        {
            var existing = local.SettingsPanel;
            Check(HitHandler(ScreenBounds((RectTransform)local.Bottom.Setting.transform).center) != local.Bottom.Setting.gameObject,
                "Modal raycast layer blocks another physical settings-entry click");
            Check(HitHandler(ScreenBounds((RectTransform)local.Bottom.Replay.transform).center) != local.Bottom.Replay.gameObject,
                "Modal raycast layer blocks physical restart click");
            // Supplement physical hit tests with direct listener attempts to verify runtime modal guards as well.
            local.Bottom.Setting.onClick.Invoke();
            local.Bottom.Replay.onClick.Invoke();
            foreach (var item in local.Bottom.Items)
            {
                var button = item.Display.Click;
                Check(HitHandler(ScreenBounds((RectTransform)button.transform).center) != button.gameObject,
                    "Modal raycast layer blocks physical tool click: " + button.name);
                button.onClick.Invoke();
            }
            Check(local.SettingsPanel == existing && UnityEngine.Object.FindObjectsOfType<LocalSettingsPanel>().Length == 1,
                "Repeated settings listener cannot create duplicate modal");
            Check(startup.Replay.Panel == null && local.ToolOffer == null && !local.ToolAdPending && !local.ResultVisible &&
                !game.IsRestarting && !game.IsExchanging, "Blocked bottom listeners cannot restart, consume tools or open ads");
            CheckUnchanged();
        }

        static void CheckClosed()
        {
            Check(local.SettingsPanel == null && UnityEngine.Object.FindObjectsOfType<LocalSettingsPanel>().Length == 0,
                "Close Button removes settings panel");
            Check(!game.ModalInputBlocked && !startup.Replay.IsClickMasked, "Closing settings releases modal and click guards");
            CheckUnchanged();
        }

        static void CheckUnchanged()
        {
            Check(ReferenceEquals(board, game.Level.Board) && boardJson == OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot()),
                "Settings interaction preserves the actual board and move history");
            CheckResourcesUnchanged();
        }

        static void CheckResourcesUnchanged()
        {
            Check(game.User.Level == 11 && game.User.LevelSeed == 0 && game.User.UserLevel == 7 && Mathf.Abs(game.User.Gold - 500.58f) < .001f &&
                Math.Abs(game.User.Coin - 486.73) < .000001 && game.User.RevokeCount == revoke &&
                game.User.ExchangeCount == exchange && game.User.AddScrewCount == addScrew,
                "Settings interaction preserves level, cash, diamonds and all tool quantities");
            Check(!game.User.IsTXLevel1 && !game.User.IsTXLevel2 && game.User.LocalPendingReward == null,
                "Settings interaction does not submit payment or create an unearned reward");
        }

        static void CheckSavedSettings(bool enabled, bool vibrate)
        {
            JObject saved = JObject.Parse(PlayerPrefs.GetString(OriginalUserStore.Key));
            Check((bool)saved["IsAudio"] == enabled, "Production saved user has IsAudio=" + enabled);
            Check((bool)saved["IsVibrate"] == vibrate, "Production saved user has independent IsVibrate=" + vibrate);
        }

        static Rect ScreenBounds(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static GameObject HitHandler(Vector2 position)
        {
            Check(EventSystem.current != null, "Production EventSystem exists", false);
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            Hits.Clear(); EventSystem.current.RaycastAll(pointer, Hits);
            return Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
        }

        static void Click(Button button)
        {
            Check(button != null && button.IsInteractable(), "Target Button is interactable", false);
            Vector2 position = ScreenBounds((RectTransform)button.transform).center;
            GameObject target = HitHandler(position);
            Check(target == button.gameObject, "Actual UI raycast reaches requested Button: " + button.name);
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            Check(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), "Standard Unity Button pointer-click delivered");
        }

        static void Capture(string name)
        {
            Captures.Add(new JObject { ["image"] = name + ".png", ["width"] = Screen.width, ["height"] = Screen.height,
                ["kind"] = "Unity Editor Play Mode actual runtime UI with isolated disclosed acceptance fixture; NOT Android/device verification",
                ["settingsOpen"] = local.SettingsPanel != null, ["soundEnabled"] = audio.AudioEnabled, ["vibrationEnabled"] = audio.VibrationEnabled,
                ["settingsEntryScreenRect"] = RectJson(ScreenBounds((RectTransform)local.Bottom.Setting.transform)) });
            pendingCapture = Path.GetFullPath(Output + "/" + name + ".png");
            ScreenCapture.CaptureScreenshot(pendingCapture);
            Debug.Log("SETTINGS_ENTRY_CAPTURE " + name);
        }

        static JArray RectJson(Rect rect) => new JArray(rect.x, rect.y, rect.width, rect.height);
        static void FreshCapture()
        {
            Check(pendingCapture != null && File.Exists(pendingCapture) && File.GetLastWriteTimeUtc(pendingCapture) >= begun.AddSeconds(-2),
                "Fresh runtime screenshot saved: " + pendingCapture);
            pendingCapture = null;
        }
        static void Check(bool value, string message, bool record = true)
        {
            if (!value) throw new InvalidOperationException(message);
            if (record) Checks.Add(message);
        }
        static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }

        static void Finish(int code)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            SessionState.SetInt(Key + ".Result", code);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = Fixture, ["captures"] = Captures,
                ["checks"] = Checks, ["error"] = error, ["phase"] = phase, ["withdrawOrAdButtonInvoked"] = false,
                ["verification"] = "Unity Editor Play Mode only; isolated save; production scene and prefabs; standard Unity UI pointer events" }.ToString());
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= ConfigureCountry;
            if (audio != null) audio.VibrationRequested -= CountVibration;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            else CompleteFinish();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) CompleteFinish();
        }

        static void CompleteFinish()
        {
            if (!SessionState.GetBool(Key + ".Finish", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int code = SessionState.GetInt(Key + ".Result", 1);
            var report = JObject.Parse(SessionState.GetString(Key + ".Report", "{}"));
            try
            {
                bool existed; string saved;
                using (var reader = new BinaryReader(File.OpenRead(Backup))) { existed = reader.ReadBoolean(); saved = reader.ReadString(); }
                // Pause/quit callbacks finish saving before the byte-for-byte original preference is restored.
                OriginalPreferenceFixture.Restore();
                Check(!File.Exists(Backup) && PlayerPrefs.HasKey(OriginalUserStore.Key) == existed &&
                    (!existed || PlayerPrefs.GetString(OriginalUserStore.Key) == saved), "Original user preference restored byte for byte", false);
                report["originalPreferencesRestored"] = true;
            }
            catch (Exception ex) { Debug.LogException(ex); report["restoreError"] = ex.ToString(); code = 1; }
            report["status"] = code == 0 ? "PASS" : "FAIL"; report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("SETTINGS_ENTRY_PLAY_" + (code == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(code);
        }
    }
}
