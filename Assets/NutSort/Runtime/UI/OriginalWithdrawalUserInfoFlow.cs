using System;
using Newtonsoft.Json.Linq;
using NutSort.Content;
namespace NutSort.UI
{
    public interface IOriginalWithdrawalUserInfoUI
    {
        string Name { get;set; }
        string Email { get;set; }
        string Number { get;set; }
        string AreaNumber { set; }
        int ChannelCount { get; }
        bool IsChannelOn(int index);
        void SetChannelOn(int index,bool value);
        void SetChannelVisible(int index,bool value);
        void SetChannelIcon(int index,string channel);
        void AddChannelListener(int index,Action<bool> listener);
        void SetEmailVisible(bool value);
        void SetAreaVisible(bool value);
        void SetNumberVisible(bool value);
        void ShowTip(int id);
        void ShowPanel(int id,object[] arguments);
        void Close();
    }
    // TXUserInfoPanel 0x9d48c0..0x9d59c8; no payment/network implementation.
    public sealed class OriginalWithdrawalUserInfoFlow
    {
        private readonly OriginalUserLocalData user;
        private readonly OriginalPayChannels channels;
        private readonly OriginalChannelInfos infos;
        private readonly Func<string> country,area;
        private readonly IOriginalWithdrawalUserInfoUI ui;
        private readonly Action save,clearHint;
        private readonly Action<object> guideCallback;
        public int GetTypeIndex { get;private set; }
        public int Level { get;private set; }
        public bool IsEmail { get;private set; }
        public bool IsName { get;private set; }
        public OriginalWithdrawalUserInfoFlow(OriginalUserLocalData user,OriginalPayChannels channels,OriginalChannelInfos infos,
            Func<string> country,Func<string> area,IOriginalWithdrawalUserInfoUI ui,Action save,Action<object> guideCallback,Action clearHint)
        {
            this.user=user;this.channels=channels;this.infos=infos;this.country=country;this.area=area;
            this.ui=ui;this.save=save;this.guideCallback=guideCallback;this.clearHint=clearHint;
        }
        public void Init(object[] arguments,Action baseInit,Action bindGet,Action bindClose,Action bindChannel)
        {
            baseInit();if(arguments.Length!=0)Level=(int)arguments[0];
            bindGet();bindClose();bindChannel();
        }
        public void Refresh()
        {
            if(user.UserLssInfo!=null)GetTypeIndex=(int?)Field(user.UserLssInfo,"GetType")??0;
            SetInfo();ui.AreaNumber="+"+area();
            var row=channels.GetPayChannel(string.Empty,country);int count=row.Channels.Count;
            Action<bool> changed=value=>
            {
                GetTypeIndex=-1;
                for(int i=0;i<count;i++)if(ui.IsChannelOn(i)){GetTypeIndex=i;SetInfo();}
            };
            for(int i=0;i<ui.ChannelCount;i++)
            {
                ui.SetChannelOn(i,i==GetTypeIndex);
                ui.SetChannelVisible(i,i<count);
                if(i>=count)continue;
                ui.SetChannelIcon(i,row.Channels[i]);
                ui.AddChannelListener(i,changed);
            }
        }
        public void SetInfo()
        {
            var row=channels.GetPayChannel(string.Empty,country);
            if(GetTypeIndex==-1)return;
            var info=infos.GetChannelInfo(row.Channels[GetTypeIndex]);
            IsEmail=info.IsEmail;IsName=info.IsName;
            if(user.UserLssInfo!=null)
            {
                ui.Name=(string)Field(user.UserLssInfo,"Name");
                ui.Email=(string)Field(user.UserLssInfo,"Email");
                ui.Number=(string)Field(user.UserLssInfo,"Number");
            }
            ui.SetEmailVisible(IsEmail);ui.SetAreaVisible(!IsEmail);ui.SetNumberVisible(!IsEmail);
        }
        public void Get()
        {
            string operation=LocalAnalytics.NewId();bool edit=user.UserLssInfo!=null;
            if(IsName&&ui.Name.Length<=1){AccountResult(operation,"validation_error","name_too_short",edit);ui.ShowTip(118);return;}
            if(IsEmail)
            {
                if(ui.Email.Length<=5){AccountResult(operation,"validation_error","email_too_short",edit);ui.ShowTip(120);return;}
            }
            else if(ui.Number.Length<=4){AccountResult(operation,"validation_error","phone_too_short",edit);ui.ShowTip(119);return;}
            if(GetTypeIndex<0){AccountResult(operation,"validation_error","channel_not_selected",edit);ui.ShowTip(122);return;}
            if(user.UserLssInfo==null)user.UserLssInfo=new JObject();
            Set(user.UserLssInfo,"Name",ui.Name);
            Set(user.UserLssInfo,"Number",ui.Number);
            Set(user.UserLssInfo,"Email",ui.Email);
            Set(user.UserLssInfo,"GetType",GetTypeIndex);
            Set(user.UserLssInfo,"OtherInfo",string.Empty);
            ui.ShowPanel(21,new object[]{Level});ui.Close();save();
            AccountResult(operation,"local_saved",null,edit);LocalAnalytics.Current?.SyncProperties();
        }
        private void AccountResult(string operation,string result,string reason,bool edit)
        {
            var fields=LocalAnalytics.Current?.WithdrawalFields()??LocalAnalytics.Fields();
            var list=channels.GetPayChannel(string.Empty,country).Channels;
            fields["operation_id"]=operation;fields["result"]=result;fields["is_edit"]=edit;
            if(reason!=null)fields["validation_reason"]=reason;
            if(GetTypeIndex>=0&&GetTypeIndex<list.Count)fields["channel"]=list[GetTypeIndex];
            LocalAnalytics.Track("withdraw_account_result",fields);
        }
        public void ChannelInfo()=>ui.ShowPanel(22,new object[]{Level});
        public void Close(){ui.Close();guideCallback(Level);clearHint();}
        private static JToken Field(JObject row,string name)
        {
            JToken value=null;foreach(var p in row.Properties())if(string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase))value=p.Value;return value;
        }
        private static void Set(JObject row,string name,JToken value)
        {
            JProperty found=null;foreach(var p in row.Properties())if(string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase))found=p;
            if(found==null)row[name]=value;else found.Value=value??JValue.CreateNull();
        }
    }
}
