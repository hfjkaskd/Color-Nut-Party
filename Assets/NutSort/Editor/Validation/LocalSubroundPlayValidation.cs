using System;
using System.Collections.Generic;
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
    public static class LocalSubroundPlayValidation
    {
        private const string Key = "NutSort.LocalSubround";
        private const string FinishKey = Key + ".Finish";
        private const string ResultKey = Key + ".Result";
        private const string TimeScaleKey = Key + ".TimeScale";
        private static double deadline, readyAt, nextSceneProbe;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static LocalGameplayController boundLocal;
        private static OriginalBoardState board;
        private static OriginalSuccessPanel heldPanel;
        private static List<Vector2Int> solution;
        private static int move, phase, boardLevel, shownCount, claimedCount, adRequests;
        private static float nextAction, expectedGold, pendingGold;
        private static double expectedDiamonds, pendingDiamonds;
        private static string pendingDocument;
        private static bool pendingReloadChecked, claimedReloadChecked, failedAdChecked;
        private static Action<bool> adCompletion;

        static LocalSubroundPlayValidation()
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
                phase = move = boardLevel = shownCount = claimedCount = adRequests = 0;
                expectedGold = pendingGold = 0; expectedDiamonds = pendingDiamonds = 0;
                pendingReloadChecked = claimedReloadChecked = failedAdChecked = false;
                game = null; startup = null; board = null; boundLocal = null; adCompletion = null;
                SessionState.SetFloat(TimeScaleKey, Time.timeScale);
                SessionState.SetBool(FinishKey, false);
                var user = new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"))
                {
                    Level = 9, NewGameplayUnlockIndex = 1, IsCompleteRecordGuide = true,
                    NormalGetTimes = 2, InterAdTimes = 0
                };
                OriginalPreferenceFixture.Begin(OriginalUserDataJson.Write(user));
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(480, 1040, "Every subround settlement");
                SessionState.SetBool(Key, true);
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void StartPolling()
        {
            deadline = EditorApplication.timeSinceStartup + 600;
            nextSceneProbe = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static bool ResolveScene()
        {
            if (game != null && startup != null) return true;
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
                Check(EditorApplication.timeSinceStartup < deadline, "Subround timeout phase=" + phase + " completed=" + shownCount);
                if (!ResolveScene() || startup.LocalGameplay == null) return;
                var local = startup.LocalGameplay;
                if (boundLocal != local)
                {
                    boundLocal = local;
                    local.BindSettlementAds(CaptureAd, CaptureAd);
                }
                Check(game.User.GoldRewardTargetS2CData != null, "Real country reward configuration is available");
                switch (phase)
                {
                    case 0:
                        if (local.AwaitingNext)
                        {
                            if (!PanelReady(local)) return;
                            Check(boardLevel == 9 + claimedCount && shownCount == claimedCount,
                                "Each solved source board reaches exactly one new settlement");
                            shownCount++;
                            CheckProgress(game.User, shownCount);
                            CheckBalances(game.User);
                            Check(game.User.LocalPendingReward != null, "Every small round prepares a pending reward");
                            pendingGold = (float)game.User.LocalPendingReward["amount"];
                            pendingDiamonds = (double)game.User.LocalPendingReward["diamonds"];
                            pendingDocument = game.User.LocalPendingReward.ToString();
                            Check(pendingGold > 0 && pendingDiamonds > 0, "Current configuration supplies both cash and diamonds for every small round");
                            Check(local.SuccessPanel.Views.Count == 2 &&
                                Mathf.Abs(local.SuccessPanel.Views[0].ItemInfo.Count - pendingGold) < .0001f &&
                                Math.Abs(local.SuccessPanel.Views[1].ItemInfo.Count - pendingDiamonds) < .001,
                                "Native panel displays this small round's actual pending cash and diamonds");
                            CheckPending(local);
                            CheckSaved(true);
                            Debug.Log("NUT_LOCAL_SUBROUND_SETTLEMENT completed=" + boardLevel + " pendingNext=" + game.PlayerLevel);
                            readyAt = EditorApplication.timeSinceStartup + 1.1;
                            phase = 1;
                            return;
                        }
                        SolveBoard(local);
                        return;
                    case 1:
                        if (!PanelReady(local)) return;
                        CheckPending(local);
                        if (EditorApplication.timeSinceStartup < readyAt) return;
                        if (!pendingReloadChecked)
                        {
                            phase = 2; Reload(); return;
                        }
                        Claim(local);
                        return;
                    case 2:
                        if (!PanelReady(local)) return;
                        CheckPending(local);
                        CheckSaved(true);
                        pendingReloadChecked = true;
                        readyAt = EditorApplication.timeSinceStartup + .1;
                        phase = 1;
                        return;
                    case 3:
                        Check(Time.timeScale == 0 && local.SuccessPanel == heldPanel && heldPanel.Closing,
                            "Claimed panel remains in native closing before next-board initialization");
                        CheckProgress(game.User, shownCount);
                        CheckBalances(game.User);
                        Check(game.User.LocalPendingReward == null && !game.IsRestarting, "Claim is complete but next board waits for close");
                        if (EditorApplication.timeSinceStartup < readyAt) return;
                        int requests = adRequests;
                        local.NextButton.onClick.Invoke();
                        local.NextButton.onClick.Invoke();
                        adCompletion?.Invoke(true);
                        adCompletion?.Invoke(true);
                        adCompletion?.Invoke(false);
                        Check(adRequests == requests && !local.SettlementAdPending, "Duplicate closing clicks and late ad callbacks request no second ad");
                        CheckBalances(game.User);
                        CheckSaved(false);
                        Time.timeScale = 1;
                        phase = 4;
                        if (claimedCount == 2 && !claimedReloadChecked)
                        {
                            claimedReloadChecked = true;
                            Reload();
                        }
                        return;
                    case 4:
                        if (!NextBoardReady(local)) return;
                        CheckProgress(game.User, claimedCount);
                        CheckBalances(game.User);
                        Check(game.User.LocalPendingReward == null && shownCount == claimedCount,
                            "Close or claimed-state reload never recreates or duplicates the settlement");
                        if (claimedCount == 4)
                        {
                            Check(game.PlayerLevel == 13 && shownCount == 4 && pendingReloadChecked && claimedReloadChecked && failedAdChecked,
                                "All four source subrounds were solved and claimed; pending, closing and failed-ad paths ran");
                            Check(local.SubroundView != null && local.SubroundView.DotCount == 5 && local.SubroundView.SubRounds.activeSelf,
                                "Next source group grows the progress strip from four to five dots");
                            readyAt = EditorApplication.timeSinceStartup + 1.1;
                            phase = 5;
                            return;
                        }
                        board = null; solution = null; phase = 0;
                        return;
                    case 5:
                        Check(NextBoardReady(local), "Next group stays playable with no extra group-end modal");
                        CheckProgress(game.User, 4);
                        CheckBalances(game.User);
                        Check(game.User.LocalPendingReward == null, "Group end adds no second pending reward");
                        if (EditorApplication.timeSinceStartup < readyAt) return;
                        Debug.Log("NUT_LOCAL_SUBROUND_PLAY_PASS real boards 9/10/11/12 solved through world input and original solver; four native settlement panels, unclaimed hold and reload, cash plus diamonds once per claim, normal-ad failure and duplicate late callbacks, closing reload, exactly four level/daily increments and playable level13 with no extra group-end reward. Ads use controlled delegates only.");
                        Finish(0);
                        return;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void SolveBoard(LocalGameplayController local)
        {
            if (game.InputBlocked || game.IsRestarting || !game.Level.AreNutsInitialized || !game.IsInitDone || game.ModalInputBlocked) return;
            CheckProgress(game.User, claimedCount);
            CheckBalances(game.User);
            Check(game.User.LocalPendingReward == null && local.SuccessPanel == null, "Playable board has no unclaimed reward");
            if (Time.time < nextAction || !WorldAnimationsFinished()) return;
            if (board != game.Level.Board)
            {
                Check(local.SubroundView != null && local.SubroundView.DotCount == 4 && local.SubroundView.SubRounds.activeSelf,
                    "Each source small round retains the four-dot progress strip");
                board = game.Level.Board; boardLevel = game.PlayerLevel;
                Check(boardLevel >= 9 && boardLevel <= 12 && startup.MainLevel.Label.text == game.Tables.Text.GetText(2, "en", 5),
                    "Four source small rounds retain display level 5");
                solution = LocalGameplayPlayValidation.Solve(board); move = 0;
                Debug.Log("NUT_LOCAL_SUBROUND_BOARD level=" + boardLevel + " moves=" + solution.Count);
                return;
            }
            if (move >= solution.Count) return;
            Physics.SyncTransforms();
            Vector2Int step = solution[move++];
            Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(step.x).Bounds.bounds.center)).Kind == ScrewOperationKind.Ready,
                "Actual world selection");
            Check(game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(step.y).Bounds.bounds.center)).Kind == ScrewOperationKind.Moved,
                "Actual world transfer");
            if (move % 20 == 0) Debug.Log("NUT_LOCAL_SUBROUND_MOVES level=" + boardLevel + " completed=" + move);
            // Animation state, not an arbitrary long sleep, gates the next operation.
            nextAction = Time.time + .05f;
        }

        private static bool WorldAnimationsFinished()
        {
            var current = game.Level.Board;
            for (int i = 0; i < current.Screws.Length; i++)
            {
                var rod = current.Screws[i];
                if (!rod.IsCanOperator || game.Level.GetScrew(i).IsDoneAnimating) return false;
                foreach (var slot in rod.Slots)
                {
                    if (slot.Nut == null) continue;
                    var nut = game.Level.GetNut(slot.Nut);
                    if (nut.IsEntryAnimating || nut.IsSelectionAnimating) return false;
                }
            }
            return true;
        }

        private static bool PanelReady(LocalGameplayController local)
        {
            return !game.InputBlocked && !game.IsRestarting && local.AwaitingNext && local.SuccessPanel != null &&
                startup.Replay != null && !startup.Replay.IsClickMasked && !startup.Loading.IsVisible &&
                Vector3.Distance(local.SuccessPanel.Title.localScale, Vector3.one) < .001f &&
                Vector3.Distance(local.SuccessPanel.Main.localScale, Vector3.one) < .001f;
        }

        private static bool NextBoardReady(LocalGameplayController local)
        {
            return !game.InputBlocked && !game.IsRestarting && game.IsInitDone && game.Level.AreNutsInitialized &&
                !game.ModalInputBlocked && !local.AwaitingNext && local.SuccessPanel == null && !startup.Replay.IsClickMasked;
        }

        private static void CheckPending(LocalGameplayController local)
        {
            CheckProgress(game.User, shownCount);
            CheckBalances(game.User);
            Check(local.AwaitingNext && local.SuccessPanel != null && !local.SuccessPanel.Closing && game.ModalInputBlocked && !game.IsRestarting,
                "Unclaimed small-round settlement never automatically advances or closes");
            Check(game.User.LocalPendingReward != null && game.User.LocalPendingReward.ToString() == pendingDocument &&
                (int)game.User.LocalPendingReward["nextLevel"] == boardLevel + 1,
                "Pending reward amount, metadata and next level survive waiting and reload without reroll");
        }

        private static void Claim(LocalGameplayController local)
        {
            adCompletion = null;
            int beforeRequests = adRequests;
            heldPanel = local.SuccessPanel;
            Time.timeScale = 0;
            local.NextButton.onClick.Invoke();
            local.NextButton.onClick.Invoke();
            if (adCompletion != null)
            {
                Check(adRequests == beforeRequests + 1 && local.SettlementAdPending, "Duplicate normal Claim starts one controlled ad request");
                CheckBalances(game.User);
                bool success = failedAdChecked;
                adCompletion(success);
                if (!success) failedAdChecked = true;
            }
            else Check(adRequests == beforeRequests && !local.SettlementAdPending, "Below-threshold Claim uses no ad request");
            expectedGold += pendingGold;
            expectedDiamonds += pendingDiamonds;
            claimedCount++;
            CheckBalances(game.User);
            CheckProgress(game.User, claimedCount);
            Check(local.SuccessPanel == heldPanel && heldPanel.Closing && !game.IsRestarting && local.AwaitingNext && game.User.LocalPendingReward == null,
                "Each Claim grants exactly the displayed base reward then starts one native close");
            CheckSaved(false);
            readyAt = EditorApplication.timeSinceStartup + 1.1;
            phase = 3;
        }

        private static void CaptureAd(Action<bool> completed) { adRequests++; adCompletion = completed; }

        private static void CheckProgress(OriginalUserLocalData user, int completed)
        {
            Check(user.Level == 9 + completed && user.TodayPassLevelCount == completed && user.LevelSeed == 0,
                "Each completed small round advances Level and daily completion exactly once");
        }

        private static void CheckBalances(OriginalUserLocalData user)
        {
            Check(Mathf.Abs(user.Gold - expectedGold) < .0001f && Math.Abs(user.Coin - expectedDiamonds) < .0001,
                "Balances equal only the sum of individually claimed small-round rewards, including group end");
        }

        private static void CheckSaved(bool pending)
        {
            var saved = OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),
                Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            CheckProgress(saved, shownCount);
            CheckBalances(saved);
            Check(pending ? saved.LocalPendingReward != null && saved.LocalPendingReward.ToString() == pendingDocument : saved.LocalPendingReward == null,
                "Pending or claimed settlement is durable before native close/reconstruction");
        }

        private static void Reload()
        {
            game = null; startup = null; boundLocal = null; board = null; nextSceneProbe = 0;
            SceneManager.LoadScene("LuoSiSortGame");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(int code)
        {
            if (SessionState.GetBool(FinishKey, false)) return;
            SessionState.SetBool(Key, false); SessionState.SetBool(FinishKey, true); SessionState.SetInt(ResultKey, code);
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
            try { OriginalPreferenceFixture.Restore(); }
            catch (Exception error) { Debug.LogException(error); code = 1; }
            SessionState.SetBool(FinishKey, false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.Exit(code);
        }
    }
}
