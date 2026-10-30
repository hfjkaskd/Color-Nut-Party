using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using UnityEngine;

namespace NutSort.World
{
    public sealed class OriginalGameScene : MonoBehaviour
    {
        [SerializeField] private Camera GameCamera;
        [SerializeField] private Camera TopGameCamera;
        [SerializeField] private GameObject background;
        [SerializeField] private OriginalPrefabPool pool;
        [SerializeField] private OriginalContentSettings content;
        [SerializeField] private OriginalSceneSession session;
        [SerializeField] private OriginalGameplayEffects effects;
        [SerializeField] private OriginalTableSettings tableSettings;
        [SerializeField] private OriginalAudioPlayer audioPlayer;
        private OriginalLevelView level;
        private OriginalLevelRepository repository;
        private OriginalLevelSelector selector;
        private OriginalGameplayUnlockConfig unlockConfig;
        private Func<UnityEngine.Object> initializationMainPanel;
        private OriginalInitializationContinuation initializationContinuation;

        // Bind the restored UI lifecycle before initialization. The current local
        // startup has not yet supplied all panel consumers; no fake completion is
        // substituted when it has not bound this lifecycle.
        public void BindInitialization(Func<UnityEngine.Object> mainPanel,
            OriginalInitializationContinuation continuation)
        {
            if (mainPanel == null) throw new ArgumentNullException(nameof(mainPanel));
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            initializationMainPanel = mainPanel;
            initializationContinuation = continuation;
        }

        private struct PendingInitialization
        {
            public bool Reset, LongEntry;
            public float Elapsed;
        }
        private readonly List<PendingInitialization> pendingInitializations = new List<PendingInitialization>();
        public bool ModalInputBlocked { get; set; }
        public bool IsCanOperatorScrew { get; set; }
        public bool IsRestarting { get; private set; }
        private int viewportWidth, viewportHeight;
        public OriginalLevelView Level => level;
        public Camera WorldCamera => GameCamera;
        public OriginalTables Tables { get; private set; }
        public OriginalUserLocalData User => audioPlayer.UserState.Data;
        public int PlayerLevel => audioPlayer.UserState.Data.Level;
        public int ShowLevel => Tables.GetShowLevel(PlayerLevel);
        public bool InputBlocked { get; set; }
        public bool IsExchanging { get; set; }
        public bool IsInitDone { get; set; }
        public bool IsSucceed { get; set; }
        public bool IsSkipLevel { get; set; }
        public bool PreserveCompletedBoardForSettlement { get; set; }
        // IsSuccess 0x9FB348 short-circuits before reading the board.
        public bool IsSuccess() => IsSkipLevel || level.Board.IsSuccess;
        private readonly OriginalFailureFlow failure = new OriginalFailureFlow();
        public bool IsFail { get => failure.IsFail; set => failure.IsFail = value; }

        public bool NewLevelMode => session.LSS260820;
        public event Action BoardReady;
        public bool LastBoardRestored { get; private set; }

        public bool NewGameplayUnlock(bool showBanner, Action<int,int,bool> showPanel)
        {
            return new OriginalGameplayUnlock(audioPlayer.UserState.Data, unlockConfig.Read, showPanel).Run(showBanner);
        }

        public void Fail(Action<int> showPanel)
        {
            if(!IsFail)LocalAnalytics.Current?.LevelResult("fail","no_legal_move");
            failure.Fail(session.FailPanelDelay, ScheduleDelay, () => showPanel(session.FailPanelId));
        }

