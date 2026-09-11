using NutSort.Gameplay;
using UnityEngine;
namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string teachingPrefabPath,localFinalTeachingText,localTeachingCaption;
        private OriginalNewbieGuideView teachingPanel;
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
                teaching=new OriginalTeachingFlow(game.User,teachingPanel,()=>game.NewLevelMode);
                game.ModalInputBlocked=true;
            }
            teaching.Run();
            // Source text 151 promises withdrawal. The approved local SDK-skip
            // mode substitutes this serialized local progression message only.
            if(teachingPanel!=null&&game.User.LevelSeed>2)teachingPanel.Tip.text=localFinalTeachingText;
        }
        private bool TryTeachingClick()
        {
            return game.IsInitDone&&!game.InputBlocked&&!game.IsRestarting&&startup.Replay.BeginClick();
        }
        private void CloseTeaching()
        {
            if(teachingPanel==null)return;
            Destroy(teachingPanel.gameObject);teachingPanel=null;teaching=null;
            // Source Close does not clear IsCanOperatorScrew. The later-level
            // SetTeach branch above owns that transition.
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
        }
    }
}
