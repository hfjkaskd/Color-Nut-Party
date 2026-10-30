using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalSettlementAdRetryPlayValidation
    {
        private const string Key = "NutSort.LocalSettlementAdRetry";
        private const string FinishKey = Key + ".Finish";
        private const string ResultKey = Key + ".Result";
        private const string TimeScaleKey = Key + ".TimeScale";
        private const string ScenePath = "Assets/Scenes/LuoSiSortGame.unity";
        private const float Cash = .11f;
        private const double Diamonds = 81.22;
        private const double UnmaskedWaitSeconds = 1.1;
        private static int phase, caseIndex, adRequests, selectedRoute;
        private static int claimedNormalCount, claimedInterCount, completedPassCount;
        private static double deadline, readyAt, nextSceneProbe;
        private static Action<bool> adCompletion;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalSuccessPanel closingPanel;

        static LocalSettlementAdRetryPlayValidation()
        {
            if (SessionState.GetBool(FinishKey, false))
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
                phase = caseIndex = adRequests = selectedRoute = 0;
                game = null; startup = null; closingPanel = null; adCompletion = null;
                SessionState.SetFloat(TimeScaleKey, Time.timeScale);
                SessionState.SetBool(FinishKey, false);
                OriginalPreferenceFixture.Begin(Fixture(false));
                EditorSceneManager.OpenScene(ScenePath);
                PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Settlement ad timeout regression");
                SessionState.SetBool(Key, true);
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void StartPolling()
        {
            deadline = EditorApplication.timeSinceStartup + 180;
            nextSceneProbe = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static string Fixture(bool rewardedThreshold)
        {
            var board = new OriginalBoardState(new LevelData { B = new[] { Rod(1), Rod(1, 1, 1) } },
                Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user = new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"))
            {
                Level = 5,
                TodayPassLevelCount = 1,
                NewGameplayUnlockIndex = 1,
                IsCompleteRecordGuide = true,
                NormalGetTimes = 2,
                InterAdTimes = rewardedThreshold ? 5 : 0,
                LocalPendingReward = new JObject
                {
                    { "country", "BR" }, { "amount", .11 }, { "diamonds", Diamonds },
                    { "diamondSource", "explicit-test-fixture" }, { "nextLevel", 5 }
                },
                LevelInfo = OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board, new List<OriginalMoveRecord>()))
            };
            return OriginalUserDataJson.Write(user);
        }

        private static ScrewData Rod(params int[] colors)
        {
            var slots = new CData[4];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new CData { LP = new LPData { y = i }, BIM = i < colors.Length ? new BIMData { Id = 1, CI = colors[i] } : null };
            return new ScrewData { Id = 1, C = slots };
        }

        private static bool ResolveScene()
        {
            if (game != null && startup != null) return true;
            // Resolve only during scene replacement, at most twice per second.
            if (EditorApplication.timeSinceStartup < nextSceneProbe) return false;
            nextSceneProbe = EditorApplication.timeSinceStartup + .5;
            if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>(true);
            if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>(true);
            return game != null && startup != null;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Settlement timeout regression timed out at phase " + phase);
                if (!ResolveScene() || startup.LocalGameplay == null) return;
                var local = startup.LocalGameplay;
                switch (phase)
                {
                    case 0:
                        if (!PendingPanelReady(local)) return;
                        CheckPendingFixture(local);
                        phase = 1;
                        Reload();
                        return;
                    case 1:
                        if (!PendingPanelReady(local)) return;
                        CheckPendingFixture(local);
                        completedPassCount = game.User.TodayPassLevelCount;
                        local.BindSettlementAds(done => CaptureAd(1, done), done => CaptureAd(2, done));
                        local.NextButton.onClick.Invoke();
                        local.NextButton.onClick.Invoke();
                        Check(adRequests == 1 && selectedRoute == (caseIndex == 0 ? 1 : 2) && adCompletion != null,
                            "Normal Claim selects exactly one expected ad provider");
                        Check(local.SettlementAdPending && !local.SuccessPanel.Closing, "Ad pending keeps settlement open");
                        CheckUnclaimed();
                        readyAt = EditorApplication.timeSinceStartup + UnmaskedWaitSeconds;
                        phase = 2;
                        return;
                    case 2:
                        if (EditorApplication.timeSinceStartup < readyAt || startup.Replay.IsClickMasked) return;
                        Check(!game.InputBlocked && !game.IsRestarting, "Pending duplicate click is tested without other input blockers");
                        local.NextButton.onClick.Invoke();
                        local.NextButton.onClick.Invoke();
                        Check(adRequests == 1 && local.SettlementAdPending, "Pending ad rejects clicks even after the click mask expires");
                        CheckUnclaimed();
                        closingPanel = local.SuccessPanel;
                        // Freeze scaled close animation, not the test's real-time clock.
                        Time.timeScale = 0;
                        adCompletion(false);
                        Check(!local.SettlementAdPending, "Timeout clears pending ad state");
                        CheckClaimed(game.User);
                        Check(local.SuccessPanel == closingPanel && closingPanel.Closing && local.AwaitingNext && game.ModalInputBlocked,
                            "Failed normal-claim ad grants base reward and starts native modal closing");
                        claimedNormalCount = game.User.NormalGetTimes;
                        claimedInterCount = game.User.InterAdTimes;
                        CheckPersistedClaim();
                        readyAt = EditorApplication.timeSinceStartup + UnmaskedWaitSeconds;
                        phase = 3;
                        return;
                    case 3:
                        Check(Time.timeScale == 0 && local.SuccessPanel == closingPanel && closingPanel.Closing,
                            "Closing panel remains present while scaled animation is frozen");
                        if (EditorApplication.timeSinceStartup < readyAt) return;
                        Check(!startup.Replay.IsClickMasked && !game.InputBlocked && !game.IsRestarting,
                            "Closing duplicate click is tested after at least one unmasked real-time second");
                        local.NextButton.onClick.Invoke();
                        local.NextButton.onClick.Invoke();
                        adCompletion(true);
                        adCompletion(false);
                        adCompletion(false);
                        CheckUnchangedAfterCompletion(local);
                        Check(local.SuccessPanel == closingPanel && closingPanel.Closing, "Late callbacks do not replace or reopen closing panel");
                        CheckPersistedClaim();
                        Time.timeScale = 1;
                        phase = 4;
                        return;
                    case 4:
                        if (!NextBoardReady(local)) return;
                        CheckUnchangedAfterCompletion(local);
                        adCompletion(true);
                        adCompletion(false);
                        CheckUnchangedAfterCompletion(local);
                        Check(game.User.TodayPassLevelCount == completedPassCount, "Close advances no extra level or daily completion");
                        phase = 5;
                        Reload();
                        return;
                    case 5:
                        if (!NextBoardReady(local)) return;
                        CheckClaimed(game.User);
                        Check(game.User.NormalGetTimes == claimedNormalCount && game.User.InterAdTimes == claimedInterCount,
                            "Claimed counters persist without another ad request after reload");
                        Check(game.User.TodayPassLevelCount == completedPassCount && adRequests == 1,
                            "Reload resumes playable next board without duplicate settlement");
                        Debug.Log("NUT_SETTLEMENT_AD_RETRY_CASE_PASS " + (caseIndex == 0 ? "interstitial timeout" : "normal-claim rewarded timeout"));
                        if (caseIndex == 0)
                        {
                            caseIndex = 1; phase = 0; adRequests = selectedRoute = 0;
                            adCompletion = null; closingPanel = null;
                            PlayerPrefs.SetString(OriginalUserStore.Key, Fixture(true));
                            PlayerPrefs.Save();
                            Reload();
                            return;
                        }
                        Debug.Log("NUT_LOCAL_SETTLEMENT_AD_RETRY_PLAY_PASS real scene and Buttons, pending reward reload, both normal-claim ad routes, unmasked duplicate clicks, timeout base reward once, frozen closing, late callbacks and playable next board after reload; ad completions are controlled fixtures, no ad provider invoked.");
                        Finish(0);
                        return;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static bool PendingPanelReady(LocalGameplayController local)
        {
            return !game.InputBlocked && !game.IsRestarting && local.AwaitingNext && local.SuccessPanel != null &&
                startup.Replay != null && !startup.Replay.IsClickMasked && !startup.Loading.IsVisible &&
                Vector3.Distance(local.SuccessPanel.Title.localScale, Vector3.one) < .001f;
        }

        private static bool NextBoardReady(LocalGameplayController local)
        {
            return game.IsInitDone && !game.IsRestarting && !game.InputBlocked && !game.ModalInputBlocked &&
                game.Level.AreNutsInitialized && !local.AwaitingNext && local.SuccessPanel == null &&
                !startup.Replay.IsClickMasked;
        }

        private static void CheckPendingFixture(LocalGameplayController local)
        {
            CheckUnclaimed();
            Check(game.User.NormalGetTimes == 2 && game.User.InterAdTimes == (caseIndex == 0 ? 0 : 5), "Fixture counters survive pending reload");
            Check(local.SuccessPanel.Views.Count == 2 && Mathf.Abs(local.SuccessPanel.Views[0].ItemInfo.Count - Cash) < .0001f &&
                Math.Abs(local.SuccessPanel.Views[1].ItemInfo.Count - Diamonds) < .001,
                "Real success panel displays .11 cash and 81.22 diamonds");
            Check(local.NextButton.gameObject.activeInHierarchy && !local.SuccessPanel.Ad.activeSelf,
                "The visible normal Claim action, not a More bonus offer, is exercised");
        }

        private static void CheckUnclaimed()
        {
            Check(game.PlayerLevel == 5 && game.User.LocalPendingReward != null && game.User.Gold == 0 && game.User.Coin == 0,
                "Completed level's reward stays unclaimed while ad is pending");
            Check(Math.Abs((double)game.User.LocalPendingReward["amount"] - Cash) < .0001 &&
                Math.Abs((double)game.User.LocalPendingReward["diamonds"] - Diamonds) < .0001,
                "Pending reward never rerolls on reload or duplicate click");
        }

        private static void CheckClaimed(OriginalUserLocalData user)
        {
            Check(user.Level == 5 && user.LocalPendingReward == null && Mathf.Abs(user.Gold - Cash) < .0001f &&
                Math.Abs(user.Coin - Diamonds) < .0001, "Base cash and diamonds granted exactly once; no multiplier");
        }

        private static void CheckPersistedClaim()
        {
            var saved = OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),
                Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            CheckClaimed(saved);
            Check(saved.NormalGetTimes == claimedNormalCount && saved.InterAdTimes == claimedInterCount, "Claim and counters are persisted before close finishes");
        }

        private static void CheckUnchangedAfterCompletion(LocalGameplayController local)
        {
            Check(adRequests == 1 && !local.SettlementAdPending, "No second request after timeout, close click or stale callback");
            CheckClaimed(game.User);
            Check(game.User.NormalGetTimes == claimedNormalCount && game.User.InterAdTimes == claimedInterCount,
                "Ignored clicks and stale callbacks mutate no ad counters");
        }

        private static void CaptureAd(int route, Action<bool> completion)
        {
            adRequests++;
            selectedRoute = route;
            adCompletion = completion;
        }

        private static void Reload()
        {
            game = null; startup = null; nextSceneProbe = 0;
            SceneManager.LoadScene("LuoSiSortGame");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            if (SessionState.GetBool(FinishKey, false)) return;
            SessionState.SetBool(Key, false);
            SessionState.SetBool(FinishKey, true);
            SessionState.SetInt(ResultKey, code);
            EditorApplication.update -= Tick;
            Time.timeScale = SessionState.GetFloat(TimeScaleKey, 1);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            else CompleteFinish();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode) CompleteFinish();
        }

        private static void CompleteFinish()
        {
            if (!SessionState.GetBool(FinishKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            int code = SessionState.GetInt(ResultKey, 1);
            try
            {
                // Restore after runtime quit/pause callbacks, so a final fixture save
                // cannot overwrite the user's original PlayerPrefs envelope.
                OriginalPreferenceFixture.Restore();
            }
            catch (Exception error) { Debug.LogException(error); code = 1; }
            SessionState.SetBool(FinishKey, false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.Exit(code);
        }
    }
}