        private OriginalSuccessFlow successFlow;
        public void BindSuccess(Func<bool> newLevelMode,Action refreshTeaching,Action hideGoldHint,Action hideProgress,
            Transform effectParent,Action<OriginalLevelInfo> requestReward)
        {
            successFlow=new OriginalSuccessFlow(User,()=>IsSucceed,()=>IsSucceed=true,newLevelMode,
                ()=>InitLevel(true,false,false),refreshTeaching,()=>Tables.GetLevelInfo(User.Level,User.Level),
                hideGoldHint,hideProgress,()=>audioPlayer.PlaySound(session.StageCompleteSound),()=>effects.PlaySuccess(effectParent),
                ScheduleDelay,session.SuccessRequestDelay,requestReward);
        }
        public OriginalSuccessPanelFlow CreateSuccessPanelFlow(Func<long> localTimeSeconds,Action<int> showPanel)
        {
            return new OriginalSuccessPanelFlow(User,localTimeSeconds,()=>audioPlayer.PlaySound(session.SuccessPanelSound),showPanel);
        }
        public void HideSuccessPanel(OriginalSuccessPanelFlow flow,Action baseHide,Func<bool> newLevelMode,
            OriginalRewardProgress progress,Action synchronizeCompletedStage)
        {
            flow.Hide(baseHide,newLevelMode,progress.IsCompletePassStage2Level,synchronizeCompletedStage,()=>InitLevel(true,true,false));
        }
        public Action<bool> CreateSuccessReward(Action<bool,Action<JObject>> request,Action<int,OriginalItemGetInfo> showPanel,Action close)
        {
            return new OriginalSuccessRewardFlow(request,showPanel,close).GetReward;
        }
        public Action<JObject> CreateSuccessSettlement(Func<int,bool> isPanelOpen,Action<Action> enqueue,
            Action closePanels,Action<int,OriginalItemGetInfo> showPanel)
        {
            return new OriginalSuccessSettlementFlow(User,SaveUserData,isPanelOpen,enqueue,closePanels,showPanel).Run;
        }

        public void BindSuccessResponse(Func<bool> newLevelMode,Action refreshTeaching,Action hideGoldHint,Action hideProgress,
            Transform effectParent,Action<Action<JObject>> requestReward,Action<int,Action> showPanel,Action<JObject> settlement)
        {
            var responseFlow=new OriginalSuccessResponseFlow(()=>User.IsCompleteCoinNewPeopleReward,
                ()=>InitLevel(true,false,false),showPanel,settlement);
            BindSuccess(newLevelMode,refreshTeaching,hideGoldHint,hideProgress,effectParent,
                captured=>requestReward(response=>responseFlow.Run(captured,response)));
        }
        public void Success()
        {
            if(!IsSucceed)
            {
                LocalAnalytics.Current?.LevelResult("win",IsSkipLevel?"skip":"board_complete");
                if(User.Level==1)LocalAnalytics.Current?.TutorialStep("teaching_"+User.LevelSeed,User.LevelSeed+1,true);
            }
            successFlow.Run();
        }

        // Type transitions are owned by the scene; the optional observer runs
        // after their native animation/save requests, never instead of them.
        public void BindMoveCompletion(Action<ScrewState,bool> doneEvent,Action hideGuide,Action pushHint,
            Action<ScrewState> typesRefreshed,Action<int> showPanel)
        {
            var completion=new OriginalMoveCompletionFlow(User,IsSuccess,level.IsCannotMove,
                ScheduleDelay,doneEvent,hideGuide,pushHint,target=>{level.RefreshScrewTypes(target);typesRefreshed?.Invoke(target);},()=>Fail(showPanel),
                session.ScrewDoneEventDelay,session.SuccessDoneEventDelay);
            level.BindMoveCompletion(completion.Run);
            level.BindLandingCompletion(()=>IsSkipLevel,doneEvent);
        }

        // Compose the native delayed DoneEvent and victory path. External
        // effects retain real config/account/request semantics.
        public void BindScrewCompletion(IOriginalScrewDoneEffects doneEffects,Action hideGuide,Action pushHint)
        {
            var done=new OriginalScrewDoneFlow(()=>User,()=>ShowLevel,Success,doneEffects);
            BindMoveCompletion(done.Run,hideGuide,pushHint,null,doneEffects.ShowPanel);
        }

