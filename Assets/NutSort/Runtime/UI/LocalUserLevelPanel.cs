using System;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    // MyLevelPanel uses the stored user rank, not the current puzzle number.
    public sealed class LocalUserLevelPanel : MonoBehaviour
    {
        [SerializeField] private Button closeButton, continueButton;
        [SerializeField] private TMP_Text levelLabel, progressLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private string levelFormat, progressFormat, unavailableProgressText;
        private Func<bool> canClick;
        private Action closed, playClick;
        private bool bound, closing;

        public Button CloseButton => closeButton;
        public Button ContinueButton => continueButton;
        public TMP_Text LevelLabel => levelLabel;
        public TMP_Text ProgressLabel => progressLabel;
        public Image ProgressFill => progressFill;
        public bool Closing => closing;

        public void Bind(OriginalUserLocalData user, Func<bool> tryClick, Action close, Action clickSound)
        {
            if (bound) throw new InvalidOperationException("User-level panel is already bound.");
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (closeButton == null || continueButton == null || levelLabel == null || progressLabel == null || progressFill == null ||
                string.IsNullOrEmpty(levelFormat) || string.IsNullOrEmpty(progressFormat) || string.IsNullOrEmpty(unavailableProgressText))
                throw new InvalidOperationException("User-level panel prefab references are incomplete.");
            canClick = tryClick ?? throw new ArgumentNullException(nameof(tryClick));
            closed = close ?? throw new ArgumentNullException(nameof(close));
            playClick = clickSound ?? throw new ArgumentNullException(nameof(clickSound));
            closeButton.onClick.AddListener(Close);
            continueButton.onClick.AddListener(Close);
            bound = true;
            levelLabel.text = LocalUiLocalization.Format(levelFormat, user.UserLevel);
            // Source: MyLevelPanel.RefreshLssPanel 0x9E1B24, bear_list[2].caliper_rank.
            var rows = user.GoldRewardTargetS2CData?.GetValue("bear_list", StringComparison.OrdinalIgnoreCase) as JArray;
            var row = rows != null && rows.Count > 2 ? rows[2] as JObject : null;
            var value = row?.GetValue("caliper_rank", StringComparison.OrdinalIgnoreCase);
            int target = value != null && value.Type == JTokenType.Integer ? (int)value : 0;
            progressFill.fillAmount = target > 0 ? Mathf.Clamp01((float)user.UserLevel / target) : 0;
            progressLabel.text = target > 0
                ? LocalUiLocalization.Format(progressFormat, progressFill.fillAmount * 100f)
                : LocalUiLocalization.Text(unavailableProgressText);
        }

        private void Close()
        {
            if (closing || !canClick()) return;
            LocalAnalyticsPopup.Action(this,"close");
            closing = true;
            playClick();
            closed();
        }

        private void OnDestroy()
        {
            if (!bound) return;
            closeButton.onClick.RemoveListener(Close);
            continueButton.onClick.RemoveListener(Close);
        }
    }
}
