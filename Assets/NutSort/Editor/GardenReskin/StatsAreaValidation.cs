using System;
using System.Collections.Generic;
using NutSort.UI;
using TMPro;
using UnityEngine;

namespace NutSort.Validation
{
    // Editor acceptance helper: measures the existing, settled runtime Prefab.
    // Only temporary sample text is assigned, and it is restored before return.
    public static class StatsAreaValidation
    {
        private const float Tolerance = .5f;

        public static void Check(OriginalPlayerInfo stats)
        {
            if (stats == null || !stats.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Stats area must be active before measurement.");

            var problems = new List<string>();
            string[] paths = { "1/label_32", "2/label_33", "3/label_34" };
            TMP_Text[] values = { stats.PeopleLabel, stats.ChallengeLabel, stats.GoldLabel };
            string[] samples = { "123456", "128.5", "Rp8.000.000" };
            string[] originals = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) throw new InvalidOperationException("Missing stats value binding: " + i);
                originals[i] = values[i].text;
            }

            try
            {
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < paths.Length; i++)
                {
                    Transform child = stats.transform.Find(paths[i]);
                    TMP_Text header = child == null ? null : child.GetComponent<TMP_Text>();
                    if (header == null) throw new InvalidOperationException("Missing stats header: " + paths[i]);
                    header.ForceMeshUpdate();
                    if (header.fontSize < 29.99f)
                        problems.Add(paths[i] + " shrank below 30: " + header.fontSize);
                    // Independent safe regions, inside the measured cream interior.
                    // Column centers remain -300, 0, 300; data and title stay put.
                    float center = (i - 1) * 300f;
                    Measure(header, stats.transform, new Rect(center - 144f, -35f, 288f, 110f),
                        false, paths[i], problems);

                    values[i].text = samples[i];
                    values[i].ForceMeshUpdate();
                    Measure(values[i], stats.transform, new Rect(center - 144f, -135f, 288f, 90f),
                        true, "value " + samples[i], problems);
                    if (values[i].textInfo.characterCount != samples[i].Length)
                        problems.Add("Incomplete sample characters: " + samples[i]);
                }
            }
            finally
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i].text = originals[i];
                    values[i].ForceMeshUpdate();
                }
            }

            if (problems.Count > 0)
                throw new InvalidOperationException("STATS_AREA_GEOMETRY_FAIL: " + string.Join("; ", problems));
            Debug.Log("STATS_AREA_GEOMETRY_PASS: three current headers at size 30, no overflowing glyphs; " +
                "123456 / 128.5 / Rp8.000.000 fit completely on one line; original displayed values restored.");
        }

        private static void Measure(TMP_Text text, Transform root, Rect columnSafe,
            bool singleLine, string label, List<string> problems)
        {
            if (text.isTextOverflowing) problems.Add(label + " reports text overflow");
            if (singleLine && text.textInfo.lineCount != 1)
                problems.Add(label + " wraps to " + text.textInfo.lineCount + " lines");

            Rect ownSafe = text.rectTransform.rect;
            // TMP margins reserve layout space for the visible glyph mesh, whose
            // SDF/bold padding can extend beyond its advance. Enforce the actual
            // Text Rect and the independent column bounds, including that mesh.
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool visible = false, ownFits = true, columnFits = true;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = text.textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                visible = true;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 local = corner == 0 ? character.bottomLeft : corner == 1 ? character.topLeft :
                        corner == 2 ? character.topRight : character.bottomRight;
                    Vector3 point = root.InverseTransformPoint(text.transform.TransformPoint(local));
                    ownFits &= Contains(ownSafe, local);
                    columnFits &= Contains(columnSafe, point);
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
            }
            if (!visible) problems.Add(label + " has no visible glyphs");
            if (!ownFits) problems.Add(label + " has glyphs outside its text rectangle");
            if (!columnFits) problems.Add(label + " has glyphs outside its column safe area");
            Debug.Log("STATS_AREA_MEASURE " + label + ": font=" + text.fontSize +
                ", lines=" + text.textInfo.lineCount + ", glyph bounds=" + min + ".." + max +
                ", overflow=" + text.isTextOverflowing + ", ownFits=" + ownFits + ", columnFits=" + columnFits);
        }

        private static bool Contains(Rect rect, Vector3 point) =>
            point.x >= rect.xMin - Tolerance && point.x <= rect.xMax + Tolerance &&
            point.y >= rect.yMin - Tolerance && point.y <= rect.yMax + Tolerance;
    }
}
