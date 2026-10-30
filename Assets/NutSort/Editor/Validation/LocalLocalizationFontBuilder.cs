using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace NutSort.Validation
{
    public static class LocalLocalizationFontBuilder
    {
        const string Output = "Assets/Resources/Localization/Fonts";
        const string Report = "Docs/Localization/20261013/font-report.json";
        public static void Run()
        {
            try { Build(); Debug.Log("LOCALIZATION_FONTS_PASS"); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void Build()
        {
            Directory.CreateDirectory(Output); Directory.CreateDirectory(Path.GetDirectoryName(Report));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var tables = new OriginalTables(Resources.Load<OriginalTableSettings>("Configuration/OriginalTables"));
            var original = tables.ReadTable("text.json");
            var additions = JObject.Parse(Resources.Load<TextAsset>("Configuration/Localization/InterfaceText").text);
            var report = new JObject();
            report["latin"] = Create("LocalLatin", "Assets/Font/LocalRubik-Bold.ttf", Characters(original, additions, new[] { "en", "pt", "de", "fr", "id", "ru", "es" }), false, 1024);
            report["japanese"] = Create("LocalJapanese", "Assets/NutSort/Editor/Validation/Fonts/NotoSansJP-Bold.ttf", Characters(original, additions, new[] { "ja" }), true, 2048);
            report["japaneseSource"] = "https://github.com/google/fonts/tree/main/ofl/notosansjp";
            report["japaneseLicense"] = "SIL Open Font License 1.1; OFL-NotoSansJP.txt";
            report["latinSource"] = "https://github.com/google/fonts/tree/main/ofl/rubik";
            report["latinLicense"] = "SIL Open Font License 1.1; OFL-Rubik.txt";
            report["sourceFontPackaging"] = "Japanese font source stays in Editor. Static TMP asset has no runtime source Font reference.";
            File.WriteAllText(Report, report.ToString()); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        static string Characters(JObject original, JObject additions, string[] languages)
        {
            var values = new HashSet<char>();
            for (char c = ' '; c <= '~'; c++) values.Add(c);
            foreach (char c in "€£¥₽¢…×–—‘’“”") values.Add(c);
            foreach (string language in languages)
            {
                foreach (var row in (JArray)original["list"]) Add(values, (string)row[language]);
                foreach (var entry in additions.Properties()) Add(values, (string)entry.Value[language]);
            }
            var chars = new List<char>(values); chars.Sort(); return new string(chars.ToArray());
        }
        static void Add(HashSet<char> values, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = Regex.Replace(text, "<[^>]*>", "");
            foreach (char c in text) if (!char.IsControl(c) && c != '\u00AD' && c != '\u200B' && c != '\uFEFF') values.Add(c);
        }
        static JObject Create(string name, string sourcePath, string chars, bool makeStatic, int atlasSize)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (font == null) throw new FileNotFoundException(sourcePath);
            var asset = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
            asset.name = name; asset.normalStyle = 0; asset.boldStyle = .75f;
            asset.TryAddCharacters(chars, out string missing, false);
            // A shared currency safety set may contain glyphs absent from a particular
            // font. Actual text coverage is additionally checked per rendered label.
            var missingList = new JArray(); foreach (char c in missing ?? "") missingList.Add("U+" + ((int)c).ToString("X4"));
            if (makeStatic) asset.atlasPopulationMode = AtlasPopulationMode.Static;
            string path = Output + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset);
            int index = 0; long bytes = 0;
            foreach (var atlas in asset.atlasTextures)
            {
                if (atlas == null) continue;
                atlas.name = name + " Atlas " + index++; AssetDatabase.AddObjectToAsset(atlas, asset);
                bytes += (long)atlas.width * atlas.height;
            }
            EditorUtility.SetDirty(asset);
            return new JObject { ["asset"] = path, ["requestedCharacters"] = chars.Length, ["glyphs"] = asset.characterTable.Count, ["atlasPages"] = index, ["atlasSize"] = atlasSize, ["alphaAtlasBytes"] = bytes, ["mode"] = makeStatic ? "static" : "dynamic", ["missingRequested"] = missingList };
        }
    }
}
