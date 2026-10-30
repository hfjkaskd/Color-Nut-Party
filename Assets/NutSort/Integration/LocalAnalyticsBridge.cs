using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NutSort.Content;
using NutSort.UI;
using ThinkingDataIntegration;
using UnityEngine;

namespace NutSort.Integration
{
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class LocalAnalyticsBridge : MonoBehaviour
    {
        [SerializeField] private SDKAdapter sdkAdapter;
        [SerializeField] private BizzaAnalyticsAgent analyticsAgent;
        [SerializeField] private LocalAnalyticsSettings settings;
        private LocalAnalyticsService service;
        private LocalGameplayController gameplay;
        private bool transportBound;
        private double nextInitializationCheck;
        private int diagnosticCount;
        private StreamWriter diagnostic;
        private void Awake()
        {
            if(settings==null||sdkAdapter==null||analyticsAgent==null)throw new InvalidOperationException("Analytics scene references are incomplete.");
            if(LocalAnalytics.Current!=null)return;
            sdkAdapter.Initialize();analyticsAgent.Initialize();
            if(settings.DiagnosticLog)
            {
                string path=Path.Combine(Application.persistentDataPath,"cnp_analytics_diagnostic.jsonl");
                if(File.Exists(path)&&new FileInfo(path).Length>2*1024*1024)File.Delete(path);
                diagnostic=new StreamWriter(path,true){AutoFlush=true};
            }
            service=new LocalAnalyticsService(settings,new UnityUserPreferences(),
                ()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),()=>Time.realtimeSinceStartupAsDouble,
                Application.version,settings.BuildId);
            LocalAnalytics.Bind(service);
            OriginalStartupFlow.LocalGameplayReady+=BindGameplay;
            if(OriginalStartupFlow.CurrentLocalGameplay!=null)BindGameplay(OriginalStartupFlow.CurrentLocalGameplay);
            Application.logMessageReceived+=ObserveException;
            BindTransport();
        }
        private void BindGameplay(LocalGameplayController value) { gameplay=value; }
        private void BindTransport()
        {
            if(service==null||transportBound||!analyticsAgent.IsReady||!ThinkingDataAdapter.IsInitialized)return;
            if(settings.IsTest&&settings.DiagnosticLog)ThinkingData.Analytics.TDAnalytics.EnableLog(true);
            service.BindTransport(Forward,properties=>analyticsAgent.BUserProp(properties));
            transportBound=true;service.SyncProperties();
        }
        private void Forward(string name,Dictionary<string,object> fields)
        {
            sdkAdapter.Track(name,fields);
            if(!settings.DiagnosticLog||diagnosticCount>=settings.DiagnosticRecordLimit)return;
            diagnosticCount++;
            diagnostic?.WriteLine(JsonConvert.SerializeObject(new Dictionary<string,object>
                {{"event_name",name},{"payload",fields},{"forwarded",true}}));
            Debug.Log("[CNPAnalytics] forwarded event="+name+" event_id="+(string)fields["event_id"]);
        }
        private void Update()
        {
            if(service==null)return;
            service.GameplayActive=gameplay!=null&&gameplay.AnalyticsGameplayActive;
            service.Tick();
            if(!transportBound&&Time.realtimeSinceStartupAsDouble>=nextInitializationCheck)
            {nextInitializationCheck=Time.realtimeSinceStartupAsDouble+1;BindTransport();}
        }
        private void OnApplicationPause(bool paused)
        {
            service?.Pause(paused);
            if(paused&&transportBound)ThinkingDataAdapter.Flush();
        }
        private void ObserveException(string message,string stack,LogType type)
        {
            if(type!=LogType.Exception||service==null||string.IsNullOrEmpty(stack)||stack.IndexOf("NutSort.",StringComparison.Ordinal)<0)return;
            // Do not send exception messages or raw stack strings that may contain account input or file paths.
            service.Exception("NutSort","unhandled_runtime_exception","Exception");
            service.StartupResult(false,"game_initialization","runtime_exception");
        }
        private void OnApplicationQuit() {service?.Quit();diagnostic?.Flush();}
        private void OnDestroy()
        {
            if(service==null)return;
            OriginalStartupFlow.LocalGameplayReady-=BindGameplay;Application.logMessageReceived-=ObserveException;
            if(LocalAnalytics.Current==service)LocalAnalytics.Bind(null);
            diagnostic?.Dispose();
        }
    }
}
