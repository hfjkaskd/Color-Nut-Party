using System;
using System.Collections.Generic;
using NutSort.Content;
using NutSort.Gameplay;
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
    // Real UI raycast/standard Button dispatch; no direct onClick invocation,
    // reflection, input-gate overrides, synthetic completion, or currency grants.
    [InitializeOnLoad]
    public static class LocalTeachingClickFeedbackPlayValidation
    {
        private const string Key = "NutSort.LocalTeachingClickFeedback";
        private static double deadline;
        private static float readyAt;
        private static int phase, seed, selectedRod;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalPopTip closingFeedback;
        private static string missingReferenceDuringClose;
        private static ProtectedState before;
        private static readonly List<RaycastResult> hits = new List<RaycastResult>();

        private struct ProtectedState
        {
            public int Level, Seed, Revoke, Exchange, Add, Added, Moves, Passed, History;
            public float Gold;
            public double Coin;
            public string Board;
        }

        static LocalTeachingClickFeedbackPlayValidation()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            OriginalPreferenceFixture.Begin(Fixture(0));
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(1080, 2400, "Teaching HUD click feedback");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static string Fixture(int teachingSeed)
        {
            return "{\"IsCompleteRecordGuide\":true,\"Level\":" + (teachingSeed < 4 ? "1" : "2") +
                ",\"LevelSeed\":" + (teachingSeed < 4 ? teachingSeed : 0) +
                ",\"NewGameplayUnlockIndex\":1,\"Gold\":123.45,\"Coin\":6789," +
                "\"RevokeCount\":2,\"ExchangeCount\":2,\"AddScrewCount\":2}";
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline,
                    "Teaching click feedback timeout; seed=" + seed + " phase=" + phase);
                if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if (game == null || startup == null || startup.LocalGameplay == null ||
                    game.Level == null || !game.Level.AreNutsInitialized || !game.IsInitDone ||
                    game.InputBlocked || game.IsRestarting || startup.Replay.IsClickMasked ||
                    Time.time < readyAt) return;
                var local = startup.LocalGameplay;

                if (seed < 4)
                {
                    TickTeaching(local);
                    return;
                }

                Check(local.TeachingPanel == null && !game.IsCanOperatorScrew,
                    "Level two removes teaching and resets its world-input override");
                if (phase == 0)
                {
                    Check(!game.ModalInputBlocked && Tips().Length == 0, "No stale teaching modal or feedback");
                    before = CaptureState();
                    readyAt = Time.time + .8f;
                    phase = 10;
                    return;
                }
                if (phase == 10)
                {
                    Click(local.RewardsTop.Gold.Click);
                    Check(local.WithdrawalPanel != null && game.ModalInputBlocked,
                        "Level two cash Button opens the ordinary withdrawal panel");
                    CheckUnchanged(true);
                    readyAt = Time.time + .7f;
                    phase = 11;
                    return;
                }
                if (phase == 11)
                {
                    Click(local.WithdrawalPanel.CloseButton);
                    readyAt = Time.time + .7f;
                    phase = 12;
                    return;
                }
                if (phase == 12)
                {
                    Check(local.WithdrawalPanel == null && !game.ModalInputBlocked,
                        "Closing level two withdrawal restores ordinary input");
                    Click(local.Bottom.Replay);
                    Check(startup.Replay.Panel != null && game.ModalInputBlocked,
                        "Level two replay Button opens its original panel");
                    CheckUnchanged(true);
                    readyAt = Time.time + .7f;
                    phase = 13;
                    return;
                }
                if (phase == 13)
                {
                    Click(startup.Replay.Panel.ContinueButton);
                    readyAt = Time.time + .7f;
                    phase = 14;
                    return;
                }
                Check(startup.Replay.Panel == null && !game.ModalInputBlocked && Tips().Length == 0,
                    "Replay Continue closes normally without teaching feedback");
                CheckUnchanged(true);
                Debug.Log("NUT_LOCAL_TEACHING_CLICK_FEEDBACK_PASS four tutorial seeds: cash/coin/replay/exchange/add through EventSystem raycast and standard Button dispatch; one current-instruction toast with duplicate suppression and expiry; unchanged board/inventory/level/economy; real world selection and cancellation remain enabled; closing teaching hides live feedback immediately and its original lifetime destroys it without MissingReferenceException; level two cash/replay open and close normally. Explicit saved-state fixtures; OS mouse polling is verified separately.");
                Finish(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Finish(1);
            }
        }

        private static void TickTeaching(LocalGameplayController local)
        {
            if (phase == 6)
            {
                Application.logMessageReceived -= CaptureCloseException;
                Check(string.IsNullOrEmpty(missingReferenceDuringClose),
                    "Closing live teaching feedback must not produce MissingReferenceException: " + missingReferenceDuringClose);
                Check(local.TeachingPanel == null && closingFeedback == null &&
                    startup.TopCanvas.GetComponentsInChildren<OriginalPopTip>(true).Length == 0,
                    "The original lifetime destroys hidden feedback while the game remains alive");
                Check(game.IsCanOperatorScrew && !game.ModalInputBlocked,
                    "Ordinary teaching close keeps the existing world override and releases its modal");
                CheckUnchanged(true);
                LoadNextFixture();
                return;
            }
            Check(local.TeachingPanel != null && local.TeachingPanel.IsOpen &&
                game.PlayerLevel == 1 && game.User.LevelSeed == seed,
                "Expected live teaching panel for explicit seed " + seed);
            Check(game.IsCanOperatorScrew && game.ModalInputBlocked,
                "Teaching keeps its original modal and world-operation override");

            if (phase == 0)
            {
                foreach (var rod in game.Level.Board.Screws)
                    foreach (var slot in rod.Slots)
                        if (slot.Nut != null && game.Level.GetNut(slot.Nut).IsEntryAnimating) return;
                Check(Tips().Length == 0, "New fixture has no stale feedback");
                before = CaptureState();
                readyAt = Time.time + .7f;
                phase = 1;
                return;
            }
            if (phase == 1)
            {
                Click(local.RewardsTop.Gold.Click);
                var first = CheckSingleFeedback(local);
                Check(!local.RewardsTop.Coin.gameObject.activeInHierarchy &&
                    !local.RewardsTop.Hidden.gameObject.activeInHierarchy,
                    "Teaching hides the normal diamond card and milestone row; hidden Buttons are not dispatched");
                Click(local.Bottom.Replay);
                Check(CheckSingleFeedback(local) == first, "Replay does not stack feedback");
                Click(local.Bottom.GetOtherItem(3).Display.Click);
                Check(CheckSingleFeedback(local) == first, "Exchange does not stack feedback");
                Click(local.Bottom.GetOtherItem(4).Display.Click);
                Check(CheckSingleFeedback(local) == first, "Add-screw does not stack feedback");
                CheckBlockedPanels(local);
                CheckUnchanged(true);
                ScreenCapture.CaptureScreenshot("Library/local-teaching-click-feedback-seed-" + seed + ".png");
                readyAt = Time.time + .2f;
                phase = 2;
                return;
            }
            if (phase == 2)
            {
                CheckSingleFeedback(local);
                selectedRod = FindMovableRod();
                Check(ClickWorld(selectedRod).Kind == ScrewOperationKind.Ready,
                    "Visible teaching feedback still permits actual world selection");
                CheckBlockedPanels(local);
                CheckUnchanged(false);
                readyAt = Time.time + .25f;
                phase = 3;
                return;
            }
            if (phase == 3)
            {
                Check(ClickWorld(selectedRod).Kind == ScrewOperationKind.Reverted,
                    "Clicking the selected world rod cancels selection normally");
                CheckBlockedPanels(local);
                CheckUnchanged(true);
                readyAt = Time.time + 1.1f;
                phase = 4;
                return;
            }
            if (phase == 4)
            {
                Check(Tips().Length == 0, "Original one-second toast expires");
                Click(local.Bottom.Replay);
                CheckSingleFeedback(local);
                CheckBlockedPanels(local);
                CheckUnchanged(true);
                readyAt = Time.time + .2f;
                phase = 5;
                return;
            }
            if (seed == 3)
            {
                closingFeedback = CheckSingleFeedback(local);
                missingReferenceDuringClose = null;
                Application.logMessageReceived += CaptureCloseException;
                // Exercise the public teaching close callback without completing
                // a board, changing progress, or overriding any runtime gate.
                local.TeachingPanel.Close();
                Check(local.TeachingPanel == null && closingFeedback != null &&
                    !closingFeedback.gameObject.activeSelf && Tips().Length == 0,
                    "Closing teaching immediately hides the live feedback instance");
                var remaining = startup.TopCanvas.GetComponentsInChildren<OriginalPopTip>(true);
                Check(remaining.Length == 1 && remaining[0] == closingFeedback,
                    "Hidden feedback remains owned by its existing lifetime callback");
                CheckUnchanged(true);
                readyAt = Time.time + 1.2f;
                phase = 6;
                return;
            }
            LoadNextFixture();
        }

        private static void CaptureCloseException(string message, string stackTrace, LogType type)
        {
            if (message.IndexOf("MissingReferenceException", StringComparison.Ordinal) >= 0 ||
                stackTrace.IndexOf("MissingReferenceException", StringComparison.Ordinal) >= 0)
                missingReferenceDuringClose = message + "\n" + stackTrace;
        }

        private static void LoadNextFixture()
        {
            seed++;
            phase = 0;
            readyAt = 0;
            game = null;
            startup = null;
            PlayerPrefs.SetString(OriginalUserStore.Key, Fixture(seed));
            PlayerPrefs.Save();
            SceneManager.LoadScene("LuoSiSortGame");
        }

        private static void Click(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(),
                "Expected active standard Button");
            Check(EventSystem.current != null, "Scene EventSystem exists");
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var rect = (RectTransform)button.transform;
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                clickCount = 1
            };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            var target = hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Check(target == button.gameObject,
                "Actual UI raycast resolves " + button.name + "; first hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            Check(ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler),
                "Standard Button pointer-click handler receives the raycast hit");
        }

        private static OriginalPopTip[] Tips() => startup.TopCanvas.GetComponentsInChildren<OriginalPopTip>();

        private static OriginalPopTip CheckSingleFeedback(LocalGameplayController local)
        {
            var tips = Tips();
            Check(tips.Length == 1 && tips[0].Content.text == local.TeachingPanel.Tip.text,
                "One popup shows the current dynamic/localized teaching instruction");
            Check(!startup.Replay.IsClickMasked,
                "Teaching feedback does not acquire the counted full-screen click shield");
            return tips[0];
        }

        private static void CheckBlockedPanels(LocalGameplayController local)
        {
            Check(local.WithdrawalPanel == null && local.DiamondWithdrawalPanel == null &&
                local.WithdrawalAccount == null && local.WithdrawalConfirmation == null &&
                startup.Replay.Panel == null && local.ToolOffer == null && !local.ToolAdPending &&
                !local.ResultVisible && !game.IsExchanging,
                "Teaching HUD clicks keep cash/coin/account/replay/tool/result panels and exchange closed");
            Check(game.IsCanOperatorScrew && game.ModalInputBlocked && !game.InputBlocked,
                "Feedback leaves teaching and startup input permissions unchanged");
        }

        private static int FindMovableRod()
        {
            foreach (var rod in game.Level.Board.Screws)
                if (!rod.IsDone && !rod.IsNull && !rod.IsHidden && !rod.IsColorMask &&
                    !rod.IsDontMove && !rod.IsLocked && rod.IsCanOperator) return rod.Index;
            throw new InvalidOperationException("Teaching fixture needs a normally selectable rod");
        }

        private static ScrewOperation ClickWorld(int index)
        {
            Physics.SyncTransforms();
            return game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(
                game.Level.GetScrew(index).Bounds.bounds.center));
        }

        private static ProtectedState CaptureState()
        {
            var user = game.User;
            return new ProtectedState
            {
                Level = user.Level, Seed = user.LevelSeed, Revoke = user.RevokeCount,
                Exchange = user.ExchangeCount, Add = user.AddScrewCount,
                Added = user.CurrentLevelAddScrewCount, Moves = user.ScrewMoveCount,
                Passed = user.TodayPassLevelCount, Gold = user.Gold, Coin = user.Coin,
                History = game.Level.MoveHistoryCount,
                Board = OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot())
            };
        }

        private static void CheckUnchanged(bool checkBoard)
        {
            var user = game.User;
            Check(user.Level == before.Level && user.LevelSeed == before.Seed &&
                user.RevokeCount == before.Revoke && user.ExchangeCount == before.Exchange &&
                user.AddScrewCount == before.Add && user.CurrentLevelAddScrewCount == before.Added &&
                user.ScrewMoveCount == before.Moves && user.TodayPassLevelCount == before.Passed &&
                user.Gold == before.Gold && user.Coin == before.Coin && user.LocalPendingReward == null &&
                game.Level.MoveHistoryCount == before.History,
                "HUD feedback and select/cancel do not change inventory, level, rewards, balances, or history");
            if (checkBoard)
                Check(OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot()) == before.Board,
                    "Board and move records remain unchanged");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(int exitCode)
        {
            Application.logMessageReceived -= CaptureCloseException;
            OriginalPreferenceFixture.Restore();
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(exitCode);
        }
    }
}
