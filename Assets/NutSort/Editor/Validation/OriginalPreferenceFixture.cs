using System.IO;
using NutSort.Content;
using UnityEngine;

namespace NutSort.Validation
{
    // Validation-only backup survives Unity domain reload and interrupted runs.
    // Runtime continues to use the real PlayerPrefs implementation on all platforms.
    public static class OriginalPreferenceFixture
    {
        private static string Backup => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Library/ValidationBackups/GameplayUserLocalData.bin"));

        public static void Begin(string json = "{}")
        {
            Restore();
            Directory.CreateDirectory(Path.GetDirectoryName(Backup));
            using (var writer = new BinaryWriter(File.Create(Backup)))
            {
                writer.Write(PlayerPrefs.HasKey(OriginalUserStore.Key));
                writer.Write(PlayerPrefs.GetString(OriginalUserStore.Key, string.Empty));
            }
            using(var writer=new BinaryWriter(File.Create(Backup+".analytics")))
            {writer.Write(PlayerPrefs.HasKey(LocalAnalyticsService.StateKey));writer.Write(PlayerPrefs.GetString(LocalAnalyticsService.StateKey,string.Empty));}
            PlayerPrefs.DeleteKey(LocalAnalyticsService.StateKey);
            PlayerPrefs.SetString(OriginalUserStore.Key, json);
            PlayerPrefs.Save();
        }

        public static void Restore()
        {
            if(File.Exists(Backup+".analytics"))
            {
                bool hadKey;string saved;
                using(var reader=new BinaryReader(File.OpenRead(Backup+".analytics"))){hadKey=reader.ReadBoolean();saved=reader.ReadString();}
                if(hadKey)PlayerPrefs.SetString(LocalAnalyticsService.StateKey,saved);else PlayerPrefs.DeleteKey(LocalAnalyticsService.StateKey);
                PlayerPrefs.Save();File.Delete(Backup+".analytics");
            }
            if (!File.Exists(Backup)) return;
            bool existed; string json;
            using (var reader = new BinaryReader(File.OpenRead(Backup)))
            { existed = reader.ReadBoolean(); json = reader.ReadString(); }
            if (existed) PlayerPrefs.SetString(OriginalUserStore.Key, json);
            else PlayerPrefs.DeleteKey(OriginalUserStore.Key);
            PlayerPrefs.Save();
            File.Delete(Backup);
        }
    }
}
