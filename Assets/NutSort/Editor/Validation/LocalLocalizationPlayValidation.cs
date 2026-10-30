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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NutSort.Validation
{
    // Renders the actual production prefabs against the production camera stack.
    // Only the saved fixture is isolated; no product objects or styles are fabricated.
    [InitializeOnLoad]
    public static class LocalLocalizationPlayValidation
    {
        const string Key = "NutSort.LocalLocalizationPlayValidation";
        static bool JapaneseOnly => SessionState.GetBool(Key + ".JapaneseOnly", false);
        static bool HomeOnly => SessionState.GetBool(Key + ".HomeOnly", false);
        static bool RewardsOnly => SessionState.GetBool(Key + ".RewardsOnly", false);
        static string Output => "Docs/Localization/20261013/" + (RewardsOnly ? "validation-reward-art" : JapaneseOnly ? "validation-ja" : HomeOnly ? "validation-home" : "validation");
        static int ExpectedCaptures => (JapaneseOnly ? 1 : Languages.Length) * 2 * CurrentPages.Length;
        const string Fixture = "{\"Level\":9,\"LevelSeed\":0,\"Gold\":512.34,\"Coin\":486.73,\"RevokeCount\":5,\"ExchangeCount\":2,\"AddScrewCount\":3," +
            "\"UserName\":\"Player_C3f1\",\"UserId\":\"90703fb2c51f383a\",\"UserLevel\":1,\"IsAudio\":true,\"IsVibrate\":true," +
            "\"NewGameplayUnlockIndex\":4,\"IsCompleteRecordGuide\":true,\"IsCompleteCoinNewPeopleReward\":true,\"IsShowCoin\":true," +
            "\"IsCompleteShowCoinRewardHint\":true,\"IsCompleteExtraGoldGuide\":true,\"Level1Gold\":0.4,\"Level2Gold\":0.6," +
            "\"IsGoldReduceLevel1\":true,\"IsGoldReduceLevel2\":true,\"LocalWithdrawalData\":{\"tutorialBalanceApplied\":true," +
            "\"tutorialBalanceNoticeAcknowledged\":true,\"earningFlow\":{\"activationComplete\":true,\"activationStep\":6}}}";
        static readonly string[] Languages = { "en", "ja", "pt", "de", "fr", "id", "ru", "es" };
        static readonly string[] Countries = { "US", "JP", "BR", "DE", "FR", "ID", "RU", "ES" };
        static readonly string[] Pages = { "home", "loading", "settings", "user-level", "cash", "diamond", "account", "assessment", "activating", "activated", "summary", "diamond-notice", "bonuses", "history", "cash-conditions", "diamond-conditions", "undo", "shuffle", "bolt", "settlement" };
        static readonly string[] HomePages = { "home", "loading" };
        static readonly string[] RewardPages = { "settlement", "settlement-no-ad", "lucky", "undo", "shuffle", "bolt" };
        static string[] CurrentPages => RewardsOnly ? RewardPages : HomeOnly ? HomePages : Pages;
        static readonly JArray Results = new JArray(), Issues = new JArray(), HomeChecks = new JArray(), HiddenBanners = new JArray();
        static readonly JArray ResourceChecks = new JArray();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static OriginalAudioPlayer audio;
        static GameObject current;
        static int languageIndex, viewport, page, phase;
        static double readyAt, deadline;
        static JObject earning, withdrawal;
        static string fatal;
        static string pendingCapture;
        static DateTime captureRequested;
        static int captureFrame;
        static bool borrowed;
        static LocalLocalizationPlayValidation()
        {
            if (SessionState.GetBool(Key + ".Finish", false)) EditorApplication.delayCall += Complete;
            else if (SessionState.GetBool(Key, false)) StartPolling();
        }
        public static void Run()
        {
            SessionState.SetBool(Key + ".RewardsOnly", false);
            SessionState.SetBool(Key + ".JapaneseOnly", false); SessionState.SetBool(Key + ".HomeOnly", false); RunInternal();
        }
        public static void RunJapanese()
        {
            SessionState.SetBool(Key + ".RewardsOnly", false);
            SessionState.SetBool(Key + ".JapaneseOnly", true); SessionState.SetBool(Key + ".HomeOnly", false); RunInternal();
        }
        public static void RunHomeAndLoading()
        {
            SessionState.SetBool(Key + ".RewardsOnly", false);
            SessionState.SetBool(Key + ".JapaneseOnly", false); SessionState.SetBool(Key + ".HomeOnly", true); RunInternal();
        }
        public static void RunRewards()
        {
            SessionState.SetBool(Key + ".JapaneseOnly", false); SessionState.SetBool(Key + ".HomeOnly", false);
            SessionState.SetBool(Key + ".RewardsOnly", true); RunInternal();
        }
        static void RunInternal()
        {
            try
            {
                Directory.CreateDirectory(Output);
                OriginalPreferenceFixture.Begin(Fixture);
                SessionState.SetBool(Key, true); SessionState.SetBool(Key + ".Finish", false);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720, 1600, "Localized production UI");
                StartPolling(); EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Fail(e); }
        }
        static void StartPolling()
        {
            viewport = page = phase = 0; languageIndex = JapaneseOnly ? 1 : 0; readyAt = 0;
            deadline = EditorApplication.timeSinceStartup + 1200;
            Results.Clear(); Issues.Clear(); HomeChecks.Clear(); HiddenBanners.Clear(); ResourceChecks.Clear(); fatal = null; borrowed = false;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            SceneManager.sceneLoaded -= Configure; SceneManager.sceneLoaded += Configure;
        }
        static void Configure(Scene scene, LoadSceneMode mode)
        {
            var session = UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
            if (session != null) session.ConfigureRewardCountry(Countries[languageIndex]);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Localization matrix timed out.");
                if (EditorApplication.timeSinceStartup < readyAt) return;
                if (game == null) game = UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if (startup == null) startup = UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if (audio == null) audio = UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
                if (game == null || startup == null || audio == null || startup.LocalGameplay == null || !game.IsInitDone || startup.Loading.IsVisible || game.IsRestarting) return;
                if (phase == 0)
                {
                    if (ResourceChecks.Count == 0) ValidateCurrencyResources();
                    // Explicit catalog selection is a presentation matrix, not a device-country test.
                    LocalUiLocalization.Configure(Languages[languageIndex], game.Tables.Text);
                    earning = JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningFlow").text);
                    withdrawal = JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalWithdrawal").text);
                    if (CurrentPages[page] == "settings")
                    {
                        Canvas.ForceUpdateCanvases();
                        Validate(startup.LocalGameplay.transform.parent.gameObject, Languages[languageIndex] + "-" + (viewport == 0 ? "tall" : "short") + "-home", "", false);
                    }
                    borrowed = false; current = Show(CurrentPages[page]);
                    phase = 1; readyAt = EditorApplication.timeSinceStartup + (CurrentPages[page] == "bonuses" ? 2.2 : .65);
                    return;
                }
                if (phase == 1)
                {
                    Canvas.ForceUpdateCanvases();
                    foreach (var text in current.GetComponentsInChildren<TMP_Text>(true)) if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
                    string id = Languages[languageIndex] + "-" + (viewport == 0 ? "tall-" : "short-") + CurrentPages[page];
                    pendingCapture = Output + "/" + id + ".png";
                    captureRequested = DateTime.UtcNow; captureFrame = Time.frameCount;
                    ScreenCapture.CaptureScreenshot(pendingCapture);
                    // Measure the exact requested GameView frame; retain the instance
                    // until the asynchronous screenshot has actually been written.
                    Validate(current, id, pendingCapture); phase = 2; return;
                }
                if (Time.frameCount <= captureFrame || !File.Exists(pendingCapture) ||
                    File.GetLastWriteTimeUtc(pendingCapture) < captureRequested.AddMilliseconds(-100)) return;
                if (new FileInfo(pendingCapture).Length < 1000) throw new InvalidDataException("Empty GameView screenshot: " + pendingCapture);
                if (!borrowed) UnityEngine.Object.Destroy(current); current = null;
                page++; phase = 0; readyAt = EditorApplication.timeSinceStartup + .05;
                if (page < CurrentPages.Length) return;
                page = 0; viewport++;
                if (viewport > 1) { viewport = 0; languageIndex++; }
                Save();
                if (languageIndex >= (JapaneseOnly ? 2 : Languages.Length)) { Finish(); return; }
                PlayModeWindow.SetCustomRenderingResolution(720, viewport == 0 ? 1600u : 1280u, "Localized production UI");
                game = null; startup = null; audio = null;
                PlayerPrefs.SetString(OriginalUserStore.Key, Fixture); PlayerPrefs.Save();
                SceneManager.LoadScene("LuoSiSortGame"); readyAt = EditorApplication.timeSinceStartup + 1;
            }
            catch (Exception e) { Fail(e); }
        }
        static GameObject Load(string name)
        {
            var prefab = Resources.Load<GameObject>("prefabs/panels/" + name);
            if (prefab == null) throw new FileNotFoundException(name);
            return UnityEngine.Object.Instantiate(prefab, startup.LocalGameplay.transform.parent, false);
        }
        static string T(string value) => LocalUiLocalization.Text(value);
        static GameObject Show(string name)
        {
            string language = Languages[languageIndex], country = audio.UserState.DisplayCountry(game.Tables);
            string culture = game.Tables.Countries.Get(country).CountryLanguageCode;
            var formatter = new OriginalGoldFormatter(() => culture);
            GameObject root;
            switch (name)
            {
                case "home":
                    borrowed = true; return startup.LocalGameplay.transform.parent.gameObject;
                case "loading":
                    root = Load("BootLoadingPanel");
                    root.GetComponent<OriginalBootLoadingPanel>().SetState(true); return root;
                case "settings":
                    root = Load("LocalSettings");
                    root.GetComponent<LocalSettingsPanel>().Bind(audio, () => true, () => { }, () => { }, () => { }, () => { }, () => { }, () => { });
                    var settings = root.GetComponent<LocalSettingsPanel>();
                    bool wasOn = audio.AudioEnabled;
                    settings.SoundButton.onClick.Invoke();
                    if (settings.StateLabel.text != T(wasOn ? "OFF" : "ON")) throw new InvalidOperationException("Localized settings toggle was overwritten.");
                    settings.SoundButton.onClick.Invoke();
                    return root;
                case "user-level":
                    root = Load("LocalUserLevel"); root.GetComponent<LocalUserLevelPanel>().Bind(game.User, () => true, () => { }, () => { }); return root;
                case "cash":
                    root = Load("TXPanel");
                    root.GetComponent<OriginalWithdrawalPanel>().InitLocal(game.Tables, language, country, null, formatter.Format(game.User.Gold),
                        game.Tables.Text.GetText(30, language, 5), game.Tables.Text.GetText(22, language, 5), "4/5", game.Tables.Text.GetText(25, language, 1), .8f,
                        () => true, () => { }, () => { }, _ => { }, stats => stats.InitSimulated(new OriginalSimulatedStatsSnapshot(string.Format(T(stats.SimulatedStatsSettings.TitleFormat), "10/13/2026"), 25553, 8, .4f, false), game.Tables, language, amount => formatter.Format(amount)));
                    root.GetComponent<OriginalWithdrawalPanel>().SetLocalHistoryAction(() => { }, T("View withdrawal history")); return root;
                case "diamond":
                    root = Load("TXCoinPanel"); root.GetComponent<LocalDiamondWithdrawalPanel>().Init(game.User, game.Tables, language, formatter, () => true, () => { }, () => { }, _ => { }, _ => { }); return root;
                case "account":
                    root = Load("TXUserInfoPanel"); var account = root.GetComponent<OriginalWithdrawalUserInfoPanel>();
                    account.Bind(game.User, game.Tables, language, () => country, () => "1", () => true, _ => { }, _ => { }, (a, b) => { }, () => { }, _ => { }, () => { }, () => { }, () => { }, (a, b) => { }, () => { });
                    account.Init(new object[] { 1 }); account.Refresh(); return root;
                case "assessment": case "activating": case "activated": case "summary":
                    root = Load("LocalEarningSequence");
                    LocalEarningMessageStyle style = name == "assessment" ? LocalEarningMessageStyle.Assessment : name == "activating" ? LocalEarningMessageStyle.Activating : name == "activated" ? LocalEarningMessageStyle.Activated : LocalEarningMessageStyle.Summary;
                    root.GetComponent<LocalEarningSequencePanel>().ShowMessage(style, (string)earning[name + "Title"], (string)earning[name + "Body"]); return root;
                case "bonuses":
                    root = Load("LocalEarningSequence"); root.GetComponent<LocalEarningSequencePanel>().ShowBonuses((string)earning["bonusesTitle"], earning["bonuses"].ToObject<string[]>()); return root;
                case "diamond-notice":
                    root = Load("LocalEarningSequence");
                    var rewards = JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalEarningRewards").text);
                    string noticeTitle = T((string)rewards["coinPreviewTitle"]), noticeBody = LocalUiLocalization.Format((string)rewards["coinPreviewBody"], "100");
                    var notice = root.GetComponent<LocalEarningSequencePanel>();
                    notice.ShowMessage(LocalEarningMessageStyle.Summary, noticeTitle, noticeBody);
                    if (notice.MessageTitle.text != noticeTitle || notice.MessageBody.text != noticeBody) throw new InvalidOperationException("Reused message text was overwritten by OnEnable localization.");
                    return root;
                case "history":
                    root = Load("LocalCashHistory"); var data = new LocalCashHistoryPanel.RowData[3];
                    for (int i = 0; i < data.Length; i++) data[i] = new LocalCashHistoryPanel.RowData { Stage = i + 1, Title = LocalUiLocalization.Format("Cash Reward #{0}", i + 1), ActionCaption = T(i == 1 ? "Pending - not paid" : "Withdraw"), Time = i < 2 ? "10/13/2026 10:38 AM" : "", Amount = formatter.Format(i == 0 ? .4f : i == 1 ? .6f : game.User.Gold), Interactable = i != 1 };
                    var history = root.GetComponent<LocalCashHistoryPanel>();
                    history.Init(T("Withdrawal History"), formatter.Format(game.User.Gold), data, _ => { }, () => { });
                    if (history.Rows[1].StatusLabel.text != T("Pending - not paid")) throw new InvalidOperationException("History pending status was overwritten on activation.");
                    return root;
                case "cash-conditions": case "diamond-conditions":
                    root = Load("LocalWithdrawalConditions"); bool diamond = name == "diamond-conditions";
                    root.GetComponent<LocalWithdrawalConditionsPanel>().Init(withdrawal, game.Tables, diamond, formatter, () => LocalWithdrawalPolicy.Evaluate(game.User, game.Tables, diamond, 0), () => true, _ => { }, () => { }); return root;
                case "undo": case "shuffle": case "bolt":
                    root = Load("ItemGetPanel"); root.GetComponent<LocalToolOfferPanel>().Init(name == "undo" ? 2 : name == "shuffle" ? 3 : 4, game.Tables, language, () => true, () => { }, () => { }); return root;
                case "settlement": case "settlement-no-ad": case "lucky":
                    bool lucky = name == "lucky", showAd = name != "settlement-no-ad";
                    root = Load(lucky ? "LocalLuckyReward" : "LocalSuccess");
                    var factory = new OriginalRewardItemFactory(Resources.Load<OriginalRewardItemSettings>("Configuration/" + (lucky ? "LocalLuckyRewardItem" : "OriginalRewardItem")), formatter, () => country);
                    var info = new OriginalItemGetInfo { ItemInfos = new List<OriginalItemInfo> { new OriginalItemInfo { ItemType = 0, Count = 512.34f } } };
                    if (!lucky) info.ItemInfos.Add(new OriginalItemInfo { ItemType = 1, Count = 486.73f });
                    var success = root.GetComponent<OriginalSuccessPanel>();
                    Vector2 authoredPosition = success.MoreLabel.rectTransform.anchoredPosition;
                    // Exercise reuse as well as the first-open state.
                    success.InitSettlement(game.Tables, language, () => true, _ => { }, () => { }, factory, info, _ => { }, true, false, T("Claim"));
                    if (success.MoreLabel.rectTransform.anchoredPosition != Vector2.zero) throw new InvalidOperationException("No-ad caption is not centered.");
                    success.InitSettlement(game.Tables, language, () => true, _ => { }, () => { }, factory, info, _ => { }, true, showAd, showAd ? LocalUiLocalization.Format("Claim {0}X", 10) : T("Claim"));
                    if (showAd && success.MoreLabel.rectTransform.anchoredPosition != authoredPosition) throw new InvalidOperationException("Rewarded caption did not restore its authored icon gutter.");
                    if (lucky) success.SetPresentationTitle(T("Lucky Reward"));
                    return root;
                default: throw new InvalidOperationException(name);
            }
        }
        sealed class TextSample { public TMP_Text Text; public Rect Bounds; public List<Rect> Glyphs = new List<Rect>(); }
        static void ValidateCurrencyResources()
        {
            foreach (var country in game.Tables.Countries.Countries)
            {
                foreach (string config in new[] { "OriginalRewardItem", "LocalLuckyRewardItem" })
                {
                    var settings = Resources.Load<OriginalRewardItemSettings>("Configuration/" + config);
                    foreach (bool large in new[] { false, true })
                    {
                        string path = settings.GetIconPath(0, large, country.Code);
                        var sprite = Resources.Load<Sprite>(path);
                        bool valid = sprite != null && sprite.texture != null && sprite.rect.width > 1 && sprite.rect.height > 1;
                        if (!large) valid &= Resources.Load<Texture2D>(path) != null;
                        ResourceChecks.Add(new JObject { ["country"] = country.Code, ["configuration"] = config, ["large"] = large, ["path"] = path, ["valid"] = valid });
                        if (!valid) throw new InvalidOperationException("Missing currency artwork: " + country.Code + " / " + path);
                    }
                }
            }
        }
        static void Validate(GameObject root, string id, string screenshot, bool capture = true)
        {
            var texts = new JArray(); var samples = new List<TextSample>();
            var images = new JArray();
            foreach (var reward in root.GetComponentsInChildren<OriginalRewardItemView>()) CheckCurrencyImage(reward.Icon, root, id, images);
            foreach (var gold in root.GetComponentsInChildren<OriginalGoldImage>()) CheckCurrencyImage(gold.Icon, root, id, images);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!Visible(label) || string.IsNullOrWhiteSpace(label.text)) continue;
                label.ForceMeshUpdate();
                var sample = Sample(label); if (sample.Glyphs.Count == 0) continue;
                // The original banner parks at its configured off-screen RestPosition
                // until Show starts an entry animation. Do not mistake that deliberate
                // hidden state for a visible UI failure. No other component is exempt.
                var banner = label.GetComponentInParent<OriginalTargetRewardBanner>();
                if (banner != null && !banner.IsAnimating &&
                    !new Rect(0, 0, Screen.width, Screen.height).Overlaps(sample.Bounds))
                {
                    HiddenBanners.Add(new JObject { ["capture"] = id, ["path"] = PathOf(label.transform, root.transform),
                        ["component"] = "OriginalTargetRewardBanner", ["animating"] = false,
                        ["bounds"] = Bounds(sample.Bounds), ["reason"] = "Wholly off-screen at the original banner's configured idle position." });
                    continue;
                }
                samples.Add(sample); var failures = new JArray();
                if (label.isTextTruncated) failures.Add("truncated");
                if (label.isTextOverflowing) failures.Add("overflowing");
                var box = ScreenRect(label.rectTransform, label.GetComponentInParent<Canvas>().worldCamera);
                if (!Contains(box, sample.Bounds, 4)) failures.Add("glyph-outside-text-rect");
                if (!Contains(new Rect(0, 0, Screen.width, Screen.height), sample.Bounds, 2)) failures.Add("glyph-outside-screen");
                for (int i = 0; i < label.textInfo.characterCount; i++)
                {
                    var ch = label.textInfo.characterInfo[i];
                    if (char.IsWhiteSpace(ch.character) || char.IsControl(ch.character)) continue;
                    if (!label.font.HasCharacter(ch.character, true, true)) { failures.Add("missing-glyph-U+" + ((int)ch.character).ToString("X4")); break; }
                }
                var data = new JObject { ["path"] = PathOf(label.transform, root.transform), ["text"] = label.text, ["fontSize"] = label.fontSize, ["font"] = label.font.name, ["bounds"] = Bounds(sample.Bounds), ["failures"] = failures };
                texts.Add(data); if (failures.Count > 0) Issues.Add(new JObject { ["capture"] = id, ["label"] = data.DeepClone() });
                var button = label.GetComponentInParent<Button>();
                if (button != null)
                    foreach (var icon in button.GetComponentsInChildren<Image>())
                    {
                        if (!icon.enabled || icon.color.a < .02f ||
                            !(string.Equals(icon.name, "ad", StringComparison.OrdinalIgnoreCase) || string.Equals(icon.name, "video", StringComparison.OrdinalIgnoreCase))) continue;
                        Rect iconBounds = ScreenRect(icon.rectTransform, icon.GetComponentInParent<Canvas>().worldCamera);
                        foreach (Rect glyph in sample.Glyphs)
                            if (Intersection(glyph, iconBounds) > 4) { Issues.Add(new JObject { ["capture"] = id, ["kind"] = "text-ad-icon-overlap", ["label"] = PathOf(label.transform, root.transform), ["icon"] = PathOf(icon.transform, root.transform) }); break; }
                    }
            }
            for (int a = 0; a < samples.Count; a++) for (int b = a + 1; b < samples.Count; b++)
            {
                if (!samples[a].Bounds.Overlaps(samples[b].Bounds)) continue;
                bool collision = false;
                foreach (var ra in samples[a].Glyphs) { foreach (var rb in samples[b].Glyphs) if (Intersection(ra, rb) > 9) { collision = true; break; } if (collision) break; }
                if (collision) Issues.Add(new JObject { ["capture"] = id, ["kind"] = "text-overlap", ["first"] = PathOf(samples[a].Text.transform, root.transform), ["second"] = PathOf(samples[b].Text.transform, root.transform) });
            }
            (capture ? Results : HomeChecks).Add(new JObject { ["id"] = id, ["language"] = LocalUiLocalization.Language, ["width"] = Screen.width, ["height"] = Screen.height, ["screenshot"] = Path.GetFileName(screenshot), ["texts"] = texts, ["currencyImages"] = images });
            Debug.Log("LOCALIZATION_CAPTURE " + id + " texts=" + texts.Count);
        }
        static void CheckCurrencyImage(Image image, GameObject root, string id, JArray checks)
        {
            bool valid = image != null && image.sprite != null && image.sprite.texture != null;
            string path = image == null ? "unassigned" : PathOf(image.transform, root.transform);
            checks.Add(new JObject { ["path"] = path, ["sprite"] = valid ? image.sprite.name : null, ["valid"] = valid });
            if (!valid) Issues.Add(new JObject { ["capture"] = id, ["kind"] = "missing-currency-image", ["path"] = path });
        }
        static TextSample Sample(TMP_Text label)
        {
            var result = new TextSample { Text = label }; Camera camera = label.GetComponentInParent<Canvas>().worldCamera;
            bool first = true;
            for (int i = 0; i < label.textInfo.characterCount; i++)
            {
                var ch = label.textInfo.characterInfo[i]; if (!ch.isVisible || ch.textElement == null || ch.textElement.glyph == null) continue;
                var metrics = ch.textElement.glyph.metrics;
                // TMP quads include the transparent SDF padding; glyph metrics do not.
                float left = ch.origin + metrics.horizontalBearingX * ch.scale;
                float top = ch.baseLine + metrics.horizontalBearingY * ch.scale;
                var min = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(new Vector3(left, top - metrics.height * ch.scale, 0)));
                var max = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(new Vector3(left + metrics.width * ch.scale, top, 0)));
                var rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                result.Glyphs.Add(rect); result.Bounds = first ? rect : Union(result.Bounds, rect); first = false;
            }
            return result;
        }
        static bool Visible(TMP_Text label)
        {
            if (!label.enabled || !label.gameObject.activeInHierarchy || label.color.a < .02f) return false;
            foreach (var group in label.GetComponentsInParent<CanvasGroup>()) if (group.alpha < .02f) return false;
            return true;
        }
        static Rect ScreenRect(RectTransform transform, Camera camera)
        {
            var corners = new Vector3[4]; transform.GetWorldCorners(corners); var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]); var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        static bool Contains(Rect box, Rect value, float tolerance) => value.xMin >= box.xMin - tolerance && value.xMax <= box.xMax + tolerance && value.yMin >= box.yMin - tolerance && value.yMax <= box.yMax + tolerance;
        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        static float Intersection(Rect a, Rect b) => Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)) * Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
        static JArray Bounds(Rect rect) => new JArray(rect.x, rect.y, rect.width, rect.height);
        static string PathOf(Transform value, Transform root) { string path = value.name; while (value.parent != null && value != root) { value = value.parent; path = value.name + "/" + path; } return path; }
        static void Save() => File.WriteAllText(Output + "/report.json", new JObject { ["passed"] = fatal == null && Issues.Count == 0 && Results.Count == ExpectedCaptures, ["fatal"] = fatal, ["issueCount"] = Issues.Count, ["captures"] = Results, ["currencyResources"] = ResourceChecks, ["homeChecks"] = HomeChecks, ["excludedHiddenBanners"] = HiddenBanners, ["issues"] = Issues, ["method"] = "Production prefab initialization; actual GameView screenshots; glyph/text/ad-icon bounds and currency sprite/texture loads; saved fixture restored on exit." }.ToString());
        static void Fail(Exception e) { fatal = e.ToString(); Debug.LogException(e); Finish(); }
        static void Finish()
        {
            Save(); SessionState.SetBool(Key, false); SessionState.SetBool(Key + ".Finish", true); SessionState.SetInt(Key + ".Code", fatal == null && Issues.Count == 0 ? 0 : 1);
            EditorApplication.update -= Tick; SceneManager.sceneLoaded -= Configure;
            if (EditorApplication.isPlaying) { EditorApplication.playModeStateChanged += OnMode; EditorApplication.ExitPlaymode(); }
            else Complete();
        }
        static void OnMode(PlayModeStateChange change) { if (change == PlayModeStateChange.EnteredEditMode) Complete(); }
        static void Complete()
        {
            EditorApplication.playModeStateChanged -= OnMode; SessionState.SetBool(Key + ".Finish", false); OriginalPreferenceFixture.Restore();
            Debug.Log("LOCALIZATION_MATRIX_FINISHED"); EditorApplication.Exit(SessionState.GetInt(Key + ".Code", 1));
        }
    }
}
