using System.Collections.Generic;
using NutSort.Content;
using NutSort.Gameplay;

namespace NutSort.UI
{
    public sealed partial class LocalGameplayController
    {
        private OriginalBoardState analyticsBoard;
        private string settlementOperation, luckyOperation;
        public bool AnalyticsGameplayActive => bound && game.IsInitDone && !game.IsRestarting && !game.InputBlocked &&
            !game.IsFail && !game.IsSucceed && !awaitingNext && !settlementAdPending && !luckyAdPending &&
            ((!game.ModalInputBlocked && !HasLocalModal) || (teachingPanel != null && game.IsCanOperatorScrew && !welcomePending));
        private void ObserveAnalyticsBoard()
        {
            var board=game.Level.Board;
            if(board==analyticsBoard||LocalAnalytics.Current==null)return;
            analyticsBoard=board;
            int nuts=0;var colors=new HashSet<int>();
            foreach(var screw in board.Screws)foreach(var slot in screw.Slots)if(slot.Nut!=null){nuts++;colors.Add(slot.Nut.Color);}
            LocalAnalytics.Current.BoardReady(game.LastBoardRestored,board.Screws.Length,nuts,colors.Count);
        }
        private void TrackClaim(string source,string claimId,string operation,string result,string reason,int prop=0)
        {
            var fields=LocalAnalytics.Current?.AttemptFields()??new Dictionary<string,object>();
            fields["reward_source"]=source;fields["claim_id"]=claimId;fields["operation_id"]=operation;
            fields["result"]=result;fields["reason"]=reason;
            if(prop>=2)fields["prop"]=LocalAnalytics.Prop(prop);
            LocalAnalytics.Track("reward_claim_result",fields);
        }
        private string AnalyticsChannel()
        {
            int index=(int?)game.User.UserLssInfo?.GetValue("GetType",System.StringComparison.OrdinalIgnoreCase)??0;
            var channels=game.Tables.PayChannels.GetPayChannel(string.Empty,()=>audio.UserState.DisplayCountry(game.Tables)).Channels;
            return index>=0&&index<channels.Count?channels[index]:null;
        }
        private void EnsureAnalyticsWithdrawal(bool diamonds,int tier)
        {
            var analytics=LocalAnalytics.Current;if(analytics==null)return;
            if(string.IsNullOrEmpty(analytics.FlowId)||analytics.WithdrawalType!=(diamonds?"diamond":"cash")||analytics.WithdrawalTier!=tier)
                analytics.BeginWithdrawal(diamonds,tier);
        }
    }
}