        private OriginalAddScrewFlow addScrewFlow;
        public void BindAddScrew(Func<int> maximum,Func<bool> singleTile,OriginalItemManager items,
            Action refreshBottom,Action<int,int> showToolPanel,Action<int> showTip)
        {
            addScrewFlow=new OriginalAddScrewFlow(User,maximum,singleTile,()=>level.Unlock(singleTile),
                ()=>level.AddTile(singleTile),()=>AddNullScrew(singleTile,refreshBottom),items.AddTool,showToolPanel,showTip);
            level.BindAddScrew(AddScrew);
        }
        public void AddScrew() => addScrewFlow.Run();

        public void AddNullScrew(Func<bool> singleTile,Action refreshBottom)
        {
            level.AddNullScrew(singleTile,()=>ResizeCameras(Screen.width,Screen.height));
            refreshBottom();
        }

        public void BindExchange(OriginalItemManager items,Action<int> showPanel)
        {
            level.BindExchange(target=>level.Exchange(target,items.AddTool,()=>Fail(showPanel)));
        }

        public void Revoke(Action<int> refreshButtonState, Action<int> showPanel)
        {
            level.Revoke(refreshButtonState, () => Fail(showPanel));
        }

        public event Action<ScrewOperation, ScrewState> OperationApplied;

        // TimeLSSUtil.DelayCallback runs on the main LuoSiSort MonoBehaviour.
        public void ScheduleAfterMainPanel(Func<UnityEngine.Object> mainPanel,Action continuation)
        {
            StartCoroutine(OriginalInitializationWait.Run(mainPanel,session.MainPanelReadyDelay,continuation));
        }
        // SetExchangeState 0x9FE8B4: activeSelf is the early-return authority.
        // Visuals change now; only IsExchanging may be deferred (0xA021F4).
        public void SetExchangeState(GameObject exchangeMask,bool enabled,float delay=0)
        {
            if(exchangeMask.activeSelf==enabled)return;
            if(delay>0)ScheduleDelay(delay,()=>IsExchanging=enabled);
            else IsExchanging=enabled;
            TopGameCamera.gameObject.SetActive(enabled);
            background.SetActive(!enabled);
            exchangeMask.SetActive(enabled);
            level.SetScrewState(!enabled);
        }
        public void ShowEveryDayGift(Func<bool> hasPanel,Action<int> showPanel)
        {
            new OriginalEveryDayGiftFlow(()=>User,hasPanel,ScheduleUntil,showPanel,SaveUserData).Show();
        }
        public void ScheduleUntil(Func<bool> predicate,Action callback)
        {
            StartCoroutine(OriginalUntilCallback.Run(predicate,callback));
        }
        public void ScheduleDelay(float seconds,Action callback)
        {
            StartCoroutine(DelayCallback(seconds,callback));
        }
        private static System.Collections.IEnumerator DelayCallback(float seconds,Action callback)
        {
            yield return new WaitForSeconds(seconds);
            callback?.Invoke();
        }
        private void Start() { Initialize(); }

        public void Initialize()
        {
            if (level != null) return;
            if (pool == null || content == null || session == null || GameCamera == null || TopGameCamera == null)
                throw new InvalidOperationException("Original game scene references are incomplete.");
            Tables = new OriginalTables(tableSettings);
            repository = new OriginalLevelRepository(content);
            repository.Initialize();
            audioPlayer.Initialize();
            unlockConfig = new OriginalGameplayUnlockConfig(audioPlayer.UserState.Data, session.GameplayUnlockLevels);
            selector = new OriginalLevelSelector(repository, content, UnityLevelRandom.Instance);
            level = pool.Rent(session.LevelPrefabPath, transform).GetComponent<OriginalLevelView>();
            level.BindGuide(()=>session.LSS260820,()=>User.Level,()=>User.LevelSeed,IsSuccess);
            effects.Bind(level, pool);
            audioPlayer.Bind(level);
            level.OperationApplied += ForwardOperation;
            level.MoveAttempted += CountMoveAttempt;
            level.SaveRequested += SaveCurrentBoard;
            // LuoSiSortMgr.Init uses IsNullOrEmpty, not whitespace or a parse
            // exception fallback. Nonempty invalid saved data must stay visible.
            BeginInitialization(string.IsNullOrEmpty(audioPlayer.UserState.Data.LevelInfo), session.LongEntryDelay);
        }

