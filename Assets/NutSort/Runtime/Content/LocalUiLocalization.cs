using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NutSort.Content
{
    // Country selection and UI binding use this same locale, including the original table.
    public static class LocalUiLocalization
    {
        private static JObject translations;
        private static OriginalTextCatalog original;
        private static string language = NormalizeLanguage(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        public static string Language => language;
        public static event Action Changed;

        public static void Configure(string languageCode, OriginalTextCatalog originalText)
        {
            original = originalText;
            language = NormalizeLanguage(languageCode);
            EnsureCatalog();
            Changed?.Invoke();
        }

        public static string NormalizeLanguage(string value)
        {
            string code = (value ?? string.Empty).Split('-','_')[0].ToLowerInvariant();
            switch (code)
            {
                case "ja": case "pt": case "de": case "fr": case "id": case "ru": case "es": return code;
                case "in": return "id";
                default: return "en";
            }
        }

        public static bool HasTranslation(string english)
        {
            if (string.IsNullOrEmpty(english)) return false;
            EnsureCatalog();
            return translations[english] != null || (original != null && original.TryTranslateEnglish(english, language, out _));
        }

        public static string Text(string english)
        {
            if (string.IsNullOrEmpty(english)) return english ?? string.Empty;
            EnsureCatalog();
            string value = (string)translations[english]?[language];
            if (!string.IsNullOrEmpty(value)) return value;
            if (original != null && original.TryTranslateEnglish(english, language, out value)) return value;
            return english;
        }

        public static bool TryTranslate(string english, string languageCode, out string value)
        {
            EnsureCatalog();
            value = string.IsNullOrEmpty(english) ? null : (string)translations[english]?[NormalizeLanguage(languageCode)];
            return !string.IsNullOrEmpty(value);
        }

        public static string Format(string english, params object[] arguments)
            => string.Format(CultureInfo.CurrentCulture, Text(english), arguments);

        private static void EnsureCatalog()
        {
            if (translations != null) return;
            var asset = Resources.Load<TextAsset>("Configuration/Localization/InterfaceText");
            if (asset == null) throw new InvalidOperationException("Missing interface translation catalog.");
            translations = JObject.Parse(asset.text);
            Resources.UnloadAsset(asset);
        }
    }
}
