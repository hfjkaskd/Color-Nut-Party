using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Editor acceptance only. Captures the real automatic scene Loading once, then
    // reopens that same scene view to inspect 0/50/90/100 through its public APIs.
    [InitializeOnLoad]
    public static class ColorNutPartyLoadingValidation
    {
        const string Key = "NutSort.ColorNutPartyLoadingValidation";
        const string Output = "Docs/GardenReskin/loading-color-nut-party-20261009/implementation";
        const string Fixture = "{\"Level\":4,\"LevelSeed\":0,\"Gold\":0,\"Coin\":0,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}";
        static readonly JArray States = new JArray();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static OriginalLoadingView panel;
        static Canvas canvas;
        static Image logo;
        static string shape, error;
        static int phase, height, captureFrame;
        static float nextTime, completeRequestedAt;
        static double deadline;
        static DateTime begun;
        static bool finishing, sawCompleteWhileVisible, preferencesRestored, automaticCaptured;

        static ColorNutPartyLoadingValidation()
        {
            if (!SessionState.GetBool(Key, false)) return;
            shape = SessionState.GetBool(Key + ".Wide", false) ? "wide" : "tall";
            height = shape == "wide" ? 1280 : 1560;
            Directory.CreateDirectory(Output);
            begun = DateTime.UtcNow;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        public static void RunTall() => Run(false);
        public static void RunWide() => Run(true);

        static void Run(bool wide)
        {
            try
            {
                shape = wide ? "wide" : "tall";
                height = wide ? 1280 : 1560;
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetBool(Key + ".Wide", wide);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, (uint)height, "Color Nut Party Loading: actual scene runtime acceptance");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static void Tick()
        {
            if (finishing || !EditorApplication.isPlaying) return;
            try
            {
                Check(error == null, error ?? "No runtime error");
                Check(EditorApplication.timeSinceStartup < deadline, "Loading acceptance timeout at phase " + phase);
                if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if (!automaticCaptured && startup != null && startup.Loading != null && startup.Loading.IsVisible && startup.Loading.Value >= .25f)
                {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/automatic-" + shape + ".png"));
                    States.Add(new JObject { ["source"] = "Actual automatic scene startup before any manual presentation call",
                        ["fillAtCaptureRequest"] = startup.Loading.Value, ["percentAtCaptureRequest"] = startup.Loading.PercentageLabel.text });
                    automaticCaptured = true;
                }
                if (phase == 0)
                {
                    if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                    if (game == null || startup == null || startup.LocalGameplay == null || game.Level == null ||
                        !game.IsInitDone || !game.Level.AreNutsInitialized || game.IsRestarting || game.InputBlocked || startup.Replay.IsClickMasked) return;
                    Check(EventSystem.current != null && startup.MainLevel.gameObject.activeInHierarchy, "Original scene UI and initialized board are usable before Loading overlay");
                    Check(automaticCaptured, "Automatic scene Loading was observed and captured before manual checks");
                    string automatic = Output + "/automatic-" + shape + ".png";
                    Check(File.Exists(automatic) && File.GetLastWriteTimeUtc(automatic) > begun.AddSeconds(-2), "Fresh automatic scene screenshot completed");
                    panel = startup.Loading;
                    Check(!panel.IsVisible, "Automatic startup completed before reopening the same scene Loading");
                    canvas = panel.GetComponentInParent<Canvas>();
                    Check(canvas != null, "Original scene Loading Canvas exists");
                    var logoNode = panel.transform.Find("Logo");
                    Check(logoNode != null, "Original Logo node exists");
                    logo = logoNode.GetComponent<Image>();
                    ValidateStructure();
                    panel.SetState(true);
                    CheckState(0, 0, "0%", "Existing presentation API: reset");
                    panel.Advance(1);
                    CheckState(.5f, 375, "50%", "Existing presentation API: one-second advance");
                    // Reset once more so the screenshot is reached by actual Update,
                    // independently of the direct API assertions above.
                    panel.SetState(true);
                    phase = 1;
                    nextTime = Time.time + 2.3f;
                    return;
                }
                if (phase == 1)
                {
                    if (Time.time < nextTime) return;
                    CheckState(.9f, 675, "90%", "Real Unity Update reaches the original cap");
                    ValidateStructure();
                    Check(Screen.width == 720 && Screen.height == height, "Actual requested render resolution");
                    Canvas.ForceUpdateCanvases();
                    CheckOnScreen(panel.Marker, "90% progress marker");
                    CheckOnScreen(logo.rectTransform, "Native-size Logo");
                    panel.PercentageLabel.ForceMeshUpdate();
                    Check(!panel.PercentageLabel.isTextOverflowing, "Dynamic percentage fits its original text area");
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/runtime-" + shape + ".png"));
                    captureFrame = Time.frameCount;
                    nextTime = Time.time + .4f;
                    phase = 2;
                    return;
                }
                if (phase == 2)
                {
                    if (Time.time < nextTime || Time.frameCount < captureFrame + 2) return;
                    string capture = Output + "/runtime-" + shape + ".png";
                    Check(File.Exists(capture) && File.GetLastWriteTimeUtc(capture) > begun.AddSeconds(-2), "Fresh real runtime screenshot completed");
                    panel.SetState(false);
                    Check(panel.gameObject.activeSelf, "Completion starts before delayed hide");
                    completeRequestedAt = Time.time;
                    phase = 3;
                    return;
                }
                if (!sawCompleteWhileVisible && Mathf.Approximately(panel.ProgressImage.fillAmount, 1))
                {
                    Check(panel.gameObject.activeSelf, "100% is displayed before the delayed hide");
                    CheckState(1, 750, "100%", "Real global animation pump completed the progress tween");
                    Canvas.ForceUpdateCanvases();
                    CheckOnScreen(panel.Marker, "100% progress marker");
                    panel.PercentageLabel.ForceMeshUpdate();
                    Check(!panel.PercentageLabel.isTextOverflowing, "100% still fits its original text area");
                    sawCompleteWhileVisible = true;
                }
                if (panel.gameObject.activeSelf) return;
                Check(sawCompleteWhileVisible && panel.PercentageLabel.text == "100%" && Time.time > completeRequestedAt + .5f,
                    "Existing scaled delayed hide follows real 100% completion");
                Check(game.IsInitDone && game.Level.AreNutsInitialized && !game.IsRestarting && !game.InputBlocked,
                    "Original scene remains initialized and usable after Loading closes");
                Debug.Log("COLOR_NUT_PARTY_LOADING_PASS " + shape + " 720x" + height + "; original eight-node scene view, native Logo, dynamic 0/50/90/100, real Update/tween/delayed hide.");
                Finish(0);
            }
            catch (Exception ex) { Debug.LogException(ex); Finish(1); }
        }

        static void ValidateStructure()
        {
            Check(panel.GetComponentsInChildren<Transform>(true).Length == 8, "Eight original Loading objects retained");
            foreach (var component in panel.GetComponentsInChildren<Component>(true)) Check(component != null, "No missing scripts");
            Check(panel.PercentageLabel != null && panel.ProgressImage != null && panel.Marker != null && logo != null, "Original component bindings retained");
            Check(panel.PercentageLabel.font != null && panel.ProgressImage.sprite != null && logo.sprite != null, "Fonts and replacement sprites resolve");
            Check(logo.sprite.rect.size == new Vector2(818, 413) && logo.rectTransform.sizeDelta == new Vector2(818, 413), "Logo Sprite and SetNativeSize remain 818x413");
            Check(panel.ProgressImage.type == Image.Type.Filled && panel.ProgressImage.fillMethod == Image.FillMethod.Horizontal, "Progress remains a native horizontal Filled Image");
            Check(panel.Marker.sizeDelta == new Vector2(115, 119) && panel.PercentageLabel.rectTransform.sizeDelta == new Vector2(96.5f, 80.4f), "Marker and dynamic percentage geometry retained");
        }

        static void CheckState(float fill, float markerX, string percent, string source)
        {
            Check(Mathf.Approximately(panel.ProgressImage.fillAmount, fill) && Mathf.Approximately(panel.Marker.anchoredPosition.x, markerX) &&
                Mathf.Approximately(panel.Marker.anchoredPosition.y, 0) && panel.PercentageLabel.text == percent, "Original fill/marker/percentage at " + percent);
            States.Add(new JObject { ["fill"] = panel.ProgressImage.fillAmount, ["percent"] = panel.PercentageLabel.text,
                ["markerX"] = panel.Marker.anchoredPosition.x, ["source"] = source });
        }

        static void CheckOnScreen(RectTransform rect, string label)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var corner in corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(camera, corner);
                Check(p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1, label + " stays on screen");
            }
        }

        static void OnLog(string value, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = value;
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        static void Finish(int code)
        {
            if (finishing) return;
            finishing = true;
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            try
            {
                const string backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
                bool hadBackup = File.Exists(backup), existed = false;
                string saved = null;
                if (hadBackup)
                    using (var reader = new BinaryReader(File.OpenRead(backup)))
                    { existed = reader.ReadBoolean(); saved = reader.ReadString(); }
                OriginalPreferenceFixture.Restore();
                preferencesRestored = hadBackup && !File.Exists(backup) && PlayerPrefs.HasKey(OriginalUserStore.Key) == existed &&
                    (!existed || PlayerPrefs.GetString(OriginalUserStore.Key) == saved);
                Check(preferencesRestored, "Original preference presence and value restored exactly");
            }
            catch (Exception ex) { Debug.LogException(ex); code = 1; }
            if (error != null) code = 1;
            Application.logMessageReceived -= OnLog;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/report-" + shape + ".json", new JObject {
                ["status"] = code == 0 ? "PASS" : "FAIL", ["utc"] = DateTime.UtcNow.ToString("o"),
                ["width"] = 720, ["height"] = height, ["image"] = "runtime-" + shape + ".png",
                ["kind"] = "Unity Editor Play Mode runtime: automatic first startup capture plus same actual scene Loading view reopened at 90% through its normal public APIs",
                ["automaticImage"] = "automatic-" + shape + ".png", ["automaticStartupCaptured"] = automaticCaptured,
                ["fixture"] = Fixture, ["states"] = States, ["originalPreferencesRestored"] = preferencesRestored,
                ["runtimeError"] = error, ["saw100BeforeDelayedHide"] = sawCompleteWhileVisible }.ToString());
            EditorApplication.Exit(code);
        }
    }
}
