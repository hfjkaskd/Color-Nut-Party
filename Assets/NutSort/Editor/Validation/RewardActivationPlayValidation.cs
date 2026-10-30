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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Acceptance tooling only: real scene, real prefab and real saved activation continuation.
    [InitializeOnLoad]
    public static class RewardActivationPlayValidation
    {
        private const string Key = "NutSort.RewardActivationPlayValidation";
        private const string Output = "Docs/CashReceiptStyle/20261012";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private const string Fixture = "{\"Level\":3,\"LevelSeed\":0,\"Gold\":0,\"Coin\":0,\"RevokeCount\":5," +
            "\"IsAudio\":true,\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true," +
            "\"IsCompleteCoinNewPeopleReward\":true,\"IsShowCoin\":true,\"IsCompleteShowCoinRewardHint\":true," +
            "\"UserName\":\"Activation_Tester\",\"UserId\":\"activation-acceptance-fixture\"," +
            "\"Level1Gold\":0.4,\"Level2Gold\":0.6,\"IsGoldReduceLevel1\":true,\"IsGoldReduceLevel2\":true," +
            "\"LocalWithdrawalData\":{\"earningFlow\":{\"activationStep\":2,\"records\":[" +
            "{\"stage\":1,\"amount\":0.4,\"createdUtcSeconds\":1791777600,\"archived\":true,\"pending\":false}," +
            "{\"stage\":2,\"amount\":0.6,\"createdUtcSeconds\":1791777660,\"archived\":true,\"pending\":true,\"submittedUtcSeconds\":1791777660}]}}}";

        private static readonly JArray Checks = new JArray(), Captures = new JArray(), Timings = new JArray();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static JObject config;
        private static int phase, viewport, grants;
        private static double deadline, readyAt, firstObserved, presentationDoneAt;
        private static float previousCash;
        private static bool firstCaptured, completeCaptured, receiptBefore, receiptAfter;
        private static DateTime began;
        private static string error;

        static RewardActivationPlayValidation()
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
                SessionState.SetBool(Key + ".Finish", false);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Reward activation: isolated acceptance fixture");
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void StartPolling()
        {
            Checks.Clear(); Captures.Clear(); Timings.Clear();
            phase = viewport = 0;
            began = DateTime.UtcNow;
            error = null;
            deadline = EditorApplication.timeSinceStartup + 180;
            config = null;
            ResetRun();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= ConfigureCountry; SceneManager.sceneLoaded += ConfigureCountry;
        }

        private static void ResetRun()
        {
            game = null; startup = null; audio = null; local = null;
            firstObserved = presentationDoneAt = -1;
            readyAt = 0; grants = 0; previousCash = 0;
            firstCaptured = completeCaptured = receiptBefore = receiptAfter = false;
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
            if (game == null || startup == null || audio == null || startup.LocalGameplay == null) return false;
            local = startup.LocalGameplay;
            config = JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningFlow").text);
            Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Production US reward configuration is selected");
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
                if (now >= deadline) throw new TimeoutException("Reward activation timed out at phase " + phase);
                if (now < readyAt || !SceneReady()) return;
                if (phase == 0)
                {
                    ObserveCash();
                    var panel = local.EarningSequencePanel;
                    if (panel != null && panel.BonusesVisible)
                    {
                        if (firstObserved < 0)
                        {
                            firstObserved = Time.realtimeSinceStartupAsDouble;
                            Check(LocalEarningFlowPolicy.GetActivationStep(game.User) == 2 &&
                                LocalEarningFlowPolicy.EligibleForActivation(game.User), "Saved step 2 resumes the legal activation path");
                            Check(game.User.Gold == 0, "Bonus presentation begins before the cash balance is granted");
                            CheckBonuses(panel, true);
                        }
                        else CheckRowsVisible(panel);
                        if (!firstCaptured && !startup.Loading.IsVisible)
                        {
                            Check(!panel.BonusPresentationComplete, "First visible screenshot precedes completion of the attention animation");
                            CheckBonuses(panel, true);
                            Capture("activation-first-" + Suffix, panel.gameObject);
                            firstCaptured = true;
                        }
                        if (!completeCaptured && panel.BonusPresentationComplete && !startup.Loading.IsVisible)
                        {
                            CheckBonuses(panel, true);
                            presentationDoneAt = Time.realtimeSinceStartupAsDouble;
                            Capture("activation-complete-" + Suffix, panel.gameObject);
                            completeCaptured = true;
                        }
                    }
                    if (panel != null && panel.ReceiptVisible)
                    {
                        Check(firstCaptured && completeCaptured, "Both the first visible and completed bonus page were captured");
                        Check(panel.ReceiptAmount.text == "$500", "The next automatic panel displays the real configured $500 receipt", false);
                        if (!receiptBefore && !panel.Collected)
                        {
                            double elapsed = Time.realtimeSinceStartupAsDouble - firstObserved;
                            double expected = (double)config["bonusSeconds"];
                            Check(Math.Abs(elapsed - expected) < .65, "Bonus page automatically advances after its configured " + expected + " seconds");
                            Check(game.User.Gold == 0, "Receipt is visible before its collect delay, wallet is still zero");
                            Timings.Add(new JObject { ["viewport"] = Suffix, ["bonusObservedSeconds"] = elapsed,
                                ["configuredSeconds"] = expected, ["attentionCompleteSeconds"] = presentationDoneAt - firstObserved });
                            Capture("activation-3-receipt-" + Suffix, panel.gameObject);
                            receiptBefore = true;
                        }
                        if (!receiptAfter && panel.Collected)
                        {
                            Check(game.User.Gold == 500 && grants == 1, "$500 is collected exactly once by the real receipt callback");
                            Capture("activation-3-collected-" + Suffix, panel.gameObject);
                            receiptAfter = true;
                        }
                    }
                    if ((bool?)game.User.LocalWithdrawalData?["earningFlow"]?["activationComplete"] != true || panel != null) return;
                    Check(firstCaptured && completeCaptured && receiptBefore && receiptAfter && grants == 1,
                        "Entire automatic activation completed with all visual stages and one cash mutation");
                    Check(game.User.Gold == 500 && game.User.Coin == 0, "Activation affects only the expected $500 cash balance");
                    CheckNoPayment();
                    game.SaveUserData();
                    phase = 1;
                    readyAt = now + .4;
                    return;
                }
                if (phase == 1)
                {
                    ClearSceneReferences();
                    SceneManager.LoadScene("LuoSiSortGame");
                    phase = 2; readyAt = now + 3;
                    return;
                }
                if (phase == 2)
                {
                    if (!game.IsInitDone || startup.Loading.IsVisible || game.IsRestarting) return;
                    Check(game.User.Gold == 500 && game.User.Coin == 0 && local.EarningSequencePanel == null &&
                        (bool?)game.User.LocalWithdrawalData?["earningFlow"]?["activationComplete"] == true,
                        "Actual scene reload preserves $500 and does not replay or grant the activation again");
                    CheckNoPayment();
                    if (viewport == 1) { Finish(0); return; }
                    viewport = 1;
                    PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Reward activation: short portrait acceptance fixture");
                    phase = 3; readyAt = now + .5;
                    return;
                }
                if (phase == 3)
                {
                    if (Screen.width != 720 || Screen.height != 1280) return;
                    PlayerPrefs.SetString(OriginalUserStore.Key, Fixture); PlayerPrefs.Save();
                    ResetRun();
                    phase = 0;
                    SceneManager.LoadScene("LuoSiSortGame");
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static string Suffix => viewport == 0 ? "tall" : "short";
        private static void ClearSceneReferences() { game = null; startup = null; audio = null; local = null; }

        private static void ObserveCash()
        {
            if (Mathf.Abs(previousCash - game.User.Gold) < .0001f) return;
            Check(previousCash == 0 && game.User.Gold == 500 && grants == 0,
                "Only one legal zero-to-500 cash mutation occurs during activation");
            grants++; previousCash = game.User.Gold;
        }

        private static void CheckRowsVisible(LocalEarningSequencePanel panel)
        {
            Check(panel.BonusRows.Length == 5 && panel.BonusCaptions.Length == 5, "Five benefit rows are configured", false);
            for (int i = 0; i < 5; i++)
                Check(panel.BonusRows[i] != null && panel.BonusRows[i].gameObject.activeInHierarchy && panel.BonusRows[i].alpha >= .99f,
                    "All five benefit cards remain fully visible during the attention animation: " + (i + 1), false);
        }

        private static void CheckBonuses(LocalEarningSequencePanel panel, bool detailed)
        {
            Canvas.ForceUpdateCanvases();
            CheckRowsVisible(panel);
            Check(Screen.width == 720 && Screen.height == (viewport == 0 ? 1600 : 1280), "Requested viewport is the actual rendered size");
            Check(panel.GetComponentsInChildren<Button>(true).Length == 0, "Activation presentation requires no ad, payment or confirmation Button");
            var avatar = panel.transform.Find("Bonuses/AvatarMask/Avatar")?.GetComponent<Image>();
            Check(avatar != null && avatar.sprite != null && avatar.isActiveAndEnabled, "Player portrait is present above the benefit cards");
            var player = panel.transform.Find("Bonuses/PlayerRibbon/PlayerCaption")?.GetComponent<TMP_Text>();
            Check(player != null && player.text == "You", "Player ribbon identifies the portrait as You");
            var sprites = new HashSet<Sprite>();
            Rect previous = default;
            for (int i = 0; i < 5; i++)
            {
                Transform row = panel.BonusRows[i].transform;
                var icon = row.Find("Icon")?.GetComponent<Image>();
                var value = row.Find("BonusBadge/Value")?.GetComponent<TMP_Text>();
                var caption = panel.BonusCaptions[i];
                Check(icon != null && icon.sprite != null && icon.preserveAspect && icon.isActiveAndEnabled,
                    "Benefit " + (i + 1) + " has a configured, aspect-preserving icon");
                Check(sprites.Add(icon.sprite), "Benefit " + (i + 1) + " has its own distinct reward icon");
                Check(value != null && value.text == "Bonus " + (i + 1), "Benefit title is numbered correctly: " + (i + 1));
                Check(caption.text == (string)config["bonuses"][i], "Benefit caption uses the production configuration: " + (i + 1));
                Rect bounds = Bounds((RectTransform)row);
                Check(Contains(bounds, Bounds(icon.rectTransform)) && Contains(bounds, Bounds(caption.rectTransform)),
                    "Icon and caption fit within benefit card " + (i + 1));
                Check(Contains(Bounds((RectTransform)value.transform.parent), Bounds(value.rectTransform)),
                    "Bonus title fits its ribbon " + (i + 1));
                if (i > 0) Check(bounds.yMax <= previous.yMin + 1, "Benefit cards have clear vertical separation: " + i + " / " + (i + 1));
                previous = bounds;
            }
            if (detailed) CheckUI(panel.gameObject);
        }

        private static void CheckUI(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>())
            {
                if (!text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing && !text.isTextTruncated, "Text is complete inside its own rectangle: " + text.name);
                Check(InViewport(Bounds(text.rectTransform)), "Text remains inside the screen: " + text.name);
            }
            foreach (var image in root.GetComponentsInChildren<Image>())
            {
                if (!image.isActiveAndEnabled || image.sprite == null || image.color.a < .01f) continue;
                Check(InViewport(Bounds(image.rectTransform)), "Image remains inside the screen: " + image.name);
            }
        }

        private static void CheckReceipt(LocalEarningSequencePanel panel)
        {
            Canvas.ForceUpdateCanvases();
            var receipt = panel.transform.Find("Receipt") as RectTransform;
            Check(receipt != null && receipt.gameObject.activeInHierarchy, "Cash receipt container is visible");
            Check(receipt.GetComponent<Image>() == null, "Cash receipt has no old cream-card shell");
            var banner = receipt.Find("Banner")?.GetComponent<Image>();
            Check(banner != null && banner.sprite != null && banner.isActiveAndEnabled && banner.preserveAspect,
                "Cash illustration and wooden ribbon use a visible, aspect-preserving banner");
            Check(AssetDatabase.GetAssetPath(banner.sprite).EndsWith("/success-title.png", StringComparison.Ordinal),
                "Cash receipt uses the complete cash, coin and wrench artwork");
            Rect bannerBounds = Bounds(banner.rectTransform);
            Check(InViewport(bannerBounds), "Full cash receipt banner remains inside the screen");
            float spriteAspect = banner.sprite.rect.width / banner.sprite.rect.height;
            Check(Mathf.Abs(bannerBounds.width / bannerBounds.height / spriteAspect - 1f) < .03f,
                "Cash receipt banner keeps its source aspect ratio without stretching");

            TMP_Text amount = panel.ReceiptAmount;
            Check(amount != null && amount.isActiveAndEnabled && amount.transform.IsChildOf(receipt) &&
                amount.text == "$500" && amount.color.a >= .99f && amount.canvasRenderer.GetAlpha() >= .99f,
                "Cash receipt amount remains fully visible and uses the real $500 value");
            amount.ForceMeshUpdate();
            Check(!amount.isTextOverflowing && !amount.isTextTruncated && amount.textInfo.characterCount == 4,
                "All four characters of $500 render without overflow or truncation");
            Rect amountGlyphs = TextBounds(amount);
            Check(amountGlyphs.height >= 40 && amountGlyphs.width >= 140,
                "Cash receipt amount remains large enough to read at the actual viewport size");
            // Source success-title.png: blank ribbon face occupies x 12%-88%,
            // y 20%-52% measured from the bottom. Validate rendered glyphs rather
            // than the larger TMP layout rectangle, so the art remains unaltered.
            var ribbonSafeArea = new Rect(bannerBounds.xMin + bannerBounds.width * .12f,
                bannerBounds.yMin + bannerBounds.height * .20f,
                bannerBounds.width * .76f, bannerBounds.height * .32f);
            Check(Contains(ribbonSafeArea, amountGlyphs),
                "Amount glyphs fit the blank ribbon and do not cover the cash illustration or curled edges" +
                " (glyphs=" + amountGlyphs + ", safe=" + ribbonSafeArea + ")");
            Check(InViewport(amountGlyphs), "Rendered receipt amount remains inside the screen");
        }

        private static Rect TextBounds(TMP_Text text)
        {
            var canvas = text.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            // TMP textBounds uses font ascender/descender metrics (line height),
            // not the visible characters. Inspect actual glyph mesh corners.
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo glyph = text.textInfo.characterInfo[i];
                if (!glyph.isVisible) continue;
                Vector3[] corners = { glyph.bottomLeft, glyph.topLeft, glyph.topRight, glyph.bottomRight };
                for (int j = 0; j < corners.Length; j++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(corners[j]));
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1 &&
            inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
        private static bool InViewport(Rect rect) => Contains(new Rect(0, 0, Screen.width, Screen.height), rect);
        private static Rect Bounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void CheckNoPayment()
        {
            var records = LocalEarningFlowPolicy.GetRecords(game.User);
            Check(records.Count == 2 && records[0].Archived && !records[0].Pending && records[1].Archived && records[1].Pending,
                "Early withdrawal records preserve their unsubmitted/pending states");
            foreach (JObject row in (JArray)game.User.LocalWithdrawalData["earningFlow"]["records"])
                Check(row["paid"] == null && row["success"] == null && row["paymentSuccess"] == null,
                    "No invented payment-success state is written");
        }

        private static void Capture(string name, GameObject root)
        {
            CheckUI(root);
            var panel = root.GetComponent<LocalEarningSequencePanel>();
            if (panel != null && panel.ReceiptVisible) CheckReceipt(panel);
            string path = Path.GetFullPath(Output + "/" + name + ".png");
            Captures.Add(new JObject { ["image"] = path, ["width"] = Screen.width, ["height"] = Screen.height,
                ["cash"] = game.User.Gold, ["frame"] = Time.frameCount,
                ["stage"] = LocalEarningFlowPolicy.GetActivationStep(game.User),
                ["fixture"] = "Isolated Unity scene acceptance fixture, not a payment or device capture" });
            ScreenCapture.CaptureScreenshot(path);
        }

        private static void Check(bool condition, string message, bool record = true)
        {
            if (!condition) throw new InvalidOperationException(message);
            if (record) Checks.Add(Suffix + ": " + message);
        }
        private static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }
        private static void Finish(int result)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            if (result == 0)
                foreach (JObject capture in Captures)
                {
                    string path = (string)capture["image"];
                    if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < began.AddSeconds(-2))
                    { result = 1; error = "Missing fresh screenshot: " + path; break; }
                }
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = JObject.Parse(Fixture),
                ["checks"] = Checks, ["captures"] = Captures, ["timings"] = Timings, ["error"] = error,
                ["verification"] = "Real Unity scene + production saved continuation at step 2; no payment/ad clicks; two actual viewport sizes" }.ToString());
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
            report["status"] = result == 0 ? "PASS" : "FAIL";
            report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("REWARD_ACTIVATION_PLAY_" + (result == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(result);
        }
    }
}
