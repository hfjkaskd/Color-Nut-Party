using System;
using System.Collections.Generic;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public sealed class LocalCashHistoryPanel : MonoBehaviour
    {
        [Serializable]
        public sealed class RowData
        {
            public int Stage;
            public string Title, ActionCaption, Time, Amount;
            public bool Interactable;
        }

        [SerializeField] private TMP_Text titleLabel, balanceLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform rowParent;
        [SerializeField] private LocalCashHistoryRow rowPrefab;
        [SerializeField] private int maximumRows = 3;
        private readonly List<LocalCashHistoryRow> rows = new List<LocalCashHistoryRow>(3);
        private Action closeAction;
        private Action<int> selectAction;
        private bool bound, closing;
        public Button CloseButton => closeButton;
        public TMP_Text TitleLabel => titleLabel;
        public TMP_Text BalanceLabel => balanceLabel;
        public IReadOnlyList<LocalCashHistoryRow> Rows => rows;
        public bool Closing => closing;

        public void Init(string title, string balance, RowData[] data, Action<int> select, Action close)
        {
            if (data == null || data.Length > maximumRows)
                throw new ArgumentException("History rows must fit the configured panel.", nameof(data));
            if (rowPrefab == null || rowParent == null) throw new InvalidOperationException("History row prefab is not configured.");
            titleLabel.text = LocalUiLocalization.Text(title ?? string.Empty);
            balanceLabel.text = balance ?? string.Empty;
            closeAction = close ?? throw new ArgumentNullException(nameof(close));
            selectAction = select ?? throw new ArgumentNullException(nameof(select));
            closing = false;
            if (!bound)
            {
                closeButton.onClick.AddListener(Close);
                bound = true;
            }
            while (rows.Count < data.Length) rows.Add(Instantiate(rowPrefab, rowParent, false));
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(i < data.Length);
                if (i < data.Length) rows[i].Bind(data[i], Select);
            }
        }

        private void Select(int stage)
        {
            if (!closing) selectAction(stage);
        }

        private void Close()
        {
            if (closing) return;
            LocalAnalyticsPopup.Action(this,"close");
            closing = true;
            closeAction();
        }

        private void OnDestroy()
        {
            if (bound) closeButton.onClick.RemoveListener(Close);
        }
    }
}
