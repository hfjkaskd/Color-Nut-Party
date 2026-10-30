using System;
using NutSort.UI;
using UnityEngine;

namespace NutSort.Integration
{
    // Compiles beside SDKAdapter in Assembly-CSharp. The game assemblies only
    // expose typed callbacks and never need to reference or reflect into the SDK.
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class QTGameAdsBridge : MonoBehaviour
    {
        [SerializeField] private SDKAdapter sdkAdapter;
        [SerializeField] private string undoPlacement;
        [SerializeField] private string refreshPlacement;
        [SerializeField] private string addScrewPlacement;
        [SerializeField] private string settlementInterstitialPlacement;
        [SerializeField] private string settlementRewardedPlacement;

        private LocalGameplayController controller;
        private Action<bool> pendingCompletion;

        private void OnEnable()
        {
            OriginalStartupFlow.LocalGameplayReady += Bind;
            if(OriginalStartupFlow.CurrentLocalGameplay != null)
                Bind(OriginalStartupFlow.CurrentLocalGameplay);
        }

        private void OnDisable()
        {
            OriginalStartupFlow.LocalGameplayReady -= Bind;
            Unbind();
            pendingCompletion?.Invoke(false);
        }

        private void Bind(LocalGameplayController value)
        {
            pendingCompletion?.Invoke(false);
            Unbind();
            controller = value;
            controller.BindRewardedToolAds(ShowToolReward);
            controller.BindSettlementAds(ShowSettlementInterstitial, ShowSettlementReward);
        }

        private void Unbind()
        {
            if(controller == null)return;
            controller.BindRewardedToolAds(null);
            controller.BindSettlementAds(null, null);
            controller = null;
        }

        private void ShowToolReward(int toolType, Action<bool> completed)
        {
            switch(toolType)
            {
                case 2: Show(true, undoPlacement, completed); break;
                case 3: Show(true, refreshPlacement, completed); break;
                case 4: Show(true, addScrewPlacement, completed); break;
                default: completed?.Invoke(false); break;
            }
        }

        private void ShowSettlementInterstitial(Action<bool> completed) =>
            Show(false, settlementInterstitialPlacement, completed);

        private void ShowSettlementReward(Action<bool> completed) =>
            Show(true, settlementRewardedPlacement, completed);

        private void Show(bool rewarded, string placement, Action<bool> completed)
        {
            if(!isActiveAndEnabled || sdkAdapter == null ||
               string.IsNullOrWhiteSpace(placement) || pendingCompletion != null)
            {
                completed?.Invoke(false);
                return;
            }

            bool finished = false;
            Action<bool> finish = success =>
            {
                if(finished)return;
                finished = true;
                pendingCompletion = null;
                completed?.Invoke(success);
            };
            pendingCompletion = finish;
            try
            {
                // SDKAdapter handles initialization, readiness, no fill,
                // cancellation and show failure through this same callback.
                if(rewarded)sdkAdapter.ShowRewardVideo(placement, finish);
                else sdkAdapter.ShowInterstitial(placement, finish);
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
                finish(false);
            }
        }
    }
}
