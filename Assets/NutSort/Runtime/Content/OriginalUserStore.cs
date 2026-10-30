using System;
using UnityEngine;
namespace NutSort.Content
{
    public interface IOriginalUserPreferences
    {
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Save();
    }
    public sealed class UnityUserPreferences : IOriginalUserPreferences
    {
        public string GetString(string key,string fallback)=>PlayerPrefs.GetString(key,fallback);
        public void SetString(string key,string value)=>PlayerPrefs.SetString(key,value);
        public void Save()=>PlayerPrefs.Save();
    }
    public sealed class OriginalUserStore
    {
        public const string Key="UserLocalData";
        private readonly IOriginalUserPreferences preferences;
        private LocalResourceSnapshot resourceBaseline;
        public OriginalUserLocalData Data { get; }
        public OriginalUserStore(OriginalUserDefaults defaults,IOriginalUserPreferences storage,Action<OriginalUserLocalData> initializeFresh=null)
        {
            if(defaults==null)throw new ArgumentNullException(nameof(defaults));
            preferences=storage??throw new ArgumentNullException(nameof(storage));
            string json=preferences.GetString(Key,string.Empty);
            if(string.IsNullOrEmpty(json))
            {
                var fresh=new OriginalUserLocalData(defaults);
                initializeFresh?.Invoke(fresh);
                Data=fresh;
            }
            else Data=OriginalUserDataJson.Read(json,defaults);
            resourceBaseline=new LocalResourceSnapshot(Data);
        }
        // Source SaveData skips a null user. It replaces LevelInfo only when
        // a current board exists, then writes the entire user envelope and flushes.
        // A caller must supply the original board JSON; no new board format here.
        public void SaveData(bool hasCurrentBoard,string currentBoardJson)
        {
            if(Data==null)return;
            if(hasCurrentBoard)Data.LevelInfo=currentBoardJson;
            var resourceRecords=LocalAnalytics.Current?.PrepareResources(Data,resourceBaseline);
            LocalAnalytics.Current?.PrepareSave(Data);
            preferences.SetString(Key,OriginalUserDataJson.Write(Data));
            preferences.Save();
            resourceBaseline=new LocalResourceSnapshot(Data);
            LocalAnalytics.Current?.CommitResources(resourceRecords);
            LocalAnalytics.Current?.StateCommitted(Data);
        }
    }
}
