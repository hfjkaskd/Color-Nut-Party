using System;
using System.Collections.Generic;
using NutSort.Content;
using UnityEngine;

namespace NutSort.UI
{
    // Authored on each reachable popup prefab. This component only observes visibility and accepted callbacks.
    [DisallowMultipleComponent]
    public sealed class LocalAnalyticsPopup : MonoBehaviour
    {
        [SerializeField] private string popupId;
        [SerializeField] private string entry = "game_flow";
        private string instanceId, closeReason;
        private double shownAt;
        private bool shown;
        private static readonly List<LocalAnalyticsPopup> active=new List<LocalAnalyticsPopup>();
        public string PopupId=>popupId;
        private void OnEnable() { Show(); }
        private void Show()
        {
            if(shown||LocalAnalytics.Current==null||string.IsNullOrEmpty(popupId))return;
            shown=true;instanceId=LocalAnalytics.NewId();shownAt=Time.realtimeSinceStartupAsDouble;closeReason="flow_replaced";
            active.Add(this);
            LocalAnalytics.Current.SetPage(popupId);
            LocalAnalytics.Track("popup_show",LocalAnalytics.Fields("popup_id",popupId,"popup_instance_id",instanceId,"entry",entry));
        }
        public void AcceptedAction(string action,string selected=null,string operation=null)
        {
            Show();if(!shown)return;
            if(action=="close"||action=="cancel")closeReason="user_close";
            LocalAnalytics.Track("popup_action",LocalAnalytics.Fields("popup_id",popupId,"popup_instance_id",instanceId,
                "action_id",action,"selected_id",selected,"operation_id",operation));
        }
        public static void Action(Component owner,string action,string selected=null,string operation=null)
        { if(owner!=null&&owner.TryGetComponent<LocalAnalyticsPopup>(out var popup))popup.AcceptedAction(action,selected,operation); }
        private void OnDisable()
        {
            if(!shown)return;shown=false;
            active.Remove(this);
            LocalAnalytics.Track("popup_close",LocalAnalytics.Fields("popup_id",popupId,"popup_instance_id",instanceId,
                "reason",closeReason,"visible_seconds",Math.Max(0,Time.realtimeSinceStartupAsDouble-shownAt)));
            LocalAnalytics.Current?.SetPage(active.Count>0?active[active.Count-1].popupId:"gameplay");
        }
    }
}
