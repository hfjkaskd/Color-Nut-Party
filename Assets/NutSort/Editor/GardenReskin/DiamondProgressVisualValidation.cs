using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Acceptance only: actual scene and original Coin Cash Button, with an isolated save.
    [InitializeOnLoad]
    public static class DiamondProgressVisualValidation
    {
        const string Key = "NutSort.DiamondProgressVisual";
        const string Output = "Docs/DiamondProgressFix/20261012";
        const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        const string Fixture = "{\"Level\":11,\"LevelSeed\":0,\"Gold\":500.58,\"Coin\":486.73," +
            "\"RevokeCount\":5,\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true," +
            "\"IsCompleteExtraGoldGuide\":true,\"IsCompleteCoinNewPeopleReward\":true}";
        static readonly double[] SampleCoins = { 0, 25000, 50000 };
        static readonly JArray Captures = new JArray();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static LocalDiamondWithdrawalPanel panel;
        static Image fill, track;
        static string mode, pendingCapture, error;
        static int phase, sample;
        static double readyAt, deadline, nextProbe, expectedCoin;
        static DateTime begun;

        static DiamondProgressVisualValidation()
        {
            if (SessionState.GetBool(Key + ".Finish", false))
            {
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
                EditorApplication.delayCall += CompleteFinish;
            }
            else if (SessionState.GetBool(Key, false)) StartPolling();
        }

        public static void RunBefore() => Run("before");
        public static void RunAfter() => Run("after");

        static void Run(string selectedMode)
        {
            try
            {
                Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Begin in Edit Mode");
                Directory.CreateDirectory(Output);
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetString(Key + ".Mode", selectedMode);
                SessionState.SetBool(Key + ".Finish", false);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Diamond progress: isolated runtime acceptance fixture");
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void StartPolling()
        {
            mode = SessionState.GetString(Key + ".Mode", "before");
            phase = 0; sample = -1; expectedCoin = 486.73;
            game = null; startup = null; panel = null;
            pendingCapture = error = null; Captures.Clear();
            begun = DateTime.UtcNow;
            deadline = EditorApplication.timeSinceStartup + 180;
            readyAt = nextProbe = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                Check(now < deadline, "Diamond progress capture timeout, phase " + phase);
                if (phase == 0)
                {
                    if (now < nextProbe) return;
                    nextProbe = now + .2;
                    if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                    if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                    if (game == null || startup == null || startup.LocalGameplay == null || game.Level == null ||
                        !game.IsInitDone || !game.Level.AreNutsInitialized || game.IsRestarting || game.InputBlocked ||
                        startup.Replay.IsClickMasked) return;
                    UnityEngine.Object.FindObjectOfType<OriginalUserSession>().ConfigureRewardCountry("US");
                    startup.LocalGameplay.RefreshRewardsTop();
                    OpenPanel();
                    phase = 1; readyAt = now + 1; return;
                }
                if (now < readyAt) return;
                if (phase == 1)
                {
                    CheckPanel();
                    string name = sample < 0 ? "diamond-" + mode :
                        "diamond-fixture-" + (sample == 0 ? "000" : sample == 1 ? "050" : "100");
                    Capture(name);
                    phase = 2; readyAt = now + .5; return;
                }
                if (phase == 2)
                {
                    FreshCapture();
                    if (mode == "before" || sample == SampleCoins.Length - 1)
                    {
                        game.User.Coin = 486.73;
                        Check(Math.Abs(game.User.Coin - 486.73) < .000001, "Fixture balance restored before exiting Play Mode");
                        Finish(0); return;
                    }
                    Check(!startup.Replay.IsClickMasked, "Close Button is ready");
                    panel.CloseButton.onClick.Invoke();
                    phase = 3; readyAt = now + .5; return;
                }
                if (game.InputBlocked || game.IsRestarting || startup.Replay.IsClickMasked) return;
                Check(startup.LocalGameplay.DiamondWithdrawalPanel == null, "Original Close Button destroys the panel");
                sample++;
                // Disclosed visual fixtures affect only this isolated in-memory user, never the original save.
                expectedCoin = SampleCoins[sample]; game.User.Coin = expectedCoin;
                startup.LocalGameplay.RefreshRewardsTop();
                OpenPanel();
                phase = 1; readyAt = now + 1;
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void OpenPanel()
        {
            startup.LocalGameplay.RewardsTop.Coin.Click.onClick.Invoke();
            panel = startup.LocalGameplay.DiamondWithdrawalPanel;
            Check(panel != null, "Original Coin Cash Button opens the real diamond withdrawal panel");
            track = panel.ProgressLabel.transform.parent.GetComponent<Image>();
            Check(track != null, "Original progress track exists");
            var child = track.transform.Find("Bar");
            Check(child != null, "Original progress fill child exists");
            fill = child.GetComponent<Image>();
            Check(fill != null, "Original fill Image exists");
        }

        static void CheckPanel()
        {
            Check(game.User.Level == 11 && Mathf.Abs(game.User.Gold - 500.58f) < .001f &&
                Math.Abs(game.User.Coin - expectedCoin) < .000001, "Opening the panel preserves level, gold, and fixture diamonds");
            Check(panel.GetComponentsInChildren<RectTransform>(true).Length == 37 &&
                panel.GetComponentsInChildren<Button>(true).Length == 6, "Original 37 nodes and 6 Buttons retained");
            Check(panel.SelectedIndex == 0, "Original first $500 option selected");
            Check(fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal && fill.fillOrigin == 0,
                "Original left-to-right horizontal Filled rendering retained");
            Check(Math.Abs(fill.fillAmount - (float)(expectedCoin / 50000)) < .0000001f,
                "True diamond-to-target fill is exact, without minimum-fill inflation");
            var formatter = new OriginalGoldFormatter(() => "en-US");
            string expectedLabel = formatter.Format((float)(expectedCoin / 100)) + "/$500";
            Check(panel.ProgressLabel.text == expectedLabel, "Original dynamic currency label: " + expectedLabel);
            Check(panel.BalanceLabel.text == formatter.Format((float)(expectedCoin / 100)), "Original converted balance label retained");
            Canvas.ForceUpdateCanvases();
            if (mode != "after") return;
            Check(fill.sprite != null && fill.sprite.name == "diamond-progress-fill", "Dedicated border-free diamond fill sprite loaded");
            var corners = new Vector3[4]; fill.rectTransform.GetWorldCorners(corners);
            var bounds = track.rectTransform.rect;
            foreach (var corner in corners)
            {
                Vector3 point = track.rectTransform.InverseTransformPoint(corner);
                Check(point.x >= bounds.xMin - .01f && point.x <= bounds.xMax + .01f &&
                    point.y >= bounds.yMin - .01f && point.y <= bounds.yMax + .01f,
                    "Fill RectTransform stays completely inside the track RectTransform");
            }
        }

        static void Capture(string name)
        {
            Check(Screen.width == 720 && Screen.height == 1600, "Actual 720x1600 render resolution");
            Captures.Add(new JObject { ["image"] = name + ".png",
                ["kind"] = "Unity Editor Play Mode actual original Coin Cash Button route, isolated acceptance fixture; NOT live player data",
                ["fixtureDiamonds"] = expectedCoin, ["fillAmount"] = fill.fillAmount, ["label"] = panel.ProgressLabel.text,
                ["fillRectSize"] = new JArray(fill.rectTransform.rect.width, fill.rectTransform.rect.height),
                ["fillPosition"] = new JArray(fill.rectTransform.anchoredPosition.x, fill.rectTransform.anchoredPosition.y),
                ["trackRectSize"] = new JArray(track.rectTransform.rect.width, track.rectTransform.rect.height),
                ["fillSprite"] = fill.sprite.name, ["width"] = Screen.width, ["height"] = Screen.height });
            pendingCapture = Path.GetFullPath(Output + "/" + name + ".png");
            ScreenCapture.CaptureScreenshot(pendingCapture);
            Debug.Log("DIAMOND_PROGRESS_CAPTURE " + name + " fill=" + fill.fillAmount);
        }

        static void FreshCapture()
        {
            Check(pendingCapture != null && File.Exists(pendingCapture) &&
                File.GetLastWriteTimeUtc(pendingCapture) >= begun.AddSeconds(-2), "Fresh screenshot saved: " + pendingCapture);
            pendingCapture = null;
        }

        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }

        static void Finish(int code)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            SessionState.SetInt(Key + ".Result", code);
            SessionState.SetString(Key + ".Report", new JObject { ["mode"] = mode, ["fixture"] = Fixture,
                ["captures"] = Captures, ["error"] = error, ["withdrawOrAdButtonInvoked"] = false }.ToString());
            EditorApplication.update -= Tick;
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
                using (var reader = new BinaryReader(File.OpenRead(Backup)))
                { existed = reader.ReadBoolean(); saved = reader.ReadString(); }
                // Runtime quit/pause saving must finish before restoring the original preference.
                OriginalPreferenceFixture.Restore();
                Check(!File.Exists(Backup) && PlayerPrefs.HasKey(OriginalUserStore.Key) == existed &&
                    (!existed || PlayerPrefs.GetString(OriginalUserStore.Key) == saved), "Original saved-user preference restored byte for byte");
                report["originalPreferencesRestored"] = true;
            }
            catch (Exception ex) { Debug.LogException(ex); report["restoreError"] = ex.ToString(); code = 1; }
            report["status"] = code == 0 ? "PASS" : "FAIL"; report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/report-" + SessionState.GetString(Key + ".Mode", "unknown") + ".json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("DIAMOND_PROGRESS_VISUAL_" + (code == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(code);
        }
    }
}