        public void RestartLevel()
        {
            LocalAnalytics.Current?.LevelResult("restart","replay_confirmed");
            LocalAnalytics.Current?.NextBoardOrigin("replay");
            // ReplayCallback calls InitLevel directly, even during reconstruction.
            BeginInitialization(true, false);
        }

        // FailPanel.RestartCallback (0x9D6BE4): unlike ordinary replay,
        // advance the seed and clear failure before reinitializing, then close.
        public void RestartAfterFailure(Action closePanel)
        {
            LocalAnalytics.Current?.NextBoardOrigin("failure_retry");
            var user = audioPlayer.UserState.Data;
            user.LevelSeed = unchecked(user.LevelSeed + 1);
            IsFail = false;
            BeginInitialization(true, false);
            closePanel();
        }

        // Public counterpart of InitLevel(reset, showBanner, firstInit).
        // Uses the same established local initialization boundary as restart.
        public void InitLevel(bool reset=true,bool showBanner=true,bool firstInit=false)
        {
            BeginInitialization(reset,firstInit,showBanner);
        }

        private void BeginInitialization(bool reset, bool useLongEntry, bool showBanner=true)
        {
            IsInitDone = false;
            IsSucceed = false;
            IsRestarting = true;
            var user = audioPlayer.UserState.Data;
            user.LuckyScrewDoneCount = 0;
            if (reset) { user.PassLevelTime = 0; user.Init(); }
            level.Clear();
            IsExchanging = false;
            pendingInitializations.Add(new PendingInitialization { Reset = reset, LongEntry = useLongEntry });
            if (initializationContinuation != null)
            {
                // The successful local initialization boundary schedules these
                // independently of board reconstruction, as in 0x9FF7CC.
                var continuation = initializationContinuation;
                ScheduleAfterMainPanel(initializationMainPanel, () => continuation.Run(showBanner, useLongEntry));
            }

        }

