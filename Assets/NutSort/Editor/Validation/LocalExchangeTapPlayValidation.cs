using System;
using System.Collections.Generic;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class LocalExchangeTapPlayValidation
    {
        private const string Key="NutSort.ExchangeTapPlay";
        private static double timeout;
        private static float waitUntil;
        private static int phase,source,expectedIndex;
        private static OriginalGameScene game;
        private static OriginalStartupFlow startup;
        private static LocalGameplayController local;
        private static NutSlot[] before;
        private static readonly List<RaycastResult> hits=new List<RaycastResult>();
        static LocalExchangeTapPlayValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;}}
        public static void Run()
        {
            OriginalPreferenceFixture.Begin("{\"IsCompleteRecordGuide\":true,\"Level\":4,\"NewGameplayUnlockIndex\":1,\"ExchangeCount\":2}");
            EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");PlayModeWindow.SetCustomRenderingResolution(480,1040,"Exchange tap ordering");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static Vector2 WorldPoint(){Physics.SyncTransforms();return game.WorldCamera.WorldToScreenPoint(game.Level.GetScrew(source).Bounds.bounds.center);}
        private static Vector2 UIPosition(Button button)
        {
            var canvas=button.GetComponentInParent<Canvas>();return RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,button.transform.position);
        }
        private static PointerEventData RaycastUI(Vector2 position)
        {
            Canvas.ForceUpdateCanvases();var data=new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left,eligibleForClick=true,clickCount=1};
            hits.Clear();EventSystem.current.RaycastAll(data,hits);Check(hits.Count>0,"UI raycaster found a target");return data;
        }
        private static void ClickUI(Vector2 position,Button expected)
        {
            var data=RaycastUI(position);var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Check(handler==expected.gameObject,"Topmost UI raycast resolves the expected standard Button: "+expected.name);
            ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);
        }
        private static void RememberOrder()
        {
            var rod=game.Level.Board.Screws[source];before=(NutSlot[])rod.Slots.Clone();var top=new List<NutSlot>();rod.GetTopSame(top);
            int count=0;foreach(var slot in rod.Slots)if(slot.Nut!=null)count++;expectedIndex=count-top.Count;
        }
        private static void CheckRotation(int remaining)
        {
            Check(game.Level.Board.Screws[source].Slots[0]==before[expectedIndex]&&game.User.ExchangeCount==remaining,"One native rotation and one inventory cost per release");
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                Check(EditorApplication.timeSinceStartup<timeout,"Exchange tap timeout phase "+phase);
                if(game==null)game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                if(startup==null)startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                if(game==null||startup==null||startup.LocalGameplay==null||game.IsRestarting||game.InputBlocked||!game.IsInitDone)return;
                local=startup.LocalGameplay;if(Time.time<waitUntil)return;
                if(phase==0)
                {
                    source=-1;for(int i=0;i<game.Level.Board.Screws.Length;i++)
                    {
                        var rod=game.Level.Board.Screws[i];if(rod.IsLocked||rod.IsNull||rod.IsDone||rod.IsHidden||rod.IsColorMask||rod.IsDontMove||rod.IsNutColorSame)continue;
                        source=i;break;
                    }
                    Check(source>=0,"Original later board has a mixed selectable rod");
                    ClickUI(UIPosition(local.Bottom.GetOtherItem(3).Display.Click),local.Bottom.GetOtherItem(3).Display.Click);
                    Check(game.IsExchanging,"Actual UI exchange Button enters mode");waitUntil=Time.time+.4f;phase=1;return;
                }
                if(phase==1)
                {
                    RememberOrder();Vector2 point=WorldPoint();
                    // EventSystem handles the same release before game Update.
                    ClickUI(point,local.ExchangeMask);
                    Check(startup.Replay.IsClickMasked&&!local.ExchangeMask.gameObject.activeSelf&&game.IsExchanging,"UI cancel retains original half-second gameplay flag");
                    Check(!game.ModalInputBlocked,"A counted UI click mask is not a registered modal panel");
                    Check(game.TryOperateAtScreenPoint(point).Kind==ScrewOperationKind.ExchangeRequested,"UI-first release still reaches original world exchange");
                    CheckRotation(1);waitUntil=Time.time+.7f;phase=2;return;
                }
                if(phase==2)
                {
                    Check(!game.IsExchanging,"First cancel delay completed");
                    ClickUI(UIPosition(local.Bottom.GetOtherItem(3).Display.Click),local.Bottom.GetOtherItem(3).Display.Click);
                    waitUntil=Time.time+.4f;phase=3;return;
                }
                if(phase==3)
                {
                    RememberOrder();Vector2 point=WorldPoint();
                    // Game Update handles the release before EventSystem.
                    Check(game.TryOperateAtScreenPoint(point).Kind==ScrewOperationKind.ExchangeRequested,"World-first release reaches exchange");
                    ClickUI(point,local.ExchangeMask);CheckRotation(0);
                    Check(!game.ModalInputBlocked&&game.IsExchanging,"World-first UI cancel also retains native window");
                    waitUntil=Time.time+.7f;phase=4;return;
                }
                if(phase==4)
                {
                    startup.Replay.BeginClick();RaycastUI(UIPosition(local.Bottom.Replay));
                    var mask=(GameObject)new SerializedObject(startup).FindProperty("clickMask").objectReferenceValue;
                    Check(hits[0].gameObject==mask&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==null,"Counted mask still blocks actual UI raycasts without becoming a modal");
                    Check(!game.ModalInputBlocked,"UI-only click shield does not block core ray input");
                    waitUntil=Time.time+.4f;phase=5;return;
                }
                if(phase==5)
                {
                    ClickUI(UIPosition(local.Bottom.Replay),local.Bottom.Replay);
                    Check(startup.Replay.Panel!=null&&game.ModalInputBlocked,"Registered replay remains a true world-input blocker");
                    Check(game.TryOperateAtScreenPoint(WorldPoint()).Kind==ScrewOperationKind.Ignored,"World click rejected beneath replay");
                    waitUntil=Time.time+.7f;phase=6;return;
                }
                if(phase==6)
                {
                    Check(!startup.Replay.IsClickMasked&&game.ModalInputBlocked,"Replay blocks after counted UI mask expires");
                    ClickUI(UIPosition(startup.Replay.Panel.ContinueButton),startup.Replay.Panel.ContinueButton);
                    waitUntil=Time.time+.7f;phase=7;return;
                }
                Check(startup.Replay.Panel==null&&!game.ModalInputBlocked&&game.User.ExchangeCount==0,"Closing replay restores input and preserves exactly two exchange costs");
                Debug.Log("NUT_EXCHANGE_TAP_PLAY_PASS actual GraphicRaycaster/Button pointer-click dispatch plus world camera ray; UI-first and world-first exchange releases each rotate/spend once; counted UI mask still intercepts UI but is not a modal; registered replay blocks world before/after mask expiry and releases on close.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
