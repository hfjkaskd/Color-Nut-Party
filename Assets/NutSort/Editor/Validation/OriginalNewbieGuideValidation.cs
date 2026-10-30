using System;
using System.IO;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.UI;
using NutSort.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.Validation
{
    [InitializeOnLoad]
    public static class OriginalNewbieGuideValidation
    {
        private const string Key="NutSort.NewbieGuidePlay",PathName="prefabs/panels/NewbieGuidePanel";
        private static double deadline,timeout;
        private static int phase;
        private static OriginalNewbieGuideView view;
        private static OriginalUserLocalData user;
        private static OriginalTeachingFlow teaching;
        private static bool canOperate;
        static OriginalNewbieGuideValidation(){if(SessionState.GetBool(Key,false)){timeout=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;}}
        public static void Validate()
        {
            var p=Resources.Load<GameObject>(PathName);Check(p!=null,"Original guide prefab loads");
            Check(p.GetComponentsInChildren<RectTransform>(true).Length==27,"All 26 original RectTransforms plus the shared screw decoration");
            foreach(var t in p.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"No missing guide scripts");
            Check(p.GetComponentsInChildren<Button>(true).Length==3,"Three original standard Buttons");
            var v=p.GetComponent<OriginalNewbieGuideView>();Check(v.MarkerCount==4 && v.Tip.font!=null,"Four progress markers and original font");
            var mask=new SerializedObject(v.HollowMask);
            Check(mask.FindProperty("Radius").floatValue==25 && mask.FindProperty("TriangleNum").intValue==6 && mask.FindProperty("realtimeRefresh").boolValue,"Original mask parameters");
            Check(mask.FindProperty("inner_trans").objectReferenceValue!=null && v.HollowMask.raycastTarget,"Mask target and original Graphic raycast flag");
            Check(p.GetComponentsInChildren<OriginalGuideLoopMotion>(true).Length==2 && p.GetComponentsInChildren<OriginalRecordPunchRotation>(true).Length==1,"Hand, arrow and Get-marker native animation replacements");
            Check(Resources.Load<Sprite>("Atlas/NewbieGuidePanel/dian")!=null && Resources.Load<Sprite>("Atlas/NewbieGuidePanel/dian1")!=null,"Both original marker sprites");
            Debug.Log("NUT_NEWBIE_GUIDE_VALIDATION_PASS original hierarchy plus screw decoration, standard UI references, mask, progress sprites and native animation components; full guide routing pending.");
        }
        public static void RunPlay()
        {
            OriginalPreferenceFixture.Begin();EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            PlayModeWindow.SetCustomRenderingResolution(480,1040,"Newbie guide validation");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                double now=EditorApplication.timeSinceStartup;Check(now<timeout,"Newbie play timeout");
                if(phase==0)
                {
                    var startup=UnityEngine.Object.FindObjectOfType<OriginalStartupFlow>();
                    var game=UnityEngine.Object.FindObjectOfType<OriginalGameScene>();
                    if(startup==null || startup.MainLevel==null || game==null || game.IsRestarting || game.InputBlocked)return;
                    Validate();view=UnityEngine.Object.Instantiate(Resources.Load<GameObject>(PathName),startup.MainLevel.transform.parent,false).GetComponent<OriginalNewbieGuideView>();
                    user=game.User;view.BindTeaching(game.Tables,"en",v=>canOperate=v,()=>throw new InvalidOperationException("Unexpected teaching close"));
                    teaching=new OriginalTeachingFlow(user,view,()=>false);
                    view.SetHollowMaskVisible(false);view.BindMask(game.ScheduleDelay);
                    view.ShowMask(view.ContinueButton.GetComponent<RectTransform>());
                    phase=1;deadline=now+.7;return;
                }
                if(now<deadline)return;
                if(phase==1)
                {
                    Check(view.HollowMask.gameObject.activeSelf,"Actual scene timer reveals placed mask");
                    var mesh=view.HollowMask.canvasRenderer.GetMesh();Check(mesh!=null && mesh.vertexCount==40,"Actual Graphic renders the native six-segment mesh");
                    Capture("newbie-mask-placed");phase=2;deadline=now+.3;return;
                }
                if(phase==2){user.Level=1;user.LevelSeed=0;Check(teaching.Run() && canOperate,"Real view receives first teaching branch");phase=3;deadline=now+.3;return;}
                if(phase==3){Check(!view.HollowMask.gameObject.activeSelf,"Teaching hides actual hollow mask");Capture("newbie-teach-0");phase=4;deadline=now+.3;return;}
                if(phase==4){user.LevelSeed=3;teaching.Run();phase=5;deadline=now+.3;return;}
                if(phase==5){Capture("newbie-teach-3");phase=6;deadline=now+.3;return;}
                Debug.Log("NUT_NEWBIE_GUIDE_PLAY_PASS actual delayed mask placement, Graphic mesh and original prefab teaching display at seeds zero/three; full ShowGuide, Button routing and production binding remain pending.");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Capture(string name){Directory.CreateDirectory("Library/ValidationCaptures");ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Library/ValidationCaptures/"+name+".png"));}
        private static void Check(bool v,string message){if(!v)throw new InvalidOperationException(message);}
        private static void Finish(int code){OriginalPreferenceFixture.Restore();SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);}
    }
}
