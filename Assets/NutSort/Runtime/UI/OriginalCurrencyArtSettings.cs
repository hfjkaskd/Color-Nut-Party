using System;
using UnityEngine;

namespace NutSort.UI
{
    // Currency formatting and reward values remain country-specific. Artwork is
    // selected separately: not every currency has a dedicated large cash pile.
    [CreateAssetMenu(menuName = "Nut Sort/Currency artwork")]
    public sealed class OriginalCurrencyArtSettings : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Country;
            public string SmallCode;
            public string LargeCode;
        }

        [SerializeField] private string defaultSmallCode;
        [SerializeField] private string defaultLargeCode;
        [SerializeField] private Entry[] entries;
        private static OriginalCurrencyArtSettings cached;

        public string GetCode(string country, int type)
        {
            string code = OriginalRecordGuidePanel.GoldCode(country);
            if (type != 1 && type != 3) return code;
            foreach (var entry in entries)
                if (string.Equals(entry.Country, code, StringComparison.OrdinalIgnoreCase))
                    return type == 3 ? entry.LargeCode : entry.SmallCode;
            return type == 3 ? defaultLargeCode : defaultSmallCode;
        }

        public static string ResolvePath(string format, string country, int type)
        {
            if (cached == null) cached = Resources.Load<OriginalCurrencyArtSettings>("Configuration/CurrencyArt");
            if (cached == null) throw new InvalidOperationException("Currency artwork configuration is missing.");
            return string.Format(format, cached.GetCode(country, type), type);
        }
    }
}
