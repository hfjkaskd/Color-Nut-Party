using System;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class LocalCashHistoryRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel, timeLabel, amountLabel, actionLabel, statusLabel;
        [SerializeField] private Button actionButton;
        [SerializeField] private GameObject readyVisual, pendingVisual;
        private int stage;
        private Action<int> select;
        private bool bound;
        public int Stage => stage;
        public Button ActionButton => actionButton;
        public TMP_Text TitleLabel => titleLabel;
        public TMP_Text TimeLabel => timeLabel;
        public TMP_Text AmountLabel => amountLabel;
        public TMP_Text ActionLabel => actionLabel;
        public TMP_Text StatusLabel => statusLabel;

        public void Bind(LocalCashHistoryPanel.RowData data, Action<int> onSelect)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            stage = data.Stage;
            select = onSelect;
            titleLabel.text = LocalUiLocalization.Text(data.Title ?? string.Empty);
            timeLabel.text = data.Time ?? string.Empty;
            amountLabel.text = data.Amount ?? string.Empty;
            actionLabel.text = LocalUiLocalization.Text(data.ActionCaption ?? string.Empty);
            statusLabel.text = LocalUiLocalization.Text(data.ActionCaption ?? string.Empty);
            actionButton.interactable = data.Interactable;
            actionButton.gameObject.SetActive(data.Interactable);
            statusLabel.gameObject.SetActive(!data.Interactable);
            readyVisual.SetActive(data.Interactable);
            pendingVisual.SetActive(!data.Interactable);
            if (bound) return;
            actionButton.onClick.AddListener(Select);
            bound = true;
        }

        private void Select()
        {
            if (actionButton.interactable) select?.Invoke(stage);
        }

        private void OnDestroy()
        {
            if (bound) actionButton.onClick.RemoveListener(Select);
        }
    }
}
