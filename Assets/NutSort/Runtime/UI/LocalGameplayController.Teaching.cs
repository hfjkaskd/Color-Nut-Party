using NutSort.Gameplay;
using NutSort.Content;
using UnityEngine;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string teachingPrefabPath,localFinalTeachingText,localTeachingCaption;
        private OriginalNewbieGuideView teachingPanel;
        private OriginalPopTip teachingClickFeedback;
        private OriginalTeachingFlow teaching;
        public OriginalNewbieGuideView TeachingPanel=>teachingPanel;

        private void RefreshTeaching()
        {
            if(game.PlayerLevel>=2)
            {
                // SetTeach's later-level branch clears the teaching override.
                // Local mode skips the subsequent account/reward guide branches.
                game.IsCanOperatorScrew=false;
                CloseTeaching();
                return;
            }
            if(teachingPanel==null)
            {
                teachingPanel=Instantiate(Resources.Load<GameObject>(teachingPrefabPath),transform.parent,false).GetComponent<OriginalNewbieGuideView>();
                teachingPanel.BindTeaching(game.Tables,language,value=>game.IsCanOperatorScrew=value,CloseTeaching);
                teachingPanel.InitializeLabels(game.Tables,language);
                teachingPanel.InitializeInteractions(game.User,false,TryTeachingClick,startup.Replay.EndClick);
                teachingPanel.BindMask(game.ScheduleDelay);
                // This teaching-only instance lets the guarded HUD Buttons explain
                // rejected clicks. Other guide instances retain their input shield.
                teachingPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
                teaching=new OriginalTeachingFlow(game.User,teachingPanel,()=>game.NewLevelMode);
                game.ModalInputBlocked=true;
            }
            teaching.Run();
            LocalAnalytics.Current?.TutorialStep("teaching_"+game.User.LevelSeed,game.User.LevelSeed+1,false);
            // Source text 151 promises withdrawal. The approved local SDK-skip
            // mode substitutes this serialized local progression message only.
            if(teachingPanel!=null&&game.User.LevelSeed>2)teachingPanel.Tip.text=LocalUiLocalization.Text(localFinalTeachingText);
        }
        private bool TryTeachingClick()
        {
            return game.IsInitDone&&!game.InputBlocked&&!game.IsRestarting&&startup.Replay.BeginClick();
        }
        private bool RejectTeachingHudClick()
        {
            if(teachingPanel==null)return false;
            if(startup.Replay.IsClickMasked)return true;
            // Keep the tutorial lock, but explain a rejected standard Button click
            // using the current localized/configured instruction and existing tip.
            if(teachingClickFeedback==null)
            {
                teachingClickFeedback=Instantiate(Resources.Load<GameObject>(popTipPrefabPath),startup.TopCanvas,false).GetComponent<OriginalPopTip>();
                teachingClickFeedback.Show(teachingPanel.Tip.text,game.ScheduleDelay);
            }
            return true;
        }
        private void CloseTeaching()
        {
            // Its existing lifetime callback still owns destruction. Hide it now
            // so that callback never runs against an already-destroyed component.
            if(teachingClickFeedback!=null){teachingClickFeedback.gameObject.SetActive(false);teachingClickFeedback=null;}
            if(teachingPanel==null)return;
            Destroy(teachingPanel.gameObject);teachingPanel=null;teaching=null;
            // Source Close does not clear IsCanOperatorScrew. The later-level
            // SetTeach branch above owns that transition.
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
        }
    }
}
