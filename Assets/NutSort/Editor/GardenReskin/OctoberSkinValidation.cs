using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
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
    // Editor acceptance driver only. Instantiates existing runtime Prefabs and binds their
    // normal public presentation APIs. No generated UI, reflection or runtime fallback.
    [InitializeOnLoad]
    public static class OctoberSkinValidation
    {
        const string Key = "NutSort.OctoberSkinValidation";
        const string Root = "Docs/GardenReskin/implementation-20261008";
        static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        static readonly JArray Results = new JArray();
        static JArray pages;
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static Transform canvas;
        static GameObject subject;
        static Action afterCapture;
        static string page, runtimeError, folder;
        static int index, phase, width, height, nextFrame, clicks;
        static float ready;
        static double deadline;
        static bool finishing;
        static DateTime begun;

        static OctoberSkinValidation()
        {
            if (!SessionState.GetBool(Key, false)) return;
            pages = (JArray)JObject.Parse(File.ReadAllText("Docs/GardenReskin/regenerated-20261008/comparison-manifest.json"))["pages"];
            string filter = SessionState.GetString(Key + ".Filter", "");
            if (filter.Length > 0)
            {
                var selected = new JArray();
                foreach (var p in pages) if (Array.IndexOf(filter.Split(','), (string)p["id"]) >= 0) selected.Add(p.DeepClone());
                pages = selected;
            }
            folder = Root + (SessionState.GetBool(Key + ".Wide", false) ? "/runtime-wide" : "/runtime");
            Directory.CreateDirectory(folder);
            begun = DateTime.UtcNow;
            deadline = EditorApplication.timeSinceStartup + 480;
            Application.logMessageReceived += Error;
            EditorApplication.update += Tick;
        }

        public static void RunAll() => Run("", false);
        public static void RunCore() => Run("main,cash,diamond,account,refresh", false);
        public static void RunCoreWide() => Run("main,cash,diamond,account,refresh", true);
        public static void RunAuxiliary() => Run("account_confirmation,channel_contact,withdrawal_level,withdrawal_steps,withdrawal_verification,withdrawal_loading,withdrawal_guide,target_complete", false);
        public static void RunSecondary() => Run("startup_loading,tutorial,success,failure,replay,undo,add_screw,reward,unlock,message", false);

        static void Run(string filter, bool wide)
        {
            try
            {
                ValidatePoolReuseAndShutdown();
                OriginalPreferenceFixture.Begin(Fixture("main"));
                SessionState.SetString(Key + ".Filter", filter);
                SessionState.SetBool(Key + ".Wide", wide);
                SessionState.SetBool(Key, true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1280, "October skin actual runtime");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static void ValidatePoolReuseAndShutdown()
        {
            var owner = new GameObject("October pool acceptance");
            try
            {
                var pool = owner.AddComponent<OriginalPrefabPool>();
                var activeParent = new GameObject("Rented visual parent");
                activeParent.transform.SetParent(owner.transform, false);
                var first = pool.Rent("Game/Nuts/Nut_1", activeParent.transform);
                pool.Return(first);
                Check(!first.activeSelf && first.transform.parent == owner.transform, "Normal pool return deactivates and reparents");
                var reused = pool.Rent("Game/Nuts/Nut_1", activeParent.transform);
                Check(ReferenceEquals(first, reused) && reused.activeSelf, "Normal pool rent reuses the same instance");
                pool.BeginShutdown();
                pool.Return(reused);
                Check(!reused.activeSelf && reused.transform.parent == activeParent.transform, "Shutdown return preserves the existing hierarchy");
                bool rejected = false;
                try { pool.Rent("Game/Nuts/Nut_1", activeParent.transform); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "No new rent after pool shutdown");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
            Debug.Log("OCTOBER_POOL_REUSE_SHUTDOWN_PASS normal reuse; teardown return without reparent; shutdown rent rejected.");
        }

        static string Fixture(string id)
        {
            int level = id == "tutorial" ? 1 : id == "main" ? 5 : 4;
            return "{\"Level\":" + level + ",\"LevelSeed\":" + (id == "tutorial" ? "1" : "0") + ",\"Gold\":" + (id == "main" ? "662.66" : "0") +
                ",\"Coin\":" + (id == "main" ? "1932.58" : "0") +
                ",\"ExchangeCount\":0,\"RevokeCount\":" + (id == "main" ? "5" : "0") + ",\"AddScrewCount\":0,\"IsCompleteRecordGuide\":true,\"NewGameplayUnlockIndex\":1}";
        }

        static void Tick()
        {
            if (finishing || !EditorApplication.isPlaying) return;
            try
            {
                Check(EditorApplication.timeSinceStartup < deadline, "Timeout: " + page + " phase " + phase);
                if (phase == 0)
                {
                    if (index >= pages.Count) { Finish(0); return; }
                    page = (string)pages[index]["id"];
                    var size = (JArray)pages[index]["sourceSize"];
                    width = 720;
                    height = SessionState.GetBool(Key + ".Wide", false) ? 1280 : Mathf.RoundToInt(720f * (int)size[1] / (int)size[0]);
                    PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "October " + page);
                    PlayerPrefs.SetString(OriginalUserStore.Key, Fixture(page)); PlayerPrefs.Save();
                    // Deliberately unload the populated scene without pre-clearing
                    // its pool. This exercises the same teardown used on devices.
                    game = null; startup = null; subject = null; afterCapture = null;
                    SceneManager.LoadScene("LuoSiSortGame");
                    phase = 1; nextFrame = Time.frameCount + 2; return;
                }
                if (Time.frameCount < nextFrame) return;
                if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if (game == null || startup == null || startup.LocalGameplay == null || game.Level == null ||
                    !game.Level.AreNutsInitialized || game.IsRestarting || !game.IsInitDone || game.InputBlocked || startup.Replay.IsClickMasked) return;
                if (phase == 1)
                {
                    canvas = startup.MainLevel.transform.parent;
                    UnityEngine.Object.FindObjectOfType<OriginalUserSession>().ConfigureRewardCountry(page == "diamond" ? "BR" : "US");
                    startup.LocalGameplay.RefreshRewardsTop();
                    ready = Time.time + .9f; phase = 2; return;
                }
                if (Time.time < ready) return;
                if (phase == 2)
                {
                    ShowPage(); phase = 3; nextFrame = Time.frameCount + 2; return;
                }
                if (phase == 3)
                {
                    Check(subject != null && subject.activeInHierarchy, "Live page exists: " + page);
                    Check(Screen.width == width && Screen.height == height, "Actual render resolution");
                    Canvas.ForceUpdateCanvases();
                    var report = Inspect(subject);
                    report["id"] = page; report["width"] = width; report["height"] = height;
                    report["kind"] = "Unity Editor Play Mode screenshot; live Prefab components; explicit fixture data";
                    report["fixture"] = Fixture(page);
                    report["note"] = "Dynamic fixture values and background game state can differ from the reference; no payment or production ad request is sent.";
                    Results.Add(report);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(folder + "/" + page + ".png"));
                    Debug.Log("OCTOBER_SKIN_CAPTURE " + page + " " + width + "x" + height);
                    phase = 4; ready = Time.time + .35f; nextFrame = Time.frameCount + 2; return;
                }
                Check(File.Exists(folder + "/" + page + ".png") && File.GetLastWriteTimeUtc(folder + "/" + page + ".png") > begun.AddSeconds(-2), "Fresh screenshot completed");
                afterCapture?.Invoke();
                index++; phase = 0;
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }

        static T Make<T>(string prefab) where T : Component =>
            UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/Panels/" + prefab), canvas, false).GetComponent<T>();
        static string Money(float v) => new OriginalGoldFormatter(() => "en-US").Format(v);
        static void Nothing() { }
        static void Sound(string value) { }
        static bool Allowed() => true;

        static void ShowPage()
        {
            var local = startup.LocalGameplay;
            ready = Time.time + .8f;
            switch (page)
            {
                case "main": subject = canvas.gameObject; break;
                case "tutorial":
                    subject = local.TeachingPanel.gameObject;
                    Check(local.TeachingPanel != null, "Normal startup teaching route"); break;
                case "cash":
                    Click(local.RewardsTop.Gold.Click); subject = local.WithdrawalPanel.gameObject;
                    afterCapture = () => { StatsAreaValidation.Check(local.WithdrawalPanel.PlayerParent.GetComponentInChildren<OriginalPlayerInfo>()); Click(local.WithdrawalPanel.CloseButton); };
                    break;
                case "diamond":
                    Click(local.RewardsTop.Coin.Click); subject = local.DiamondWithdrawalPanel.gameObject;
                    afterCapture = () => { Check(local.DiamondWithdrawalPanel.Options.Length == 4, "Four unchanged amount Buttons"); Click(local.DiamondWithdrawalPanel.Options[3]); };
                    break;
                case "account":
                    var account = new OriginalWithdrawalUserInfoHost(canvas, "Prefabs/Panels/TXUserInfoPanel", game.User, game.Tables, "en", () => "US", () => "1", Allowed, Sound,
                        id => { }, (id, args) => { }, Nothing, v => { }, Nothing, Nothing, game.ScheduleDelay, Nothing).Show(new object[] { 1 });
                    subject = account.gameObject; break;
                case "account_confirmation":
                    game.User.UserLssInfo = JObject.Parse("{\"Name\":\"Fixture User\",\"Email\":\"fixture@example.test\",\"GetType\":2,\"OtherInfo\":\"\"}");
                    subject = new OriginalWithdrawalConfirmationHost(canvas, "Prefabs/Panels/TXUserInfoSurePanel", game.User, game.Tables, "en", () => "US", () => "1", Allowed, Sound,
                        (id, args) => { }, (level, args) => { }, v => { }, Nothing, game.ScheduleDelay, Nothing).Show(new object[] { 1 }).gameObject; break;
                case "channel_contact":
                    var channel = new OriginalWithdrawalChannelHost(canvas, "Prefabs/Panels/TXChannelPanel", game.User, game.Tables, "en", Allowed, Sound,
                        id => { }, (id, args) => { }, id => { }, Nothing, game.ScheduleDelay, Nothing).Show(new object[] { 1 });
                    channel.Info = "Fixture channel\nfixture@example.test"; subject = channel.gameObject; break;
                case "withdrawal_level":
                    game.User.Level1Gold = 5;
                    subject = new OriginalWithdrawalLevelHost(canvas, "Prefabs/Panels/TXLevelPanel", game.User, game.Tables, "en", Money, Allowed, Sound,
                        new OriginalWithdrawalLevelPanelValidation.Services(), (level, cb) => { }, Nothing, v => null, Nothing, game.ScheduleDelay, Nothing).Show(new object[] { 1 }).gameObject;
                    ready = Time.time + 5.2f; break;
                case "withdrawal_steps":
                    StageConfig();
                    game.User.UserLssInfo = new JObject { { "TX0IsOpened", true } };
                    var time = new OriginalWithdrawalTime(() => "en-US");
                    var steps = new OriginalWithdrawalStageZeroHost(canvas, "Prefabs/Panels/TXProgress0Panel", game.User, game.Tables, "en",
                        OriginalWithdrawalTime.LocalSeconds, Money, time.Format, (a, b) => a, () => false, () => 7,
                        Nothing, v => { }, Allowed, Sound, (id, args) => { }, Nothing, game.ScheduleDelay, Nothing).Show(new object[] { true });
                    steps.PlayProgress(); subject = steps.gameObject; ready = Time.time + 5.2f; break;
                case "withdrawal_verification":
                    StageConfig();
                    subject = new OriginalWithdrawalTooFastHost(canvas, "Prefabs/Panels/TXTooFastHintPanel", game.User, game.Tables, "en", Allowed, Sound,
                        () => 7, () => Nothing, Nothing, game.ScheduleDelay, Nothing).Show(Array.Empty<object>()).gameObject; break;
                case "withdrawal_loading":
                    var loading = Make<OriginalWithdrawalLoadingPanel>("TXGuideLoadingPanel");
                    loading.Bind(game.Tables, "en", Nothing, Nothing, Nothing, game.ScheduleDelay, Nothing);
                    loading.Init(); loading.Refresh(); subject = loading.gameObject; ready = Time.time + 1.5f; break;
                case "withdrawal_guide":
                    subject = new OriginalRecordGuidePanelHost(canvas, "Prefabs/Panels/TXRecordGuidePanel", game.Tables, "en", "US", Allowed, Nothing, Nothing,
                        new OriginalPanelActionQueue(), game.ScheduleDelay, Nothing).Show().gameObject;
                    ready = Time.time + 3; break;
                case "target_complete":
                    game.User.Level1Gold = 12;
                    subject = new OriginalGuideTargetPanelHost(canvas, "Prefabs/Panels/TXGuideTargetCompletePanel", game.User, game.Tables, "en", Allowed, Sound, () => "US", Money,
                        (a, b) => { }, new OriginalPanelActionQueue(), game.ScheduleDelay, Nothing).Show(1).gameObject; break;
                case "startup_loading":
                    var boot = Make<OriginalBootLoadingPanel>("BootLoadingPanel"); boot.SetState(true); subject = boot.gameObject; ready = Time.time + 2.3f; break;
                case "refresh":
                case "undo":
                case "add_screw":
                    var tool = Make<LocalToolOfferPanel>("ItemGetPanel");
                    tool.Init(page == "undo" ? 2 : page == "refresh" ? 3 : 4, game.Tables, "en", Allowed, Nothing, Nothing);
                    subject = tool.gameObject; break;
                case "replay":
                    startup.Replay.OpenPanel(); subject = startup.Replay.Panel.gameObject;
                    afterCapture = () => Click(startup.Replay.Panel.ContinueButton); break;
                case "message":
                    var message = Make<OriginalMessagePanel>("MessagePanel"); message.Bind(id => game.Tables.Text.GetText(id, "en"));
                    message.Init(); message.Show(120, Nothing); subject = message.gameObject;
                    afterCapture = () => { Click(message.ConfirmButton); Check(!message.gameObject.activeSelf, "Message Button closes live panel"); }; break;
                case "unlock":
                    subject = new OriginalGameplayUnlockPanelHost(canvas, "Prefabs/Panels/UnlockGameplayPanel", game.User, game.Tables, "en", Allowed, Sound,
                        cb => cb(), new OriginalPanelActionQueue(), game.ScheduleDelay, Nothing).Show(0, false).gameObject; break;
                case "failure": ShowFailure(); break;
                case "success": ShowSuccess(); break;
                case "reward": ShowReward(); ready = Time.time + .2f; break;
                default: throw new InvalidOperationException("Unmapped page: " + page);
            }
        }

        static void StageConfig() => game.User.GoldRewardTargetS2CData = new JObject { { "bear_list", new JArray(new JObject(), new JObject(), new JObject { { "Stage2StartShowLevel", 12 } }) } };
        static OriginalRewardItemFactory Factory() => new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/OriginalRewardItem"), new OriginalGoldFormatter(() => "en-US"), () => "US");

        static void ShowFailure()
        {
            var panel = Make<OriginalFailurePanelView>("FailPanel");
            var user = new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            user.GoldRewardTargetS2CData = JObject.Parse("{\"bear_zs_show\":\"1,250\"}");
            user.ServerConfigData = JObject.Parse("{\"LSSLSMAC\":12}");
            panel.Bind(user, game.Tables, "en", Allowed, Sound, cb => cb(), Nothing, Nothing, Nothing);
            panel.BindHide(new OriginalPanelActionQueue(), game.ScheduleDelay);
            panel.Init(); panel.Refresh(); subject = panel.gameObject;
        }
        static void ShowSuccess()
        {
            var panel = Make<OriginalSuccessPanel>("SuccessPanel");
            var claims = new OriginalRewardGetFlow(game.User, () => 1, () => 0, cb => { }, (cb, b) => { }, () => 0, v => { }, more => { });
            panel.Bind(new OriginalSuccessPanelFlow(game.User, () => 0, Nothing, id => { }), claims, Factory(), game.Tables, "en", Allowed, Sound,
                Nothing, cb => cb(), game.ScheduleDelay, new OriginalPanelActionQueue());
            panel.Init(new OriginalItemGetInfo { ItemInfos = new List<OriginalItemInfo> { new OriginalItemInfo { ItemType = 0, Count = 4.98f, MoreCount = 49.8f } } });
            panel.Refresh(); subject = panel.gameObject;
        }
        static void ShowReward()
        {
            var panel = Make<OriginalRewardPanel>("RewardPanel");
            var inventory = new OriginalItemManager(game.User, Nothing, Nothing, (v, a, b) => { }, (v, a) => { });
            panel.Bind(Factory(), inventory, (type, icon) => { }, game.ScheduleDelay, () => UnityEngine.Object.Destroy(panel.gameObject), new OriginalPanelActionQueue(), Nothing);
            panel.Init(new OriginalItemGetInfo { ItemInfos = new List<OriginalItemInfo> { new OriginalItemInfo { ItemType = 2, Count = 2 }, new OriginalItemInfo { ItemType = 3, Count = 3 } } });
            panel.Refresh(); subject = panel.gameObject;
        }

        static JObject Inspect(GameObject target)
        {
            var texts = new JArray(); var controls = new JArray();
            foreach (var label in target.GetComponentsInChildren<TMP_Text>())
            {
                Vector3 scale = label.rectTransform.lossyScale;
                if (!label.isActiveAndEnabled || !label.gameObject.activeInHierarchy || Mathf.Abs(scale.x * scale.y) < 1e-10f || label.alpha < .01f || label.text.Length == 0) continue;
                label.ForceMeshUpdate();
                Rect rect = label.rectTransform.rect;
                var issue = new JArray(); float minX = float.PositiveInfinity, minY = minX, maxX = float.NegativeInfinity, maxY = maxX;
                for (int n = 0; n < label.textInfo.characterCount; n++)
                {
                    var c = label.textInfo.characterInfo[n]; if (!c.isVisible) continue;
                    minX = Mathf.Min(minX, c.bottomLeft.x); maxX = Mathf.Max(maxX, c.topRight.x);
                    minY = Mathf.Min(minY, c.bottomLeft.y); maxY = Mathf.Max(maxY, c.topRight.y);
                }
                if (label.isTextOverflowing) issue.Add("TMP reports overflow");
                if (minX < rect.xMin - 3 || maxX > rect.xMax + 3 || minY < rect.yMin - 3 || maxY > rect.yMax + 3) issue.Add("Visible glyphs exceed own RectTransform by >3 UI units");
                texts.Add(new JObject { { "path", PathOf(label.transform, target.transform) }, { "text", label.text }, { "fontSize", label.fontSize }, { "rect", rect.ToString() }, { "issues", issue } });
            }
            foreach (var button in target.GetComponentsInChildren<Button>())
            {
                Vector3 scale = button.transform.lossyScale;
                if (!button.isActiveAndEnabled || !button.IsInteractable() || Mathf.Abs(scale.x * scale.y) < 1e-10f) continue;
                string hit; bool reached = Reach(button, out hit);
                controls.Add(new JObject { { "path", PathOf(button.transform, target.transform) }, { "centerRaycastReachesButton", reached }, { "firstHit", hit } });
                Check(reached, "Visible Button raycast must reach " + PathOf(button.transform, target.transform) + "; first=" + hit);
                Check(button.onClick.GetPersistentEventCount() == 0, "Code-bound Button events: " + button.name);
            }
            // These four representative states intentionally contain no UI Button:
            // both loading pages, the board-only teaching overlay and auto-collection.
            bool noButtonExpected = page == "startup_loading" || page == "withdrawal_loading" || page == "tutorial" || page == "reward";
            if (!noButtonExpected) Check(controls.Count > 0, "At least one visible Button must be measured for " + page);
            foreach (var component in target.GetComponentsInChildren<Component>(true)) Check(component != null, "No missing scripts: " + page);
            return new JObject { { "texts", texts }, { "buttons", controls } };
        }
        static string PathOf(Transform t, Transform root)
        {
            string value = t.name;
            while (t.parent != null && t != root) { t = t.parent; value = t.name + "/" + value; }
            return value;
        }
        static bool Reach(Selectable control, out string hit)
        {
            var ui = control.GetComponentInParent<Canvas>(); var rect = (RectTransform)control.transform;
            var point = RectTransformUtility.WorldToScreenPoint(ui.renderMode == RenderMode.ScreenSpaceOverlay ? null : ui.worldCamera, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left, clickCount = 1 };
            Hits.Clear(); EventSystem.current.RaycastAll(data, Hits); hit = Hits.Count > 0 ? Hits[0].gameObject.name : "none";
            return Hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject) == control.gameObject;
        }
        static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases(); string hit; Check(Reach(button, out hit), "Button raycast reaches " + button.name + ", first=" + hit);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1 };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); clicks++;
        }
        static void Error(string value, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception) runtimeError = value; }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Finish(int code)
        {
            finishing = true; EditorApplication.update -= Tick; SessionState.SetBool(Key, false);
            try
            {
                string backup = "Library/ValidationBackups/GameplayUserLocalData.bin";
                bool hadBackup = File.Exists(backup), existed = false;
                string value = null;
                if (hadBackup)
                    using (var reader = new BinaryReader(File.OpenRead(backup)))
                    { existed = reader.ReadBoolean(); value = reader.ReadString(); }
                OriginalPreferenceFixture.Restore();
                if (code == 0) Check(hadBackup, "Original preference backup existed");
                if (hadBackup) Check(!File.Exists(backup) && PlayerPrefs.HasKey(OriginalUserStore.Key) == existed &&
                    (!existed || PlayerPrefs.GetString(OriginalUserStore.Key) == value), "Original PlayerPrefs value and presence restored exactly");
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (runtimeError != null) code = 1;
            Application.logMessageReceived -= Error;
            if (folder != null) File.WriteAllText(folder + "/capture-report.json", new JObject { { "status", code == 0 ? "CAPTURE_COMPLETE" : "FAILED" }, { "utc", DateTime.UtcNow.ToString("o") }, { "buttonClickDispatches", clicks }, { "runtimeError", runtimeError }, { "pages", Results }, { "note", "Bounds issues are reported for review, not suppressed. Dedicated ApprovedSkinPlayValidation covers real modal/input/tier/ad-callback behavior." } }.ToString());
            if (code == 0) Debug.Log("OCTOBER_SKIN_CAPTURE_PASS " + Results.Count + " current-runtime screenshots; original PlayerPrefs restored; text bounds and raycast report written.");
            EditorApplication.Exit(code);
        }
    }
}
