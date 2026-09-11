using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Gameplay;
using NutSort.World;
using UnityEditor;
using UnityEngine;

namespace NutSort.Validation
{
    public static class CountryRewardConfigurationValidation
    {
        public static void Run()
        {
            int code=1;
            try
            {
                var catalog=Resources.Load<CountryRewardCatalog>("Configuration/CountryRewardCatalog");
                Check(catalog!=null,"Serialized catalog imports");
                foreach(string value in new[]{null,"","US","JP","BR","BRA","unknown","  br  "})
                    Check(catalog.Load(value).CountryCode=="BR","Non-Indonesian country defaults to Brazil");
                foreach(string value in new[]{"ID","id","IDN"," idn "})
                    Check(catalog.Load(value).CountryCode=="ID","Indonesian aliases select Indonesia");
                foreach(string country in new[]{"BR","ID"})
                {
                    var config=catalog.Load(country);
                    string source="Docs/RewardEvidence/CountryProbe/new-"+country+"-1-";
                    Check(JToken.DeepEquals(config.CreateRewardDocument(),Read(source+"reward-config.json")["kinetic_data"]),"Full captured reward document: "+country);
                    Check(JToken.DeepEquals(config.CreateDisplayDocument(),Read(source+"display-config.json")["kinetic_data"]),"Full captured display document: "+country);
                    var rules=config.CreateRulesDocument();
                    var decrypted=JObject.Parse(OriginalContentCodec.Decrypt((string)config.CreateRewardDocument()["bear_delay_info"],"octluositxoctluo","873hs61k7q3l3eyk"));
                    Check(JToken.DeepEquals(rules,decrypted)&&JToken.DeepEquals(rules,Read(source+"rules-decoded.json")),"Every decoded band matches original encrypted capture: "+country);
                    Check(((JArray)rules["shuaijianList"]).Count==(country=="ID"?15:13),"Complete tail bands");
                }

                // Exercise the actual session initializer, with a stale persisted reward document.
                OriginalPreferenceFixture.Begin("{\"Level\":4,\"Gold\":557.58,\"Coin\":123,\"GoldRewardTargetS2CData\":{\"gap_day_gates\":7,\"gap_all_gates\":11,\"ext_gap_logs\":3,\"bear_list\":[{\"bear_id\":1,\"caliper_count\":2}],\"bear_zs_list\":[{\"bear_id\":101,\"caliper_count\":4}]}}");
                // Obtain configured user-session defaults from the actual game scene, without reflection.
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
                var session=UnityEngine.Object.FindObjectOfType<OriginalUserSession>(true);
                Check(session!=null,"Scene contains configured user session");
                session.Initialize();
                Check(session.Rewards!=null&&session.Data.GoldRewardTargetS2CData!=null,"Normal initialization publishes selected reward config");
                foreach(string country in new[]{"IDN","US","BR","ID"})
                {
                    session.ConfigureRewardCountry(country);
                    var user=session.Data;
                    bool indonesia=country=="IDN"||country=="ID";
                    Check(user.Gold==557.58f&&user.Coin==123&&user.Level==4,"Selection never changes balance or level");
                    Check((int)user.GoldRewardTargetS2CData["gap_day_gates"]==7&&(int)user.GoldRewardTargetS2CData["gap_all_gates"]==11,"Selection preserves daily/total progress");
                    Check((int)user.GoldRewardTargetS2CData["bear_list"][0]["caliper_count"]==2&&(int)user.GoldRewardTargetS2CData["bear_zs_list"][0]["caliper_count"]==4,"Selection preserves claimed counts by reward ID");
                    for(int level=2;level<=3;level++)
                    {
                        user.Level=level;
                        OriginalItemGetInfo settlement=null;
                        var flow=new OriginalSuccessSettlementFlow(user,()=>{},id=>false,action=>action(),()=>{},(id,info)=>settlement=info);
                        flow.Run(new JObject{{"kinetic_data",new JObject{{"hg_amt",""},{"hg_zs_amt",""}}}});
                        float expected=indonesia?(level==2?400:600):(level==2?.4f:.6f);
                        Check(settlement!=null&&settlement.ItemInfos[0].Count==expected,"Existing settlement flow consumes selected country configuration");
                        Check(user.Gold==557.58f&&user.Coin==123,"Settlement preview grants nothing");
                    }
                    user.Level=4;
                }
                session.Store.SaveData(false,null);
                Check(PlayerPrefs.GetString(OriginalUserStore.Key).Contains("6000000"),"Selected policy is persisted by existing user writer");
                Debug.Log("NUT_COUNTRY_REWARD_CONFIG_PASS full source equality, encrypted rules parity, ID/IDN selection, BR fallback, session initialization, progression preservation and settlement consumption");
                code=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally
            {
                OriginalPreferenceFixture.Restore();
                EditorApplication.Exit(code);
            }
        }
        private static JObject Read(string path)=>JObject.Parse(File.ReadAllText(path));
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
