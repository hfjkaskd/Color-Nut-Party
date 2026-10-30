using UnityEngine;
namespace NutSort.UI
{
    [CreateAssetMenu(menuName="Nut Sort/Original reward items")]
    public sealed class OriginalRewardItemSettings : ScriptableObject
    {
        public string ItemPath, BigItemPath, GoldFormat, ItemPrefix, BigCoinPath;
        public string[] ItemNames;
        public int GoldType, BigGoldType;
        public string GetIconPath(int type,bool isBig,string country)
        {
            if(type==0)return OriginalCurrencyArtSettings.ResolvePath(GoldFormat,country,isBig?BigGoldType:GoldType);
            if(type==1 && isBig)return BigCoinPath;
            string name=type>=0 && type<ItemNames.Length?ItemNames[type]:type.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return ItemPrefix+name;
        }
    }
}
