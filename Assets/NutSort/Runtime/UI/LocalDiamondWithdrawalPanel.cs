using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class LocalDiamondWithdrawalPanel:MonoBehaviour
    {
        [SerializeField] private Button TXBtn,Close;
        [SerializeField] private TMP_Text TXTip,GoldValue,CoinValue,TipValue,TipBarValue,IitemTip;
        [SerializeField] private Image TipBar;
        [SerializeField] private Button[] options;
        [SerializeField] private GameObject[] selectionFrames;
        [SerializeField] private TMP_Text[] amounts;
        [SerializeField] private OriginalReplayPanel.Label[] labels;
        [SerializeField] private string selectedSpritePath,normalSpritePath,serviceUnavailable;
        private OriginalUserLocalData user;private OriginalTables tables;private string language;
        private OriginalGoldFormatter formatter;private JArray rows;private double rate;
        private Sprite selectedSprite,normalSprite;private int index;
        public Button CloseButton=>Close;
        public Button WithdrawalButton=>TXBtn;
        public Button[] Options=>options;
        public TMP_Text ProgressLabel=>TipBarValue;
        public TMP_Text BalanceLabel=>GoldValue;
        public int SelectedIndex=>index;
        public void Init(OriginalUserLocalData user,OriginalTables tables,string language,OriginalGoldFormatter formatter,Func<bool> canClick,Action closed,Action sound,Action<string> showFeedback)
        {
            this.user=user;this.tables=tables;this.language=language;this.formatter=formatter;
            rows=(JArray)user.GoldRewardTargetS2CData["bear_zs_list"];
            rate=double.Parse((string)user.GoldRewardTargetS2CData["bear_rates"],CultureInfo.InvariantCulture);
            selectedSprite=Resources.Load<Sprite>(selectedSpritePath);normalSprite=Resources.Load<Sprite>(normalSpritePath);
            foreach(var label in labels)label.Text.text=tables.Text.GetText(label.Id,language);
            CoinValue.text=formatter.FormatCoin(user.Coin);GoldValue.text=formatter.Format((float)(user.Coin/rate));
            TipValue.text=formatter.FormatCoin(rate)+"="+formatter.Format(1);
            for(int i=0;i<options.Length;i++)
            {
                int slot=i;options[i].gameObject.SetActive(i<rows.Count);
                if(i>=rows.Count)continue;
                amounts[i].text=formatter.Format((float)rows[i]["psi_value"]);
                options[i].onClick.RemoveAllListeners();options[i].onClick.AddListener(()=>{if(!canClick())return;Select(slot);sound();});
            }
            Close.onClick.RemoveAllListeners();Close.onClick.AddListener(()=>{if(!canClick())return;closed();sound();});
            TXBtn.onClick.RemoveAllListeners();TXBtn.onClick.AddListener(()=>{if(!canClick())return;if(user.Coin>=Target())showFeedback(serviceUnavailable);else{Select(index);showFeedback(IitemTip.text);}sound();});
            Select(0);
        }
        private double Target()=>double.Parse((string)rows[index]["caliper_psi"],CultureInfo.InvariantCulture);
        private void Select(int value)
        {
            index=value;double target=Target();float payout=(float)rows[index]["psi_value"];
            string amount=formatter.Format(payout),remaining=formatter.Format((float)(Math.Max(0,target-user.Coin)/rate));
            TXTip.text=tables.Text.GetText(101,language,amount);
            IitemTip.text=tables.Text.GetText(125,language,amount,remaining);
            TipBar.fillAmount=Mathf.Clamp01((float)(user.Coin/target));
            TipBarValue.text=formatter.Format((float)(Math.Min(user.Coin,target)/rate))+"/"+amount;
            for(int i=0;i<options.Length;i++){((Image)options[i].targetGraphic).sprite=i==index?selectedSprite:normalSprite;selectionFrames[i].SetActive(i==index);}
        }
    }
}
