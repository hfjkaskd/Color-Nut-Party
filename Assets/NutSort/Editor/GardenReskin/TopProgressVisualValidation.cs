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
    // Editor acceptance only: real scene/Canvas, isolated save, no button/ad requests.
    [InitializeOnLoad]
    public static class TopProgressVisualValidation
    {
        const string Key = "NutSort.TopProgressVisual";
        const string Output = "Docs/TopProgressFix/20261012";
        const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        const string Fixture = "{\"Level\":11,\"LevelSeed\":0,\"Gold\":500.58,\"Coin\":486.73," +
            "\"RevokeCount\":5,\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true," +
            "\"IsCompleteExtraGoldGuide\":true,\"IsCompleteCoinNewPeopleReward\":true}";
        static readonly JArray Captures = new JArray();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static OriginalHiddenLevelView view;
        static Image fill, track;
        static Vector3 fillPosition, trackPosition;
        static Vector2 fillSize, trackSize;
        static int phase, sample;
        static double deadline, readyAt, nextProbe;
        static string mode, pendingCapture, error;
        static DateTime begun;

        static TopProgressVisualValidation()
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
                SessionState.SetFloat(Key + ".TimeScale", Time.timeScale);
                SessionState.SetBool(Key + ".Finish", false);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Top progress actual runtime");
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void StartPolling()
        {
            mode = SessionState.GetString(Key + ".Mode", "before");
            phase = sample = 0; game = null; startup = null; view = null;
            Captures.Clear(); pendingCapture = error = null;
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
                Check(now < deadline, "Top progress capture timeout, phase " + phase);
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
                    view = startup.LocalGameplay.RewardsTop.Hidden;
                    fill = view.Progress;
                    track = fill.transform.parent.GetComponent<Image>();
                    Check(view.gameObject.activeInHierarchy && fill.isActiveAndEnabled && track != null, "Real HUD progress is visible");
                    Check(fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal &&
                        fill.fillOrigin == 0, "Original left-to-right Filled Image retained");
                    Check(fill.sprite != null && track.sprite != null, "Progress sprites resolve");
                    view.RefreshLevels();
                    Check(Mathf.Approximately(fill.fillAmount, 1), "Native level 11 RefreshLevels produces full milestone progress");
                    Canvas.ForceUpdateCanvases();
                    fillPosition = fill.rectTransform.position; trackPosition = track.rectTransform.position;
                    fillSize = fill.rectTransform.rect.size; trackSize = track.rectTransform.rect.size;
                    phase = 1; readyAt = now + 1.5; return;
                }
                if (now < readyAt) return;
                if (phase == 1)
                {
                    // Stabilize existing animation only after normal scene initialization.
                    Time.timeScale = 0;
                    Capture(mode, "Native level 11 RefreshLevels; no progress override");
                    phase = 2; readyAt = now + .4; return;
                }
                FreshCapture();
                if (mode == "before") { Finish(0); return; }
                if (phase == 2)
                {
                    if (sample > 4)
                    {
                        view.RefreshLevels();
                        Check(Mathf.Approximately(fill.fillAmount, 1), "Normal refresh restores native full state after render fixtures");
                        Finish(0); return;
                    }
                    // Explicit renderer samples, not invented player progression.
                    fill.fillAmount = sample * .25f;
                    phase = 3; readyAt = now + .2; return;
                }
                Check(Mathf.Approximately(fill.fillAmount, sample * .25f), "Render sample retains requested fill");
                Capture("fill-" + (sample * 25).ToString("000"), "Explicit Canvas fillAmount visual fixture; player remains level 11");
                sample++; phase = 2; readyAt = now + .4;
            }
            catch (Exception ex) { Fail(ex); }
        }

        static void Capture(string name, string source)
        {
            Check(Screen.width == 720 && Screen.height == 1280, "Actual 720x1280 render resolution");
            Check(game.User.Level == 11 && Mathf.Abs(game.User.Gold - 500.58f) < .001f &&
                Math.Abs(game.User.Coin - 486.73) < .001, "Level and balances unchanged");
            Canvas.ForceUpdateCanvases();
            Check(fill.rectTransform.position == fillPosition && track.rectTransform.position == trackPosition &&
                fill.rectTransform.rect.size == fillSize && track.rectTransform.rect.size == trackSize,
                "Fill samples do not move or resize existing UI");
            var bounds = ScreenBounds(fill.rectTransform);
            var trackBounds = ScreenBounds(track.rectTransform);
            Captures.Add(new JObject { ["image"] = name + ".png", ["source"] = source,
                ["fillAmount"] = fill.fillAmount, ["fillScreenRect"] = bounds, ["trackScreenRect"] = trackBounds,
                ["fillSprite"] = fill.sprite.name, ["trackSprite"] = track.sprite.name,
                ["playerLevel"] = game.User.Level, ["width"] = Screen.width, ["height"] = Screen.height });
            pendingCapture = Path.GetFullPath(Output + "/" + name + ".png");
            ScreenCapture.CaptureScreenshot(pendingCapture);
            Debug.Log("TOP_PROGRESS_CAPTURE " + name + " fill=" + fill.fillAmount);
        }

        static JArray ScreenBounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var result = new JArray();
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                Check(point.x >= -1 && point.x <= Screen.width + 1 && point.y >= -1 && point.y <= Screen.height + 1,
                    rect.name + " remains inside the screen");
                result.Add(new JArray(point.x, point.y));
            }
            return result;
        }

        static void FreshCapture()
        {
            if (pendingCapture == null) return;
            Check(File.Exists(pendingCapture) && File.GetLastWriteTimeUtc(pendingCapture) >= begun.AddSeconds(-2), "Fresh screenshot saved: " + pendingCapture);
            pendingCapture = null;
        }

        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }

        static void Finish(int code)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            SessionState.SetInt(Key + ".Result", code);
            SessionState.SetString(Key + ".Report", new JObject { ["mode"] = mode,
                ["kind"] = "Unity Editor Play Mode actual scene capture with isolated saved-user fixture",
                ["fixture"] = Fixture, ["captures"] = Captures, ["error"] = error,
                ["adOrPaymentButtonInvoked"] = false }.ToString());
            EditorApplication.update -= Tick;
            Time.timeScale = SessionState.GetFloat(Key + ".TimeScale", 1);
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
                // Restore only after runtime quit/pause callbacks have finished saving.
                OriginalPreferenceFixture.Restore();
                Check(!File.Exists(Backup) && PlayerPrefs.HasKey(OriginalUserStore.Key) == existed &&
                    (!existed || PlayerPrefs.GetString(OriginalUserStore.Key) == saved), "Original saved-user preference restored exactly");
                report["originalPreferencesRestored"] = true;
            }
            catch (Exception ex) { Debug.LogException(ex); report["restoreError"] = ex.ToString(); code = 1; }
            report["status"] = code == 0 ? "PASS" : "FAIL";
            report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/report-" + SessionState.GetString(Key + ".Mode", "unknown") + ".json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("TOP_PROGRESS_VISUAL_" + (code == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(code);
        }
    }
}
