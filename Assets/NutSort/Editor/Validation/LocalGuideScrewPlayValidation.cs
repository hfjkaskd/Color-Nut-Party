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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Acceptance only: the real fourth-level initialization opens the shared guide prefab.
    [InitializeOnLoad]
    public static class LocalGuideScrewPlayValidation
    {
        private const string Key = "NutSort.LocalGuideScrewPlayValidation";
        private const string Output = "Docs/GuideScrewRestore/20261012";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private const string Fixture = "{\"Level\":4,\"LevelSeed\":0,\"Gold\":512.5,\"Coin\":0,\"RevokeCount\":5," +
            "\"UserName\":\"Guide_Tester\",\"UserId\":\"guide-screw-acceptance\",\"IsAudio\":true," +
            "\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true,\"IsCompleteCoinNewPeopleReward\":true," +
            "\"IsCompleteExtraGoldGuide\":true,\"IsShowCoin\":false,\"IsCompleteShowCoinRewardHint\":false," +
            "\"LocalWithdrawalData\":{\"tutorialBalanceApplied\":true,\"tutorialBalanceAmount\":500," +
            "\"tutorialBalanceNoticeAcknowledged\":true,\"earningFlow\":{\"activationComplete\":true,\"activationStep\":6}}}";
        // OriginalTeachingFlow selects LevelSeed + 70 for seeds 0..2, then 151.
        private static readonly int[] SharedTextIds = { 126, 127, 187, 70, 71, 72, 151 };
        private static readonly JArray Checks = new JArray(), Captures = new JArray(), TextCases = new JArray();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static int phase;
        private static double readyAt, deadline;
        private static DateTime began;
        private static string error, pendingCapture;

        static LocalGuideScrewPlayValidation()
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
                OriginalNewbieGuideValidation.Validate();
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetBool(Key + ".Finish", false); SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Shared guide screw: isolated US fixture");
                StartPolling(); EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void StartPolling()
        {
            ClearReferences(); phase = 0; readyAt = 0;
            deadline = EditorApplication.timeSinceStartup + 150; began = DateTime.UtcNow;
            error = pendingCapture = null; Checks.Clear(); Captures.Clear(); TextCases.Clear();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= ConfigureCountry; SceneManager.sceneLoaded += ConfigureCountry;
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
            if (game == null || startup == null || audio == null || startup.LocalGameplay == null || game.Level == null ||
                !game.Level.AreNutsInitialized || game.IsRestarting || startup.Loading.IsVisible) return false;
            local = startup.LocalGameplay;
            Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Uses production US configuration");
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
                if (now >= deadline) throw new TimeoutException("Guide screw validation timed out at phase " + phase);
                if (now < readyAt || !SceneReady() || startup.Replay.IsClickMasked) return;
                switch (phase)
                {
                    case 0:
                        if (!GuideReady()) return;
                        Check(game.User.Level == 4 && game.User.IsShowCoin && !game.User.IsCompleteShowCoinRewardHint &&
                            local.CoinUnlockGuide.InitialGuideIndex == 3 && game.ModalInputBlocked,
                            "Fourth-level initialization opens the real uncompleted diamond spotlight");
                        CheckBalances(); Advance(1, .5); break;
                    case 1:
                        Check(Screen.width == 720 && Screen.height == 1600, "Tall viewport is 720x1600");
                        CheckGuideAndSharedText(); Capture("guide-tall"); Advance(2, .5); break;
                    case 2:
                        FreshCapture(); PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Shared guide screw: short portrait fixture");
                        Advance(20, .2); break;
                    case 20:
                        if (Screen.width != 720 || Screen.height != 1280) return;
                        // The guide captures its target world position when it opens.
                        // Re-enter the real scene at the new viewport instead of
                        // resizing an already-open guide with cached target geometry.
                        Check(!game.User.IsCompleteShowCoinRewardHint, "Unclicked tall-screen guide remains incomplete before short-screen reload");
                        ClearReferences(); SceneManager.LoadScene("LuoSiSortGame"); Advance(3, .2); break;
                    case 3:
                        if (!GuideReady()) return;
                        Check(Screen.width == 720 && Screen.height == 1280, "Fresh short-screen scene uses 720x1280 before guide layout");
                        Advance(30, .5); break;
                    case 30:
                        CheckGuideAndSharedText(); Capture("guide-short"); Advance(4, .5); break;
                    case 4:
                        FreshCapture(); Click(local.CoinUnlockGuide.ContinueButton); Advance(5, .8); break;
                    case 5:
                        if (local.CoinUnlockGuide != null || game.ModalInputBlocked || !game.IsInitDone) return;
                        Check(game.User.IsCompleteShowCoinRewardHint && game.User.IsShowCoin,
                            "Actual guide Button finishes the guide and keeps the diamond entry unlocked");
                        CheckBalances(); CheckPersistedCompletion();
                        Check(UnityEngine.Object.FindObjectsOfType<OriginalNewbieGuideView>().Length == 0,
                            "Guide object is destroyed and gameplay input is released");
                        ClearReferences(); SceneManager.LoadScene("LuoSiSortGame"); Advance(6, 3); break;
                    case 6:
                        if (!game.IsInitDone || startup.Loading.IsVisible || game.IsRestarting || game.ModalInputBlocked) return;
                        Check(local.CoinUnlockGuide == null && local.EarningSequencePanel == null &&
                            game.User.IsShowCoin && game.User.IsCompleteShowCoinRewardHint,
                            "Real scene reload does not repeat the completed preview or diamond guide");
                        CheckBalances(); CheckPersistedCompletion(); Finish(0); break;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static bool GuideReady() => local.CoinUnlockGuide != null &&
            local.CoinUnlockGuide.HollowMask.gameObject.activeInHierarchy && !game.InputBlocked;
        private static void Advance(int value, double delay) { phase = value; readyAt = EditorApplication.timeSinceStartup + delay; }
        private static void CheckBalances()
        {
            Check(Mathf.Abs(game.User.Gold - 512.5f) < .0001f && game.User.Coin == 0 && game.User.LocalPendingReward == null,
                "Guide presentation and clicks leave cash at $512.50 and do not grant preview diamonds");
        }
        private static void CheckPersistedCompletion()
        {
            var saved = OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),
                Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            Check(saved.IsShowCoin && saved.IsCompleteShowCoinRewardHint && saved.Coin == 0 && Mathf.Abs(saved.Gold - 512.5f) < .0001f,
                "Production save contains completed guide markers and unchanged balances");
        }

        private static void CheckGuideAndSharedText()
        {
            Check(GuideReady(), "Real diamond guide and hollow mask are visible");
            var guide = local.CoinUnlockGuide;
            Check(InViewport(Bounds((RectTransform)guide.ContinueButton.transform)), "Fresh guide ContinueButton stays within the current viewport");
            var frame = guide.transform.Find("Pos/Tip") as RectTransform;
            Check(frame != null && frame.gameObject.activeInHierarchy, "Shared Pos/Tip frame is visible");
            var icon = frame.Find("YellowScrewDecoration")?.GetComponent<Image>();
            Check(icon != null && icon.sprite != null && icon.isActiveAndEnabled && icon.color.a >= .99f,
                "Yellow screw decoration is present with a nonempty visible sprite");
            Check(icon.type == Image.Type.Simple && icon.preserveAspect && !icon.raycastTarget && icon.GetComponent<Button>() == null,
                "Yellow screw preserves its artwork ratio and never intercepts clicks");
            Canvas.ForceUpdateCanvases();
            Rect frameBounds = Bounds(frame), iconBounds = Bounds(icon.rectTransform);
            Check(Contains(frameBounds, iconBounds) && InViewport(frameBounds), "Tip frame and full yellow screw stay inside the viewport");
            string original = guide.Tip.text;
            string expected = (string)JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningRewards").text)["coinGuideBody"];
            Check(original == expected, "Guide shows its real configured diamond-unlock instruction");
            CheckTextFit(guide.Tip, frameBounds, iconBounds, "Configured diamond instruction");
            try
            {
                foreach (int id in SharedTextIds)
                {
                    string text = game.Tables.Text.GetText(id, "en");
                    Check(!string.IsNullOrWhiteSpace(text) && text != id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "English shared-guide text exists in the original table: " + id);
                    guide.Tip.text = text;
                    guide.Tip.ForceMeshUpdate();
                    CheckTextFit(guide.Tip, frameBounds, iconBounds, "Original English text " + id);
                    TextCases.Add(new JObject { ["textId"] = id, ["text"] = text, ["width"] = Screen.width,
                        ["height"] = Screen.height, ["fontSize"] = guide.Tip.fontSize,
                        ["source"] = id == 70 || id == 71 || id == 72 || id == 151 ? "OriginalTeachingFlow" : "OriginalNewbieGuideView" });
                }
            }
            finally
            {
                guide.Tip.text = original; guide.Tip.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
            }
            Check(guide.Tip.text == expected, "Production guide text is restored before its screenshot");
            CheckTextFit(guide.Tip, Bounds(frame), Bounds(icon.rectTransform), "Restored diamond instruction");
            CheckBalances();
        }
        private static void CheckTextFit(TMP_Text text, Rect frame, Rect icon, string context)
        {
            text.ForceMeshUpdate();
            Rect glyphs = GlyphBounds(text);
            Check(!text.isTextOverflowing && !text.isTextTruncated, context + ": text is complete");
            Check(Contains(Bounds(text.rectTransform), glyphs) && Contains(frame, glyphs) && InViewport(glyphs),
                context + ": actual glyph mesh fits its text rectangle, tip frame and screen (glyphs=" + glyphs + ", frame=" + frame + ")");
            float overlapX = Mathf.Min(icon.xMax, glyphs.xMax) - Mathf.Max(icon.xMin, glyphs.xMin);
            float overlapY = Mathf.Min(icon.yMax, glyphs.yMax) - Mathf.Max(icon.yMin, glyphs.yMin);
            Check(overlapX <= 0 || overlapY <= 0, context + ": yellow screw does not overlap text");
        }

        private static void Click(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Guide Button is interactable");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = Bounds((RectTransform)button.transform).center,
                button = PointerEventData.InputButton.Left };
            Hits.Clear(); EventSystem.current.RaycastAll(pointer, Hits);
            var target = Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target == button.gameObject, "Real UI raycast reaches the standard guide ContinueButton");
            Check(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), "Standard Button receives the pointer click");
        }
        private static Camera CameraFor(Transform transform)
        {
            var canvas = transform.GetComponentInParent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
        private static Rect Bounds(RectTransform rect)
        {
            Camera camera = CameraFor(rect);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = min;
            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(corner));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 &&
            inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
        private static bool InViewport(Rect value) => Contains(new Rect(0, 0, Screen.width, Screen.height), value);
        private static void Capture(string name)
        {
            pendingCapture = Path.GetFullPath(Output + "/" + name + ".png");
            Captures.Add(new JObject { ["image"] = pendingCapture, ["width"] = Screen.width, ["height"] = Screen.height,
                ["cash"] = game.User.Gold, ["diamonds"] = game.User.Coin, ["text"] = local.CoinUnlockGuide.Tip.text });
            ScreenCapture.CaptureScreenshot(pendingCapture);
        }
        private static void FreshCapture()
        {
            Check(pendingCapture != null && File.Exists(pendingCapture) && File.GetLastWriteTimeUtc(pendingCapture) >= began.AddSeconds(-2),
                "Fresh guide screenshot is written before the next UI action");
            pendingCapture = null;
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Checks.Add(message);
        }
        private static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }
        private static void Finish(int result)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = JObject.Parse(Fixture),
                ["checks"] = Checks, ["captures"] = Captures, ["sharedEnglishTextCases"] = TextCases, ["error"] = error,
                ["verification"] = "Real fourth-level startup and shared guide prefab; two viewports; original English table layout probes restored before screenshots; real Button click; no ad or payment" }.ToString());
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
            Debug.Log("LOCAL_GUIDE_SCREW_PLAY_" + (result == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(result);
        }
    }
}
