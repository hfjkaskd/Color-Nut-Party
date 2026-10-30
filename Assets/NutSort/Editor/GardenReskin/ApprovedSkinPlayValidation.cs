using System;
using System.Collections.Generic;
using System.IO;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Editor acceptance only: real scene/prefabs, standard UI raycasts/events,
    // explicit saved-state and ad fixtures. No runtime gate or input replacement.
    [InitializeOnLoad]
    public static class ApprovedSkinPlayValidation
    {
        private const string Key = "NutSort.ApprovedSkinPlay";
        private static readonly string[] Captures = { "cash", "account", "diamond", "refresh" };
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static readonly HashSet<string> InputFieldsReached = new HashSet<string>();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static int phase, earliestFrame, channelIndex, diamondIndex, adRequests, buttonClicks;
        private static int initialLevel, initialSeed, initialRevoke, initialAdd, initialAdded, initialHistory;
        private static float ready, initialGold;
        private static double deadline, initialCoin;
        private static DateTime startedUtc;
        private static Action<bool> adCompletion;
        private static string initialBoard, runtimeError;
        private static string country, culture;
        private static int captureWidth, captureHeight;
        private static bool finishing;

        static ApprovedSkinPlayValidation()
        {
            if (!SessionState.GetBool(Key, false)) return;
            country = SessionState.GetString(Key + ".Country", "BR");
            culture = country == "US" ? "en-US" : "pt-BR";
            captureWidth = SessionState.GetInt(Key + ".Width", 720);
            captureHeight = SessionState.GetInt(Key + ".Height", 1600);
            startedUtc = DateTime.UtcNow;
            deadline = EditorApplication.timeSinceStartup + 240;
            Application.logMessageReceived += CaptureError;
            EditorApplication.update += Tick;
        }

        public static void Run() => RunConfigured("BR", 720, 1600);
        public static void RunUS() => RunConfigured("US", 720, 1600);
        public static void RunUSWide() => RunConfigured("US", 720, 1280);

        private static void RunConfigured(string selectedCountry, int width, int height)
        {
            try
            {
                OriginalPreferenceFixture.Begin("{\"Level\":4,\"Gold\":1.76,\"Coin\":0," +
                    "\"ExchangeCount\":0,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}");
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Approved skin acceptance " + selectedCountry);
                SessionState.SetString(Key + ".Country", selectedCountry);
                SessionState.SetInt(Key + ".Width", width);
                SessionState.SetInt(Key + ".Height", height);
                SessionState.SetBool(Key, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Tick()
        {
            if (finishing) return;
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout at phase " + phase);
                if (!EditorApplication.isPlaying) return;
                if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if (game == null || startup == null || startup.LocalGameplay == null || game.Level == null ||
                    !game.Level.AreNutsInitialized || !game.IsInitDone || game.IsRestarting || game.InputBlocked ||
                    startup.Replay.IsClickMasked || Time.time < ready || Time.frameCount < earliestFrame) return;
                var local = startup.LocalGameplay;
                if (phase > 0) CheckProtectedState();
                switch (phase)
                {
                    case 0:
                        Check(local.TeachingPanel == null && !game.ModalInputBlocked, "Non-teaching fixture ready");
                        UnityEngine.Object.FindObjectOfType<OriginalUserSession>().ConfigureRewardCountry(country);
                        local.RefreshRewardsTop();
                        initialGold = game.User.Gold; initialCoin = game.User.Coin;
                        initialLevel = game.User.Level; initialSeed = game.User.LevelSeed;
                        initialRevoke = game.User.RevokeCount; initialAdd = game.User.AddScrewCount;
                        initialAdded = game.User.CurrentLevelAddScrewCount; initialHistory = game.Level.MoveHistoryCount;
                        initialBoard = OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot());
                        Check(game.User.ExchangeCount == 0, "Zero exchange inventory fixture");
                        Next(1, .8f); return;
                    case 1:
                        Click(local.RewardsTop.Gold.Click);
                        Check(local.WithdrawalPanel != null && game.ModalInputBlocked, "Cash opens and blocks board");
                        Next(2, .8f); return;
                    case 2:
                        Check(UnityEngine.Object.FindObjectsOfType<OriginalWithdrawalPanel>().Length == 1,
                            "One cash panel instance");
                        Check(Vector3.Distance(local.WithdrawalPanel.Main.localScale, Vector3.one) < .001f,
                            "Cash opening animation settled");
                        Check(local.WithdrawalPanel.GoldLabel.text == new OriginalGoldFormatter(() => culture).Format(initialGold) &&
                            local.WithdrawalPanel.ProgressLabel.text == "3/5" && Mathf.Abs(local.WithdrawalPanel.Fill - .6f) < .001f,
                            "Cash dynamic amount and level progress");
                        Probe(local.WithdrawalPanel.CloseButton); Probe(local.WithdrawalPanel.WithdrawalButton);
                        StatsAreaValidation.Check(local.WithdrawalPanel.PlayerParent.GetComponentInChildren<OriginalPlayerInfo>());
                        Capture("cash"); Next(3, .3f); return;
                    case 3:
                        Click(local.WithdrawalPanel.WithdrawalButton);
                        Next(4, .8f); return;
                    case 4:
                        Check(local.WithdrawalAccount != null && !local.WithdrawalPanel.gameObject.activeSelf &&
                            game.ModalInputBlocked, "Cash routes to account modal");
                        CheckInputs(local.WithdrawalAccount);
                        Probe(local.WithdrawalAccount.CloseButton); Probe(local.WithdrawalAccount.GetButton);
                        Capture("account"); Next(5, .3f); return;
                    case 5:
                        local.WithdrawalAccount.Name = ""; local.WithdrawalAccount.Email = "";
                        local.WithdrawalAccount.Number = "";
                        Click(local.WithdrawalAccount.GetButton);
                        Check(!local.WithdrawalAccount.Closing && local.WithdrawalConfirmation == null &&
                            UnityEngine.Object.FindObjectOfType<OriginalPopTip>() != null, "Blank account rejected visibly");
                        Next(6, 1.2f); return;
                    case 6:
                        while (channelIndex < local.WithdrawalAccount.ChannelCount &&
                            !local.WithdrawalAccount.ChannelButton(channelIndex).gameObject.activeInHierarchy) channelIndex++;
                        if (channelIndex < local.WithdrawalAccount.ChannelCount)
                        {
                            var channel = local.WithdrawalAccount.ChannelButton(channelIndex);
                            Probe(channel);
                            if (local.WithdrawalAccount.Flow.GetTypeIndex != channelIndex) Click(channel);
                            Next(7, .4f); return;
                        }
                        Check(InputFieldsReached.Count >= 2, "Visible editable account fields reached");
                        local.WithdrawalAccount.Name = "Fixture User";
                        local.WithdrawalAccount.Email = "fixture@example.test";
                        local.WithdrawalAccount.Number = "123456789";
                        Click(local.WithdrawalAccount.GetButton);
                        Next(8, .8f); return;
                    case 7:
                        Check(local.WithdrawalAccount.Flow.GetTypeIndex == channelIndex, "Channel Button selection");
                        CheckInputs(local.WithdrawalAccount);
                        channelIndex++; Next(6, .3f); return;
                    case 8:
                        Check(local.WithdrawalAccount == null && local.WithdrawalConfirmation != null &&
                            !string.IsNullOrEmpty(local.WithdrawalConfirmation.TipLabel.text), "Account confirmation and dynamic details");
                        Click(local.WithdrawalConfirmation.ReenterButton);
                        Next(9, .8f); return;
                    case 9:
                        Check(local.WithdrawalConfirmation == null && local.WithdrawalAccount != null &&
                            local.WithdrawalAccount.Name == "Fixture User", "Reenter preserves submitted fixture values");
                        Click(local.WithdrawalAccount.CloseButton);
                        Next(10, .8f); return;
                    case 10:
                        Check(local.WithdrawalAccount == null && local.WithdrawalPanel.gameObject.activeSelf &&
                            game.ModalInputBlocked, "Closing account restores cash modal");
                        Click(local.WithdrawalPanel.CloseButton);
                        Next(11, .8f); return;
                    case 11:
                        Check(local.WithdrawalPanel == null && !game.ModalInputBlocked, "Cash close releases modal input");
                        Click(local.RewardsTop.Coin.Click);
                        Next(12, .8f); return;
                    case 12:
                        Check(local.DiamondWithdrawalPanel != null && game.ModalInputBlocked, "Coin Cash opens diamond panel");
                        Check(local.DiamondWithdrawalPanel.Options.Length == 4 &&
                            local.DiamondWithdrawalPanel.BalanceLabel.text == new OriginalGoldFormatter(() => culture).Format(0) &&
                            local.DiamondWithdrawalPanel.ProgressLabel.text.Contains(new OriginalGoldFormatter(() => culture).Format(500)),
                            "Four tiers and dynamic country-formatted diamond amount");
                        foreach (var option in local.DiamondWithdrawalPanel.Options) Probe(option);
                        Probe(local.DiamondWithdrawalPanel.CloseButton); Probe(local.DiamondWithdrawalPanel.WithdrawalButton);
                        Capture("diamond"); Next(13, .3f); return;
                    case 13:
                        if (local.DiamondWithdrawalPanel.SelectedIndex != diamondIndex)
                            Click(local.DiamondWithdrawalPanel.Options[diamondIndex]);
                        else Probe(local.DiamondWithdrawalPanel.Options[diamondIndex]);
                        Next(14, .4f); return;
                    case 14:
                        Check(local.DiamondWithdrawalPanel.SelectedIndex == diamondIndex &&
                            !string.IsNullOrEmpty(local.DiamondWithdrawalPanel.ProgressLabel.text), "Diamond tier selection refreshes text");
                        diamondIndex++; Next(diamondIndex < 4 ? 13 : 15, .3f); return;
                    case 15:
                        Click(local.DiamondWithdrawalPanel.WithdrawalButton);
                        var tip = UnityEngine.Object.FindObjectOfType<OriginalPopTip>();
                        Check(tip != null && tip.Content.text.Contains(new OriginalGoldFormatter(() => culture).Format(2000)),
                            "Fourth tier insufficient-balance feedback");
                        Next(16, 1.2f); return;
                    case 16:
                        Click(local.DiamondWithdrawalPanel.CloseButton);
                        Next(17, .8f); return;
                    case 17:
                        Check(local.DiamondWithdrawalPanel == null && !game.ModalInputBlocked, "Diamond close releases input");
                        local.BindRewardedToolAds((type, completed) => { Check(type == 3, "Exchange ad type"); adRequests++; adCompletion = completed; });
                        Click(local.Bottom.GetOtherItem(3).Display.Click);
                        Check(local.ToolOffer != null && adRequests == 0 && game.ModalInputBlocked, "Refresh appears before ad");
                        Next(18, .8f); return;
                    case 18:
                        Probe(local.ToolOffer.CloseButton); Probe(local.ToolOffer.ClaimButton);
                        Capture("refresh"); Next(19, .3f); return;
                    case 19:
                        Click(local.ToolOffer.CloseButton);
                        Next(20, .7f); return;
                    case 20:
                        Check(local.ToolOffer == null && !game.ModalInputBlocked && adRequests == 0 &&
                            game.User.ExchangeCount == 0, "Refresh close grants nothing and requests no ad");
                        Click(local.Bottom.GetOtherItem(3).Display.Click);
                        Next(21, .7f); return;
                    case 21:
                        Click(local.ToolOffer.ClaimButton);
                        Check(local.ToolOffer == null && local.ToolAdPending && adRequests == 1 &&
                            adCompletion != null && game.User.ExchangeCount == 0, "Claim requests ad without early grant");
                        adCompletion(true); adCompletion(true);
                        Check(game.User.ExchangeCount == 1 && game.IsExchanging && !local.ToolAdPending,
                            "Duplicate ad completion grants once and activates exchange");
                        Next(22, 1.2f); return;
                    case 22:
                        Click(local.ExchangeMask);
                        Next(23, .8f); return;
                    case 23:
                        Check(!game.IsExchanging && !game.ModalInputBlocked && !local.ToolAdPending &&
                            local.ToolOffer == null && game.User.ExchangeCount == 1 && adRequests == 1,
                            "Exchange cancellation restores input with one granted tool");
                        CheckProtectedState(); CheckCaptures();
                        Check(runtimeError == null, "No runtime Error/Exception: " + runtimeError);
                        Finish(0); return;
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }

        private static void Next(int next, float delay)
        { phase = next; ready = Time.time + delay; earliestFrame = Time.frameCount + 1; }

        private static PointerEventData Probe(Selectable control)
        {
            Check(control != null && control.gameObject.activeInHierarchy && control.IsInteractable(), "Active interactable UI control");
            Check(EventSystem.current != null, "Scene EventSystem exists");
            Canvas.ForceUpdateCanvases();
            var canvas = control.GetComponentInParent<Canvas>();
            var rect = (RectTransform)control.transform;
            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            Check(point.x >= 0 && point.x < Screen.width && point.y >= 0 && point.y < Screen.height,
                "Control center inside render area: " + control.name);
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left, clickCount = 1 };
            Hits.Clear(); EventSystem.current.RaycastAll(data, Hits);
            var target = Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target == control.gameObject, "UI raycast must reach " + control.name + "; first=" +
                (Hits.Count == 0 ? "none" : Hits[0].gameObject.name));
            return data;
        }

        private static void Click(Button button)
        {
            var data = Probe(button);
            Check(ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler), "Button pointer down");
            Check(ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler), "Button pointer up");
            Check(ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler), "Button pointer click");
            buttonClicks++;
        }

        private static void CheckInputs(OriginalWithdrawalUserInfoPanel panel)
        {
            int count = 0;
            foreach (var field in panel.GetComponentsInChildren<TMP_InputField>())
            {
                // Country area code is intentionally disabled in the original Prefab.
                if (!field.IsInteractable()) continue;
                Probe(field); InputFieldsReached.Add(field.name); count++;
            }
            Check(count >= 2, "Current channel exposes reachable editable account fields");
        }

        private static void Capture(string name)
        {
            Check(Screen.width == captureWidth && Screen.height == captureHeight,
                "Actual render dimensions match configured " + captureWidth + "x" + captureHeight);
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot("Library/approved-skin-" + name + ".png");
        }

        private static void CheckCaptures()
        {
            foreach (string name in Captures)
            {
                string path = "Library/approved-skin-" + name + ".png";
                Check(File.Exists(path) && File.GetLastWriteTimeUtc(path) >= startedUtc.AddSeconds(-2), "Fresh completed screenshot: " + name);
                using (var stream = File.OpenRead(path))
                {
                    var header = new byte[24];
                    Check(stream.Read(header, 0, header.Length) == 24 && header[0] == 137 && header[1] == 80 &&
                        header[2] == 78 && header[3] == 71 && ReadBigEndian(header, 16) == captureWidth && ReadBigEndian(header, 20) == captureHeight,
                        "PNG dimensions: " + name);
                }
            }
        }

        private static int ReadBigEndian(byte[] bytes, int start) =>
            (bytes[start] << 24) | (bytes[start + 1] << 16) | (bytes[start + 2] << 8) | bytes[start + 3];

        private static void CheckProtectedState()
        {
            Check(game.User.Gold == initialGold && game.User.Coin == initialCoin && game.User.Level == initialLevel &&
                game.User.LevelSeed == initialSeed && game.User.RevokeCount == initialRevoke && game.User.AddScrewCount == initialAdd &&
                game.User.CurrentLevelAddScrewCount == initialAdded && game.User.LocalPendingReward == null &&
                game.Level.MoveHistoryCount == initialHistory && OriginalBoardSnapshotJson.Write(game.Level.CaptureSnapshot()) == initialBoard,
                "Balances, level, board, history and unrelated inventory remain unchanged");
        }

        private static void CaptureError(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) runtimeError = message; }

        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        private static void Finish(int code)
        {
            finishing = true;
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            try
            {
                string backup = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../Library/ValidationBackups/GameplayUserLocalData.bin"));
                bool hadBackup = File.Exists(backup), existed = false;
                string original = null;
                if (hadBackup)
                    using (var reader = new BinaryReader(File.OpenRead(backup)))
                    { existed = reader.ReadBoolean(); original = reader.ReadString(); }
                OriginalPreferenceFixture.Restore();
                if (code == 0) Check(hadBackup, "Original preference backup existed before restoration");
                if (hadBackup)
                    Check(!File.Exists(backup) && PlayerPrefs.HasKey(OriginalUserStore.Key) == existed &&
                        (!existed || PlayerPrefs.GetString(OriginalUserStore.Key) == original),
                        "Exact original preference value and presence restored");
            }
            catch (Exception error) { Debug.LogException(error); code = 1; }
            // Keep error capture active through Restore, and never print PASS if
            // persistence/restoration failed after the gameplay checks finished.
            if (runtimeError != null) code = 1;
            Application.logMessageReceived -= CaptureError;
            if (code == 0)
                Debug.Log("APPROVED_SKIN_PLAY_PASS " + country + " " + captureWidth + "x" + captureHeight +
                    " four settled Unity captures; " + buttonClicks +
                    " Button raycast/down/up/click dispatches; active editable account fields reachable; channels, blank rejection, confirmation/reenter and modal restoration; all four diamond tiers; unchanged balances/board; Refresh close/no-ad and Claim/one grant from duplicate fixture callbacks; exact original preferences restored. Input text uses fixture values; OS keyboard and production ad SDK are not tested.");
            EditorApplication.Exit(code);
        }
    }
}
