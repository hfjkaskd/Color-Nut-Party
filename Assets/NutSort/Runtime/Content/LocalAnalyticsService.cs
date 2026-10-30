using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    // No platform branches or SDK dependencies. Clock/storage/transport are injected for the same runtime logic in validation.
    public sealed class LocalAnalyticsService
    {
        public const string StateKey = "CNP.Analytics.State.v1";
        private readonly LocalAnalyticsSettings settings;
        private readonly IOriginalUserPreferences storage;
        private readonly Func<long> utc;
        private readonly Func<double> monotonic;
        private readonly string version, build;
        private readonly Queue<KeyValuePair<string, Dictionary<string, object>>> startupQueue;
        private readonly Dictionary<string, Dictionary<string, object>> eligibility = new Dictionary<string, Dictionary<string, object>>();
        private readonly Dictionary<string, string> taskStates = new Dictionary<string, string>();
        private readonly Dictionary<string, ExceptionRecord> exceptions = new Dictionary<string, ExceptionRecord>();
        private readonly Dictionary<string, object> lastProperties = new Dictionary<string, object>();
        private Action<string, Dictionary<string, object>> track;
        private Action<Dictionary<string, object>> userProp;
        private JObject state, attempt;
        private OriginalUserLocalData user;
        private OriginalTables tables;
        private bool foreground = true, startupReported, quitting;
        private double lastTick, bootAt, backgroundAt, windowForeground, windowGameplay, summaryAt;
        private double attemptElapsed, attemptActive;
        private long windowStart;
        private int summarySequence;
        private string sessionId, runId, page = "loading", origin = "new_board";
        private long sequence;
        private Scope scope;
        private int committedLevel,committedRank,committedDaily,committedLogin,committedDiamondLogin;
        private bool committedGuide;
        private double committedCash,committedDiamond;
        public event Action<string, Dictionary<string, object>> Observed;
        public string FlowId { get; private set; }
        public string WithdrawalType { get; private set; } = "cash";
        public int WithdrawalTier { get; private set; }
        public int BufferedCount => startupQueue.Count;
        public int DroppedStartupEvents { get; private set; }
        public bool GameplayActive { get; set; }
        public string OperationId => scope == null ? string.Empty : scope.Id;
        public string OperationSource => scope == null ? "game_state_change" : scope.Source;

        public LocalAnalyticsService(LocalAnalyticsSettings settings, IOriginalUserPreferences storage,
            Func<long> utcMilliseconds, Func<double> monotonicSeconds, string version, string build)
        {
            this.settings = settings; this.storage = storage; utc = utcMilliseconds; monotonic = monotonicSeconds;
            this.version = version; this.build = build;
            startupQueue = new Queue<KeyValuePair<string, Dictionary<string, object>>>(settings.StartupQueueCapacity);
            string saved = storage.GetString(StateKey, string.Empty);
            state = string.IsNullOrEmpty(saved) ? new JObject() : JObject.Parse(saved);
            lastTick = bootAt = summaryAt = monotonic(); windowStart = utc();
            runId = LocalAnalytics.NewId();
            StartSession("cold_start");
            var previous = state["run"] as JObject;
            if (previous != null && !((bool?)previous["closed"] ?? false))
                Track("abnormal_exit", LocalAnalytics.Fields("previous_run_id", (string)previous["id"],
                    "previous_session", (string)previous["session"], "previous_page", (string)previous["page"],
                    "previous_build_id", (string)previous["build"], "last_app_state", (string)previous["app_state"],
                    "last_observed_utc_ms", (long?)previous["observed"], "observed_utc_ms", utc(),
                    "reason", "unclosed_run", "diagnostic_source", "local_run_marker", "confirmed", false));
            if (!((bool?)state["first_open"] ?? false))
            { state["first_open"] = true; Track("first_open", LocalAnalytics.Fields("first_open_basis", "local_marker_absent")); }
            Checkpoint();
        }

        public void BindTransport(Action<string, Dictionary<string, object>> events, Action<Dictionary<string, object>> properties)
        { track = events; userProp = properties; DrainStartupQueue(); }
        public void DrainStartupQueue()
        {
            if (track == null) return;
            while (startupQueue.Count > 0)
            {
                var item = startupQueue.Peek();
                track(item.Key, item.Value);
                startupQueue.Dequeue();
            }
        }
        public void Track(string name, Dictionary<string, object> fields = null)
        {
            if (!settings.Enabled || quitting) return;
            var payload = fields == null ? new Dictionary<string, object>() : new Dictionary<string, object>(fields);
            payload["schema_version"] = 1; payload["event_id"] = LocalAnalytics.NewId(); payload["event_utc_ms"] = utc();
            payload["app_session_id"] = sessionId; payload["sequence"] = ++sequence;
            payload["app_version"] = version; payload["build_id"] = build; payload["is_test"] = settings.IsTest;
            payload["current_page"] = page;
            Observed?.Invoke(name, payload);
            LocalAnalytics.Observe(name,payload);
            if (track != null) track(name, payload);
            else if (startupQueue.Count < settings.StartupQueueCapacity) startupQueue.Enqueue(new KeyValuePair<string, Dictionary<string, object>>(name, payload));
            else DroppedStartupEvents++;
        }
        public void SetPage(string value) { page = value; }
        public void BindUser(OriginalUserLocalData value, OriginalTables originalTables)
        {
            user = value; tables = originalTables;
            if (user.LocalAnalyticsData == null) user.LocalAnalyticsData = new JObject();
            attempt = user.LocalAnalyticsData["attempt"] as JObject;
            RestoreAttemptClock();
            RememberProgress();
            SyncProperties();
        }
        public void StartupResult(bool success, string phase, string error = null)
        {
            if (startupReported) return;
            startupReported = true;
            Track("startup_result", LocalAnalytics.Fields("result", success ? "success" : "failure", "phase", phase,
                "duration_seconds", Math.Max(0, monotonic() - bootAt), "error_code", error));
        }
        private void StartSession(string reason)
        {
            sessionId = LocalAnalytics.NewId(); sequence = 0; summarySequence = 0;
            exceptions.Clear();
            Track("session_start", LocalAnalytics.Fields("reason", reason));
        }
        public void Tick()
        {
            double now = monotonic(), delta = Math.Max(0, now - lastTick); lastTick = now;
            if (!foreground || quitting) return;
            windowForeground += delta;
            if (GameplayActive) windowGameplay += delta;
            if (attempt != null && attempt["result"] == null)
            {
                attemptElapsed += delta;
                if (GameplayActive) attemptActive += delta;
            }
            if (now - summaryAt >= settings.SummaryIntervalSeconds)
            { Summary("interval"); FlushExceptions(false); Checkpoint(); }
        }
        public void Pause(bool paused)
        {
            if (foreground == !paused || quitting) return;
            if (paused)
            { Tick(); Summary("background"); foreground = false; backgroundAt = monotonic(); FlushExceptions(true); }
            else
            {
                double now = monotonic();
                if (now - backgroundAt >= settings.SessionTimeoutSeconds) StartSession("background_timeout");
                foreground = true; lastTick = summaryAt = now; windowStart = utc();
            }
            Checkpoint();
        }
        public void Quit()
        {
            if (quitting) return;
            Tick(); Summary("normal_quit"); FlushExceptions(true); Checkpoint(true); quitting = true;
        }
        private void Summary(string reason)
        {
            long end = utc();
            if (windowForeground > 0)
                Track("session_summary", LocalAnalytics.Fields("summary_seq", ++summarySequence,
                    "window_start_ms", windowStart, "window_end_ms", end, "report_reason", reason,
                    "foreground_seconds", windowForeground, "gameplay_seconds", windowGameplay));
            windowForeground = windowGameplay = 0; windowStart = end; summaryAt = monotonic();
        }
        private void Checkpoint(bool closed = false)
        {
            state["run"] = new JObject { { "id", runId }, { "session", sessionId }, { "page", page }, { "build", build },
                { "app_state", foreground ? "foreground" : "background" }, { "observed", utc() }, { "closed", closed } };
            storage.SetString(StateKey, state.ToString(Formatting.None)); storage.Save();
        }

        public void NextBoardOrigin(string reason) => origin = reason;
        public void BoardReady(bool restored, int screwCount, int nutCount, int colorCount)
        {
            if (user == null) return;
            Tick();
            if (user.LocalPendingReward != null) { SyncProperties(); StartupResult(true, "pending_settlement_ready"); return; }
            var saved = user.LocalAnalyticsData["attempt"] as JObject;
            bool same = restored && saved != null && (int?)saved["level_id"] == user.Level &&
                (int?)saved["teaching_seed"] == user.LevelSeed;
            if (same) { attempt = saved; RestoreAttemptClock(); }
            else
            {
                bool partial = restored;
                var level = tables.GetLevelInfo(user.Level, user.Level);
                string key = user.Level + ":" + (user.Level == 1 ? user.LevelSeed : 0);
                var counts = user.LocalAnalyticsData["attempt_counts"] as JObject;
                if (counts == null) user.LocalAnalyticsData["attempt_counts"] = counts = new JObject();
                int number = ((int?)counts[key] ?? 0) + 1;
                if (!partial) counts[key] = number;
                while(counts.Count>16)((JProperty)counts.First).Remove();
                attempt = new JObject { { "attempt_id", LocalAnalytics.NewId() }, { "level_id", user.Level },
                    { "display_level", tables.GetShowLevel(user.Level) }, { "content_level_id", user.LevelId },
                    { "teaching_seed", user.LevelSeed }, { "sub_round", level == null ? 1 : level.SubRound },
                    { "sub_total_round", level == null ? 1 : level.SubTotalRound }, { "observation_scope", partial ? "partial" : "complete" },
                    { "origin", partial ? "recovery" : origin }, { "elapsed_seconds", 0d }, { "active_seconds", 0d },
                    { "valid_move_count", 0 }, { "undo_used", 0 }, { "refresh_used", 0 }, { "add_screw_used", 0 } };
                if (!partial)
                {
                    attempt["attempt_no"] = number; attempt["initial_screw_count"] = screwCount;
                    attempt["initial_nut_count"] = nutCount; attempt["color_count"] = colorCount;
                }
                user.LocalAnalyticsData["attempt"] = attempt;
                RestoreAttemptClock();
                if (!partial) Track("level_start", AttemptFields());
            }
            origin = "new_board"; SetPage("gameplay"); SyncProperties(); StartupResult(true, "board_ready");
        }
        public Dictionary<string, object> AttemptFields()
        {
            var fields = new Dictionary<string, object>();
            if (attempt == null) return fields;
            PrepareSave(user);
            foreach (var p in attempt.Properties()) if (p.Name != "teaching_seed" && p.Name != "first_action" && p.Name != "result")
                fields[p.Name] = ((JValue)p.Value).Value;
            if ((int?)attempt["level_id"] == 1) fields["step_id"] = "teaching_" + (int)attempt["teaching_seed"];
            return fields;
        }
        private void RestoreAttemptClock()
        { attemptElapsed=(double?)attempt?["elapsed_seconds"]??0; attemptActive=(double?)attempt?["active_seconds"]??0; }
        public void PrepareSave(OriginalUserLocalData value)
        {
            if(value!=user||attempt==null)return;
            attempt["elapsed_seconds"]=attemptElapsed;attempt["active_seconds"]=attemptActive;
        }
        private void RememberProgress()
        {
            committedLevel=user.Level;committedRank=user.UserLevel;committedDaily=user.TodayPassLevelCount;
            committedLogin=user.LoginDay;committedDiamondLogin=user.LoginDayCoin;committedGuide=user.IsGuideGold;
            committedCash=user.Gold;committedDiamond=user.Coin;
        }
        public void StateCommitted(OriginalUserLocalData value)
        {
            if(user==null||value!=user)return;
            if(committedLevel==user.Level&&committedRank==user.UserLevel&&committedDaily==user.TodayPassLevelCount&&
                committedLogin==user.LoginDay&&committedDiamondLogin==user.LoginDayCoin&&committedGuide==user.IsGuideGold&&
                committedCash==user.Gold&&committedDiamond==user.Coin)return;
            RememberProgress();SyncProperties();
            string key=WithdrawalType+":"+WithdrawalTier;
            if(FlowId!=null&&eligibility.ContainsKey(key)&&user.GoldRewardTargetS2CData!=null&&(WithdrawalType=="diamond"||WithdrawalTier==0))
                ObserveEligibility(LocalWithdrawalPolicy.Evaluate(user,tables,WithdrawalType=="diamond",WithdrawalTier),"state_committed");
        }
        public void ValidMove()
        {
            if (attempt == null || attempt["result"] != null) return;
            Tick(); attempt["valid_move_count"] = ((int?)attempt["valid_move_count"] ?? 0) + 1;
            if (!((bool?)attempt["first_action"] ?? false) && (string)attempt["observation_scope"] == "complete")
            {
                attempt["first_action"] = true;
                var fields = AttemptFields(); fields["first_action_type"] = "move";
                fields["time_to_first_action_seconds"] = attemptElapsed;
                Track("level_first_action", fields);
            }
        }
        public void LevelResult(string result, string reason)
        {
            if (attempt == null || attempt["result"] != null) return;
            Tick(); attempt["result"] = result;
            var fields = AttemptFields(); fields["result"] = result; fields["reason"] = reason;
            Track("level_result", fields);
        }
        public void PreparePropUse(int type)
        {
            if (attempt != null && attempt["result"] == null)
            {
                string key = LocalAnalytics.Prop(type) + "_used";
                attempt[key] = ((int?)attempt[key] ?? 0) + 1;
            }
        }
        public void PropUsed(int type, string id, string source)
        {
            var fields = AttemptFields();
            fields["operation_id"] = id; fields["prop"] = LocalAnalytics.Prop(type); fields["source"] = source;
            Track("prop_use", fields);
        }
        public IDisposable Operation(string source, string id)
        { var next = new Scope(this, scope, source, string.IsNullOrEmpty(id) ? LocalAnalytics.NewId() : id); scope = next; return next; }
        private sealed class Scope : IDisposable
        {
            private readonly LocalAnalyticsService owner;
            private readonly Scope previous;
            public readonly string Source, Id;
            public Scope(LocalAnalyticsService owner, Scope previous, string source, string id)
            { this.owner = owner; this.previous = previous; Source = source; Id = id; }
            public void Dispose() { owner.scope = previous; }
        }
        public void ResourcesCommitted(OriginalUserLocalData value, LocalResourceSnapshot before)
        {
            CommitResources(PrepareResources(value,before));
        }
        public List<Dictionary<string,object>> PrepareResources(OriginalUserLocalData value,LocalResourceSnapshot before)
        {
            if(before.Cash==value.Gold&&before.Diamond==value.Coin&&before.Undo==value.RevokeCount&&
                before.Refresh==value.ExchangeCount&&before.AddScrew==value.AddScrewCount)return null;
            var records=new List<Dictionary<string,object>>(5);
            string id = string.IsNullOrEmpty(OperationId) ? LocalAnalytics.NewId() : OperationId;
            string transaction = LocalAnalytics.NewId();
            Resource(records,transaction, id, "soft_cash", "game_cash", before.Cash, value.Gold, value);
            Resource(records,transaction, id, "diamond", "diamond_count", before.Diamond, value.Coin, value);
            Resource(records,transaction, id, "undo", "item_count", before.Undo, value.RevokeCount, value);
            Resource(records,transaction, id, "refresh", "item_count", before.Refresh, value.ExchangeCount, value);
            Resource(records,transaction, id, "add_screw", "item_count", before.AddScrew, value.AddScrewCount, value);
            if(records.Count>0)value.LocalAnalyticsData["last_resource_transaction"]=new JObject{{"transaction_id",transaction},{"operation_id",id},
                {"last_mutation_index",(long)value.LocalAnalyticsData["mutation_index"]}};
            return records;
        }
        public void CommitResources(List<Dictionary<string,object>> records)
        {
            if(records==null)return;
            foreach(var record in records)Track("resource_change",record);
            SyncProperties();
        }
        private void Resource(List<Dictionary<string,object>> records,string transaction, string operation, string resource, string unit, double before, double after, OriginalUserLocalData value)
        {
            if (before == after || double.IsNaN(before) || double.IsInfinity(before) || double.IsNaN(after) || double.IsInfinity(after)) return;
            if (value.LocalAnalyticsData == null) value.LocalAnalyticsData = new JObject();
            long index = ((long?)value.LocalAnalyticsData["mutation_index"] ?? 0) + 1;
            value.LocalAnalyticsData["mutation_index"] = index;
            records.Add(LocalAnalytics.Fields("transaction_id", transaction, "operation_id", operation,
                "mutation_index", index, "source", OperationSource, "resource", resource, "value_unit", unit,
                "balance_before", before, "delta", after - before, "balance_after", after));
        }
        public void SyncProperties()
        {
            if (user == null || tables == null || userProp == null) return;
            var current = LocalAnalytics.Fields("cnp_schema_version", 1, "cnp_current_level", user.Level,
                "cnp_display_level", tables.GetShowLevel(user.Level), "cnp_tutorial_completed", user.Level >= 2,
                "cnp_cash_balance", user.Gold, "cnp_diamond_balance", user.Coin, "cnp_undo_count", user.RevokeCount,
                "cnp_refresh_count", user.ExchangeCount, "cnp_add_screw_count", user.AddScrewCount,
                "cnp_user_rank", user.UserLevel, "cnp_has_withdraw_account", HasSavedAccount());
            var changed = new Dictionary<string, object>();
            foreach (var p in current) if (!lastProperties.TryGetValue(p.Key, out var old) || !Equals(old, p.Value))
            { changed[p.Key] = p.Value; lastProperties[p.Key] = p.Value; }
            if (changed.Count > 0) userProp(changed);
        }
        private bool HasSavedAccount()
        {
            var account=user.UserLssInfo;
            if(account==null||account.GetValue("GetType",StringComparison.OrdinalIgnoreCase)==null)return false;
            return !string.IsNullOrEmpty((string)account.GetValue("Email",StringComparison.OrdinalIgnoreCase))||
                !string.IsNullOrEmpty((string)account.GetValue("Number",StringComparison.OrdinalIgnoreCase));
        }
        public void TutorialStep(string step, int index, bool complete)
        {
            if (user == null || user.Level >= 2 && !complete) return;
            var guide = user.LocalAnalyticsData["guide"] as JObject;
            if (guide == null) user.LocalAnalyticsData["guide"] = guide = new JObject();
            if (guide["started"] == null)
            { guide["started"] = utc(); Track("tutorial_start", LocalAnalytics.Fields("guide_id", "initial_guide", "guide_version", 1)); }
            string key = step + (complete ? ":complete" : ":show");
            if (guide[key] != null || complete && guide[step + ":show"] == null) return;
            guide[key] = utc();
            var fields = LocalAnalytics.Fields("guide_id", "initial_guide", "step_id", step, "step_index", index);
            if (complete) fields["elapsed_seconds"] = Math.Max(0, (utc() - (long)guide[step + ":show"]) / 1000d);
            Track(complete ? "tutorial_step_complete" : "tutorial_step_show", fields);
            if (complete && index == 4 && guide["complete"] == null)
            {
                guide["complete"] = utc();
                Track("tutorial_complete", LocalAnalytics.Fields("guide_id", "initial_guide", "elapsed_seconds", Math.Max(0, (utc() - (long)guide["started"]) / 1000d)));
            }
        }
        public void BeginWithdrawal(bool diamonds, int tier)
        { FlowId = LocalAnalytics.NewId(); WithdrawalType = diamonds ? "diamond" : "cash"; WithdrawalTier = tier; taskStates.Clear(); }
        public Dictionary<string, object> WithdrawalFields()
        { return LocalAnalytics.Fields("flow_id", FlowId, "withdraw_type", WithdrawalType, "tier_id", WithdrawalTier, "status_source", "client"); }
        public void ObserveEligibility(LocalWithdrawalEligibility value, string source)
        {
            string key = WithdrawalType + ":" + WithdrawalTier;
            eligibility.TryGetValue(key, out var previous);
            bool initial = previous == null || !Equals(previous["flow_id"], FlowId);
            if (initial || !Equals(previous["eligible"], value.Eligible) || !Equals(previous["blocking_condition"], value.FirstIncomplete))
            {
                var fields = WithdrawalFields(); fields["observation"] = initial ? "initial" : "change";
                fields["eligible"] = value.Eligible; fields["blocking_condition"] = value.FirstIncomplete;
                if (!initial) fields["previous_eligible"] = previous["eligible"];
                eligibility[key] = fields; Track("withdraw_eligibility_change", fields);
            }
            foreach (var item in value.Requirements)
            {
                string status = !item.Known ? "unknown" : item.Waived ? "waived" : item.Complete ? "complete" : "pending";
                string taskKey = FlowId + ":" + key + ":" + item.Key;
                string signature = status + ":" + item.Current.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + item.Target.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (taskStates.TryGetValue(taskKey, out string saved) && saved == signature) continue;
                taskStates[taskKey] = signature;
                var fields = WithdrawalFields(); fields["condition_id"] = item.Key; fields["state"] = status; fields["source"] = source;
                if (item.Known) { fields["progress"] = item.Current; fields["target"] = item.Target; }
                Track("withdraw_task_change", fields);
            }
        }
        public string WithdrawSubmit(double amount, string channel)
        {
            string id = LocalAnalytics.NewId(); var fields = WithdrawalFields();
            fields["operation_id"] = id; fields["amount"] = amount; fields["value_unit"] = WithdrawalType == "diamond" ? "diamond_count" : "game_cash";
            fields["request_sent"] = false; if (!string.IsNullOrEmpty(channel)) fields["channel"] = channel;
            Track("withdraw_submit", fields); return id;
        }
        public void WithdrawResult(string id, string reason, string localStatus = null)
        {
            var fields = WithdrawalFields(); fields["operation_id"] = id; fields["request_sent"] = false;
            fields["result"] = "not_submitted"; fields["reason"] = reason;
            if (localStatus != null) fields["local_record_status"] = localStatus;
            Track("withdraw_submit_result", fields);
        }
        private sealed class ExceptionRecord
        { public string Fingerprint, Component, Error, Type; public int Count; public long First, Last; public double FlushAt; public bool Sent; }
        public void Exception(string component, string code, string type)
        {
            string fingerprint = component + ":" + code + ":" + type;
            if (!exceptions.TryGetValue(fingerprint, out var record))
            {
                if (exceptions.Count >= settings.ExceptionFingerprintLimit) return;
                record = new ExceptionRecord { Fingerprint = fingerprint, Component = component, Error = code, Type = type,
                    First = utc(), FlushAt = monotonic() + settings.ExceptionWindowSeconds };
                exceptions.Add(fingerprint, record);
            }
            record.Count++; record.Last = utc();
            if (!record.Sent) { record.Sent=true; EmitException(record); }
        }
        private void EmitException(ExceptionRecord record)
        {
            Track("runtime_exception", LocalAnalytics.Fields("fingerprint", record.Fingerprint, "component", record.Component,
                "error_code", record.Error, "exception_type", record.Type, "count", record.Count,
                "first_utc_ms", record.First, "last_utc_ms", record.Last));
            record.Count = 0; record.First = utc(); record.FlushAt = monotonic() + settings.ExceptionWindowSeconds;
        }
        private void FlushExceptions(bool force)
        { foreach (var pair in exceptions) if (pair.Value.Count > 0 && (force || monotonic() >= pair.Value.FlushAt)) EmitException(pair.Value); }
    }
}
