using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class LocalWithdrawalConditionsPanel : MonoBehaviour
    {
        [Serializable] public sealed class Row { public GameObject Root; public TMP_Text Name, Value, Status; }
        [SerializeField] private TMP_Text title, subtitle, notice, actionCaption;
        [SerializeField] private Button action, close;
        [SerializeField] private Row[] rows;
        [SerializeField] private Color completeColor, pendingColor;
        private Func<LocalWithdrawalEligibility> evaluate;
        private Action<LocalWithdrawalEligibility> proceed;
        private JObject text;
        private OriginalTables tables;
        private bool diamonds;
        private OriginalGoldFormatter formatter;
        public Button ActionButton=>action;
        public Button CloseButton=>close;
        public string Notice=>notice.text;
        public Row[] Rows=>rows;
        public void Init(JObject configuration,OriginalTables tables,bool diamonds,OriginalGoldFormatter formatter,
            Func<LocalWithdrawalEligibility> evaluate,Func<bool> canClick,Action<LocalWithdrawalEligibility> proceed,Action closed)
        {
            this.text=configuration;this.tables=tables;this.diamonds=diamonds;this.formatter=formatter;
            this.evaluate=evaluate;this.proceed=proceed;
            title.text=Text("title");subtitle.text=Text(diamonds?"diamondSubtitle":"cashSubtitle");
            action.onClick.RemoveAllListeners();action.onClick.AddListener(()=>{if(!canClick())return;LocalAnalyticsPopup.Action(this,"continue");var result=evaluate();Refresh(result);proceed(result);});
            close.onClick.RemoveAllListeners();close.onClick.AddListener(()=>{if(canClick()){LocalAnalyticsPopup.Action(this,"close");closed();}});
            Refresh(evaluate());
        }
        private void Refresh(LocalWithdrawalEligibility result)
        {
            notice.text=Text(result.Eligible?"ready":"blocked");
            actionCaption.text=Text(result.Eligible?"account":!diamonds&&result.FirstIncomplete=="secondLevel"?"progress":"continue");
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];row.Root.SetActive(i<result.Requirements.Count);
                if(i>=result.Requirements.Count)continue;
                var item=result.Requirements[i];
                bool level=item.Key=="firstLevel"||item.Key=="secondLevel";
                row.Name.text=level?string.Format(CultureInfo.InvariantCulture,Text(item.Key),tables.GetShowLevel((int)item.Target)):Text(item.Key);
                row.Value.text=!item.Known?Text("unknown"):level?
                    string.Format(CultureInfo.InvariantCulture,"{0}/{1}",Math.Min(tables.GetShowLevel(Math.Max(1,(int)item.Current+1))-1,tables.GetShowLevel((int)item.Target)),tables.GetShowLevel((int)item.Target)):
                    item.Key=="balance"?Format(item.Current)+" / "+Format(item.Target):
                    string.Format(CultureInfo.InvariantCulture,"{0:0} / {1:0}",item.Current,item.Target);
                row.Status.text=Text(item.Waived?"waived":item.Complete?"complete":"pending");
                row.Status.color=item.Complete?completeColor:pendingColor;
            }
        }
        private string Text(string key)=>LocalUiLocalization.Text((string)text[key]);
        private string Format(double amount)=>diamonds?formatter.FormatCoin(amount):formatter.Format((float)amount);
    }
}
