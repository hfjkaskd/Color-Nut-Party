using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class OriginalFailurePanelView : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinValue;
        [SerializeField] private Button restart,revive;
        [SerializeField] private int defaultMaximum;
        [SerializeField] private string grayPath,loseSound,clickSound;
        [SerializeField] private float queueDelay;
        private OriginalPanelActionQueue actionQueue;
        private Action<float,Action> schedule;
        private Action hidden;
        private OriginalTables tables;
        private string language;
        private OriginalFailurePanelData data;
        private Func<bool> gate;
        private Action<string> audio;
        private Action<Action> restartAction;
        private Action clearFailure,requestRevive,close;
        private readonly List<Image> images=new List<Image>();
        private static Material gray;
        public Button Restart => restart;
        public Button Revive => revive;
        public TMP_Text CoinValue => coinValue;
        public void Bind(OriginalUserLocalData user,OriginalTables tables,string language,Func<bool> gate,
            Action<string> audio,Action<Action> restartAction,Action clearFailure,Action requestRevive,Action close)
        {
            this.tables=tables;this.language=language;this.gate=gate;this.audio=audio;
            this.restartAction=restartAction;this.clearFailure=clearFailure;this.requestRevive=requestRevive;this.close=close;
            data=new OriginalFailurePanelData(user,value=>coinValue.text=value,SetGray,defaultMaximum);
        }
        public void Init()
        {
            // The source prefab has no "main" child: its optional base tween is absent.
            foreach(var label in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if(!label.name.StartsWith("label_"))continue;
                string token=label.name.Trim().Split('_')[1];
                if(!int.TryParse(token,out int id)){Debug.LogError("ToInt fail s:"+token);id=0;}
                label.text=tables.Text.GetText(id,language);
            }
            restart.transition=Selectable.Transition.ColorTint;
            restart.onClick.RemoveAllListeners();restart.onClick.AddListener(RestartClicked);
            revive.transition=Selectable.Transition.ColorTint;
            revive.onClick.RemoveAllListeners();revive.onClick.AddListener(ReviveClicked);
            audio(loseSound);
        }
        public void Refresh() {data.Refresh();}
        public void BindHide(OriginalPanelActionQueue actionQueue, Action<float,Action> schedule, Action hidden = null)
        {
            this.actionQueue=actionQueue;this.schedule=schedule;this.hidden=hidden;
        }
        // BaseLSSPanel.HideLssPanel (0x9C5218). The queued callback belongs
        // to the manager, so destroying this view must not cancel it.
        public void Hide()
        {
            hidden?.Invoke();
            schedule(queueDelay,actionQueue.Dequeue);
        }
        private void SetGray(bool value)
        {
            if(gray==null)gray=Resources.Load<Material>(grayPath);
            revive.GetComponentsInChildren(false,images);
            foreach(var image in images)image.material=value?gray:null;
        }
        private void RestartClicked()
        {
            if(!gate())return;
            LocalAnalyticsPopup.Action(this,"restart");
            restartAction(close);audio(clickSound);
        }
        private void ReviveClicked()
        {
            if(!gate())return;
            clearFailure();requestRevive();audio(clickSound);
        }
    }
}
