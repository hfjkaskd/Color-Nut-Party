using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Acceptance-only state comparison. The second image uses disclosed, isolated
    // fixture readers through the existing native presentation APIs, not live data.
    [InitializeOnLoad]
    public static class CashStateComparisonValidation
    {
        const string Key = "NutSort.CashStateComparison";
        const string Output = "Docs/GardenReskin/implementation-20261008/cash-review";
        static readonly JArray Results = new JArray();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static OriginalWithdrawalPanel panel;
        static FixtureServices services;
        static int phase;
        static float ready;
        static double deadline;
        static string error, savedUser, board;
        static bool finishing;
        static DateTime begun;

        static CashStateComparisonValidation()
        {
            if (!SessionState.GetBool(Key, false)) return;
            Directory.CreateDirectory(Output);
            begun = DateTime.UtcNow;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            try
            {
                Check(Resources.Load<GameObject>("Prefabs/Panels/TXPanel").GetComponentsInChildren<RectTransform>(true).Length == 28,
                    "Original withdrawal Prefab hierarchy retained");
                Check(Resources.Load<GameObject>("Prefabs/PlayerInfo/PlayerInfo").GetComponentsInChildren<RectTransform>(true).Length == 13,
                    "Original statistics Prefab hierarchy retained");
                Check(Resources.Load<GameObject>("Prefabs/PlayerInfo/PMDSimple").GetComponentsInChildren<RectTransform>(true).Length == 8,
                    "Original feed Prefab hierarchy retained");
                OriginalPreferenceFixture.Begin(UserFixture(4));
                SessionState.SetBool(Key + ".PriorGoldTween", OriginalWithdrawalPanel.IsPlayGoldTween);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Cash comparison: real local state and explicit fixture");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static string UserFixture(int level) => "{\"Level\":" + level +
            ",\"LevelSeed\":0,\"Gold\":0,\"Coin\":0,\"ExchangeCount\":0,\"RevokeCount\":0,\"AddScrewCount\":0," +
            "\"GuideIndex\":0,\"Level1Gold\":0.4,\"TodayChallengeTimes\":1,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}";

        static void Tick()
        {
            if (finishing || !EditorApplication.isPlaying) return;
            try
            {
                Check(error == null, error ?? "No runtime errors");
                Check(EditorApplication.timeSinceStartup < deadline, "Cash state comparison timed out at phase " + phase);
                if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if (game == null || startup == null || startup.LocalGameplay == null || game.Level == null ||
                    !game.IsInitDone || !game.Level.AreNutsInitialized || game.IsRestarting || game.InputBlocked || startup.Replay.IsClickMasked) return;
                if (Time.time < ready) return;
                if (phase == 0)
                {
                    UnityEngine.Object.FindObjectOfType<OriginalUserSession>().ConfigureRewardCountry("US");
                    startup.LocalGameplay.RefreshRewardsTop();
                    savedUser = PlayerPrefs.GetString(OriginalUserStore.Key);
                    board = game.User.LevelInfo;
                    startup.LocalGameplay.RewardsTop.Gold.Click.onClick.Invoke();
                    panel = startup.LocalGameplay.WithdrawalPanel;
                    Check(panel != null, "Real local Cash Button opens existing withdrawal panel");
                    phase = 1; ready = Time.time + .85f; return;
                }
                if (phase == 1)
                {
                    Check(Mathf.Approximately(panel.Fill, .6f) && panel.ProgressLabel.text == "3/5", "Real local unfinished state remains 3/5");
                    var info = panel.PlayerParent.GetComponentInChildren<OriginalPlayerInfo>();
                    Check(info != null && info.PeopleLabel.text == "--" && info.ChallengeLabel.text == "--" && info.GoldLabel.text == "--",
                        "Missing live statistics remain explicitly unavailable");
                    var feed = info.GetComponentInChildren<OriginalSimpleMarquee>();
                    var item = feed.GetComponentInChildren<OriginalMarqueeItemView>(true);
                    Check(!item.HeadImage.gameObject.activeSelf, "No invented payment brand for missing records");
                    CheckBadgeAbsent(item);
                    CheckUnchanged(4);
                    Capture("cash-local-empty", "Actual local Cash Button route; no statistics or withdrawal record response", info,
                        new JObject { ["source"] = "No data response", ["date"] = "--", ["people"] = "--", ["attempts"] = "--", ["average"] = "--" });
                    phase = 2; ready = Time.time + .4f; return;
                }
                if (phase == 2)
                {
                    Fresh("cash-local-empty");
                    PlayerPrefs.SetString(OriginalUserStore.Key, UserFixture(2)); PlayerPrefs.Save();
                    game = null; startup = null; panel = null;
                    SceneManager.LoadScene("LuoSiSortGame");
                    phase = 3; ready = 0; return;
                }
                if (phase == 3)
                {
                    UnityEngine.Object.FindObjectOfType<OriginalUserSession>().ConfigureRewardCountry("US");
                    startup.LocalGameplay.RefreshRewardsTop();
                    // Inputs are isolated acceptance data. No production transport or save is called.
                    game.User.Level1Gold = .4f;
                    game.User.GuideIndex = 0;
                    game.User.IsGuideGold = false;
                    game.User.ComeOnGold = string.Empty;
                    game.User.ServerConfigData = JObject.Parse("{\"LSS260820\":false}");
                    game.User.GoldRewardTargetS2CData = JObject.Parse("{\"bear_list\":[{\"psi_value\":\"0.4\"},{\"psi_value\":\"0.4\"},{\"RealLevel\":5,\"Stage2RealLevel\":10}]}");
                    OriginalWithdrawalPanel.IsPlayGoldTween = false;
                    savedUser = PlayerPrefs.GetString(OriginalUserStore.Key);
                    board = game.User.LevelInfo;
                    services = new FixtureServices(game);
                    var parent = startup.MainLevel.transform.parent;
                    panel = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/Panels/TXPanel"), parent, false).GetComponent<OriginalWithdrawalPanel>();
                    panel.Bind(game.User, game.Tables, "en", Money, services);
                    panel.Init(new object[] { 1 });
                    panel.Refresh();
                    game.ModalInputBlocked = true;
                    phase = 4; ready = Time.time + .85f; return;
                }
                if (phase == 4)
                {
                    Check(Mathf.Approximately(panel.Fill, 1) && panel.ProgressLabel.text == "1/1" && panel.IsDoneTask,
                        "Native settlement renderer fills completed 1/1 progress");
                    Check(panel.GoldLabel.text == Money(.4f), "Native early reward header renders fixture 0.40");
                    bool titleFound = false;
                    foreach (var text in panel.GetComponentsInChildren<TMPro.TMP_Text>()) if (text.text == "Level 1") titleFound = true;
                    Check(titleFound, "Native settlement title is Level 1");
                    var info = services.Player;
                    Check(info != null && info.PeopleLabel.text == "25553" && info.ChallengeLabel.text == "1" && info.GoldLabel.text == Money(.4f) &&
                        info.DateLabel.text.Contains("9/9/2026"), "Native statistics labels read explicit matched fixture");
                    Check(services.Feed != null && services.Feed.Items.Count == 3, "Original simple marquee creates its normal three recycled rows");
                    var venmo = Resources.Load<Sprite>("Atlas/PaySimple/Venmo");
                    Check(venmo != null, "Original Venmo Sprite resolves");
                    foreach (var item in services.Feed.Items)
                    {
                        Check(item.HeadImage.gameObject.activeSelf && ReferenceEquals(item.HeadImage.sprite, venmo), "Normal channel binding resolves original Venmo brand");
                        Check(item.Tip.text.Contains("Player_N9s6") && item.Tip.text.Contains("0.40"), "Marquee text remains dynamic");
                        CheckBadgeAbsent(item);
                    }
                    CheckUnchanged(2);
                    Check(services.SideEffects == 0, "No claim, payment, server request, reward mutation, or save was dispatched");
                    Capture("cash-matched-fixture", "Unity runtime using original Bind/Init/Refresh APIs with explicit visual acceptance fixture; NOT live statistics or a real payout", info,
                        new JObject { ["source"] = "Explicit visual acceptance fixture", ["date"] = "9/9/2026", ["people"] = 25553, ["attempts"] = 1,
                            ["average"] = .4, ["brand"] = "Venmo", ["displayName"] = "Player_N9s6", ["realPayout"] = false });
                    phase = 5; ready = Time.time + .4f; return;
                }
                Fresh("cash-matched-fixture");
                Debug.Log("CASH_STATE_COMPARISON_PASS local 3/5 with unavailable data; native matched fixture 1/1, 25553/1/$0.40, Venmo; empty badge removed; balances/board/preferences unchanged; no payment request.");
                Finish(0);
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static void Capture(string id, string kind, OriginalPlayerInfo info, JObject inputs)
        {
            Check(Screen.width == 720 && Screen.height == 1280, "Actual 720x1280 rendering");
            Canvas.ForceUpdateCanvases();
            Results.Add(new JObject { ["id"] = id, ["kind"] = kind, ["inputs"] = inputs, ["progress"] = panel.ProgressLabel.text,
                ["fill"] = panel.Fill, ["amount"] = panel.GoldLabel.text, ["statistics"] = new JArray(info.PeopleLabel.text, info.ChallengeLabel.text, info.GoldLabel.text),
                ["buttonsClicked"] = id == "cash-local-empty" ? "Only main Cash entry Button; no Withdraw click" : "None",
                ["image"] = id + ".png", ["preferencesUnchangedDuringPresentation"] = true, ["balanceUnchanged"] = true });
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + id + ".png"));
            Debug.Log("CASH_STATE_COMPARISON_CAPTURE " + id);
        }

        static void CheckBadgeAbsent(OriginalMarqueeItemView item)
        {
            var badge = item.transform.Find("Bg");
            Check(badge != null && badge.GetComponent<Image>() != null && badge.GetComponent<Image>().color.a == 0,
                "Existing redundant badge image is transparent without removing its node");
        }
        static void CheckUnchanged(int level)
        {
            Check(game.User.Level == level && game.User.Gold == 0 && game.User.Coin == 0 && game.User.LevelInfo == board,
                "Presentation does not alter level, money, diamonds, or board");
            Check(PlayerPrefs.GetString(OriginalUserStore.Key) == savedUser, "Presentation does not persist fixture data");
        }
        static void Fresh(string id) => Check(File.Exists(Output + "/" + id + ".png") && File.GetLastWriteTimeUtc(Output + "/" + id + ".png") > begun.AddSeconds(-2), "Fresh actual screenshot: " + id);
        static string Money(float value) => new OriginalGoldFormatter(() => "en-US").Format(value);
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void OnLog(string condition, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = condition; }
        static void Finish(int code)
        {
            if (finishing) return;
            finishing = true;
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            OriginalWithdrawalPanel.IsPlayGoldTween = SessionState.GetBool(Key + ".PriorGoldTween", false);
            OriginalPreferenceFixture.Restore();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/report.json", new JObject { ["status"] = code == 0 ? "PASS" : "FAIL", ["utc"] = DateTime.UtcNow.ToString("o"), ["runtimeError"] = error,
                ["note"] = "Matched image is explicit isolated acceptance fixture through real Unity components, not a connected statistics service or real payment.",
                ["originalPreferencesRestored"] = true, ["captures"] = Results }.ToString());
            EditorApplication.Exit(code);
        }

        sealed class FixtureServices : IOriginalWithdrawalServices
        {
            readonly OriginalGameScene scene;
            public OriginalPlayerInfo Player;
            public OriginalSimpleMarquee Feed;
            public int SideEffects;
            public FixtureServices(OriginalGameScene scene) { this.scene = scene; }
            public bool CanClick => false;
            public bool OnlineTimeHint { get; set; }
            public bool Progress2Guide { set { } }
            public string MainGoldHint => string.Empty;
            public string Country => "US";
            public void SetGold(float value, bool refresh, bool showHint) { SideEffects++; throw new InvalidOperationException("Unexpected reward mutation"); }
            public void Save() { SideEffects++; throw new InvalidOperationException("Unexpected save"); }
            public void ShowPanel(int id, object[] args) { SideEffects++; throw new InvalidOperationException("Unexpected claim or guide dispatch"); }
            public void ShowTip(string value) { SideEffects++; }
            public void PlaySound(string value) { SideEffects++; }
            public void Closed() { SideEffects++; }
            public void Hidden() { SideEffects++; }
            public void Schedule(float delay, Action action) { SideEffects++; }
            public void NextPanel() { SideEffects++; }
            public void InitializePlayerInfo(OriginalPlayerInfo player, int level)
            {
                Player = player;
                player.Init(level, scene.User, scene.Tables, "en", () => true, () => "9/9/2026", () => 25553, () => .4f, Money,
                    (parent, capturedLevel) =>
                    {
                        Feed = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/PlayerInfo/PMDSimple"), parent, false).GetComponent<OriginalSimpleMarquee>();
                        var data = new OriginalMarqueeItem { IsGold = true, Minimum = .4f, Maximum = .4f, Weight = 1 };
                        var text = new OriginalMarqueeText(scene.User, scene.Tables, () => "Player_N9s6", Money, (a, b) => .4f);
                        var country = scene.Tables.PayChannels.GetPayChannel("US", () => "US");
                        int venmo = country.Channels.IndexOf("Venmo");
                        Check(venmo >= 0, "US configured payment list includes Venmo");
                        Feed.Bind(() => data, item => item.Bind(text, scene.Tables.PayChannels, "en", () => "US", scene.ScheduleDelay,
                            null, (a, b) => venmo));
                        Feed.Init(capturedLevel);
                    });
            }
        }
    }
}
