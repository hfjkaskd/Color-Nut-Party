using System;
using UnityEngine;
using UnityEngine.UI;
namespace NutSort.UI
{
    public sealed class OriginalGoldImage : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private int goldType;
        [SerializeField] private string resourceFormat;
        private Func<string> country;
        private Sprite overrideSprite;
        public void SetOverride(Sprite sprite){overrideSprite=sprite;Apply();}
        public void Bind(Func<string> currentCountry) { country = currentCountry ?? throw new ArgumentNullException(nameof(currentCountry)); }
        private void Start() { Apply(); }
        public void Apply()
        {
            image.sprite = overrideSprite!=null?overrideSprite:Resources.Load<Sprite>(string.Format(resourceFormat, OriginalRecordGuidePanel.GoldCode(country()), goldType));
            image.SetNativeSize();
        }
    }
}
