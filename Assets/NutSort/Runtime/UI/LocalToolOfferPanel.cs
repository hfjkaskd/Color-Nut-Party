using System;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
 public sealed class LocalToolOfferPanel:MonoBehaviour
 {
  [SerializeField] private Button Close,GetBtn;
  [SerializeField] private Image ItemIcon;
  [SerializeField] private GameObject CoinTip;
  [SerializeField] private TMP_Text Title,Tip,claimLabel;
  [SerializeField] private int[] titleIds,tipIds;
  [SerializeField] private string[] iconPaths;
  public Button ClaimButton=>GetBtn;public Button CloseButton=>Close;
  public void Init(int type,OriginalTables tables,string language,Func<bool> canClick,Action claim,Action close)
  {
   int slot=type-2;Title.text=tables.Text.GetText(titleIds[slot],language);Tip.text=tables.Text.GetText(tipIds[slot],language);
   claimLabel.text=tables.Text.GetText(140,language);ItemIcon.sprite=Resources.Load<Sprite>(iconPaths[slot]);CoinTip.SetActive(false);
   GetBtn.onClick.RemoveAllListeners();GetBtn.onClick.AddListener(()=>{if(canClick())claim();});
   Close.onClick.RemoveAllListeners();Close.onClick.AddListener(()=>{if(canClick())close();});
  }
 }
}
