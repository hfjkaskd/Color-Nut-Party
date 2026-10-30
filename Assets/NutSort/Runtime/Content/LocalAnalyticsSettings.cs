using UnityEngine;

namespace NutSort.Content
{
    [CreateAssetMenu(menuName = "Nut Sort/Analytics Settings")]
    public sealed class LocalAnalyticsSettings : ScriptableObject
    {
        public bool Enabled = true;
        public bool IsTest;
        public string BuildId = "1";
        [Min(10)] public float SummaryIntervalSeconds = 60;
        [Min(60)] public float SessionTimeoutSeconds = 1800;
        [Min(16)] public int StartupQueueCapacity = 256;
        [Min(1)] public int ExceptionFingerprintLimit = 20;
        [Min(10)] public float ExceptionWindowSeconds = 60;
        public bool DiagnosticLog = true;
        [Min(128)] public int DiagnosticRecordLimit = 2048;
    }
}
