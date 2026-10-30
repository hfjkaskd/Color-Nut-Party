using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace NutSort.Validation
{
    // Uses the configured project identity and Android SDK build settings.
    public static class LocalAndroidBuild
    {
        public static void Run()=>Build("Builds/Android/ColorNutParty-SDK.apk",false);
        public static void RunSignedRelease()=>Build("Builds/Android/ColorNutParty-"+PlayerSettings.bundleVersion+"-release.apk",false,true);
        // Separate app sandbox protects the installed reference app and its saves.
        // Only package identity differs; scene, runtime, assets and build options are shared.
        public static void RunDeviceTest()=>Build("Builds/Android/ColorNutParty-SDK-device-test.apk",true);
        public static int BuildAnalyticsDeviceTest()=>Build("Builds/Android/ColorNutParty-SDK-device-test.apk",true,false,false);
        private static int Build(string output,bool useTestPackage,bool signedRelease=false,bool quitAfter=true)
        {
            var analyticsSettings=Resources.Load<NutSort.Content.LocalAnalyticsSettings>("Configuration/LocalAnalytics");
            bool originalAnalyticsTest=analyticsSettings!=null&&analyticsSettings.IsTest;
            string originalGradle=AndroidExternalToolsSettings.gradlePath;
            bool originalEmbeddedGradle=EditorPrefs.GetBool("GradleUseEmbedded",true);
            const string gradleProperties="Assets/Plugins/Android/gradleTemplate.properties";
            string originalGradleProperties=File.ReadAllText(gradleProperties);
            string originalPackage=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string package=useTestPackage?originalPackage+".localtest":originalPackage;
            bool originalBuildAppBundle=EditorUserBuildSettings.buildAppBundle;
            bool originalExportProject=EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool originalDevelopment=EditorUserBuildSettings.development;
            bool originalCustomKeystore=PlayerSettings.Android.useCustomKeystore;
            string originalKeystore=PlayerSettings.Android.keystoreName;
            string originalAlias=PlayerSettings.Android.keyaliasName;
            string originalStorePassword=PlayerSettings.Android.keystorePass;
            string originalKeyPassword=PlayerSettings.Android.keyaliasPass;
            int exitCode=0;
            try
            {
                // This project's AGP 8.13 needs the installed Gradle 8.13/JDK 17.
                // Select them for this build and restore shared preferences afterward.
                string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string gradle=Path.Combine(profile,".gradle/wrapper/dists/gradle-8.13-bin/5xuhj0ry160q40clulazy9h7d/gradle-8.13");
                string jdk=Path.Combine(profile,".jdks/jbr-17.0.14");
                if(!File.Exists(Path.Combine(gradle,"lib/gradle-launcher-8.13.jar"))||!File.Exists(Path.Combine(jdk,"bin/javac.exe")))
                    throw new InvalidOperationException("The installed Android Gradle/JDK toolchain is missing.");
                AndroidExternalToolsSettings.gradlePath=gradle;
                string properties=System.Text.RegularExpressions.Regex.Replace(originalGradleProperties,@"(?m)^org\.gradle\.java\.home=.*\r?\n?",string.Empty);
                File.WriteAllText(gradleProperties,properties+"\norg.gradle.java.home="+jdk.Replace('\\','/')+"\n");
                if(useTestPackage)PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,package);
                if(useTestPackage&&analyticsSettings!=null){analyticsSettings.IsTest=true;EditorUtility.SetDirty(analyticsSettings);}
                EditorUserBuildSettings.buildAppBundle=false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
                EditorUserBuildSettings.development=!signedRelease;
                if(signedRelease)
                {
                    string keystore=RequiredEnvironment("CNP_ANDROID_KEYSTORE");
                    if(!File.Exists(keystore))throw new FileNotFoundException("Android signing keystore is missing.");
                    PlayerSettings.Android.useCustomKeystore=true;
                    PlayerSettings.Android.keystoreName=Path.GetFullPath(keystore);
                    PlayerSettings.Android.keyaliasName=RequiredEnvironment("CNP_ANDROID_KEY_ALIAS");
                    PlayerSettings.Android.keystorePass=RequiredEnvironment("CNP_ANDROID_STORE_PASSWORD");
                    PlayerSettings.Android.keyaliasPass=RequiredEnvironment("CNP_ANDROID_KEY_PASSWORD");
                    ClearSigningPasswordsFromEnvironment();
                }
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("Builds/Android");
                Debug.Log("NUT_ANDROID_BUILD_START package="+package+" label="+PlayerSettings.productName
                    +" version="+PlayerSettings.bundleVersion+" code="+PlayerSettings.Android.bundleVersionCode
                    +" minSdk="+(int)PlayerSettings.Android.minSdkVersion+" targetSdk="+(int)PlayerSettings.Android.targetSdkVersion
                    +" architectures="+PlayerSettings.Android.targetArchitectures
                    +" backend="+PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)
                    +" mode="+(signedRelease?"signed-release":"development")+"; configured SDK integration.");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{"Assets/Scenes/LuoSiSortGame.unity"},
                    locationPathName=output,
                    target=BuildTarget.Android,
                    options=signedRelease?BuildOptions.None:BuildOptions.Development
                });
                if(report.summary.result!=BuildResult.Succeeded||report.summary.totalErrors!=0)
                    throw new InvalidOperationException("Android build did not succeed; errors="+report.summary.totalErrors);
                Debug.Log("NUT_ANDROID_BUILD_PASS bytes="+report.summary.totalSize+" warnings="+report.summary.totalWarnings+" errors="+report.summary.totalErrors);
            }
            catch(Exception e){Debug.LogException(e);exitCode=1;}
            finally
            {
                if(signedRelease)ClearSigningPasswordsFromEnvironment();
                if(useTestPackage)PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,originalPackage);
                var restoredAnalytics=AssetDatabase.LoadAssetAtPath<NutSort.Content.LocalAnalyticsSettings>("Assets/Resources/Configuration/LocalAnalytics.asset");
                if(restoredAnalytics!=null){restoredAnalytics.IsTest=originalAnalyticsTest;EditorUtility.SetDirty(restoredAnalytics);}
                AndroidExternalToolsSettings.gradlePath=originalEmbeddedGradle?string.Empty:originalGradle;
                File.WriteAllText(gradleProperties,originalGradleProperties);
                EditorUserBuildSettings.buildAppBundle=originalBuildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject=originalExportProject;
                EditorUserBuildSettings.development=originalDevelopment;
                PlayerSettings.Android.useCustomKeystore=originalCustomKeystore;
                PlayerSettings.Android.keystoreName=originalKeystore;
                PlayerSettings.Android.keyaliasName=originalAlias;
                PlayerSettings.Android.keystorePass=originalStorePassword;
                PlayerSettings.Android.keyaliasPass=originalKeyPassword;
                AssetDatabase.SaveAssets();
            }
            if(quitAfter)EditorApplication.Exit(exitCode);
            return exitCode;
        }
        private static string RequiredEnvironment(string name)
        {
            string value=Environment.GetEnvironmentVariable(name);
            if(string.IsNullOrEmpty(value))throw new InvalidOperationException("Missing signing environment variable: "+name);
            return value;
        }
        private static void ClearSigningPasswordsFromEnvironment()
        {
            Environment.SetEnvironmentVariable("CNP_ANDROID_STORE_PASSWORD",null);
            Environment.SetEnvironmentVariable("CNP_ANDROID_KEY_PASSWORD",null);
        }
    }
}
