using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NutSort.Content;
using NutSort.Integration;
using NutSort.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LocalAnalyticsSetup
{
    public const string Output="Docs/AnalyticsValidation/20261014-implementation";
    public static void ConfigureBatch(){ConfigureAndValidate();EditorApplication.Exit(0);}
    [MenuItem("Nut Sort/Analytics/Configure and validate")]
    public static void ConfigureAndValidate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Configure analytics in Edit Mode.");
        Directory.CreateDirectory(Output);
        string path="Assets/Resources/Configuration/LocalAnalytics.asset";
        var settings=AssetDatabase.LoadAssetAtPath<LocalAnalyticsSettings>(path);
        if(settings==null){settings=ScriptableObject.CreateInstance<LocalAnalyticsSettings>();AssetDatabase.CreateAsset(settings,path);}
        settings.BuildId=PlayerSettings.Android.bundleVersionCode.ToString(System.Globalization.CultureInfo.InvariantCulture);EditorUtility.SetDirty(settings);
        var popups=new Dictionary<string,string>
        {
            {"LocalSuccess","level_settlement"},{"LocalLuckyReward","lucky_reward"},{"LocalFailure","failure"},
            {"ReplayPanel","replay"},{"LocalSettings","settings"},{"LocalUserLevel","user_level"},
            {"RevokeItem","tool_undo"},{"ExchangeItem","tool_refresh"},{"AddScrewItem","tool_add_screw"},
            {"TXPanel","cash_withdrawal"},{"TXCoinPanel","diamond_withdrawal"},{"LocalWithdrawalConditions","withdrawal_conditions"},
            {"TXUserInfoPanel","withdrawal_account"},{"TXUserInfoSurePanel","withdrawal_confirmation"},
            {"LocalWithdrawalVerification","withdrawal_verification"},{"TXGuideLoadingPanel","withdrawal_loading"},
            {"TXProgress0Panel","withdrawal_stage"},{"TXTooFastHintPanel","withdrawal_review"},
            {"TXRecordGuidePanel","welcome"},{"NewbieGuidePanel","tutorial"},{"UnlockGameplayPanel","gameplay_unlock"},
            {"LocalCashHistory","withdrawal_history"},{"TXGuideTargetCompletePanel","early_reward_target"}
        };
        foreach(var item in popups)
        {
            string prefabPath="Assets/Resources/prefabs/panels/"+item.Key+".prefab";
            if(!File.Exists(prefabPath))throw new FileNotFoundException("Missing analytics popup prefab",prefabPath);
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var observer=root.GetComponent<LocalAnalyticsPopup>();if(observer==null)observer=root.AddComponent<LocalAnalyticsPopup>();
                var serialized=new SerializedObject(observer);serialized.FindProperty("popupId").stringValue=item.Value;
                serialized.FindProperty("entry").stringValue="game_flow";serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        for(int i=0;i<SceneManager.sceneCount;i++){var open=SceneManager.GetSceneAt(i);if(open.isDirty&&!string.IsNullOrEmpty(open.path))EditorSceneManager.SaveScene(open);}
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");GameObject sdk=null;
        foreach(var root in scene.GetRootGameObjects())if(root.name=="SDK")sdk=root;
        if(sdk==null)throw new InvalidOperationException("The configured SDK scene node is missing.");
        var bridge=sdk.GetComponent<LocalAnalyticsBridge>();if(bridge==null)bridge=sdk.AddComponent<LocalAnalyticsBridge>();
        var data=new SerializedObject(bridge);data.FindProperty("sdkAdapter").objectReferenceValue=sdk.GetComponent<SDKAdapter>();
        data.FindProperty("analyticsAgent").objectReferenceValue=sdk.GetComponent<BizzaAnalyticsAgent>();
        data.FindProperty("settings").objectReferenceValue=settings;data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Output+"/configuration.json",new JObject{{"popupPrefabs",popups.Count},{"sceneNode","SDK"},{"adapterConfigured",true},
            {"isTest",settings.IsTest},{"summaryIntervalSeconds",settings.SummaryIntervalSeconds},{"startupQueueCapacity",settings.StartupQueueCapacity}}.ToString());
        NutSort.Validation.LocalAnalyticsPolicyValidation.Run();
    }
}

