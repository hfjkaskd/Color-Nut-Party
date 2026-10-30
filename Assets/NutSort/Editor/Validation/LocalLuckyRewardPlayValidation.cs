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
    [InitializeOnLoad]
    public static class LocalLuckyRewardPlayValidation
    {
        const string Key="NutSort.LocalLuckyRewardPlayValidation";
        static bool StyleOnly=>SessionState.GetBool(Key+".StyleOnly",false);
        static string Output=>StyleOnly?"Docs/LuckyHeaderStyle/20261012":"Docs/RewardFlowVideo/20261012/lucky-validation";
        const string Backup="Library/ValidationBackups/GameplayUserLocalData.bin";
        static readonly JArray Checks=new JArray(),Captures=new JArray();
        static readonly List<RaycastResult> Hits=new List<RaycastResult>();
        static OriginalGameScene game;
        static OriginalStartupFlow startup;
        static OriginalAudioPlayer audio;
        static LocalGameplayController local;
        static int phase,rewardedRequests,interstitialRequests;
        static int recoveryCase,recoveryCount;
        static float recoveryCash;
        static double recoveryDiamonds;
        static double readyAt,deadline,nextProbe;
        static float rodAward,expectedCash,pendingAmount,pendingMultiplier;
        static string error;
        static DateTime began;
        static Action<bool> adDone;
        static Action deferredUiAction;
        static double uiMutationAt;
        static int captureFrame;

        static LocalLuckyRewardPlayValidation()
        {
            if(SessionState.GetBool(Key+".Finish",false))
            {EditorApplication.playModeStateChanged+=OnPlayModeChanged;EditorApplication.delayCall+=CompleteFinish;}
            else if(SessionState.GetBool(Key,false))StartPolling();
        }
        public static void Run()=>StartValidation(false);
        public static void RunStyle()=>StartValidation(true);
        static void StartValidation(bool styleOnly)
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Start in Edit Mode.");
                SessionState.SetBool(Key+".StyleOnly",styleOnly);
                Directory.CreateDirectory(Output);
                if(!styleOnly)
                {
                    LocalEarningFlowPolicyValidation.Validate();
                    LocalLuckyRewardPolicyValidation.Run();
                    LocalInterruptedScrewRewardPolicyValidation.Run();
                }
                string fixture=CreateFixture();
                SessionState.SetString(Key+".Fixture",fixture);
                OriginalPreferenceFixture.Begin(fixture);
                SessionState.SetBool(Key+".Finish",false);SessionState.SetBool(Key,true);
                EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                PlayModeWindow.SetCustomRenderingResolution(720,1600,"Lucky reward acceptance: isolated US fixture");
                StartPolling();EditorApplication.EnterPlaymode();
            }
            catch(Exception exception){Fail(exception);}
        }
        static string CreateFixture()
        {
            // Three genuine 1+3 transfers. The first two are rod completions;
            // the third completes the board, without changing production gates.
            var board=new OriginalBoardState(new LevelData{B=new[]{Rod(1),Rod(1,1,1),Rod(2),Rod(2,2,2),Rod(3),Rod(3,3,3)}},
                Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"))
            {
                Level=3,Gold=500,Coin=0,RevokeCount=5,NewGameplayUnlockIndex=4,IsCompleteRecordGuide=true,
                IsCompleteExtraGoldGuide=true,IsCompleteCoinNewPeopleReward=true,LuckyScrewDoneCount=3,
                LocalWithdrawalData=new JObject
                {
                    ["tutorialBalanceApplied"]=true,["tutorialBalanceAmount"]=500,["tutorialBalanceNoticeAcknowledged"]=true,
                    ["earningFlow"]=new JObject{["activationComplete"]=true}
                },
                LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()))
            };
            return OriginalUserDataJson.Write(user);
        }
        static ScrewData Rod(params int[] colors)
        {
            var cells=new CData[4];
            for(int i=0;i<cells.Length;i++)cells[i]=new CData{LP=new LPData{y=i},BIM=i<colors.Length?new BIMData{Id=1,CI=colors[i]}:null};
            return new ScrewData{Id=1,C=cells};
        }
        static void StartPolling()
        {
            phase=rewardedRequests=interstitialRequests=0;ClearReferences();Checks.Clear();Captures.Clear();
            deferredUiAction=null;uiMutationAt=0;captureFrame=-1;
            readyAt=nextProbe=0;deadline=EditorApplication.timeSinceStartup+360;began=DateTime.UtcNow;error=null;adDone=null;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            SceneManager.sceneLoaded-=ConfigureCountry;SceneManager.sceneLoaded+=ConfigureCountry;
        }
        static void ConfigureCountry(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="LuoSiSortGame")return;
            var session=UnityEngine.Object.FindObjectOfType<OriginalUserSession>();
            if(session!=null)session.ConfigureRewardCountry("US");
        }
        static void ClearReferences(){game=null;startup=null;audio=null;local=null;}
        static bool SceneReady()
        {
            if(local!=null)return true;
            if(EditorApplication.timeSinceStartup<nextProbe)return false;nextProbe=EditorApplication.timeSinceStartup+.15;
            if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
            if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
            if(audio==null)audio=UnityEngine.Object.FindObjectOfType<OriginalAudioPlayer>();
            if(game==null||startup==null||audio==null||startup.LocalGameplay==null||game.Level==null||
                !game.Level.AreNutsInitialized||game.IsRestarting||startup.Loading.IsVisible)return false;
            local=startup.LocalGameplay;
            Check(audio.UserState.DisplayCountry(game.Tables)=="US","Production country selection loads the US fixture");
            BindAds();return true;
        }
        static void BindAds()=>local.BindSettlementAds(
            done=>{interstitialRequests++;adDone=done;},done=>{rewardedRequests++;adDone=done;});
        static bool BoardReady()=>game.IsInitDone&&!game.ModalInputBlocked&&!game.IsRestarting;
        static bool EntriesSettled()
        {
            foreach(var rod in game.Level.Board.Screws)foreach(var slot in rod.Slots)
                if(slot.Nut!=null&&game.Level.GetNut(slot.Nut).IsEntryAnimating)return false;
            return true;
        }
        static bool PanelReady(OriginalSuccessPanel panel)=>panel!=null&&!panel.Closing&&Vector3.Distance(panel.Title.localScale,Vector3.one)<.001f;
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                if(now>=deadline)throw new TimeoutException("Lucky validation timed out at phase "+phase);
                if(deferredUiAction!=null)
                {
                    if(now<uiMutationAt||Time.frameCount<=captureFrame)return;
                    var action=deferredUiAction;deferredUiAction=null;action();return;
                }
                if(now<readyAt||!SceneReady()||startup.Replay.IsClickMasked)return;
                switch(phase)
                {
                    case 0:
                        if(!BoardReady()||!EntriesSettled())return;
                        Check(game.User.Level==3&&Near(game.User.Gold,500)&&game.User.Coin==0,"Fixture enters activated third level with cash 500 and zero diamonds");
                        Check(!local.RewardsTop.Coin.gameObject.activeSelf,"Diamond entry stays hidden during the third level");
                        rodAward=LocalLuckyRewardPolicy.Number(game.User.GoldRewardTargetS2CData["bear_torque"]);
                        Check(rodAward>0,"Rod cash reads bear_torque from the loaded country configuration");
                        expectedCash=500+rodAward;
                        // InitLevel resets this session counter. Seed the preceding
                        // three completions after initialization, not in the save.
                        game.User.LuckyScrewDoneCount=3;
                        Capture("before-first-rod",null);Move(0,1);Next(1,.2);break;
                    case 1:
                        if(!PanelReady(local.LuckyPanel))return;
                        Check(game.User.ScrewDoneCount==1&&!game.IsSuccess()&&local.SuccessPanel==null,"One real rod completion opens Lucky without completing the remaining board");
                        Check(Near(game.User.Gold,expectedCash)&&game.User.Coin==0,"Real rod completion adds configured cash once and no early diamonds");
                        Check(local.LuckyPanel.GetButton.gameObject.activeInHierarchy&&local.LuckyPanel.MoreButton.gameObject.activeInHierarchy&&local.LuckyPanel.Ad.activeInHierarchy,"Lucky presents both ordinary Claim and rewarded Claim 10X Buttons");
                        var pending=LocalLuckyRewardPolicy.Pending(game.User);
                        Check(pending!=null&&!((bool)pending["claimed"]),"Threshold creates an unclaimed persisted Lucky amount");
                        pendingAmount=(float)pending["amount"];pendingMultiplier=(float)pending["multiplier"];
                        Check(Near(pendingMultiplier,10),"10X comes from the loaded original reward multiplier");
                        if(StyleOnly)CheckScrewDecoration(local.LuckyPanel);
                        Capture("lucky-tall",local.LuckyPanel.gameObject);
                        AfterCapture(()=>PlayModeWindow.SetCustomRenderingResolution(720,1280,"Lucky reward short viewport"));Next(2,1);break;
                    case 2:
                        if(Screen.width!=720||Screen.height!=1280)return;
                        if(StyleOnly)CheckScrewDecoration(local.LuckyPanel);
                        Capture("lucky-short",local.LuckyPanel.gameObject);
                        if(StyleOnly){Next(100,.5);break;}
                        Click(local.LuckyPanel.MoreButton);Next(3,.1);break;
                    case 3:
                        Check(rewardedRequests==1&&interstitialRequests==0&&local.LuckyAdPending&&adDone!=null,"More Button waits for an explicit rewarded ad result");
                        adDone(false);adDone(true);
                        Check(Near(game.User.Gold,expectedCash)&&!local.LuckyAdPending&&!local.LuckyPanel.Closing&&
                            !((bool)LocalLuckyRewardPolicy.Pending(game.User)["claimed"]),"Failed optional ad and its late duplicate success leave the base reward unclaimed");
                        Next(4,1);break;
                    case 4: Click(local.LuckyPanel.MoreButton);Next(5,.1);break;
                    case 5:
                        Check(rewardedRequests==2&&local.LuckyAdPending,"Player can retry the optional rewarded claim after failure");
                        adDone(true);adDone(true);expectedCash+=pendingAmount*pendingMultiplier;
                        Check(Near(game.User.Gold,expectedCash)&&game.User.Coin==0&&
                            ((bool)LocalLuckyRewardPolicy.Pending(game.User)["claimed"]),"Actual rewarded success grants exactly 10X once, including duplicate callback protection");
                        Next(6,.1);break;
                    case 6:
                        if(local.EarningSequencePanel==null)return;
                        Check(local.EarningSequencePanel.ReceiptVisible&&local.EarningSequencePanel.GetComponentsInChildren<Button>(true).Length==0,"Lucky receipt is automatic and has no extra confirmation Button");
                        Capture("lucky-10x-receipt",local.EarningSequencePanel.gameObject);Next(7,.2);break;
                    case 7:
                        if(!BoardReady()||local.EarningSequencePanel!=null||local.LuckyPanel!=null)return;
                        Check(Near(game.User.Gold,expectedCash)&&LocalLuckyRewardPolicy.Pending(game.User)==null,"Receipt animation closes and clears its pending state without a second grant");
                        game.SaveUserData();Reload(8);break;
                    case 8:
                        if(!BoardReady()||!EntriesSettled())return;
                        Check(Near(game.User.Gold,expectedCash)&&LocalLuckyRewardPolicy.Pending(game.User)==null&&local.LuckyPanel==null,"Scene reload preserves the claimed reward and does not reopen Lucky");
                        Check(game.Level.Board.Screws[1].IsDone&&game.User.ScrewDoneCount==1,"Scene reload retains the completed first rod and its receipt counter");
                        game.User.LuckyScrewDoneCount=3;game.User.NormalGetTimes=0;
                        expectedCash+=rodAward;Move(2,3);Next(9,.2);break;
                    case 9:
                        if(!PanelReady(local.LuckyPanel))return;
                        Check(game.User.ScrewDoneCount==2&&Near(game.User.Gold,expectedCash)&&!game.IsSuccess(),"Second real rod reaches Lucky threshold while the third pair remains unsolved");
                        pendingAmount=(float)LocalLuckyRewardPolicy.Pending(game.User)["amount"];
                        Click(local.LuckyPanel.GetButton);expectedCash+=pendingAmount;Next(10,.1);break;
                    case 10:
                        if(local.EarningSequencePanel==null)return;
                        Check(Near(game.User.Gold,expectedCash)&&rewardedRequests==2&&interstitialRequests==0,"Ordinary Claim grants only its base amount through the real Button and respects the configured ad counter");
                        Capture("lucky-base-receipt",local.EarningSequencePanel.gameObject);Next(11,.2);break;
                    case 11:
                        if(!BoardReady()||local.EarningSequencePanel!=null||local.LuckyPanel!=null)return;
                        Check(LocalLuckyRewardPolicy.Pending(game.User)==null&&game.User.Coin==0,"Ordinary receipt also closes automatically without adding diamonds");
                        game.User.LuckyScrewDoneCount=3;
                        expectedCash+=rodAward;Move(4,5);Next(12,.2);break;
                    case 12:
                        if(!PanelReady(local.SuccessPanel))return;
                        Check(game.User.Level==4&&game.User.LocalPendingReward!=null&&game.User.ScrewDoneCount==3,"Third real transfer completes the board and opens its level settlement");
                        Check(local.LuckyPanel==null&&LocalLuckyRewardPolicy.Pending(game.User)==null,"A simultaneous Lucky count threshold cannot steal or overlay board settlement");
                        Check(Near(game.User.Gold,expectedCash)&&game.User.Coin==0&&
                            (double)game.User.LocalPendingReward["diamonds"]==0&&!local.ShouldAllowDiamondRewards,"Third-level settlement remains cash only despite User.Level already advancing to four");
                        var saved=OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
                        Check(saved.Level==4&&saved.LocalPendingReward!=null&&string.IsNullOrEmpty(saved.LevelInfo),"Settlement checkpoint never serializes the old completed board under the next level");
                        Capture("third-level-cash-only-settlement",local.SuccessPanel.gameObject);Next(13,1);break;
                    case 13:
                        recoveryCase=0;SeedRecoveryFixture();Reload(20);break;
                    case 20:
                        if(!RecoveryReady())return;
                        CheckRecovery(false);
                        // The runtime recovery save must already contain both
                        // inventory and the receipt marker before this reload.
                        Reload(21);break;
                    case 21:
                        if(!RecoveryReady())return;
                        CheckRecovery(true);
                        Capture("interrupted-rod-recovery-"+recoveryCase,local.SuccessPanel==null?null:local.SuccessPanel.gameObject);
                        Next(22,.4);break;
                    case 22:
                        recoveryCase++;
                        if(recoveryCase<5){SeedRecoveryFixture();Reload(20);}
                        else Finish(0);
                        break;
                    case 100:
                        Check(StyleOnly&&rewardedRequests==0&&interstitialRequests==0&&!local.LuckyAdPending,
                            "Style acceptance opens Lucky without requesting or clicking any ad");
                        Check(Near(game.User.Gold,expectedCash)&&game.User.Coin==0&&local.LuckyPanel!=null&&
                            !((bool)LocalLuckyRewardPolicy.Pending(game.User)["claimed"]),
                            "Style acceptance preserves the unclaimed Lucky reward and only the real rod-completion cash");
                        Finish(0);break;
                }
            }
            catch(Exception exception){Fail(exception);}
        }
        static bool RecoveryReady()=>recoveryCase>=3?PanelReady(local.SuccessPanel):BoardReady()&&EntriesSettled();
        static void SeedRecoveryFixture()
        {
            // Isolated interrupted-checkpoint fixtures. No production callback
            // is invoked to simulate recovery; the next scene load must do it.
            bool solved=recoveryCase==3,pending=recoveryCase==4,legacy=recoveryCase==2;
            int level=recoveryCase==0||solved?3:4;
            int owed=recoveryCase==1||pending?2:1;
            bool twoCompleted=recoveryCase==1||legacy||pending;
            var rods=solved?new[]{Rod(1,1,1,1),Rod(2,2,2,2),Rod(3,3,3,3)}:
                new[]{Rod(),Rod(1,1,1,1),twoCompleted?Rod():Rod(2),twoCompleted?Rod(2,2,2,2):Rod(2,2,2),Rod(3),Rod(3,3,3)};
            var board=new OriginalBoardState(new LevelData{B=rods},Resources.Load<OriginalLayoutSettings>("Configuration/OriginalLayout"));
            var user=game.User;
            user.Level=level;user.LevelSeed=0;user.Gold=700;user.Coin=12;
            user.ScrewDoneCount=legacy?300:100+owed;recoveryCount=user.ScrewDoneCount;
            user.LuckyScrewDoneCount=0;user.LuckyDrawScrewDoneTimes=0;
            user.IsShowCoin=level>=4&&!pending;user.IsCompleteShowCoinRewardHint=level>=4;
            user.IsCompleteExtraGoldGuide=true;user.IsCompleteRecordGuide=true;user.IsCompleteCoinNewPeopleReward=true;
            user.LocalWithdrawalData=new JObject
            {
                ["tutorialBalanceApplied"]=true,["tutorialBalanceAmount"]=500,["tutorialBalanceNoticeAcknowledged"]=true,
                ["earningFlow"]=new JObject{["activationComplete"]=true}
            };
            if(!legacy)LocalLuckyRewardPolicy.State(user)["lastProcessedScrewDone"]=100;
            user.LocalPendingReward=pending?new JObject{{"country","US"},{"amount",1.25},{"diamonds",0},{"nextLevel",4}}:null;
            user.LevelInfo=OriginalBoardSnapshotJson.Write(OriginalBoardSnapshot.Capture(board,new List<OriginalMoveRecord>()));
            float perCoin=LocalLuckyRewardPolicy.Number(user.GoldRewardTargetS2CData["zs_ea"]);
            recoveryCash=700+(legacy?0:owed*rodAward);
            recoveryDiamonds=12+(!legacy&&level>=4&&!pending?owed*perCoin:0);
            audio.UserState.Store.SaveData(false,null);
        }
        static void CheckRecovery(bool secondLoad)
        {
            string context="Recovery case "+recoveryCase+", "+(secondLoad?"second restart":"first restart");
            Check(Near(game.User.Gold,recoveryCash)&&Math.Abs(game.User.Coin-recoveryDiamonds)<.0001,
                context+": persisted missing rod awards are credited exactly once; legacy history receives none");
            Check(game.User.ScrewDoneCount==recoveryCount&&
                (int?)LocalLuckyRewardPolicy.State(game.User)["lastProcessedScrewDone"]==recoveryCount,
                context+": completed and paid rod counters reconcile without creating another completion");
            Check(local.LuckyPanel==null&&LocalLuckyRewardPolicy.Pending(game.User)==null&&local.EarningSequencePanel==null,
                context+": restoring rod inventory does not replay Lucky or an earning receipt");
            var saved=OriginalUserDataJson.Read(PlayerPrefs.GetString(OriginalUserStore.Key),Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
            Check(Near(saved.Gold,recoveryCash)&&Math.Abs(saved.Coin-recoveryDiamonds)<.0001&&
                (int?)LocalLuckyRewardPolicy.State(saved)["lastProcessedScrewDone"]==recoveryCount,
                context+": production startup persisted inventory and receipt marker in the same checkpoint");
            if(recoveryCase>=3)
            {
                Check(local.SuccessPanel!=null&&game.User.Level==4&&game.User.LocalPendingReward!=null&&
                    (int)game.User.LocalPendingReward["nextLevel"]==4&&(double)game.User.LocalPendingReward["diamonds"]==0,
                    context+": solved third-level board or its pending checkpoint opens the same cash-only settlement without another level advance");
                Check(saved.Level==4&&saved.LocalPendingReward!=null&&string.IsNullOrEmpty(saved.LevelInfo),
                    context+": pending settlement stores no previous completed board under the next level");
                if(recoveryCase==4)Check(Near((float)game.User.LocalPendingReward["amount"],1.25f),
                    context+": an existing pending settlement amount is preserved exactly");
            }
            else Check(game.User.Level==(recoveryCase==0?3:4)&&game.User.LocalPendingReward==null&&local.SuccessPanel==null&&!game.IsSuccess(),
                context+": a partially completed board stays playable at its original level");
        }
        static void Move(int from,int to)=>AfterCapture(()=>MoveNow(from,to));
        static void MoveNow(int from,int to)
        {
            Physics.SyncTransforms();
            ScrewOperation ClickRod(int index)=>game.TryOperateAtScreenPoint(game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(index).Bounds.bounds.center));
            Check(ClickRod(from).Kind==ScrewOperationKind.Ready&&ClickRod(to).Kind==ScrewOperationKind.Moved,"World camera physics input performs transfer "+from+" to "+to);
        }
        static bool Near(float actual,float expected)=>Mathf.Abs(actual-expected)<.001f;
        static void Next(int value,double delay=.7)
        {
            phase=value;
            readyAt=Math.Max(EditorApplication.timeSinceStartup,deferredUiAction!=null?uiMutationAt:0)+Math.Max(.2,delay);
        }
        static void Reload(int next){ClearReferences();SceneManager.LoadScene("LuoSiSortGame");Next(next,.5);}
        static Rect Bounds(RectTransform rect)
        {
            var canvas=rect.GetComponentInParent<Canvas>();
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]),max=min;
            for(int i=1;i<4;i++){Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,corners[i]);min=Vector2.Min(min,point);max=Vector2.Max(max,point);}
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static void AfterCapture(Action action)
        {
            if(deferredUiAction!=null)throw new InvalidOperationException("A screenshot is already awaiting its next UI action.");
            if(EditorApplication.timeSinceStartup<uiMutationAt||Time.frameCount<=captureFrame)deferredUiAction=action;
            else action();
        }
        static void Click(Button button)=>AfterCapture(()=>ClickNow(button));
        static void ClickNow(Button button)
        {
            Check(button!=null&&button.gameObject.activeInHierarchy&&button.IsInteractable(),"Live Button is interactable",false);
            Canvas.ForceUpdateCanvases();
            var pointer=new PointerEventData(EventSystem.current){position=Bounds((RectTransform)button.transform).center,button=PointerEventData.InputButton.Left};
            Hits.Clear();EventSystem.current.RaycastAll(pointer,Hits);
            var target=Hits.Count==0?null:ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits[0].gameObject);
            Check(target==button.gameObject,"Unity UI raycast reaches "+button.name);
            Check(ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler),"Button pointer click dispatched",false);
        }
        static void CheckScrewDecoration(OriginalSuccessPanel panel)
        {
            Canvas.ForceUpdateCanvases();
            var decoration=panel.Title.Find("ScrewDecoration")?.GetComponent<Image>();
            Check(decoration!=null&&decoration.isActiveAndEnabled&&decoration.sprite!=null&&decoration.color.a>=.99f,
                "Lucky header contains its visible three-screw decoration sprite");
            Check(decoration.preserveAspect&&decoration.type==Image.Type.Simple,
                "Screw decoration preserves its artwork proportions");
            Check(!decoration.raycastTarget&&decoration.GetComponent<Button>()==null,
                "Static screw decoration cannot intercept UI clicks");
            var ribbon=panel.Title.Find("Ribbon")?.GetComponent<Image>();
            Check(ribbon!=null&&ribbon.isActiveAndEnabled&&ribbon.sprite!=null,
                "Lucky header retains its visible wooden ribbon inside the animation container");
            Rect bounds=Bounds(decoration.rectTransform),banner=Bounds(ribbon.rectTransform);
            Check(bounds.xMin>=-1&&bounds.yMin>=-1&&bounds.xMax<=Screen.width+1&&bounds.yMax<=Screen.height+1,
                "Complete screw decoration stays inside the actual viewport");
            Check(bounds.center.y>banner.yMax,
                "Screw decoration sits above the wooden title ribbon");
            bool foundTitle=false;
            foreach(var label in panel.Title.GetComponentsInChildren<TMP_Text>())
            {
                if(!label.isActiveAndEnabled||string.IsNullOrWhiteSpace(label.text))continue;
                label.ForceMeshUpdate();
                Rect glyphs=GlyphBounds(label);
                Check(!label.isTextOverflowing&&!label.isTextTruncated,
                    "Lucky title text is complete after adding the decoration");
                Check(bounds.yMin>=glyphs.yMax+2,
                    "Screw decoration does not cover the visible title glyphs");
                foundTitle=true;
            }
            Check(foundTitle,"Lucky header retains its title text");
        }
        static Rect GlyphBounds(TMP_Text text)
        {
            var canvas=text.GetComponentInParent<Canvas>();
            Camera camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                TMP_CharacterInfo glyph=text.textInfo.characterInfo[i];
                if(!glyph.isVisible)continue;
                Vector3[] corners={glyph.bottomLeft,glyph.topLeft,glyph.topRight,glyph.bottomRight};
                foreach(Vector3 corner in corners)
                {
                    Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(corner));
                    min=Vector2.Min(min,point);max=Vector2.Max(max,point);
                }
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static void Capture(string name,GameObject panel)
        {
            Canvas.ForceUpdateCanvases();
            if(panel!=null)foreach(var label in panel.GetComponentsInChildren<TMP_Text>())
            {
                if(!label.isActiveAndEnabled||string.IsNullOrEmpty(label.text)||label.canvasRenderer.GetAlpha()<.01f)continue;
                label.ForceMeshUpdate();Rect bounds=Bounds(label.rectTransform);
                Check(!label.isTextOverflowing,"Text fits its prefab rectangle: "+label.name);
                Check(bounds.xMin>=-1&&bounds.yMin>=-1&&bounds.xMax<=Screen.width+1&&bounds.yMax<=Screen.height+1,"Text stays inside viewport: "+label.name);
            }
            string file=Path.GetFullPath(Output+"/"+name+".png");
            Captures.Add(new JObject{{"image",file},{"phase",phase},{"cash",game.User.Gold},{"width",Screen.width},{"height",Screen.height}});
            ScreenCapture.CaptureScreenshot(file);
            // ScreenCapture writes at the end of a rendered frame. Keep this
            // exact viewport/UI alive until that frame has had time to finish.
            uiMutationAt=EditorApplication.timeSinceStartup+.2;captureFrame=Time.frameCount;
        }
        static void Check(bool value,string message,bool record=true){if(!value)throw new InvalidOperationException(message);if(record)Checks.Add(message);}
        static void Fail(Exception exception){error=exception.ToString();Debug.LogException(exception);Finish(1);}
        static void Finish(int result)
        {
            if(SessionState.GetBool(Key+".Finish",false))return;
            if(result==0)foreach(JObject capture in Captures)
                if(!File.Exists((string)capture["image"])||File.GetLastWriteTimeUtc((string)capture["image"])<began.AddSeconds(-2))
                {result=1;error="Missing fresh screenshot: "+capture["image"];break;}
            SessionState.SetBool(Key,false);SessionState.SetBool(Key+".Finish",true);SessionState.SetInt(Key+".Result",result);
            deferredUiAction=null;
            SessionState.SetString(Key+".Report",new JObject
            {
                ["fixture"]=SessionState.GetString(Key+".Fixture",""),["phase"]=phase,["checks"]=Checks,["captures"]=Captures,["error"]=error,
                ["styleOnly"]=StyleOnly,
                ["verification"]=StyleOnly?"Unity Play, production scene and Lucky prefab, real world physics transfer, two viewports; no claim or ad clicks":
                    "Unity Play, production scene and prefabs, isolated saved board, real world physics moves and Button pointer events; explicit test ad callbacks, no live ad network or payment service"
            }.ToString());
            EditorApplication.update-=Tick;SceneManager.sceneLoaded-=ConfigureCountry;
            EditorApplication.playModeStateChanged-=OnPlayModeChanged;EditorApplication.playModeStateChanged+=OnPlayModeChanged;
            if(EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.ExitPlaymode();else CompleteFinish();
        }
        static void OnPlayModeChanged(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredEditMode)CompleteFinish();}
        static void CompleteFinish()
        {
            if(!SessionState.GetBool(Key+".Finish",false)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            int result=SessionState.GetInt(Key+".Result",1);var report=JObject.Parse(SessionState.GetString(Key+".Report","{}"));
            try
            {
                bool existed;string saved;
                using(var reader=new BinaryReader(File.OpenRead(Backup))){existed=reader.ReadBoolean();saved=reader.ReadString();}
                OriginalPreferenceFixture.Restore();
                if(File.Exists(Backup)||PlayerPrefs.HasKey(OriginalUserStore.Key)!=existed||existed&&PlayerPrefs.GetString(OriginalUserStore.Key)!=saved)
                    throw new InvalidOperationException("Original user preferences were not restored.");
                report["originalPreferencesRestored"]=true;
            }
            catch(Exception exception){Debug.LogException(exception);report["restoreError"]=exception.ToString();result=1;}
            report["status"]=result==0?"PASS":"FAIL";report["utc"]=DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/report.json",report.ToString());
            SessionState.SetBool(Key+".Finish",false);EditorApplication.playModeStateChanged-=OnPlayModeChanged;
            Debug.Log((StyleOnly?"LOCAL_LUCKY_HEADER_STYLE_PLAY_":"LOCAL_LUCKY_REWARD_PLAY_")+(result==0?"PASS":"FAIL"));EditorApplication.Exit(result);
        }
    }
}
