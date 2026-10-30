using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalDiamondCloseAudioPlayValidation
    {
        private const string Key = "NutSort.LocalDiamondCloseAudio";
        private const string Output = "Docs/DiamondCloseAudio/20261013";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private const string Fixture = "{\"Level\":4,\"LevelSeed\":0,\"Gold\":512.5,\"Coin\":0,\"RevokeCount\":5," +
            "\"UserName\":\"Audio_Tester\",\"UserId\":\"diamond-close-audio-acceptance\",\"IsAudio\":true,\"IsVibrate\":true," +
            "\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true,\"IsCompleteCoinNewPeopleReward\":true," +
            "\"IsCompleteExtraGoldGuide\":true,\"IsShowCoin\":true,\"IsCompleteShowCoinRewardHint\":true," +
            "\"LocalWithdrawalData\":{\"tutorialBalanceApplied\":true,\"tutorialBalanceAmount\":500," +
            "\"tutorialBalanceNoticeAcknowledged\":true,\"earningFlow\":{\"activationComplete\":true,\"activationStep\":6}}}";
        private static readonly JArray Checks = new JArray(), Events = new JArray(), Actions = new JArray();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static LocalDiamondWithdrawalPanel closedPanel;
        private static int phase, sounds, clicks, successSounds, vibrations, stageStartSounds;
        private static double readyAt, deadline;
        private static string action, error;
        private static DateTime began;

        static LocalDiamondCloseAudioPlayValidation()
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
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Diamond close audio acceptance");
                StartPolling(); EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void StartPolling()
        {
            phase = sounds = clicks = successSounds = vibrations = stageStartSounds = 0;
            game = null; startup = null; audio = null; local = null; closedPanel = null;
            readyAt = 0; deadline = EditorApplication.timeSinceStartup + 150;
            began = DateTime.UtcNow; action = "startup"; error = null;
            Checks.Clear(); Events.Clear(); Actions.Clear();
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
                !game.Level.AreNutsInitialized || !game.IsInitDone || game.InputBlocked || game.IsRestarting || startup.Loading.IsVisible) return false;
            local = startup.LocalGameplay;
            audio.SoundStarted += Sound;
            audio.VibrationRequested += Vibrate;
            Check(audio.AudioEnabled && audio.VibrationEnabled && audio.UserState.DisplayCountry(game.Tables) == "US",
                "Actual scene uses isolated US fixture with audio and vibration enabled");
            local.BindSettlementAds(_ => { throw new InvalidOperationException("Unexpected interstitial request."); },
                _ => { throw new InvalidOperationException("Unexpected rewarded ad request."); });
            return true;
        }
        private static void Sound(string name)
        {
            sounds++; if (name == "Click") clicks++; if (name == "Pass_level") successSounds++;
            if (name == "StageStart") stageStartSounds++;
            Events.Add(new JObject { ["action"] = action, ["sound"] = name, ["frame"] = Time.frameCount,
                ["sceneStartupSound"] = name == "StageStart" });
        }
        private static void Vibrate()
        {
            vibrations++;
            Events.Add(new JObject { ["action"] = action, ["vibrationRequested"] = true, ["frame"] = Time.frameCount });
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup >= deadline) throw new TimeoutException("Diamond close audio timed out at phase " + phase);
                if (EditorApplication.timeSinceStartup < readyAt || !SceneReady() || startup.Replay.IsClickMasked) return;
                switch (phase)
                {
                    case 0:
                        if (game.ModalInputBlocked) return;
                        Open(); Advance(1); break;
                    case 1:
                        Check(local.DiamondWithdrawalPanel != null && game.ModalInputBlocked, "HUD opens the real diamond withdrawal prefab and blocks gameplay");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/diamond-before-close.png"));
                        Advance(2); break;
                    case 2:
                        Check(File.Exists(Output + "/diamond-before-close.png") &&
                            File.GetLastWriteTimeUtc(Output + "/diamond-before-close.png") >= began.AddSeconds(-2), "Fresh real-panel screenshot captured");
                        var panel = local.DiamondWithdrawalPanel;
                        Check(panel.Options.Length > 1 && panel.SelectedIndex == 0, "Original amount choices are present");
                        FeedbackClick(panel.Options[1], "select_amount", true, true, true);
                        Check(panel.SelectedIndex == 1, "Actual amount Button selects the second amount once");
                        Advance(3); break;
                    case 3:
                        Close("first_close", true, true); Advance(4); break;
                    case 4:
                        CheckClosed(); Open(); Advance(5); break;
                    case 5:
                        Close("reopened_close", true, true); Advance(6); break;
                    case 6:
                        CheckClosed(); audio.SetAudioEnabled(false); Open(); Advance(7); break;
                    case 7:
                        Close("muted_close", false, true); Advance(8); break;
                    case 8:
                        CheckClosed(); audio.SetAudioEnabled(true); audio.SetVibrationEnabled(false); Open(); Advance(9); break;
                    case 9:
                        Close("restored_audio_vibration_off_close", true, false); Advance(10); break;
                    case 10:
                        CheckClosed(); audio.SetVibrationEnabled(true); Open(); Advance(11); break;
                    case 11:
                        Close("restored_audio_and_vibration_close", true, true); Advance(12); break;
                    case 12:
                        CheckClosed();
                        Check(successSounds == 0 && clicks == 5 && vibrations == 5 && stageStartSounds <= 1 && sounds == 5 + stageStartSounds,
                            "Entire sequence has exactly five Click sounds and five enabled haptic requests, no Pass_level or delayed duplicate; at most one delayed scene StageStart is counted separately");
                        Check(Mathf.Abs(game.User.Gold - 512.5f) < .0001f && game.User.Coin == 0 && game.User.LocalPendingReward == null,
                            "UI-only acceptance leaves money, diamonds and pending reward unchanged");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/gameplay-after-close.png"));
                        Advance(13); break;
                    case 13:
                        Check(File.Exists(Output + "/gameplay-after-close.png") &&
                            File.GetLastWriteTimeUtc(Output + "/gameplay-after-close.png") >= began.AddSeconds(-2), "Fresh released-gameplay screenshot captured");
                        Finish(0); break;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }
        private static void Advance(int value) { phase = value; readyAt = EditorApplication.timeSinceStartup + .5; }
        private static void Open()
        {
            Check(local.DiamondWithdrawalPanel == null && !game.ModalInputBlocked && !game.InputBlocked, "Gameplay input is available before reopening");
            action = "open_from_HUD"; Click(local.RewardsTop.Coin.Click);
            Check(local.DiamondWithdrawalPanel != null, "Actual HUD diamond Cash Button opens the panel");
        }
        private static void Close(string name, bool sound, bool vibration)
        {
            closedPanel = local.DiamondWithdrawalPanel;
            Check(closedPanel != null, name + ": panel exists");
            FeedbackClick(closedPanel.CloseButton, name, sound, vibration, true);
            Check(local.DiamondWithdrawalPanel == null, name + ": controller clears the panel immediately");
        }
        private static void CheckClosed()
        {
            Check(closedPanel == null && local.DiamondWithdrawalPanel == null && !game.ModalInputBlocked && !game.InputBlocked &&
                !startup.Replay.IsClickMasked && game.IsInitDone && audio != null,
                "Closed popup is destroyed, shared audio remains alive, and modal/click blocks are released");
        }
        private static void FeedbackClick(Button button, string name, bool sound, bool vibration, bool duplicate)
        {
            action = name;
            int soundBefore = sounds, clickBefore = clicks, passBefore = successSounds, vibrationBefore = vibrations;
            Click(button);
            Check(startup.Replay.IsClickMasked, name + ": production shared click mask is active");
            if (duplicate) button.onClick.Invoke(); // Same-frame second callback must be stopped by the production guard.
            Check(sounds - soundBefore == (sound ? 1 : 0) && clicks - clickBefore == (sound ? 1 : 0) && successSounds == passBefore,
                name + ": exactly " + (sound ? "one Click" : "zero new sounds") + ", no Pass_level or duplicate");
            Check(vibrations - vibrationBefore == (vibration ? 1 : 0), name + ": haptic request respects its independent setting and is not duplicated");
            Actions.Add(new JObject { ["name"] = name, ["audioEnabled"] = audio.AudioEnabled, ["vibrationEnabled"] = audio.VibrationEnabled,
                ["clickSounds"] = clicks - clickBefore, ["allSounds"] = sounds - soundBefore,
                ["passLevelSounds"] = successSounds - passBefore, ["hapticRequests"] = vibrations - vibrationBefore,
                ["duplicateCallbackRejected"] = duplicate });
        }
        private static void Click(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Standard Button is active and interactable");
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = (RectTransform)button.transform;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left };
            Hits.Clear(); EventSystem.current.RaycastAll(pointer, Hits);
            var target = Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target == button.gameObject, "Real UI raycast reaches " + button.name);
            Check(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), "Standard Button receives the pointer click");
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
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true); SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = JObject.Parse(Fixture), ["checks"] = Checks,
                ["actions"] = Actions, ["events"] = Events, ["error"] = error,
                ["verification"] = "Actual LuoSiSortGame scene, production HUD and popup Buttons via EventSystem; SoundStarted after source.Play; haptic request events only, no physical-device test; no ads/payment/build" }.ToString());
            if (audio != null) { audio.SoundStarted -= Sound; audio.VibrationRequested -= Vibrate; }
            EditorApplication.update -= Tick; SceneManager.sceneLoaded -= ConfigureCountry;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged; EditorApplication.playModeStateChanged += OnPlayModeChanged;
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
            SessionState.SetBool(Key + ".Finish", false); EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Debug.Log("LOCAL_DIAMOND_CLOSE_AUDIO_PLAY_" + (result == 0 ? "PASS" : "FAIL"));
            EditorApplication.Exit(result);
        }
    }
}
