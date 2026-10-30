using System;
using System.Collections.Generic;
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
    public static class LocalAnalyticsPlayValidation
    {
        private const string Key = "NutSort.LocalAnalyticsPlayValidation";
        private const string Output = "Docs/AnalyticsValidation/20261014-implementation/play";
        private const string Backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
        private const string Fixture = "{\"Level\":2,\"LevelSeed\":0,\"Gold\":0,\"Coin\":0,\"RevokeCount\":5,\"IsAudio\":true," +
            "\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true,\"IsCompleteCoinNewPeopleReward\":true," +
            "\"LocalPendingReward\":{\"country\":\"US\",\"amount\":0.4,\"diamonds\":0,\"nextLevel\":2}}";
        private static readonly JArray Checks = new JArray(), Captures = new JArray(), AnalyticsEvents = new JArray();
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static readonly HashSet<int> ActivationSteps = new HashSet<int>();
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static OriginalAudioPlayer audio;
        private static LocalGameplayController local;
        private static int phase, stage;
        private static double readyAt, deadline, nextProbe;
        private static string error;
        private static DateTime began;
        private static bool receiptBefore, receiptAfter, coinPreviewSeen;
        private static Action<bool> adDone;
        private static int adRequests;
        private static Action deferredUiAction;
        private static double uiMutationAt;
        private static int captureFrame;

        static LocalAnalyticsPlayValidation()
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
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start validation in Edit Mode.");
                Directory.CreateDirectory(Output);
                var testSettings=Resources.Load<LocalAnalyticsSettings>("Configuration/LocalAnalytics");
                SessionState.SetBool(Key+".OriginalIsTest",testSettings.IsTest);testSettings.IsTest=true;
                EditorUtility.SetDirty(testSettings);AssetDatabase.SaveAssets();
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetBool(Key + ".Finish", false);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Earning flow acceptance: isolated US fixture");
                StartPolling();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void StartPolling()
        {
            phase = 0; stage = 1; ClearReferences(); AnalyticsEvents.Clear(); LocalAnalytics.EventObserved -= RecordAnalytics; LocalAnalytics.EventObserved += RecordAnalytics;
            Checks.Clear(); Captures.Clear(); ActivationSteps.Clear();
            receiptBefore = receiptAfter = coinPreviewSeen = false;
            deferredUiAction = null; uiMutationAt = 0; captureFrame = -1;
            readyAt = nextProbe = 0; deadline = EditorApplication.timeSinceStartup + 360;
            error = null; began = DateTime.UtcNow;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= ConfigureCountry; SceneManager.sceneLoaded += ConfigureCountry;
        }

        private static void ConfigureCountry(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "LuoSiSortGame") return;
            var session = UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
            if (session != null) session.ConfigureRewardCountry("US");
        }

        private static void ClearReferences() { game = null; startup = null; audio = null; local = null; }

        private static bool SceneReady()
        {
            if (local != null) return true;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextProbe) return false;
            nextProbe = now + .15;
            if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
            if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
            if (audio == null) audio = UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
            if (game == null || startup == null || audio == null || startup.LocalGameplay == null ||
                game.Level == null || !game.Level.AreNutsInitialized || game.IsRestarting || startup.Loading.IsVisible) return false;
            local = startup.LocalGameplay;
            Check(audio.UserState.DisplayCountry(game.Tables) == "US", "Fixture country enters through the production US country API");
            return true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now >= deadline) throw new TimeoutException("Earning flow validation timed out at phase " + phase);
                if (deferredUiAction != null)
                {
                    if (now < uiMutationAt || Time.frameCount <= captureFrame) return;
                    var action = deferredUiAction; deferredUiAction = null;
                    action();
                    return;
                }
                if (now < readyAt || !SceneReady()) return;
                if (startup.Replay.IsClickMasked) return;
                switch (phase)
                {
                    case 0:
                        if (!SuccessReady()) return;
                        Check(game.User.Gold == 0 && game.User.LocalPendingReward != null, "Early settlement preview has not granted cash, stage " + stage);
                        Check(!local.SuccessPanel.GetButton.gameObject.activeSelf && local.SuccessPanel.MoreButton.gameObject.activeSelf &&
                            !local.SuccessPanel.Ad.activeSelf, "Early reward uses one Withdraw button without a rewarded ad");
                        local.BindSettlementAds(done => { throw new InvalidOperationException("Early flow requested an ad."); },
                            done => { throw new InvalidOperationException("Early flow requested a rewarded ad."); });
                        Capture("early-" + stage + "-settlement", local.SuccessPanel.gameObject);
                        Click(local.SuccessPanel.MoreButton); Next(1); break;
                    case 1:
                        if (local.TutorialBalancePanel == null) return;
                        Next(2, 1); break;
                    case 2:
                        Check(Near(game.User.Gold, stage == 1 ? .4f : .6f), "Earned early amount is granted once before presentation");
                        Check(local.TutorialBalancePanel.Tip.text == (stage == 1 ? "$0.40" : "$0.60"), "Target completion displays the earned amount");
                        Capture("early-" + stage + "-target", local.TutorialBalancePanel.gameObject);
                        Click(local.TutorialBalancePanel.ContinueButton); Next(3); break;
                    case 3:
                        if (local.WithdrawalPanel == null) return;
                        Next(4, 1); break;
                    case 4:
                        Check(local.WithdrawalPanel.Fill == 1, "Early withdrawal progress fills the full track");
                        Capture("early-" + stage + "-details", local.WithdrawalPanel.gameObject);
                        Click(local.WithdrawalPanel.WithdrawalButton); Next(5, .1); break;
                    case 5:
                        if (local.EarlyLoadingPanel == null) return;
                        Capture("early-" + stage + "-loading", null); Next(6, .2); break;
                    case 6:
                        if (local.WithdrawalAccount == null) return;
                        Check(local.EarlyLoadingPanel == null, "Loading finishes automatically before account entry");
                        Next(7, 1); break;
                    case 7:
                        Capture("early-" + stage + "-account", local.WithdrawalAccount.gameObject);
                        if (stage == 1) { Click(local.WithdrawalAccount.CloseButton); Next(8, 1); }
                        else
                        {
                            Check(local.WithdrawalAccount.ChannelCount >= 3, "US account offers the expected payment-channel buttons");
                            Click(local.WithdrawalAccount.ChannelButton(2));
                            Next(10);
                        }
                        break;
                    case 8:
                        if (local.EarlyWithdrawalStage != 0 || local.WithdrawalAccount != null) return;
                        CheckRecord(1, false, .4f);
                        Check(game.User.Gold == 0, "Closing the first account form archives only its earned cash, wallet returns to zero");
                        stage = 2;
                        SeedPending(3, .6f); Reload(0); break;
                    case 10:
                        Check(local.WithdrawalAccount.Flow.IsEmail, "Selected US PayPal channel accepts email");
                        local.WithdrawalAccount.Name = "Acceptance Tester";
                        local.WithdrawalAccount.Email = "acceptance@example.test";
                        Click(local.WithdrawalAccount.GetButton); Next(11); break;
                    case 11:
                        if (local.WithdrawalConfirmation == null) return;
                        Next(12, 1); break;
                    case 12:
                        Capture("early-2-account-confirmation", local.WithdrawalConfirmation.gameObject);
                        Click(local.WithdrawalConfirmation.ConfirmButton); Next(13); break;
                    case 13:
                        if (local.EarlyVerificationPanel == null) return;
                        Next(14, 1); break;
                    case 14:
                        if (local.EarlyVerificationPanel.Steps.ActiveTracks != 0 ||
                            !local.EarlyVerificationPanel.ConfirmButton.gameObject.activeInHierarchy) return;
                        Capture("early-2-verification", local.EarlyVerificationPanel.gameObject);
                        Check(local.EarlyVerificationPanel.Flow == null, "Local verification never constructs the native payment-success flow");
                        Click(local.EarlyVerificationPanel.ConfirmButton); Next(15, .1); break;
                    case 15:
                        if (local.EarlyWithdrawalStage != 0) return;
                        CheckRecord(2, true, .6f);
                        Check(game.User.Gold == 0, "Second request is pending and its cash is archived before activation");
                        Next(16, 0); break;
                    case 16:
                        ObserveActivation();
                        if (!ActivationComplete() || local.EarningSequencePanel != null) return;
                        Check(ActivationSteps.Count == 6 && receiptBefore && receiptAfter,
                            "All six activation stages ran automatically, including the visible zero-to-500 receipt");
                        Check(Near(game.User.Gold, 500) && game.User.Coin == 0, "Activation sets the real cash balance to 500 once");
                        CheckNoPaidData();
                        Next(17, 1); break;
                    case 17:
                        if (!game.IsInitDone || game.ModalInputBlocked) return;
                        Click(local.RewardsTop.Gold.Click); Next(18, 1); break;
                    case 18:
                        Check(local.WithdrawalPanel != null && local.WithdrawalPanel.HistoryButton != null,
                            "Cash details expose the prefab-authored history Button");
                        Click(local.WithdrawalPanel.HistoryButton); Next(19, 1); break;
                    case 19:
                        CheckHistory(); Capture("cash-history-tall", local.CashHistoryPanel.gameObject);
                        QueueUiAction(() => PlayModeWindow.SetCustomRenderingResolution(720, 1280, "Cash history short viewport"));
                        Next(20, 1); break;
                    case 20:
                        if (Screen.width != 720 || Screen.height != 1280) return;
                        CheckHistory(); Capture("cash-history-short", local.CashHistoryPanel.gameObject);
                        Click(local.CashHistoryPanel.CloseButton); Next(21, 1); break;
                    case 21:
                        Click(local.WithdrawalPanel.CloseButton); Next(22, 1); break;
                    case 22:
                        Check(!game.ModalInputBlocked, "History and cash details release the gameplay input guard");
                        game.SaveUserData(); Reload(23); break;
                    case 23:
                        if (!game.IsInitDone || game.ModalInputBlocked) return;
                        Check(ActivationComplete() && Near(game.User.Gold, 500) && local.EarningSequencePanel == null,
                            "Actual scene reload preserves completed activation without replaying or granting another 500");
                        CheckRecord(1, false, .4f); CheckRecord(2, true, .6f); CheckNoPaidData();
                        SeedPending(4, 1.25f); Reload(24); break;
                    case 24:
                        if (!SuccessReady()) return;
                        adRequests = 0; adDone = null;
                        local.BindSettlementAds(done => { adRequests++; adDone = done; }, done => { adRequests++; adDone = done; });
                        Capture("later-settlement-normal-and-10x", local.SuccessPanel.gameObject);
                        Click(local.SuccessPanel.MoreButton); Next(25, .1); break;
                    case 25:
                        Check(adRequests == 1 && local.SettlementAdPending && adDone != null, "Later 10X button waits for an explicit rewarded completion");
                        adDone(false); adDone(true);
                        Check(Near(game.User.Gold, 500) && game.User.LocalPendingReward != null && !local.SuccessPanel.Closing,
                            "Failed 10X and late duplicate success preserve the pending base reward without granting");
                        Next(26, 1); break;
                    case 26:
                        Click(local.SuccessPanel.MoreButton); Next(27, .1); break;
                    case 27:
                        Check(adRequests == 2 && local.SettlementAdPending, "Rewarded bonus can be retried after failure");
                        adDone(true); adDone(true);
                        Check(Near(game.User.Gold, 512.5f) && game.User.LocalPendingReward == null,
                            "Successful 10X grants 1.25 times ten exactly once, including duplicate callback protection");
                        Next(28, .1); break;
                    case 28:
                        var coinPreview = local.EarningSequencePanel;
                        if (coinPreview != null && coinPreview.MessageTitle.gameObject.activeInHierarchy &&
                            coinPreview.MessageTitle.text == "Extra reward unlocked" && !coinPreviewSeen)
                        {
                            Check(coinPreview.MessageBody.text.Contains("30000"), "Diamond unlock preview announces the configured 30000 reward");
                            Check(game.User.Coin == 0, "Diamond preview does not grant the advertised future reward");
                            coinPreviewSeen = true;
                            Capture("diamond-unlock-preview", coinPreview.gameObject);
                        }
                        if (local.CoinUnlockGuide == null || !local.CoinUnlockGuide.HollowMask.gameObject.activeInHierarchy) return;
                        Check(coinPreviewSeen, "Diamond preview appears automatically before the HUD spotlight");
                        Check(local.CoinUnlockGuide.InitialGuideIndex == 3 && game.User.IsShowCoin &&
                            !game.User.IsCompleteShowCoinRewardHint, "Diamond spotlight waits for its real standard Button click");
                        Capture("diamond-unlock-spotlight", local.CoinUnlockGuide.gameObject);
                        Click(local.CoinUnlockGuide.ContinueButton); Next(33, 1); break;
                    case 33:
                        if (game.ModalInputBlocked || !game.IsInitDone) return;
                        Check(local.CoinUnlockGuide == null && game.User.IsCompleteShowCoinRewardHint && game.User.Coin == 0,
                            "Clicking the diamond spotlight completes its saved guide without granting preview diamonds");
                        SeedPending(4, .75f); game.User.NormalGetTimes = 2;
                        audio.UserState.Store.SaveData(false, null); Reload(29); break;
                    case 29:
                        if (!SuccessReady()) return;
                        adRequests = 0; adDone = null;
                        local.BindSettlementAds(done => { adRequests++; adDone = done; }, done => { adRequests++; adDone = done; });
                        Click(local.SuccessPanel.GetButton); Next(30, .1); break;
                    case 30:
                        Check(adRequests == 1 && adDone != null && local.SettlementAdPending, "Normal claim reaches its configured ad threshold");
                        adDone(false); adDone(true);
                        Check(Near(game.User.Gold, 513.25f) && game.User.LocalPendingReward == null,
                            "Normal ad failure completes the earned base claim once and ignores a late success");
                        Next(31, 3); break;
                    case 31:
                        if (game.ModalInputBlocked || !game.IsInitDone) return;
                        Check(game.User.IsCompleteShowCoinRewardHint && local.CoinUnlockGuide == null,
                            "Reload and another settlement do not replay the completed diamond spotlight");
                        CheckNoPaidData(); Capture("flow-completed", null); Next(32, 1); break;
                    case 32: ValidateAnalytics(); Finish(0); break;
                }
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void RecordAnalytics(string name, Dictionary<string,object> fields)
        {
            AnalyticsEvents.Add(new JObject { { "event_name", name }, { "payload", JObject.FromObject(fields) } });
        }
        private static void ValidateAnalytics()
        {
            var eventIds=new HashSet<string>();var resourceKeys=new HashSet<string>();var counts=new Dictionary<string,int>();
            int rewardedGrants=0,rewardedFailures=0,submits=0,results=0;
            foreach(JObject item in AnalyticsEvents)
            {
                string name=(string)item["event_name"];var payload=(JObject)item["payload"];
                Check(eventIds.Add((string)payload["event_id"]),"Unique observed event ID: "+name);
                counts[name]=counts.TryGetValue(name,out int n)?n+1:1;
                if(name=="resource_change")
                {
                    string key=(string)payload["transaction_id"]+":"+(string)payload["resource"]+":"+(long)payload["mutation_index"];
                    Check(resourceKeys.Add(key),"Resource transaction is observed once");
                    Check(Math.Abs((double)payload["balance_after"]-(double)payload["balance_before"]-(double)payload["delta"])<.00001,"Resource delta matches committed before/after");
                }
                if(name=="reward_claim_result")
                {
                    Check(payload["cash_amount"]==null&&payload["diamond_amount"]==null,"Claim behavior has no duplicate resource amounts");
                    if((string)payload["result"]=="granted")rewardedGrants++;else rewardedFailures++;
                }
                if(name=="withdraw_submit")submits++;
                if(name=="withdraw_submit_result"){results++;Check((string)payload["result"]=="not_submitted"&&!(bool)payload["request_sent"],"Early withdrawal stays a local record without payment");}
                Check(payload["Name"]==null&&payload["Email"]==null&&payload["Number"]==null,"Account input is absent from analytics payload");
            }
            Check(counts.ContainsKey("popup_show")&&counts.ContainsKey("popup_action")&&counts.ContainsKey("popup_close"),"Actual prefab Buttons and panel visibility emit popup events");
            Check(counts.ContainsKey("withdraw_account_result")&&submits==1&&results==1,"Actual local verification emits one submit/result pair");
            Check(rewardedGrants==4&&rewardedFailures==1,"Actual early, bonus retry and normal no-fill claims have one result each");
            File.WriteAllText(Output+"/events.json",new JObject{{"passed",true},{"counts",JObject.FromObject(counts)},{"events",AnalyticsEvents}}.ToString());
            LocalAnalytics.EventObserved-=RecordAnalytics;
        }
        private static bool SuccessReady() => local.SuccessPanel != null && !local.SuccessPanel.Closing &&
            Vector3.Distance(local.SuccessPanel.Title.localScale, Vector3.one) < .001f;
        private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < .001f;
        private static void Next(int value, double delay = .7)
        {
            phase = value;
            readyAt = Math.Max(EditorApplication.timeSinceStartup, deferredUiAction != null ? uiMutationAt : 0) + delay;
        }
        private static void Reload(int next) { ClearReferences(); SceneManager.LoadScene("LuoSiSortGame"); Next(next, .2); }
        private static bool ActivationComplete() => (bool?)game.User.LocalWithdrawalData?["earningFlow"]?["activationComplete"] == true;
        private static void SeedPending(int nextLevel, float amount)
        {
            game.User.Level = nextLevel; game.User.LevelSeed = 0; game.User.LevelInfo = string.Empty;
            game.User.LocalPendingReward = new JObject { ["country"] = "US", ["amount"] = amount, ["diamonds"] = 0, ["nextLevel"] = nextLevel };
            audio.UserState.Store.SaveData(false, null);
        }

        private static void ObserveActivation()
        {
            var panel = local.EarningSequencePanel;
            if (panel == null) return;
            int step = LocalEarningFlowPolicy.GetActivationStep(game.User);
            // A newly-created bonus page may still be presenting its rows. Capture
            // the completed production presentation, not the first transition frame.
            if (step == 2 && (!panel.BonusesVisible || !panel.BonusPresentationComplete)) return;
            if (ActivationSteps.Add(step))
            {
                if (step == 2)
                {
                    Check(panel.BonusRows.Length == 5 && panel.BonusCaptions.Length == 5,
                        "Bonus activation contains all five configured benefits");
                    for (int i = 0; i < panel.BonusRows.Length; i++)
                        Check(panel.BonusRows[i].gameObject.activeInHierarchy && panel.BonusRows[i].alpha >= .99f &&
                            !string.IsNullOrWhiteSpace(panel.BonusCaptions[i].text),
                            "Completed activation row is visible and populated: " + (i + 1));
                }
                Check(panel.GetComponentsInChildren<Button>(true).Length == 0, "Activation stage " + step + " has no confirmation Button");
                Capture("activation-" + step, panel.gameObject);
            }
            if (!panel.ReceiptVisible) return;
            Check(panel.ReceiptAmount.text == "$500", "Automatic receipt displays the configured 500 amount", false);
            if (!panel.Collected)
            {
                Check(game.User.Gold == 0, "Receipt remains visible before the one-second collect callback", false);
                if (!receiptBefore) { receiptBefore = true; Checks.Add("500 receipt is visible while wallet is still zero"); }
            }
            else
            {
                Check(Near(game.User.Gold, 500), "Receipt callback sets balance to 500 while flying icons remain visible", false);
                if (!receiptAfter) { receiptAfter = true; Capture("activation-3-collected", panel.gameObject); }
            }
        }

        private static void CheckRecord(int wantedStage, bool pending, float amount)
        {
            LocalEarningRecord found = null;
            foreach (var row in LocalEarningFlowPolicy.GetRecords(game.User)) if (row.Stage == wantedStage) found = row;
            Check(found != null && found.Archived && found.Pending == pending && Near(found.Amount, amount),
                "Record " + wantedStage + " retains its exact earned amount, archive flag and pending=" + pending);
            Check(found.SubmittedUtcSeconds > 0 == pending, "Record submission time agrees with actual local submission state");
        }

        private static void CheckNoPaidData()
        {
            var rows = (JArray)game.User.LocalWithdrawalData?["earningFlow"]?["records"];
            Check(rows != null, "Local earning records are present");
            foreach (JObject row in rows)
                Check(row["paid"] == null && row["success"] == null && row["paymentSuccess"] == null,
                    "Local records do not contain an invented payment-success field");
        }

        private static void CheckHistory()
        {
            var history = local.CashHistoryPanel;
            Check(history != null && history.Rows.Count == 3, "History contains both early records and the current cash balance");
            Check(history.Rows[0].Stage == 1 && history.Rows[0].ActionButton.interactable &&
                history.Rows[1].Stage == 2 && !history.Rows[1].ActionButton.interactable &&
                history.Rows[1].ActionLabel.text.Contains("Pending") && history.Rows[2].Stage == 3 &&
                history.Rows[2].AmountLabel.text == "$500", "History preserves unsubmitted, pending, and current-balance row states");
            CheckUI(history.gameObject);
        }

        private static void CheckUI(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var label in root.GetComponentsInChildren<TMP_Text>())
            {
                if (!label.isActiveAndEnabled || string.IsNullOrEmpty(label.text) || label.canvasRenderer.GetAlpha() < .01f) continue;
                label.ForceMeshUpdate();
                Check(!label.isTextOverflowing, "Text fits its prefab rectangle: " + label.name);
                Rect bounds = Bounds(label.rectTransform);
                Check(bounds.xMin >= -1 && bounds.yMin >= -1 && bounds.xMax <= Screen.width + 1 && bounds.yMax <= Screen.height + 1,
                    "Text stays inside the viewport: " + label.name);
            }
        }

        private static Rect Bounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = min;
            for (int i = 1; i < 4; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void Click(Button button)
        {
            QueueUiAction(() => ClickNow(button));
        }

        private static void QueueUiAction(Action action)
        {
            if (deferredUiAction != null) throw new InvalidOperationException("A screenshot is already awaiting its next UI action.");
            if (EditorApplication.timeSinceStartup < uiMutationAt || Time.frameCount <= captureFrame)
                deferredUiAction = action;
            else action();
        }

        private static void ClickNow(Button button)
        {
            Check(button != null && button.gameObject.activeInHierarchy && button.IsInteractable(), "Live Button is interactable", false);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { position = Bounds((RectTransform)button.transform).center, button = PointerEventData.InputButton.Left };
            Hits.Clear(); EventSystem.current.RaycastAll(pointer, Hits);
            var target = Hits.Count == 0 ? null : ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target == button.gameObject, "Actual Unity raycast reaches " + button.name);
            Check(ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler), "Standard Button pointer click delivered", false);
        }

        private static void Capture(string name, GameObject panel)
        {
            if (panel != null) CheckUI(panel);
            string file = Path.GetFullPath(Output + "/" + name + ".png");
            Captures.Add(new JObject { ["image"] = file, ["phase"] = phase, ["cash"] = game.User.Gold,
                ["width"] = Screen.width, ["height"] = Screen.height, ["fixture"] = "Isolated acceptance data; not a payment or device capture" });
            ScreenCapture.CaptureScreenshot(file);
            // CaptureScreenshot is written at the end of the rendered frame.
            // Keep the exact UI and viewport alive before any click or resize.
            uiMutationAt = EditorApplication.timeSinceStartup + .2;
            captureFrame = Time.frameCount;
        }

        private static void Check(bool condition, string message, bool record = true)
        {
            if (!condition) throw new InvalidOperationException(message);
            if (record) Checks.Add(message);
        }
        private static void Fail(Exception ex) { error = ex.ToString(); Debug.LogException(ex); Finish(1); }

        private static void Finish(int result)
        {
            if (SessionState.GetBool(Key + ".Finish", false)) return;
            if (result == 0)
                foreach (JObject capture in Captures)
                {
                    string file = (string)capture["image"];
                    if (!File.Exists(file) || File.GetLastWriteTimeUtc(file) < began.AddSeconds(-2))
                    { result = 1; error = "Missing fresh screenshot: " + file; break; }
                }
            SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true);
            deferredUiAction = null;
            SessionState.SetInt(Key + ".Result", result);
            SessionState.SetString(Key + ".Report", new JObject { ["fixture"] = Fixture, ["phase"] = phase,
                ["checks"] = Checks, ["captures"] = Captures, ["error"] = error,
                ["verification"] = "Unity Editor Play Mode, production UI and scene, isolated saves, actual Button pointer events; explicit success/failure ad callbacks, no payment service" }.ToString());
            EditorApplication.update -= Tick; SceneManager.sceneLoaded -= ConfigureCountry;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
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
                    existed && PlayerPrefs.GetString(OriginalUserStore.Key) != saved) throw new InvalidOperationException("Original user preference restoration failed.");
                report["originalPreferencesRestored"] = true;
            }
            catch (Exception ex) { Debug.LogException(ex); report["restoreError"] = ex.ToString(); result = 1; }
            report["status"] = result == 0 ? "PASS" : "FAIL"; report["utc"] = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output); File.WriteAllText(Output + "/report.json", report.ToString());
            SessionState.SetBool(Key + ".Finish", false);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            var analyticsSettings=Resources.Load<LocalAnalyticsSettings>("Configuration/LocalAnalytics");analyticsSettings.IsTest=SessionState.GetBool(Key+".OriginalIsTest",false);EditorUtility.SetDirty(analyticsSettings);AssetDatabase.SaveAssets();
            Debug.Log("LOCAL_EARNING_FLOW_PLAY_" + (result == 0 ? "PASS" : "FAIL"));
            LocalAnalytics.EventObserved-=RecordAnalytics;
        }
    }
}