        // Same scaled-time state machine in Editor and on device. Explicit time
        // advancement also lets validation inspect the original callback boundary.
        public void AdvanceInitialization(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsRestarting || level == null) return;
            if (pendingInitializations.Count == 0)
            {
                if (level.AreNutsInitialized)
                {
                    IsRestarting = false;
                    BoardReady?.Invoke();
                }
                return;
            }
            // Each InitLevel callback captures its own flags and delay. A later
            // invocation does not cancel an earlier scheduled reconstruction.
            int count = pendingInitializations.Count;
            for (int i = 0; i < count; i++)
            {
                var pending = pendingInitializations[i];
                pending.Elapsed += deltaTime;
                pendingInitializations[i] = pending;
            }
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                var pending = pendingInitializations[index];
                if (pending.Elapsed < session.RestartDelay) { index++; continue; }
                pendingInitializations.RemoveAt(index);
                RebuildBoard(pending.Reset, pending.LongEntry);
            }
        }

        private void RebuildBoard(bool resetBoard, bool longEntry)
        {
            LastBoardRestored=!resetBoard;
            if (resetBoard) BindFreshBoard(longEntry);
            else
            {
                OriginalBoardSnapshot snapshot = OriginalBoardSnapshotJson.Read(audioPlayer.UserState.Data.LevelInfo);
                if (snapshot == null) throw new InvalidOperationException("Original saved LevelInfo deserialized to null.");
                level.BindSaved(snapshot, pool, longEntry, session.LSSAB);
            }
            audioPlayer.PlaySound(session.StageStartSound, longEntry ? session.FirstStageSoundDelay : session.RestartStageSoundDelay);
            ResizeCameras(Screen.width, Screen.height);
            // Native resume resets completed boards. The local settlement host
            // must keep a completed saved board until its BoardReady recovery.
            if (!resetBoard && (level.Board.Screws.Length == 0 ||
                (IsSuccess() && !PreserveCompletedBoardForSettlement)))
                BeginInitialization(true, longEntry);
        }

        private void BindFreshBoard(bool longEntry)
        {
            var user = audioPlayer.UserState.Data;
            var progress = new LevelProgressState
            { Level = user.Level, LevelId = user.LevelId, LevelSeed = user.LevelSeed, IsRandomLevelSeed = user.IsRandomLevelSeed };
            LevelSelection selection;
            try { selection = selector.Select(progress, session.LSS260820, session.LSSSHSLV); }
            finally
            {
                // Source selection writes these user fields; random selection
                // deliberately does not replace the persisted LevelSeed.
                user.LevelId = progress.LevelId; user.IsRandomLevelSeed = progress.IsRandomLevelSeed;
            }
            level.Bind(repository.LoadBoard(selection.Loop, selection.Seed), pool, longEntry, session.LSSAB,
                user.Level >= session.LockedScrewStartLevel);
        }

        private void CountMoveAttempt()
        {
            // Source increments the user's cumulative count before validating
            // destination color/capacity (ScrewInfo.Operator, 0xA09718).
            audioPlayer.UserState.Data.ScrewMoveCount++;
        }

        private void ForwardOperation(ScrewOperation result, ScrewState target)
        {
            if(result.Kind==ScrewOperationKind.Moved)LocalAnalytics.Current?.ValidMove();
            if (result.SaveRequested)
            {
                // Original common save point follows the immediate data transfer
                // or invalid-destination revert, before movement animations finish.
                SaveCurrentBoard();
            }
            OperationApplied?.Invoke(result, target);
        }

        private void SaveCurrentBoard()=>SaveUserData();
        public void SaveUserData()
        {
            var store = audioPlayer.UserState.Store;
            if (store.Data != null)
                store.SaveData(level!=null,level==null?null:OriginalBoardSnapshotJson.Write(level.CaptureSnapshot()));
        }

        public void ResizeCameras(int width, int height)
        {
            viewportWidth = width; viewportHeight = height;
            level.FixCameras(GameCamera, TopGameCamera, width, height);
        }

        // LuoSiSortMgr.Update 0x9FB8F4 gates only on IsInitDone. Modal state,
        // failure/success and visual readiness do not stop an initialized clock.
        public void AdvanceElapsedTime(float deltaTime)
        {
            if (IsInitDone) audioPlayer.UserState.Data.PassLevelTime += deltaTime;
        }

        private void Update()
        {
            AdvanceElapsedTime(Time.deltaTime);
            AdvanceInitialization(Time.deltaTime);
            if (level == null || IsRestarting) return;
            if (viewportWidth != Screen.width || viewportHeight != Screen.height) ResizeCameras(Screen.width, Screen.height);
            // Use the same input policy on every platform. A single touch ends
            // or a mouse button releases; no Application.isEditor alternate path.
            int touches = Input.touchCount;
            if (touches == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended) TryOperateAtScreenPoint(touch.position);
            }
            else if (touches == 0 && Input.GetMouseButtonUp(0)) TryOperateAtScreenPoint(Input.mousePosition);
        }

        public ScrewOperation TryOperateAtScreenPoint(Vector2 position)
        {
            if (InputBlocked || (ModalInputBlocked && !IsCanOperatorScrew) || !IsInitDone || IsRestarting || level == null || !level.AreNutsInitialized) return default;
            Ray ray = GameCamera.ScreenPointToRay(position);
            // These are 3D gameplay objects, not UI controls. UI panels use
            // standard Buttons and set InputBlocked through their lifecycle.
            if (!Physics.Raycast(ray, out RaycastHit hit, session.RaycastDistance, session.GameplayLayers)) return default;
            if (!hit.transform.TryGetComponent(out OriginalScrewView screw) || screw.State == null) return default;
            return level.Operate(screw.State.Index, IsExchanging);
        }

        private void OnDestroy()
        {
            pool?.BeginShutdown();
            pendingInitializations.Clear();
            if (level == null) return;
            effects.Clear();
            audioPlayer.Unbind();
            level.OperationApplied -= ForwardOperation;
            level.MoveAttempted -= CountMoveAttempt;
            level.SaveRequested -= SaveCurrentBoard;
            level.Clear();
            level = null;
        }
    }
}