// Explicit local file command for the already open Editor. No runtime code or reflection is involved.
[InitializeOnLoad]
public static class LocalAnalyticsEditorCommands
{
    private static double nextPoll;
    static LocalAnalyticsEditorCommands(){EditorApplication.update+=Poll;}
    private static void Poll()
    {
        if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        nextPoll=EditorApplication.timeSinceStartup+3;
        const string request="Library/CNPAnalytics-request.json";
        if(!File.Exists(request))return;
        string command=(string)JObject.Parse(File.ReadAllText(request))["command"];
        File.Delete(request);
        try
        {
            if(command=="configure_validate")LocalAnalyticsSetup.ConfigureAndValidate();
            else if(command=="play_validate")NutSort.Validation.LocalAnalyticsPlayValidation.Run();
            else if(command=="device_build")BuildDeviceValidation();
            else throw new InvalidOperationException("Unknown analytics validation command.");
            File.WriteAllText("Library/CNPAnalytics-command-result.json",new JObject{{"command",command},{"passed",true}}.ToString());
        }
        catch(Exception error)
        {Debug.LogException(error);File.WriteAllText("Library/CNPAnalytics-command-result.json",new JObject{{"command",command},{"passed",false},{"error",error.ToString()}}.ToString());}
    }
    public static void BuildDeviceValidation()
    {
        var settings=Resources.Load<LocalAnalyticsSettings>("Configuration/LocalAnalytics");bool originalTest=settings.IsTest;
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");BizzaAnalyticsAgent bizza=null;
        foreach(var root in scene.GetRootGameObjects())if(root.name=="SDK")bizza=root.GetComponent<BizzaAnalyticsAgent>();
        var serialized=new SerializedObject(bizza);bool originalLog=serialized.FindProperty("logMode").boolValue;
        int result=1;
        try
        {
            settings.IsTest=true;EditorUtility.SetDirty(settings);serialized.FindProperty("logMode").boolValue=true;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            result=NutSort.Validation.LocalAndroidBuild.BuildAnalyticsDeviceTest();
        }
        finally
        {
            settings=AssetDatabase.LoadAssetAtPath<LocalAnalyticsSettings>("Assets/Resources/Configuration/LocalAnalytics.asset");
            settings.IsTest=originalTest;EditorUtility.SetDirty(settings);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
            foreach(var root in scene.GetRootGameObjects())if(root.name=="SDK")bizza=root.GetComponent<BizzaAnalyticsAgent>();
            serialized=new SerializedObject(bizza);serialized.FindProperty("logMode").boolValue=originalLog;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        EditorApplication.Exit(result);
    }
    public static void RestoreProductionAndValidate()
    {
        var settings=AssetDatabase.LoadAssetAtPath<LocalAnalyticsSettings>("Assets/Resources/Configuration/LocalAnalytics.asset");
        settings.IsTest=false;EditorUtility.SetDirty(settings);
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/LuoSiSortGame.unity");
        foreach(var root in scene.GetRootGameObjects())if(root.name=="SDK")
        {
            var data=new SerializedObject(root.GetComponent<BizzaAnalyticsAgent>());
            data.FindProperty("logMode").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene);
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.color.nutparty");
        EditorUserBuildSettings.development=false;
        UnityEditor.Android.AndroidExternalToolsSettings.gradlePath=string.Empty;
        string path="Assets/Plugins/Android/gradleTemplate.properties";
        File.WriteAllText(path,System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path),@"(?m)^org\.gradle\.java\.home=.*\r?\n?",string.Empty));
        AssetDatabase.SaveAssets();NutSort.Validation.LocalAnalyticsPolicyValidation.Run();EditorApplication.Exit(0);
    }
}
