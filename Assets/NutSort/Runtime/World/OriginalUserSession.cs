using System;
using System.Globalization;
using NutSort.Content;
using UnityEngine;
namespace NutSort.World
{
    public sealed class OriginalUserSession : MonoBehaviour
    {
        [SerializeField] private OriginalUserDefaults defaults;
        private OriginalUserStore loadedStore;
        private OriginalUserStartup startup;
        private CountryRewardCatalog rewardCatalog;
        public CountryRewardConfiguration Rewards { get; private set; }
        public string ActualCountry { get; private set; }
        public string DisplayCountry(OriginalTables tables)=>tables.Countries.Contains(ActualCountry)?ActualCountry:"US";
        public string DisplayCulture(OriginalTables tables)=>tables.Countries.Get(DisplayCountry(tables)).CountryLanguageCode;
        public event Action RewardConfigurationChanged;
        public OriginalUserStore Store=>startup!=null&&startup.Store!=null?startup.Store:loadedStore;
        public bool IsUserInitDone=>startup!=null&&startup.IsInitDone;
        public OriginalUserLocalData Data=>Store.Data;
        public void StartUser(OriginalUserStartup value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            loadedStore=Store;
            startup=value;
            // Bind before invoking: synchronous request callbacks must see published startup data.
            startup.Initialize();
        }
        private void Awake() { Initialize(); }
        public void Initialize()
        {
            if(Store==null)loadedStore=new OriginalUserStore(defaults,new UnityUserPreferences());
            if(Rewards==null)ConfigureRewardCountry(SystemCountry());
        }
        // A region SDK can supply ISO alpha-2/alpha-3 through this same runtime entry point.
        public void ConfigureRewardCountry(string countryCode)
        {
            if(Store==null)throw new InvalidOperationException("Initialize user storage before selecting rewards.");
            if(rewardCatalog==null)rewardCatalog=Resources.Load<CountryRewardCatalog>("Configuration/CountryRewardCatalog");
            if(rewardCatalog==null)throw new InvalidOperationException("Missing country reward catalog.");
            ActualCountry=NormalizeCountry(countryCode);
            Rewards=rewardCatalog.Load(ActualCountry);
            Rewards.ApplyTo(Data);
            RewardConfigurationChanged?.Invoke();
        }
        private static string NormalizeCountry(string value)
        {
            string code=(value??string.Empty).Trim().ToUpperInvariant();
            if(code.Length==2)return code;
            if(code.Length==3)
                foreach(var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                {
                    var region=new RegionInfo(culture.Name);
                    if(region.ThreeLetterISORegionName==code)return region.TwoLetterISORegionName;
                }
            return "US";
        }
        private static string SystemCountry()
        {
            try{return RegionInfo.CurrentRegion.TwoLetterISORegionName;}
            catch(ArgumentException){return string.Empty;}
        }
    }
}
