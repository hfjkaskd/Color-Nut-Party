using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
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
    // Editor acceptance only; opens the production history from standard UI Buttons.
    [InitializeOnLoad]
    public static class LocalHistoryStylePlayValidation
    {
        private const string Key = "NutSort.LocalHistoryStylePlayValidation";
        private const string Output = "Docs/WithdrawalHistoryStyle/20261012";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private const long FirstUtc = 1791777600, SecondUtc = 1791777660;
        private const string Fixture = "{\"Level\":3,\"LevelSeed\":0,\"Gold\":500,\"Coin\":0,\"RevokeCount\":5," +
            "\"UserName\":\"History_Tester\",\"UserId\":\"history-style-acceptance\",\"UserLevel\":1," +
            "\"IsAudio\":true,\"IsVibrate\":true,\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true," +
            "\"IsCompleteCoinNewPeopleReward\":true,\"IsShowCoin\":true,\"IsCompleteShowCoinRewardHint\":true," +
            "\"IsCompleteExtraGoldGuide\":true,\"Level1Gold\":0.4,\"Level2Gold\":0.6," +
            "\"IsGoldReduceLevel1\":true,\"IsGoldReduceLevel2\":true," +
            "\"LocalWithdrawalData\":{\"tutorialBalanceApplied\":true,\"tutorialBalanceAmount\":500," +
            "\"tutorialBalanceNoticeAcknowledged\":true,\"earningFlow\":{\"activationComplete\":true,\"activationStep\":6,\"records\":[" +
            "{\"stage\":1,\"amount\":0.4,\"createdUtcSeconds\":1791777600,\"archived\":true,\"pending\":false}," +
            "{\"stage\":2,\"amount\":0.6,\"createdUtcSeconds\":1791777660,\"archived\":true,\"pending\":true,\"submittedUtcSeconds\":1791777660}]}}}";

        private static readonly JArray Checks = new JArray(), Captures = new JArray();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static int phase;
        private static double readyAt, deadline;
        private static DateTime began;
        private static string pendingCapture, error;

        static LocalHistoryStylePlayValidation()
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
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start in Edit Mode.");
                Directory.CreateDirectory(Output);
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetBool(Key + ".Finish", false); SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "History style: isolated US acceptance fixture");
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void StartPolling()
        {
            game = null; startup = null; audio = null; local = null;
            phase = 0; readyAt = 0; deadline = EditorApplication.timeSinceStartup + 150;
            began = DateTime.UtcNow; error = pendingCapture = null;
            Checks.Clear(); Captures.Clear();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= ConfigureCountry; SceneManager.sceneLoaded += ConfigureCountry;
        }

        private static void ConfigureCountry(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "LuoSiSortGame") return;
            var session = UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
            if (session != null) session.ConfigureRewardCountry("US");
        }

        private static bool SceneReady()
        {
            if (local != null) return true;
            if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
            if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
            if (audio == null) audio = UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
            if (game == null || startup == null || audio == null || startup.LocalGameplay == null || game.Level == null ||
                !game.IsInitDone || !game.Level.AreNutsInitialized || game.InputBlocked || game.ModalInputBlocked ||
                game.IsRestarting || startup.Loading.IsVisible) return false;
            local = startup.LocalGameplay;
            Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Uses the real US reward configuration");
            local.BindSettlementAds(_ => { throw new InvalidOperationException("Unexpected interstitial request."); },
                _ => { throw new InvalidOperationException("Unexpected rewarded ad request."); });
            CheckCashAndRecords();
            return true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now >= deadline) throw new TimeoutException("History style validation timed out at phase " + phase);
                if (now < readyAt || !SceneReady() || startup.Replay.IsClickMasked) return;
                switch (phase)
                {
                    case 0: Advance(1, 2); break;
                    case 1:
                        Click(local.Bottom.Setting); Advance(2); break;
                    case 2:
                        Check(local.SettingsPanel != null, "Settings opens through the actual toolbar Button");
                        Click(local.SettingsPanel.HistoryButton); Advance(3); break;
                    case 3:
                        CheckHistory(1600); Capture("history-tall", local.CashHistoryPanel.gameObject); Advance(4, .5); break;
                    case 4:
                        FreshCapture();
                        PlayModeWindow.SetCustomRenderingResolution(720, 1280, "History style: short portrait fixture");
                        Advance(5); break;
                    case 5:
                        if (Screen.width != 720 || Screen.height != 1280) return;
                        CheckHistory(1280); Capture("history-short", local.CashHistoryPanel.gameObject); Advance(6, .5); break;
                    case 6:
                        FreshCapture(); Click(local.CashHistoryPanel.CloseButton); Advance(7); break;
                    case 7:
                        Check(local.CashHistoryPanel == null && local.SettingsPanel == null && !game.ModalInputBlocked,
                            "Closing history destroys its modal and releases gameplay input");
                        CheckCashAndRecords(); Click(local.Bottom.Setting); Advance(8); break;
                    case 8:
                        Check(local.SettingsPanel != null, "Settings can be reopened after the history closes");
                        Click(local.SettingsPanel.HistoryButton); Advance(9); break;
                    case 9:
                        CheckHistory(1280); Click(local.CashHistoryPanel.Rows[0].ActionButton); Advance(10, .1); break;
                    case 10:
                        if (local.WithdrawalAccount == null) return;
                        Advance(11); break;
                    case 11:
                        Check(local.CashHistoryPanel == null && local.EarlyWithdrawalStage == 1 && local.EarlyLoadingPanel == null,
                            "First history action enters the existing stage-one account flow after loading");
                        Check(game.ModalInputBlocked && local.WithdrawalAccount.ChannelCount >= 3 &&
                            local.WithdrawalAccount.CloseButton.IsInteractable(), "Account form retains US channels and a working close Button");
                        CheckCashAndRecords(); Capture("history-first-record-account", local.WithdrawalAccount.gameObject);
                        Advance(12, .5); break;
                    case 12:
                        FreshCapture(); Click(local.WithdrawalAccount.CloseButton); Advance(13, 1); break;
                    case 13:
                        if (local.WithdrawalAccount != null || local.EarlyWithdrawalStage != 0 || game.ModalInputBlocked) return;
                        CheckCashAndRecords();
                        Check(local.WithdrawalConfirmation == null && local.EarlyVerificationPanel == null,
                            "Account preview closes without submitting or entering verification");
                        Finish(0); break;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Advance(int next, double delay = .8)
        { phase = next; readyAt = EditorApplication.timeSinceStartup + delay; }

        private static void CheckHistory(int height)
        {
            var panel = local.CashHistoryPanel;
            Check(Screen.width == 720 && Screen.height == height, "Actual history viewport is 720x" + height);
            Check(panel != null && panel.Rows.Count == 3 && local.SettingsPanel == null && game.ModalInputBlocked,
                "History contains three real records and blocks underlying gameplay");
            Check(panel.TitleLabel.text == "Withdrawal History" && panel.BalanceLabel.text == "$500",
                "History header shows its name and current $500 balance");
            string[] amounts = { "$0.40", "$0.60", "$500" };
            string[] times = { Date(FirstUtc), Date(SecondUtc), string.Empty };
            Canvas.ForceUpdateCanvases();
            Rect previous = default;
            for (int i = 0; i < 3; i++)
            {
                var row = panel.Rows[i];
                bool ready = i != 1;
                Check(row.Stage == i + 1 && row.TitleLabel.text == "Cash Reward #" + (i + 1) && row.AmountLabel.text == amounts[i],
                    "Row " + (i + 1) + " retains its real stage, title and amount");
                Check(row.TimeLabel.text == times[i], "Row " + (i + 1) + " preserves its stored local time without inventing a current-record date");
                Check(row.ActionButton.interactable == ready && row.ActionButton.gameObject.activeInHierarchy == ready,
                    "Row " + (i + 1) + " exposes a Button only when its record is actionable");
                Check(row.StatusLabel != null && row.StatusLabel.gameObject.activeInHierarchy == !ready,
                    "Row " + (i + 1) + " uses a separate status label for pending records");
                Check((ready ? row.ActionLabel.text : row.StatusLabel.text) == (ready ? "Withdraw" : "Pending - not paid"),
                    "Row " + (i + 1) + " clearly distinguishes pending status from withdrawal action");
                var serialized = new SerializedObject(row);
                var readyVisual = serialized.FindProperty("readyVisual").objectReferenceValue as GameObject;
                var pendingVisual = serialized.FindProperty("pendingVisual").objectReferenceValue as GameObject;
                Check(readyVisual != null && pendingVisual != null && readyVisual.activeInHierarchy == ready &&
                    pendingVisual.activeInHierarchy == !ready, "Row " + (i + 1) + " uses the correct ready/pending visual");
                Rect bounds = Bounds((RectTransform)row.transform);
                Check(InViewport(bounds), "Record card remains within the screen: " + (i + 1));
                if (i > 0) Check(bounds.yMax < previous.yMin, "Adjacent record cards remain separated: " + i + " / " + (i + 1));
                previous = bounds;
                foreach (var text in row.GetComponentsInChildren<TMP_Text>())
                {
                    if (!HasText(text)) continue;
                    text.ForceMeshUpdate();
                    Check(Contains(bounds, GlyphBounds(text)), "Actual text glyphs stay inside record card: " + text.name);
                }
            }
            CheckUI(panel.gameObject);
        }

        private static string Date(long utc) => DateTimeOffset.FromUnixTimeSeconds(utc).ToLocalTime()
            .ToString("g", CultureInfo.GetCultureInfo(audio.UserState.DisplayCulture(game.Tables)));

        private static void CheckCashAndRecords()
        {
            Check(game.User.Gold == 500 && game.User.Coin == 0 && game.User.LocalPendingReward == null,
                "Viewing history and account details does not change the balance or grant rewards");
            var rows = LocalEarningFlowPolicy.GetRecords(game.User);
            Check(rows.Count == 2 && rows[0].Stage == 1 && Mathf.Abs(rows[0].Amount - .4f) < .001f && rows[0].Archived &&
                !rows[0].Pending && rows[0].SubmittedUtcSeconds == 0 && rows[1].Stage == 2 &&
                Mathf.Abs(rows[1].Amount - .6f) < .001f && rows[1].Archived && rows[1].Pending && rows[1].SubmittedUtcSeconds == SecondUtc,
                "Unsubmitted $0.40 and pending $0.60 records keep their original states");
            foreach (JObject row in (JArray)game.User.LocalWithdrawalData["earningFlow"]["records"])
                Check(row["paid"] == null && row["success"] == null && row["paymentSuccess"] == null,
                    "No payment success is fabricated");
        }

        private static void Click(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Button is visible and interactable", false);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = Bounds((RectTransform)button.transform).center,
                button = PointerEventData.InputButton.Left };
            Hits.Clear(); EventSystem.current.RaycastAll(pointer, Hits);
            var target = Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target == button.gameObject, "Unity raycast reaches the actual Button: " + button.name);
            Check(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), "Standard Button click delivered", false);
        }

        private static bool HasText(TMP_Text text) => text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text);
        private static void CheckUI(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            var textBounds = new List<Rect>();
            var names = new List<string>();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>())
            {
                if (!HasText(text)) continue;
                text.ForceMeshUpdate();
                Rect glyphs = GlyphBounds(text);
                Check(!text.isTextOverflowing && !text.isTextTruncated, "Full text is rendered without truncation: " + text.name);
                Check(Contains(Bounds(text.rectTransform), glyphs), "Glyph mesh remains inside its text rectangle: " + text.name);
                Check(InViewport(glyphs), "Glyph mesh remains inside the screen: " + text.name);
                for (int i = 0; i < textBounds.Count; i++)
                {
                    Rect other = textBounds[i];
                    float overlapX = Mathf.Min(other.xMax, glyphs.xMax) - Mathf.Max(other.xMin, glyphs.xMin);
                    float overlapY = Mathf.Min(other.yMax, glyphs.yMax) - Mathf.Max(other.yMin, glyphs.yMin);
                    Check(overlapX <= 1 || overlapY <= 1, "Text does not overlap: " + names[i] + " / " + text.name);
                }
                textBounds.Add(glyphs); names.Add(text.name);
            }
        }

        private static Rect GlyphBounds(TMP_Text text)
        {
            Camera camera = CameraFor(text.transform);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo glyph = text.textInfo.characterInfo[i];
                if (!glyph.isVisible) continue;
                Vector3[] corners = { glyph.bottomLeft, glyph.topLeft, glyph.topRight, glyph.bottomRight };
                foreach (var corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(corner));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Bounds(RectTransform rect)
        {
            Camera camera = CameraFor(rect);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Camera CameraFor(Transform transform)
        {
            Canvas canvas = transform.GetComponentInParent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 &&
            inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
        private static bool InViewport(Rect rect) => Contains(new Rect(0, 0, Screen.width, Screen.height), rect);

        private static void Capture(string name, GameObject root)
        {
            // This visual acceptance covers the redesigned history prefab. The
            // unchanged account form is checked for navigation and cancellation;
            // its masked input text has a separate layout acceptance suite.
            if (root.GetComponent<LocalCashHistoryPanel>() != null) CheckUI(root);
            pendingCapture = Path.GetFullPath(Output + "/" + name + ".png");
            Captures.Add(new JObject { ["image"] = pendingCapture, ["width"] = Screen.width, ["height"] = Screen.height,
                ["cash"] = game.User.Gold, ["phase"] = phase, ["fixture"] = "Isolated Unity scene; no ad or payment submission" });
            ScreenCapture.CaptureScreenshot(pendingCapture);
        }
        private static void FreshCapture()
        {
            Check(pendingCapture != null && File.Exists(pendingCapture) && File.GetLastWriteTimeUtc(pendingCapture) >= began.AddSeconds(-2),
                "Screenshot is freshly written before the next UI mutation");
            pendingCapture = null;
        }
        private static void Check(bool condition, string message, bool record = true)
        {
            if (!condition) throw new InvalidOperationException(message);
            if (record) Checks.Add(message);
        }
        private static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }
        private static void Finish(int result)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = JObject.Parse(Fixture),
                ["checks"] = Checks, ["captures"] = Captures, ["error"] = error,
                ["verification"] = "Real scene, prefab and Button raycasts; two viewports; account preview without submission; no ads or payment" }.ToString());
            EditorApplication.update -= Tick; SceneManager.sceneLoaded -= ConfigureCountry;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else CompleteFinish();
        }
        private static void OnPlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.EnteredEditMode) CompleteFinish(); }
        private static void CompleteFinish()
        {
            if (!SessionState.GetBool(Key + ".Finish", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int result = SessionState.GetInt(Key + ".Result", 1);
            var report = JObject.Parse(SessionState.GetString(Key + ".Report", "{}"));
            try
            {
                bool existed; string saved;
                using (var reader = new BinaryReader(File.OpenRead(Backup))) { existed = reader.ReadBoolean(); saved = reader.ReadString(); }
                OriginalPreferenceFixture.Restore();
                if (File.Exists(Backup) || PlayerPrefs.HasKey(OriginalUserStore.Key) != existed ||
                    existed && PlayerPrefs.GetString(OriginalUserStore.Key) != saved) throw new InvalidOperationException("User preference restoration failed.");
                report["originalPreferencesRestored"] = true;
            }
            catch (Exception ex) { Debug.LogException(ex); report["restoreError"] = ex.ToString(); result = 1; }
            report["status"] = result == 0 ? "PASS" : "FAIL"; report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("LOCAL_HISTORY_STYLE_PLAY_" + (result == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(result);
        }
    }
}
