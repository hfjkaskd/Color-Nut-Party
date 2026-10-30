using System;
using NutSort.Content;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NutSort.UI
{
    public enum LocalEarningMessageStyle
    {
        Assessment = 0,
        Activating = 1,
        Activated = 4,
        Summary = 5
    }

    // Presentation only. The owner decides when an already-persisted reward is collected.
    public sealed class LocalEarningSequencePanel : MonoBehaviour
    {
        [Serializable]
        public sealed class MessageLayout
        {
            public LocalEarningMessageStyle Style;
            public GameObject Root;
            public Image Artwork;
            public string ArtworkPath;
            public TMP_Text Title, Body;
        }

        [SerializeField] private GameObject messageGroup, bonusesGroup, receiptGroup;
        [SerializeField] private TMP_Text messageTitle, messageBody, bonusesTitle, receiptAmount;
        [SerializeField] private MessageLayout[] messageLayouts;
        [SerializeField] private CanvasGroup[] bonusRows;
        [SerializeField] private TMP_Text[] bonusCaptions;
        [SerializeField, Min(0)] private float bonusRevealInterval = .35f, bonusPulseDuration = .5f;
        [SerializeField] private AnimationCurve bonusPulseCurve;
        [SerializeField] private RectTransform[] flightIcons;
        [SerializeField] private float flightDuration = .65f, flightStagger = .075f;
        [SerializeField] private AnimationCurve flightCurve;
        private Vector3[] flightStartPositions;
        private Vector3[] bonusStartScales;
        private Transform flightTarget;
        private Action collectAction, completeAction;
        private float elapsed, collectAt, completeAt;
        private bool showingBonuses, showingReceipt, collected;
        private int activeBonusCount;
        private MessageLayout activeMessageLayout;

        public TMP_Text MessageTitle => activeMessageLayout == null ? messageTitle : activeMessageLayout.Title;
        public TMP_Text MessageBody => activeMessageLayout == null ? messageBody : activeMessageLayout.Body;
        public MessageLayout[] MessageLayouts => messageLayouts;
        public MessageLayout ActiveMessageLayout => activeMessageLayout;
        public bool MessageVisible => messageGroup.activeSelf;
        public TMP_Text ReceiptAmount => receiptAmount;
        public TMP_Text[] BonusCaptions => bonusCaptions;
        public CanvasGroup[] BonusRows => bonusRows;
        public bool BonusesVisible => showingBonuses;
        public bool BonusPresentationComplete => showingBonuses && (activeBonusCount == 0 ||
            elapsed >= (activeBonusCount - 1) * Mathf.Max(0, bonusRevealInterval) + Mathf.Max(0, bonusPulseDuration));
        public bool ReceiptVisible => showingReceipt;
        public bool Collected => collected;

        private void Awake()
        {
            bonusStartScales = new Vector3[bonusRows.Length];
            for (int i = 0; i < bonusRows.Length; i++)
                bonusStartScales[i] = bonusRows[i].transform.localScale;
            flightStartPositions = new Vector3[flightIcons.Length];
            for (int i = 0; i < flightIcons.Length; i++)
                flightStartPositions[i] = flightIcons[i].localPosition;
        }

        public void ShowMessage(string title, string body)
            => ShowMessage(LocalEarningMessageStyle.Summary, title, body);

        public void ShowMessage(LocalEarningMessageStyle style, string title, string body)
        {
            ResetPresentation();
            foreach (MessageLayout layout in messageLayouts)
            {
                if (layout.Style != style) continue;
                activeMessageLayout = layout;
                break;
            }
            if (activeMessageLayout == null)
                throw new InvalidOperationException("The requested earning message layout is not configured.");
            if (activeMessageLayout.Artwork.sprite == null)
            {
                Sprite art = Resources.Load<Sprite>(activeMessageLayout.ArtworkPath);
                if (art == null) throw new InvalidOperationException("Missing earning message artwork: " + activeMessageLayout.ArtworkPath);
                activeMessageLayout.Artwork.sprite = art;
            }
            activeMessageLayout.Title.text = LocalUiLocalization.Text(title ?? string.Empty);
            activeMessageLayout.Body.text = LocalUiLocalization.Text(body ?? string.Empty);
            activeMessageLayout.Root.SetActive(true);
            messageGroup.SetActive(true);
        }

        public void ShowBonuses(string title, string[] captions)
        {
            if (captions == null || captions.Length > bonusRows.Length || bonusRows.Length != bonusCaptions.Length)
                throw new ArgumentException("Bonus captions must fit the configured rows.", nameof(captions));
            ResetPresentation();
            bonusesGroup.SetActive(true);
            bonusesTitle.text = LocalUiLocalization.Text(title ?? string.Empty);
            activeBonusCount = captions.Length;
            for (int i = 0; i < bonusRows.Length; i++)
            {
                bonusRows[i].gameObject.SetActive(i < captions.Length);
                // Every benefit remains readable while a small pulse draws attention to each row.
                bonusRows[i].alpha = 1;
                bonusCaptions[i].text = i < captions.Length ? LocalUiLocalization.Text(captions[i] ?? string.Empty) : string.Empty;
            }
            showingBonuses = true;
        }

        public void ShowReceipt(string amount, Transform flyTarget, Action collect, Action complete,
            float collectDelay, float totalDuration)
        {
            if (collectDelay < 0 || totalDuration < collectDelay)
                throw new ArgumentOutOfRangeException(nameof(totalDuration));
            if (collect == null) throw new ArgumentNullException(nameof(collect));
            if (complete == null) throw new ArgumentNullException(nameof(complete));
            ResetPresentation();
            receiptGroup.SetActive(true);
            receiptAmount.text = amount ?? string.Empty;
            flightTarget = flyTarget;
            collectAction = collect;
            completeAction = complete;
            collectAt = collectDelay;
            completeAt = totalDuration;
            showingReceipt = true;
        }

        private void Update()
        {
            if (!showingBonuses && !showingReceipt) return;
            elapsed += Time.unscaledDeltaTime;
            if (showingBonuses)
            {
                float duration = Mathf.Max(0, bonusPulseDuration);
                float interval = Mathf.Max(0, bonusRevealInterval);
                for (int i = 0; i < activeBonusCount; i++)
                {
                    float age = elapsed - i * interval;
                    float scale = age >= 0 && age < duration && bonusPulseCurve != null
                        ? bonusPulseCurve.Evaluate(age / duration) : 1;
                    bonusRows[i].transform.localScale = bonusStartScales[i] * scale;
                }
                return;
            }
            if (!collected && elapsed >= collectAt)
            {
                collected = true;
                Action collect = collectAction;
                collectAction = null;
                collect();
                if (this == null || !showingReceipt) return;
            }
            if (collected && flightTarget != null)
            {
                for (int i = 0; i < flightIcons.Length; i++)
                {
                    float age = elapsed - collectAt - i * flightStagger;
                    RectTransform icon = flightIcons[i];
                    bool visible = age >= 0 && age < flightDuration;
                    icon.gameObject.SetActive(visible);
                    if (!visible) continue;
                    Vector3 origin = icon.parent.TransformPoint(flightStartPositions[i]);
                    float t = Mathf.Clamp01(age / Mathf.Max(.01f, flightDuration));
                    icon.position = Vector3.LerpUnclamped(origin, flightTarget.position, flightCurve.Evaluate(t));
                }
            }
            if (elapsed < completeAt) return;
            showingReceipt = false;
            Action complete = completeAction;
            completeAction = null;
            complete();
        }

        private void ResetPresentation()
        {
            showingBonuses = showingReceipt = collected = false;
            activeBonusCount = 0;
            elapsed = 0;
            collectAction = completeAction = null;
            flightTarget = null;
            messageGroup.SetActive(false);
            activeMessageLayout = null;
            if (messageLayouts != null)
                foreach (MessageLayout layout in messageLayouts) layout.Root.SetActive(false);
            bonusesGroup.SetActive(false);
            receiptGroup.SetActive(false);
            for (int i = 0; i < bonusRows.Length; i++)
            {
                bonusRows[i].alpha = 1;
                bonusRows[i].transform.localScale = bonusStartScales[i];
            }
            for (int i = 0; i < flightIcons.Length; i++)
            {
                flightIcons[i].gameObject.SetActive(false);
                flightIcons[i].localPosition = flightStartPositions[i];
            }
        }

        private void OnDisable()
        {
            showingBonuses = showingReceipt = false;
            collectAction = completeAction = null;
        }
    }
}
