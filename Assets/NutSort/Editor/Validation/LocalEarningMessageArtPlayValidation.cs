using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Presentation acceptance only: real saved continuation, real scene, no ad/payment clicks.
    [InitializeOnLoad]
    public static class LocalEarningMessageArtPlayValidation
    {
        private const string Key = "NutSort.LocalEarningMessageArtPlayValidation";
        private const string Output = "Docs/HintArtwork/20261013/validation";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private const string Fixture = "{\"Level\":3,\"LevelSeed\":0,\"Gold\":0,\"Coin\":0,\"RevokeCount\":5,\"ExchangeCount\":2,\"AddScrewCount\":3," +
            "\"IsAudio\":true,\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true,\"IsCompleteCoinNewPeopleReward\":true," +
            "\"IsShowCoin\":true,\"IsCompleteShowCoinRewardHint\":true,\"UserName\":\"Message_Art_Tester\",\"UserId\":\"message-art-acceptance\"," +
            "\"Level1Gold\":0.4,\"Level2Gold\":0.6,\"IsGoldReduceLevel1\":true,\"IsGoldReduceLevel2\":true," +
            "\"LocalWithdrawalData\":{\"earningFlow\":{\"activationStep\":0,\"records\":[" +
            "{\"stage\":1,\"amount\":0.4,\"createdUtcSeconds\":1791777600,\"archived\":true,\"pending\":false}," +
            "{\"stage\":2,\"amount\":0.6,\"createdUtcSeconds\":1791777660,\"archived\":true,\"pending\":true,\"submittedUtcSeconds\":1791777660}]}}}";
        private static readonly int[] MessageSteps = { 0, 1, 4, 5 };
        private static readonly string[] Prefixes = { "assessment", "activating", "activated", "summary" };
        private static readonly JArray Checks = new JArray(), Captures = new JArray(), Timings = new JArray(), TextDiagnostics = new JArray(), PresentationFailures = new JArray();
        private static readonly HashSet<int> CapturedSteps = new HashSet<int>();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static JObject config;
        private static int phase, viewport, observedStep, grants;
        private static double deadline, readyAt, stepObservedAt;
        private static float previousCash;
        private static bool bonusesChecked, receiptBefore, receiptAfter;
        private static DateTime began;
        private static string error;
        private static string pendingCapture;
        private static int pendingCaptureStep, pendingCaptureIndex, pendingCaptureFrame;
        private static DateTime pendingCaptureRequested;
        private static Exception pendingCaptureValidationError;

        static LocalEarningMessageArtPlayValidation()
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
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Four earning-message illustrations");
                StartPolling(); EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void StartPolling()
        {
            Checks.Clear(); Captures.Clear(); Timings.Clear(); TextDiagnostics.Clear(); PresentationFailures.Clear(); phase = viewport = 0;
            began = DateTime.UtcNow; error = null; config = null;
            deadline = EditorApplication.timeSinceStartup + 220;
            ResetRun();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= ConfigureCountry; SceneManager.sceneLoaded += ConfigureCountry;
        }
        private static void ResetRun()
        {
            ClearReferences(); CapturedSteps.Clear(); observedStep = -1; stepObservedAt = -1;
            pendingCapture = null; pendingCaptureValidationError = null;
            readyAt = 0; grants = 0; previousCash = 0;
            bonusesChecked = receiptBefore = receiptAfter = false;
        }
        private static void ClearReferences() { game = null; startup = null; audio = null; local = null; }
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
            if (game == null || startup == null || audio == null || startup.LocalGameplay == null) return false;
            local = startup.LocalGameplay;
            config = JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningFlow").text);
            Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Real scene uses the US reward configuration");
            Check((float)config["messageSeconds"] == 3, "Production message duration remains three seconds");
            local.BindSettlementAds(_ => { throw new InvalidOperationException("Unexpected interstitial request."); },
                _ => { throw new InvalidOperationException("Unexpected rewarded ad request."); });
            return true;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now >= deadline) throw new TimeoutException("Earning message art timed out at phase " + phase + ", step " + observedStep);
                if (now < readyAt || !SceneReady()) return;
                if (phase == 0)
                {
                    ObserveCash();
                    var panel = local.EarningSequencePanel;
                    int step = LocalEarningFlowPolicy.GetActivationStep(game.User);
                    if (panel != null)
                    {
                        if (observedStep != step) ObserveTransition(step);
                        if (pendingCapture != null)
                        {
                            Check(step == pendingCaptureStep, "Screenshot completes while its actual message remains visible");
                            if (Time.frameCount <= pendingCaptureFrame || !File.Exists(pendingCapture) ||
                                File.GetLastWriteTimeUtc(pendingCapture) < pendingCaptureRequested.AddMilliseconds(-100)) return;
                            Check(new FileInfo(pendingCapture).Length > 1000, "Actual GameView frame is persisted before presentation assertions");
                            if (pendingCaptureValidationError != null) throw pendingCaptureValidationError;
                            CapturedSteps.Add(pendingCaptureStep);
                            pendingCapture = null;
                        }
                        int messageIndex = Array.IndexOf(MessageSteps, step);
                        if (messageIndex >= 0 && !CapturedSteps.Contains(step) && !startup.Loading.IsVisible &&
                            Time.realtimeSinceStartupAsDouble - stepObservedAt >= .5)
                        {
                            Capture(Prefixes[messageIndex] + "-" + Suffix, panel);
                            pendingCaptureStep = step; pendingCaptureIndex = messageIndex;
                            // Editor update can expose another editor view's Screen
                            // dimensions on the following tick. Measure this exact
                            // GameView request now, but preserve its image first.
                            try { CheckMessage(panel, step, messageIndex); }
                            catch (Exception ex) { pendingCaptureValidationError = ex; }
                            return;
                        }
                        if (step == 2 && !bonusesChecked && panel.BonusPresentationComplete)
                        { CheckBonuses(panel); bonusesChecked = true; }
                        if (step == 3 && panel.ReceiptVisible)
                        {
                            if (!receiptBefore && !panel.Collected)
                            { CheckReceipt(panel); Check(game.User.Gold == 0, "Receipt appears before any cash grant"); receiptBefore = true; }
                            if (!receiptAfter && panel.Collected)
                            { Check(grants == 1 && game.User.Gold == 500, "Receipt collects the configured $500 once"); receiptAfter = true; }
                        }
                    }
                    if (panel != null || (bool?)game.User.LocalWithdrawalData?["earningFlow"]?["activationComplete"] != true) return;
                    ObserveTransition(6);
                    Check(CapturedSteps.Count == 4 && bonusesChecked && receiptBefore && receiptAfter && grants == 1,
                        "Four messages, unchanged bonuses and cash receipt complete naturally with exactly one grant");
                    CheckProtectedState(); CheckPersistedCompletion();
                    phase = 1; readyAt = now + .5; return;
                }
                if (phase == 1)
                {
                    ClearReferences(); SceneManager.LoadScene("LuoSiSortGame"); phase = 2; readyAt = now + 3; return;
                }
                if (phase == 2)
                {
                    if (!game.IsInitDone || startup.Loading.IsVisible || game.IsRestarting || game.ModalInputBlocked) return;
                    Check(local.EarningSequencePanel == null && game.User.Gold == 500 &&
                        (bool?)game.User.LocalWithdrawalData?["earningFlow"]?["activationComplete"] == true,
                        "Real scene reload preserves the single $500 grant and does not replay completed messages");
                    CheckProtectedState(); CheckPersistedCompletion();
                    if (viewport == 1) { Finish(0); return; }
                    viewport = 1;
                    PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Four earning messages: short portrait");
                    phase = 3; readyAt = now + .5; return;
                }
                if (Screen.width != 720 || Screen.height != 1280) return;
                PlayerPrefs.SetString(OriginalUserStore.Key, Fixture); PlayerPrefs.Save();
                ResetRun(); phase = 0; SceneManager.LoadScene("LuoSiSortGame");
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static string Suffix => viewport == 0 ? "tall" : "short";
        private static void ObserveTransition(int step)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (observedStep >= 0)
            {
                Check(step == observedStep + 1, "Activation advances in original order without skipping a page");
                double expected = observedStep == 2 ? (double)config["bonusSeconds"] :
                    observedStep == 3 ? (double)config["receiptTotalSeconds"] : (double)config["messageSeconds"];
                double elapsed = now - stepObservedAt;
                Check(Math.Abs(elapsed - expected) < .65, "Stage " + observedStep + " automatically advances after its configured " + expected + " seconds");
                Timings.Add(new JObject { ["viewport"] = Suffix, ["fromStep"] = observedStep, ["toStep"] = step,
                    ["elapsedSeconds"] = elapsed, ["configuredSeconds"] = expected });
            }
            else Check(step == 0 && LocalEarningFlowPolicy.EligibleForActivation(game.User), "A legal saved activation resumes at the first message");
            observedStep = step; stepObservedAt = now;
        }
        private static void ObserveCash()
        {
            if (Mathf.Abs(previousCash - game.User.Gold) < .0001f) return;
            Check(previousCash == 0 && game.User.Gold == 500 && grants == 0, "Only one zero-to-500 cash mutation occurs in the complete sequence");
            grants++; previousCash = game.User.Gold;
        }
        private static void CheckMessage(LocalEarningSequencePanel panel, int step, int index)
        {
            Canvas.ForceUpdateCanvases();
            Check(Screen.width == 720 && Screen.height == (viewport == 0 ? 1600 : 1280), "Message uses the requested rendered viewport");
            Check(panel.MessageVisible && !panel.BonusesVisible && !panel.ReceiptVisible && panel.MessageLayouts.Length == 4,
                "Only the chosen message group is visible and all four prefab layouts are configured");
            var active = panel.ActiveMessageLayout;
            Check(active != null && (int)active.Style == step && active.Root.activeInHierarchy,
                "Actual activation step selects its own statically authored layout");
            int visible = 0;
            foreach (var layout in panel.MessageLayouts) if (layout.Root.activeInHierarchy) visible++;
            Check(visible == 1, "Exactly one of the four message layouts is visible");
            Check(active.Title.text == (string)config[Prefixes[index] + "Title"] && active.Body.text == (string)config[Prefixes[index] + "Body"],
                "Message title and body retain the exact production wording: " + Prefixes[index]);
            Check(panel.MessageTitle == active.Title && panel.MessageBody == active.Body, "Public message labels reference the active layout");
            Check(active.ArtworkPath == "atlas/localearningmessages/cash-banner" || active.ArtworkPath == "atlas/localearningmessages/portrait-banner",
                "Illustration references an approved transparent Resource path");
            Image art = active.Artwork;
            Check(art != null && art.isActiveAndEnabled && art.sprite != null && art.sprite == Resources.Load<Sprite>(active.ArtworkPath) &&
                art.type == Image.Type.Simple && art.preserveAspect && !art.raycastTarget && art.color.a >= .99f,
                "Correct complete illustration loads dynamically with its aspect ratio and no intercepted clicks");
            Rect artBounds = Bounds(art.rectTransform);
            Check(InViewport(artBounds), "Full artwork rectangle stays on screen");
            float ratio = art.sprite.rect.width / art.sprite.rect.height;
            Check(Mathf.Abs(artBounds.width / artBounds.height / ratio - 1) < .03f, "Artwork layout matches its source proportions");
            Check(!active.Title.gameObject.activeInHierarchy == (step == 0), "Only Assessment intentionally hides its title");
            CheckText(active.Body, "body");
            if (active.Title.gameObject.activeInHierarchy)
            {
                CheckText(active.Title, "title");
                Check(!GlyphBounds(active.Title).Overlaps(GlyphBounds(active.Body)), "Title and body glyphs do not overlap");
            }
            Check(panel.GetComponentsInChildren<Button>().Length == 0, "Message remains an automatic presentation with no new buttons");
            CheckProtectedState();
        }
        private static void CheckBonuses(LocalEarningSequencePanel panel)
        {
            Check(panel.BonusesVisible && panel.BonusRows.Length == 5 && panel.BonusCaptions.Length == 5,
                "Existing five-benefit presentation remains configured and visible");
            for (int i = 0; i < 5; i++)
            {
                Check(panel.BonusRows[i].gameObject.activeInHierarchy && panel.BonusRows[i].alpha >= .99f &&
                    panel.BonusCaptions[i].text == (string)config["bonuses"][i], "Existing benefit " + (i + 1) + " stays fully visible with unchanged caption");
                var icon = panel.BonusRows[i].transform.Find("Icon")?.GetComponent<Image>();
                Check(icon != null && icon.sprite != null && icon.preserveAspect && InViewport(Bounds(icon.rectTransform)),
                    "Existing benefit " + (i + 1) + " keeps its icon within the viewport");
            }
        }
        private static void CheckReceipt(LocalEarningSequencePanel panel)
        {
            var banner = panel.transform.Find("Receipt/Banner")?.GetComponent<Image>();
            Check(banner != null && banner.sprite != null && banner.preserveAspect && InViewport(Bounds(banner.rectTransform)),
                "Existing cash receipt artwork remains visible and contained");
            Check(panel.ReceiptAmount.text == "$500", "Existing receipt retains the dynamic configured $500 amount");
            CheckText(panel.ReceiptAmount, "receipt amount");
        }
        private static void CheckProtectedState()
        {
            Check(game.User.Level == 3 && game.User.Coin == 0 && game.User.RevokeCount == 5 && game.User.ExchangeCount == 2 &&
                game.User.AddScrewCount == 3 && game.User.LocalPendingReward == null && game.User.UserLssInfo == null,
                "Illustrations never change progression, diamonds, tools, pending rewards or payout queue");
            var expected = JObject.Parse(Fixture)["LocalWithdrawalData"]["earningFlow"]["records"];
            Check(JToken.DeepEquals(expected, game.User.LocalWithdrawalData?["earningFlow"]?["records"]),
                "Original archived/unsubmitted and pending withdrawal records remain unchanged; no payment status is fabricated");
        }
        private static void CheckPersistedCompletion()
        {
            var saved = OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key), Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            Check(saved.Gold == 500 && saved.Coin == 0 && saved.IsCompleteExtraGoldGuide &&
                (bool?)saved.LocalWithdrawalData?["earningFlow"]?["activationComplete"] == true &&
                (bool?)saved.LocalWithdrawalData?["tutorialBalanceApplied"] == true &&
                JToken.DeepEquals(saved.LocalWithdrawalData?["earningFlow"]?["records"], JObject.Parse(Fixture)["LocalWithdrawalData"]["earningFlow"]["records"]),
                "Production save persists completion and one grant while preserving withdrawal records");
        }
        private static void CheckText(TMP_Text text, string label)
        {
            text.ForceMeshUpdate(); Rect glyphs = GlyphBounds(text), rect = Bounds(text.rectTransform);
            var diagnostic = new JObject { ["viewport"] = Suffix, ["step"] = observedStep, ["label"] = label,
                ["object"] = text.name, ["text"] = text.text, ["isActive"] = text.isActiveAndEnabled,
                ["isTextOverflowing"] = text.isTextOverflowing, ["isTextTruncated"] = text.isTextTruncated,
                ["color"] = new JObject { ["r"] = text.color.r, ["g"] = text.color.g, ["b"] = text.color.b, ["a"] = text.color.a },
                ["fontSize"] = text.fontSize, ["rectBounds"] = RectDiagnostic(rect), ["glyphBounds"] = RectDiagnostic(glyphs),
                ["glyphsInsideRect"] = Contains(rect, glyphs), ["glyphsInsideViewport"] = InViewport(glyphs),
                ["lineCount"] = text.textInfo.lineCount, ["characterCount"] = text.textInfo.characterCount };
            TextDiagnostics.Add(diagnostic);
            bool valid = text.isActiveAndEnabled && text.color.a >= .99f && !text.isTextOverflowing && !text.isTextTruncated &&
                Contains(rect, glyphs) && InViewport(glyphs);
            string message = label + " renders complete actual glyphs inside its configured text rectangle and screen; diagnostics=" + diagnostic.ToString(Newtonsoft.Json.Formatting.None);
            // Keep the original strict assertion and FAIL result, while collecting
            // all eight real frames so a failed layout has inspectable evidence.
            if (valid) Check(true, message);
            else PresentationFailures.Add(Suffix + ": " + message);
        }
        private static JObject RectDiagnostic(Rect rect) => new JObject { ["x"] = rect.x, ["y"] = rect.y,
            ["width"] = rect.width, ["height"] = rect.height };
        private static Camera CameraFor(Transform transform)
        { var canvas = transform.GetComponentInParent<Canvas>(); return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera; }
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners); Camera camera = CameraFor(rect);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (Vector3 corner in corners) { Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static Rect GlyphBounds(TMP_Text text)
        {
            Camera camera = CameraFor(text.transform);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                var glyph = text.textInfo.characterInfo[i]; if (!glyph.isVisible) continue;
                foreach (Vector3 corner in new[] { glyph.bottomLeft, glyph.topLeft, glyph.topRight, glyph.bottomRight })
                { Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(corner)); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
        private static bool InViewport(Rect value) => Contains(new Rect(0, 0, Screen.width, Screen.height), value);
        private static void Capture(string name, LocalEarningSequencePanel panel)
        {
            string path = Path.GetFullPath(Output + "/" + name + ".png");
            var canvas = panel.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera camera = game.WorldCamera;
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            Check(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null &&
                cameraData != null && cameraData.renderType == CameraRenderType.Base &&
                cameraData.cameraStack.Contains(canvas.worldCamera),
                "GameView uses the real scene camera stack and its configured UI camera");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("A graphics device is required for actual scene capture; do not launch with -nographics.");
            if (Application.isBatchMode)
                throw new InvalidOperationException("Run in the ordinary Unity Editor; batch mode does not render the required GameView UI.");
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvas.GetComponentsInChildren<TMP_Text>())
                if (text.isActiveAndEnabled) text.ForceMeshUpdate();
            pendingCapture = path; pendingCaptureRequested = DateTime.UtcNow; pendingCaptureFrame = Time.frameCount;
            // This captures the actual end-of-frame GameView, including Canvas UI.
            // Tick waits for the fresh file before evaluating presentation checks.
            ScreenCapture.CaptureScreenshot(path);
            var stackNames = new JArray(camera.name);
            foreach (Camera overlay in cameraData.cameraStack) if (overlay != null) stackNames.Add(overlay.name);
            Captures.Add(new JObject { ["image"] = path, ["width"] = Screen.width, ["height"] = Screen.height,
                ["step"] = observedStep, ["cash"] = game.User.Gold, ["title"] = panel.MessageTitle.text, ["body"] = panel.MessageBody.text,
                ["artwork"] = panel.ActiveMessageLayout.ArtworkPath, ["frame"] = Time.frameCount,
                ["captureMethod"] = "ScreenCapture: actual ordinary-Editor GameView at end of frame",
                ["cameraStack"] = stackNames, ["uiCamera"] = canvas.worldCamera.name,
                ["graphicsDevice"] = SystemInfo.graphicsDeviceName });
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); Checks.Add(Suffix + ": " + message); }
        private static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }
        private static void Finish(int result)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            if (result == 0)
            {
                if (Captures.Count != 8) { result = 1; error = "All eight requested message screenshots are required."; }
                foreach (JObject capture in Captures)
                {
                    string path = (string)capture["image"];
                    if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < began.AddSeconds(-2)) { result = 1; error = "Missing fresh screenshot: " + path; break; }
                }
                if (PresentationFailures.Count > 0)
                {
                    result = 1;
                    error = "Strict text presentation checks failed; all captured frames are retained. See presentationFailures.";
                }
            }
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true); SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = JObject.Parse(Fixture), ["checks"] = Checks,
                ["captures"] = Captures, ["timings"] = Timings, ["textDiagnostics"] = TextDiagnostics,
                ["presentationFailures"] = PresentationFailures, ["error"] = error,
                ["verification"] = "Real saved step-0 activation and scene, four message artworks at both viewports, natural three-second transitions, existing bonus/receipt regression, single grant and reload, original preferences restored" }.ToString());
            EditorApplication.update -= Tick; SceneManager.sceneLoaded -= ConfigureCountry;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged; EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else CompleteFinish();
        }
        private static void OnPlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.EnteredEditMode) CompleteFinish(); }
        private static void CompleteFinish()
        {
            if (!SessionState.GetBool(Key + ".Finish", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int result = SessionState.GetInt(Key + ".Result", 1); var report = JObject.Parse(SessionState.GetString(Key + ".Report", "{}"));
            try
            {
                bool existed; string saved;
                using (var reader = new BinaryReader(File.OpenRead(Backup))) { existed = reader.ReadBoolean(); saved = reader.ReadString(); }
                OriginalPreferenceFixture.Restore();
                if (File.Exists(Backup) || PlayerPrefs.HasKey(OriginalUserStore.Key) != existed || existed && PlayerPrefs.GetString(OriginalUserStore.Key) != saved)
                    throw new InvalidOperationException("Original preference restoration failed.");
                report["originalPreferencesRestored"] = true;
            }
            catch (Exception ex) { Debug.LogException(ex); report["restoreError"] = ex.ToString(); result = 1; }
            report["status"] = result == 0 ? "PASS" : "FAIL"; report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false); EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("LOCAL_EARNING_MESSAGE_ART_PLAY_" + (result == 0 ? "PASS" : "FAIL")); EditorApplication.Exit(result);
        }
    }
}
