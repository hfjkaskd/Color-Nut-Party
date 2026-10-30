using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;

namespace NutSort.UI
{
    // Authored on the text in its prefab. Dynamic values are still owned by the panel.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalLocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, TextArea] private string englishSource;
        private static readonly Dictionary<string, TMP_FontAsset> fonts = new Dictionary<string, TMP_FontAsset>();
        private string lastApplied;
        public string EnglishSource => englishSource;

        private void OnEnable()
        {
            LocalUiLocalization.Changed += Refresh;
            Refresh();
        }
        private void OnDisable() => LocalUiLocalization.Changed -= Refresh;
        public void Refresh()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            // A panel can populate a hidden label before enabling it. Never overwrite that data.
            if (!string.IsNullOrEmpty(englishSource) && (label.text == englishSource || label.text == lastApplied))
            {
                lastApplied = LocalUiLocalization.Text(englishSource);
                label.text = lastApplied;
            }
            string code = LocalUiLocalization.Language;
            string resource = code == "ja" ? "Localization/Fonts/LocalJapanese" : "Localization/Fonts/LocalLatin";
            if (!fonts.TryGetValue(resource, out TMP_FontAsset fallback))
            {
                fallback = Resources.Load<TMP_FontAsset>(resource);
                if (fallback != null) fonts.Add(resource, fallback);
            }
            if (fallback != null && label.font != null && label.font != fallback)
            {
                var table = label.font.fallbackFontAssetTable;
                if (table == null) label.font.fallbackFontAssetTable = table = new List<TMP_FontAsset>();
                if (!table.Contains(fallback)) table.Insert(0, fallback);
            }
        }
    }
}
