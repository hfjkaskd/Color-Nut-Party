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
    [InitializeOnLoad]
    public static class LocalCashStatsPlayValidation
    {
        private const string Key = "NutSort.LocalCashStatsPlayValidation";
        private const string Output = "Docs/StatsDisplayFix/20261013";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private static readonly JArray Checks = new JArray(), Captures = new JArray();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static readonly string[] ProtectedKeys = { "Level", "LevelSeed", "Gold", "Coin", "RevokeCount", "ExchangeCount", "AddScrewCount",
            "TodayPassLevelCount", "NormalGetTimes", "InterAdTimes", "LocalPendingReward", "UserLssInfo", "IsTXLevel1", "IsTXLevel2",
            "Level1Gold", "Level2Gold", "IsGoldReduceLevel1", "IsGoldReduceLevel2", "TXTargetGold", "ComeOnGold", "ComeOnAddGold" };
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static OriginalWithdrawalPanel closedPanel;
        private static JObject protectedState;
        private static int phase, firstAttempts;
        private static double readyAt, deadline;
        private static DateTime began, localBefore, localAfter, utcBefore, utcAfter;
        private static string error, pendingCapture, firstStatsDate;

        static LocalCashStatsPlayValidation()
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
                string fixture = Fixture();
                OriginalPreferenceFixture.Begin(fixture);
                SessionState.SetString(Key + ".Fixture", fixture);
                SessionState.SetBool(Key + ".Finish", false); SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Simulated cash statistics acceptance");
                StartPolling(); EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static string Fixture()
        {
            var value = JObject.Parse("{\"Level\":4,\"LevelSeed\":0,\"Gold\":512.5,\"Coin\":486.73,\"RevokeCount\":5,\"ExchangeCount\":2,\"AddScrewCount\":3," +
                "\"UserName\":\"Stats_Tester\",\"UserId\":\"cash-stats-acceptance\",\"UserLevel\":1,\"IsAudio\":true,\"IsVibrate\":true," +
                "\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true,\"IsCompleteCoinNewPeopleReward\":true,\"IsShowCoin\":true," +
                "\"IsCompleteShowCoinRewardHint\":true,\"IsCompleteExtraGoldGuide\":true,\"Level1Gold\":0.4,\"Level2Gold\":0.6," +
                "\"IsGoldReduceLevel1\":true,\"IsGoldReduceLevel2\":true,\"LoginDay\":2,\"LoginDayCoin\":2,\"TodayPassLevelCount\":7," +
                "\"LocalWithdrawalData\":{\"tutorialBalanceApplied\":true,\"tutorialBalanceAmount\":500,\"tutorialBalanceNoticeAcknowledged\":true," +
                "\"earningFlow\":{\"activationComplete\":true,\"activationStep\":6,\"records\":[" +
                "{\"stage\":1,\"amount\":0.4,\"createdUtcSeconds\":1791777600,\"archived\":true,\"pending\":false}," +
                "{\"stage\":2,\"amount\":0.6,\"createdUtcSeconds\":1791777660,\"archived\":true,\"pending\":true,\"submittedUtcSeconds\":1791777660}]}}}");
            value["LocalWithdrawalData"]["lastLoginDate"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            value["LoginTime"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return value.ToString();
        }
        private static void StartPolling()
        {
            ClearReferences(); closedPanel = null; protectedState = null;
            phase = firstAttempts = 0; readyAt = 0; deadline = EditorApplication.timeSinceStartup + 160;
            began = DateTime.UtcNow; error = pendingCapture = firstStatsDate = null;
            Checks.Clear(); Captures.Clear();
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
                !game.Level.AreNutsInitialized || !game.IsInitDone || game.InputBlocked || game.IsRestarting || startup.Loading.IsVisible) return false;
            local = startup.LocalGameplay;
            Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Actual scene uses isolated US display culture");
            local.BindSettlementAds(_ => { throw new InvalidOperationException("Unexpected interstitial request."); },
                _ => { throw new InvalidOperationException("Unexpected rewarded ad request."); });
            return true;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException("Cash stats validation timed out at phase " + phase);
                if (EditorApplication.timeSinceStartup < readyAt || !SceneReady() || startup.Replay.IsClickMasked) return;
                switch (phase)
                {
                    case 0:
                        if (game.ModalInputBlocked) return;
                        ValidateDataLayer(); protectedState = Protected(game.User); Open(); Advance(1); break;
                    case 1:
                        CheckStats("first open"); firstAttempts = game.User.TodayChallengeTimes; firstStatsDate = game.User.StatsDisplayUtcDate;
                        Capture("cash-stats-tall"); Advance(2); break;
                    case 2:
                        FreshCapture(); Close(); Advance(3); break;
                    case 3:
                        CheckClosed(); CheckUnchanged(); Open(); Advance(4); break;
                    case 4:
                        CheckStats("same-day reopen"); CheckCachedAttempts(); Close(); Advance(5); break;
                    case 5:
                        CheckClosed(); CheckUnchanged();
                        PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Simulated statistics short portrait"); Advance(6); break;
                    case 6:
                        if (Screen.width != 720 || Screen.height != 1280) return;
                        ClearReferences(); SceneManager.LoadScene("LuoSiSortGame"); Advance(7); break;
                    case 7:
                        if (game.ModalInputBlocked) return;
                        CheckUnchanged(); Open(); Advance(8); break;
                    case 8:
                        CheckStats("scene reload"); CheckCachedAttempts(); Capture("cash-stats-short-reloaded"); Advance(9); break;
                    case 9:
                        FreshCapture(); Close(); Advance(10); break;
                    case 10:
                        CheckClosed(); CheckUnchanged(); CheckSavedStats(); Finish(0); break;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void Advance(int next) { phase = next; readyAt = EditorApplication.timeSinceStartup + .85; }
        private static void ValidateDataLayer()
        {
            var defaults = Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults");
            var settings = Resources.Load<GameObject>("Prefabs/PlayerInfo/PlayerInfo").GetComponent<OriginalPlayerInfo>().SimulatedStatsSettings;
            var user = OriginalUserDataJson.Read(SessionState.GetString(Key + ".Fixture", "{}"), defaults);
            var profile = Resources.Load<CountryRewardCatalog>("Configuration/CountryRewardCatalog").Load("US");
            profile.ApplyTo(user);
            var marquee = OriginalMarqueeData.Read(profile.CreateDisplayDocument()); marquee.Init();
            var localTime = new DateTime(2026, 10, 12, 18, 31, 59, DateTimeKind.Local);
            var utcTime = new DateTime(2026, 10, 13, 0, 31, 59, DateTimeKind.Utc);
            var before = Protected(user);
            int challengeDraws = 0, cashDraws = 0;
            Func<int, int, int> attempts = (min, max) => { Check(min == 5 && max == 11, "Original daily attempts use integer [5,11)"); challengeDraws++; return 7; };
            Func<float, float, float> cash = (min, max) => { Check(min == marquee.CashMinimum && max == marquee.CashMaximum, "Later amount samples original captured display range"); cashDraws++; return min; };
            for (int level = 1; level <= 2; level++)
            {
                var stats = OriginalSimulatedStats.Create(level, user, marquee, settings, "en-US", localTime, utcTime, attempts, cash);
                float expected = (float)user.GoldRewardTargetS2CData["bear_list"][level - 1]["psi_value"];
                Check(stats.ChallengeTimes == 1 && Mathf.Approximately(stats.CashAmount, expected) && !stats.DisplayStateChanged,
                    "Displayed level " + level + " uses attempts 1 and its configured reward without saving/sampling");
                Check(stats.PassCount == 25553 && stats.Title.Contains("Simulated stats") && stats.Title.Contains("10/13/2026"),
                    "Original local-minute count and independently UTC-dated simulated title are explicit");
            }
            Check(challengeDraws == 0 && cashDraws == 0, "First two levels never draw random daily attempts or a later average");
            user.TodayChallengeTimes = 0; user.StatsDisplayUtcDate = "2026-10-12";
            var later = OriginalSimulatedStats.Create(3, user, marquee, settings, "en-US", localTime, utcTime, attempts, cash);
            Check(later.DisplayStateChanged && later.ChallengeTimes == 7 && challengeDraws == 1 && cashDraws == 1 && user.StatsDisplayUtcDate == "2026-10-13",
                "Later display initializes one valid daily attempt sample and records its UTC date");
            var repeated = OriginalSimulatedStats.Create(3, user, marquee, settings, "en-US", localTime, utcTime, attempts,
                (min, max) => { cashDraws++; return max; });
            Check(!repeated.DisplayStateChanged && repeated.ChallengeTimes == 7 && challengeDraws == 1 && cashDraws == 2 &&
                repeated.CashAmount == marquee.CashMaximum && later.CashAmount == marquee.CashMinimum,
                "Same UTC day preserves attempts while the original average is freshly sampled per opening");
            user = OriginalUserDataJson.Read(OriginalUserDataJson.Write(user), defaults);
            var restored = OriginalSimulatedStats.Create(3, user, marquee, settings, "en-US", localTime, utcTime, attempts, cash);
            Check(!restored.DisplayStateChanged && restored.ChallengeTimes == 7 && challengeDraws == 1,
                "Existing user JSON serialization restores same-day attempt sample and date");
            var next = OriginalSimulatedStats.Create(3, user, marquee, settings, "en-US", localTime.AddDays(1), utcTime.AddDays(1),
                (min, max) => { Check(min == 5 && max == 11, "Cross-day range remains [5,11)"); challengeDraws++; return 10; }, cash);
            Check(next.DisplayStateChanged && next.ChallengeTimes == 10 && challengeDraws == 2 && user.StatsDisplayUtcDate == "2026-10-14",
                "Next UTC day takes exactly one new 5-through-10 sample");
            user.TodayChallengeTimes = 0;
            var repaired = OriginalSimulatedStats.Create(3, user, marquee, settings, "en-US", localTime.AddDays(1), utcTime.AddDays(1), attempts, cash);
            Check(repaired.DisplayStateChanged && repaired.ChallengeTimes == 7 && challengeDraws == 3, "Nonpositive same-day attempts are repaired once");
            Check(JToken.DeepEquals(before, Protected(user)), "Statistics creation leaves all balances, tools, progression, rewards, records and queue unchanged");
        }
        private static void Open()
        {
            Check(local.WithdrawalPanel == null && !game.ModalInputBlocked && !game.InputBlocked, "Gameplay is available before opening cash statistics");
            localBefore = DateTime.Now; utcBefore = DateTime.UtcNow;
            Click(local.RewardsTop.Gold.Click);
            localAfter = DateTime.Now; utcAfter = DateTime.UtcNow;
            Check(local.WithdrawalPanel != null && game.ModalInputBlocked, "Actual HUD Cash Button opens the production withdrawal panel");
        }
        private static void Close() { closedPanel = local.WithdrawalPanel; Click(closedPanel.CloseButton); }
        private static void CheckClosed()
        {
            Check(closedPanel == null && local.WithdrawalPanel == null && !game.ModalInputBlocked && !game.InputBlocked && !startup.Replay.IsClickMasked,
                "Cash popup is destroyed and gameplay/modal/click blocks are released");
        }
        private static OriginalPlayerInfo Info() => local.WithdrawalPanel.PlayerParent.GetComponentInChildren<OriginalPlayerInfo>();
        private static void CheckStats(string context)
        {
            var info = Info();
            Check(info != null && info.gameObject.activeInHierarchy, context + ": actual statistics prefab is visible");
            var culture = new CultureInfo(audio.UserState.DisplayCulture(game.Tables));
            string dateBefore = utcBefore.ToString(info.SimulatedStatsSettings.DateFormat, culture);
            string dateAfter = utcAfter.ToString(info.SimulatedStatsSettings.DateFormat, culture);
            Check(info.DateLabel.text.Contains("Simulated stats") && (info.DateLabel.text.Contains(dateBefore) || info.DateLabel.text.Contains(dateAfter)) &&
                !info.DateLabel.text.Contains("--"), context + ": visible simulated-data disclosure and current UTC date replace placeholders");
            int people;
            Check(int.TryParse(info.PeopleLabel.text, out people) && (people == Minutes(localBefore) * 23 || people == Minutes(localAfter) * 23),
                context + ": people count uses the actual local clock at the opening boundary");
            int attempts;
            Check(int.TryParse(info.ChallengeLabel.text, out attempts) && attempts >= 5 && attempts <= 10 && attempts == game.User.TodayChallengeTimes,
                context + ": later-level attempt count displays the saved daily value");
            var marquee = OriginalMarqueeData.Read(audio.UserState.Rewards.CreateDisplayDocument()); marquee.Init();
            float amount;
            Check(float.TryParse(info.GoldLabel.text, NumberStyles.Currency, culture, out amount) &&
                amount >= marquee.CashMinimum - .01f && amount <= marquee.CashMaximum + .01f,
                context + ": formatted random average is inside the original display configuration range");
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in new[] { info.DateLabel, info.PeopleLabel, info.ChallengeLabel, info.GoldLabel })
            {
                text.ForceMeshUpdate(); Rect glyphs = GlyphBounds(text);
                Check(text.gameObject.activeInHierarchy && text.color.a >= .9f && !text.isTextOverflowing && !text.isTextTruncated &&
                    glyphs.height >= 12 && Contains(Bounds(text.rectTransform), glyphs) && Contains(new Rect(0, 0, Screen.width, Screen.height), glyphs),
                    context + ": complete readable glyphs remain in text rectangle and viewport: " + text.name + " " + text.text);
            }
            CheckSavedStats(); CheckUnchanged();
        }
        private static int Minutes(DateTime value) => (int)(value - value.Date).TotalMinutes;
        private static void CheckCachedAttempts()
        {
            Check(game.User.StatsDisplayUtcDate != firstStatsDate || game.User.TodayChallengeTimes == firstAttempts,
                "Reopening and scene reload keep the same attempts unless the UTC date actually changed");
        }
        private static void CheckSavedStats()
        {
            var saved = OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key), Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            Check(saved.TodayChallengeTimes == game.User.TodayChallengeTimes && saved.StatsDisplayUtcDate == game.User.StatsDisplayUtcDate &&
                saved.TodayChallengeTimes >= 5 && saved.TodayChallengeTimes <= 10, "Actual production save contains the daily sample and UTC date");
            Check(JToken.DeepEquals(protectedState, Protected(saved)), "Actual saved balances, reward state and withdrawal records remain unchanged");
        }
        private static JObject Protected(OriginalUserLocalData user)
        {
            var json = JObject.Parse(OriginalUserDataJson.Write(user)); var result = new JObject();
            foreach (string name in ProtectedKeys) result[name] = json[name]?.DeepClone();
            result["earningFlow"] = user.LocalWithdrawalData?["earningFlow"]?.DeepClone();
            return result;
        }
        private static void CheckUnchanged() => Check(JToken.DeepEquals(protectedState, Protected(game.User)),
            "Opening/closing/reloading never grants money, diamonds, tools or rewards and never changes withdrawal records/queue");
        private static void Click(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Standard cash-page Button is interactable");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = Bounds((RectTransform)button.transform).center, button = PointerEventData.InputButton.Left };
            Hits.Clear(); EventSystem.current.RaycastAll(pointer, Hits);
            var target = Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target == button.gameObject, "Real UI raycast reaches " + button.name);
            Check(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), "Standard Button receives the actual pointer click");
        }
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
        private static void Capture(string name)
        {
            var info = Info(); pendingCapture = Path.GetFullPath(Output + "/" + name + ".png");
            Captures.Add(new JObject { ["image"] = pendingCapture, ["width"] = Screen.width, ["height"] = Screen.height,
                ["title"] = info.DateLabel.text, ["people"] = info.PeopleLabel.text, ["attempts"] = info.ChallengeLabel.text, ["average"] = info.GoldLabel.text,
                ["cash"] = game.User.Gold, ["diamonds"] = game.User.Coin });
            ScreenCapture.CaptureScreenshot(pendingCapture);
        }
        private static void FreshCapture()
        {
            Check(pendingCapture != null && File.Exists(pendingCapture) && File.GetLastWriteTimeUtc(pendingCapture) >= began.AddSeconds(-2), "Fresh screenshot completes before the next action");
            pendingCapture = null;
        }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Checks.Add(message); }
        private static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }
        private static void Finish(int result)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true); SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = JObject.Parse(SessionState.GetString(Key + ".Fixture", "{}")),
                ["checks"] = Checks, ["captures"] = Captures, ["error"] = error,
                ["verification"] = "Original simulated-data algorithms and production cash HUD route; no real-player claim; two viewports; real Button clicks; original preferences restored; no ad/payment/build" }.ToString());
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
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/play-report.json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false); EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("LOCAL_CASH_STATS_PLAY_" + (result == 0 ? "PASS" : "FAIL")); EditorApplication.Exit(result);
        }
    }
}
