using System.Collections;
using UnityEngine;
using NutSort.Content;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        [SerializeField] private string recordGuidePrefabPath, recordGuideCountry;
        private OriginalRecordGuidePanel recordGuide;
        private bool welcomePending;
        public OriginalRecordGuidePanel RecordGuide => recordGuide;

        private void BeginWelcomeOrTeaching()
        {
            if(welcomePending || recordGuide!=null)return;
            if(game.User.IsCompleteRecordGuide)
            {
                RefreshTeaching();
                BeginCoreInitialization();
                return;
            }
            welcomePending=true;
            game.IsCanOperatorScrew=false;
            game.ModalInputBlocked=true;
            StartCoroutine(OpenWelcome());
        }

        private IEnumerator OpenWelcome()
        {
            while(startup.Loading.IsVisible)yield return null;
            recordGuide=Instantiate(Resources.Load<GameObject>(recordGuidePrefabPath),transform.parent,false)
                .GetComponent<OriginalRecordGuidePanel>();
            LocalAnalytics.Current?.TutorialStep("welcome",0,false);
            recordGuide.Initialize(game.Tables,language,audio.UserState.DisplayCountry(game.Tables),TryWelcomeClick,
                ()=>{game.User.IsCompleteRecordGuide=true;LocalAnalytics.Current?.TutorialStep("welcome",0,true);},WelcomeClosed,startup.Replay.EndClick);
            welcomePending=false;
        }

        private bool TryWelcomeClick()
        {
            if(recordGuide==null || recordGuide.Closing || recordGuide.StartContainer.localScale.x<1f ||
                game.InputBlocked || game.IsRestarting || startup.Replay.IsClickMasked)return false;
            return startup.Replay.BeginClick();
        }

        private void WelcomeClosed()
        {
            Destroy(recordGuide.gameObject);
            recordGuide=null;
            welcomePending=true;
            StartCoroutine(ContinueAfterWelcome());
        }

        private IEnumerator ContinueAfterWelcome()
        {
            // Retire the modal before teaching and keep the button release off the board.
            yield return null;
            welcomePending=false;
            game.ModalInputBlocked=HasLocalModal||startup.Replay.Panel!=null;
            RefreshTeaching();
            BeginCoreInitialization();
            game.SaveUserData();
        }
    }
}
