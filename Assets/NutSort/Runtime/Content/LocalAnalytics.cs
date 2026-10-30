using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NutSort.Content
{
    // Typed game assemblies publish here; only the configured Integration bridge calls the SDK.
    public static class LocalAnalytics
    {
        public static LocalAnalyticsService Current { get; private set; }
        public static event Action<string,Dictionary<string,object>> EventObserved;
        internal static void Observe(string name,Dictionary<string,object> fields)=>EventObserved?.Invoke(name,fields);
        public static void Bind(LocalAnalyticsService service) => Current = service;
        public static string NewId() => Guid.NewGuid().ToString("N");
        public static Dictionary<string, object> Fields(params object[] pairs)
        {
            var fields = new Dictionary<string, object>(pairs.Length / 2);
            for (int i = 0; i < pairs.Length; i += 2) if (pairs[i + 1] != null) fields.Add((string)pairs[i], pairs[i + 1]);
            return fields;
        }
        public static void Track(string name, Dictionary<string, object> fields = null) => Current?.Track(name, fields);
        public static IDisposable Operation(string source, string operationId = null) => Current?.Operation(source, operationId);
        public static string ClaimId(JObject pending)
        {
            if (pending == null) return string.Empty;
            if (pending["analytics_claim_id"] == null) pending["analytics_claim_id"] = NewId();
            return (string)pending["analytics_claim_id"];
        }
        public static string Prop(int type)
        {
            switch (type) { case 2: return "undo"; case 3: return "refresh"; case 4: return "add_screw"; default: return "unknown"; }
        }
    }

    public readonly struct LocalResourceSnapshot
    {
        public readonly double Cash, Diamond;
        public readonly int Undo, Refresh, AddScrew;
        public LocalResourceSnapshot(OriginalUserLocalData user)
        { Cash = user.Gold; Diamond = user.Coin; Undo = user.RevokeCount; Refresh = user.ExchangeCount; AddScrew = user.AddScrewCount; }
    }
}
