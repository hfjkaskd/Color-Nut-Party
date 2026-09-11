using System;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using UnityEngine;
using UnityEditor;
namespace NutSort.Validation
{
 public static class OriginalDiamondInputValidation
 {
  public static void Run()
  {
   try {
    var user=new OriginalUserLocalData(Resources.Load<OriginalUserDefaults>("Configuration/OriginalUserDefaults"));
    user.LocalPendingReward=JObject.Parse(@"{""amount"":3.01,""diamonds"":81.19,""diamondSource"":""temporary-reference-screenshot-not-server-rule""}");
    LocalCashSettlement.AddConfiguredDiamonds(user);
    Check((double)user.LocalPendingReward["diamonds"]==0,"Remove pending temporary award");
    var response=JObject.Parse(@"{""kinetic_state"":""ERR-OK"",""kinetic_data"":{""hg_zs_amt"":""12.34""}}");
    LocalCashSettlement.ApplyOriginalDiamonds(user,response);
    LocalCashSettlement.AddConfiguredDiamonds(user);
    Check((double)user.LocalPendingReward["diamonds"]==12.34,"Preserve supplied response");
    response["kinetic_data"]["hg_zs_amt"]="NaN";
    bool rejected=false;try{LocalCashSettlement.ApplyOriginalDiamonds(user,response);}catch(InvalidOperationException){rejected=true;}
    Check(rejected&&(double)user.LocalPendingReward["diamonds"]==12.34,"Invalid response leaves pending unchanged");
    Check(LocalCashSettlement.Claim(user)&&!LocalCashSettlement.Claim(user)&&user.Coin==12.34,"One-time server amount claim");
    user.LocalPendingReward=JObject.Parse(@"{""amount"":1}");
    response["kinetic_data"]["hg_zs_amt"]="";
    LocalCashSettlement.ApplyOriginalDiamonds(user,response);
    Check((double)user.LocalPendingReward["diamonds"]==0,"Empty original response is zero");
    foreach(string country in new[]{"BR","US","ID","IDN"})
    {
     bool id=country=="ID"||country=="IDN";
     user.Level=4;user.Coin=0;
     user.LocalPendingReward=new JObject{{"amount",1},{"country",country},{"nextLevel",4}};
     LocalCashSettlement.AddConfiguredDiamonds(user);
     Check((double)user.LocalPendingReward["diamonds"]==0,"First three completed levels have no sampled diamonds");
     user.Level=5;user.LocalPendingReward=new JObject{{"amount",1},{"country",country},{"nextLevel",5}};
     LocalCashSettlement.AddConfiguredDiamonds(user);
     Check((double)user.LocalPendingReward["diamonds"]>=(id?12000:80)&&(double)user.LocalPendingReward["diamonds"]<=(id?18000:120),"Regional captured sample and Brazil fallback");
     user.Coin=id?9800000:49000;
     user.LocalPendingReward=new JObject{{"amount",1},{"country",country},{"nextLevel",6}};
     LocalCashSettlement.AddConfiguredDiamonds(user);
     double pendingDiamond=(double)user.LocalPendingReward["diamonds"];
     Check(pendingDiamond>=(id?200:.8)&&pendingDiamond<=(id?450:1.2),"Inferred near-target interval");
     user.Coin=0;LocalCashSettlement.AddConfiguredDiamonds(user);
     Check((double)user.LocalPendingReward["diamonds"]==pendingDiamond,"Existing pending amount never rerolls");
    }
    var cfg=JObject.Parse(Resources.Load<TextAsset>("Configuration/LocalDiamondSettlement").text);
    foreach(string country in new[]{"BR","ID"})
    {
     var bands=(JArray)cfg["profiles"][country];
     for(int i=0;i<bands.Count;i++)
     {
      var b=(JObject)bands[i];double edge=(double)b["balance"],selected;
      double low=LocalDiamondRewardPolicy.Calculate(cfg,country,4,edge,(a,z)=>a,out selected);
      double high=LocalDiamondRewardPolicy.Calculate(cfg,country,4,edge,(a,z)=>z,out selected);
      Check(Math.Abs(low-(double)b["minimum"])<.0001&&Math.Abs(high-(double)b["maximum"])<.0001,"Range endpoints");
      Check(selected==edge,"Threshold inclusive on lower side");
      if(i>0)
      {
       LocalDiamondRewardPolicy.Calculate(cfg,country,4,edge-1,(a,z)=>a,out selected);
       Check(selected==(double)bands[i-1]["balance"],"Boundary minus one selects previous interval");
      }
     }
    }
    Debug.Log("NUT_ORIGINAL_DIAMOND_INPUT_PASS");EditorApplication.Exit(0);
   }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
  }
  private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
 }
}
