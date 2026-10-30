namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        // A device locale change may reload the scene only between interactions.
        // Checking the persisted completion cursor also covers the delay between
        // a finished rod and its reward callback, after its movement has stopped.
        public bool CanRefreshDeviceLocale
        {
            get
            {
                if (!bound || game == null || startup == null || startup.Loading == null ||
                    startup.Loading.IsVisible || startup.Replay == null || startup.Replay.IsClickMasked ||
                    startup.Replay.Panel != null || !game.IsInitDone || game.IsRestarting ||
                    game.InputBlocked || game.ModalInputBlocked || game.IsExchanging || game.IsFail ||
                    game.IsSucceed || awaitingNext || HasLocalModal || settlementAdPending ||
                    !earningRecoveryChecked || unlockQueue.Count != 0 ||
                    game.Level == null || !game.Level.AreNutsInitialized || game.User.LocalPendingReward != null)
                    return false;

                var rewardState = game.User.LocalWithdrawalData?["earningRewards"];
                if (rewardState?["pendingLucky"] != null ||
                    ((int?)rewardState?["lastProcessedScrewDone"] ?? game.User.ScrewDoneCount) != game.User.ScrewDoneCount)
                    return false;
                if (((float?)game.User.LocalWithdrawalData?["earningFlow"]?["settlementReceipt"] ?? 0) > 0 ||
                    game.IsSuccess())
                    return false;

                var rods = game.Level.Board.Screws;
                for (int i = 0; i < rods.Length; i++)
                {
                    var rod = rods[i];
                    if (!rod.IsCanOperator || game.Level.GetScrew(i).IsDoneAnimating) return false;
                    for (int j = 0; j < rod.Slots.Length; j++)
                    {
                        var slot = rod.Slots[j];
                        if (slot.Nut == null) continue;
                        var nut = game.Level.GetNut(slot.Nut);
                        if (nut.IsEntryAnimating || nut.IsSelectionAnimating || nut.IsTransferring)
                            return false;
                    }
                }
                return true;
            }
        }
    }
}
