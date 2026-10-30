using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using GooglePlayServices;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit batch Android resolution through EDM4U's public API.</summary>
public static class QTAndroidDependencies
{
    private const string ReportPath = "Docs/SDKIntegration/QT-20261009/android-resolution.json";

    public static void Resolve()
    {
        var report = new ResolutionReport
        {
            result = "RUNNING",
            startedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            androidTargetActive = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
            resolverApi = "GooglePlayServices.PlayServicesResolver.ResolveSync(true)",
            resolverStateFile = "ProjectSettings/AndroidResolverDependencies.xml",
            mainGradleTemplatePresent = File.Exists("Assets/Plugins/Android/mainTemplate.gradle"),
            bundledAndroidTools = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/AndroidPlayer")
        };
        WriteReport(report);
        bool success = false;
        try
        {
            if (!report.androidTargetActive)
                throw new InvalidOperationException("Run Unity with -buildTarget Android before resolving.");
            var declared = PlayServicesResolver.GetPackageSpecs();
            var packages = new List<DeclaredPackage>(declared.Count);
            foreach (var pair in declared)
                packages.Add(new DeclaredPackage { spec = pair.Key, declarationSource = pair.Value });
            report.declaredPackages = packages.ToArray();
            report.declaredPackageCount = packages.Count;
            var repos = new List<string>();
            foreach (var pair in PlayServicesResolver.GetRepos()) repos.Add(pair.Key);
            report.repositories = repos.ToArray();
            report.bundledAgpVersion = ReadBundledAgpVersion(report.bundledAndroidTools);
            report.bundledJavaVersion = ReadReleaseValue(
                Path.Combine(report.bundledAndroidTools, "OpenJDK/release"), "JAVA_VERSION");
            WriteReport(report);

            success = PlayServicesResolver.ResolveSync(true);
            AssetDatabase.SaveAssets();
            report.result = success ? "PASS" : "FAIL";
            report.resolverReturnedSuccess = success;
            report.resolverStateExists = File.Exists(report.resolverStateFile);
            var files = new List<string>();
            CollectFiles("Assets/Plugins/Android", "*.aar", files);
            CollectFiles("Assets/Plugins/Android", "*.jar", files);
            report.resolvedProjectLibraries = files.ToArray();
            report.resolvedProjectLibraryCount = files.Count;
            report.mainGradleTemplatePresent = File.Exists("Assets/Plugins/Android/mainTemplate.gradle");
            report.verificationScope = "EDM4U Android dependency resolution result. APK compilation and device SDK delivery are separate checks.";
        }
        catch (Exception exception)
        {
            success = false;
            report.result = "FAIL";
            report.failureType = exception.GetType().Name;
            Debug.LogException(exception);
        }
        finally
        {
            report.completedUtc = DateTime.UtcNow.ToString("O");
            WriteReport(report);
        }
        if (success) Debug.Log("QT_ANDROID_RESOLUTION_PASS: " + ReportPath);
        else Debug.LogError("QT_ANDROID_RESOLUTION_FAIL: " + ReportPath);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void CollectFiles(string root, string pattern, List<string> files)
    {
        if (!Directory.Exists(root)) return;
        foreach (string path in Directory.GetFiles(root, pattern, SearchOption.AllDirectories))
            files.Add(path.Replace('\\', '/'));
    }

    private static string ReadBundledAgpVersion(string androidTools)
    {
        string path = Path.Combine(androidTools, "Tools/GradleTemplates/baseProjectTemplate.gradle");
        if (!File.Exists(path)) return string.Empty;
        var match = Regex.Match(File.ReadAllText(path), @"id\s+'com\.android\.application'\s+version\s+'([^']+)'");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ReadReleaseValue(string path, string key)
    {
        if (!File.Exists(path)) return string.Empty;
        foreach (string line in File.ReadAllLines(path))
            if (line.StartsWith(key + "=", StringComparison.Ordinal))
                return line.Substring(key.Length + 1).Trim('"');
        return string.Empty;
    }

    private static void WriteReport(ResolutionReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
    }

    [Serializable]
    private sealed class DeclaredPackage
    {
        public string spec;
        public string declarationSource;
    }

    [Serializable]
    private sealed class ResolutionReport
    {
        public string result;
        public string startedUtc;
        public string completedUtc;
        public string unityVersion;
        public bool androidTargetActive;
        public string resolverApi;
        public string resolverStateFile;
        public bool resolverStateExists;
        public bool mainGradleTemplatePresent;
        public string bundledAndroidTools;
        public string bundledAgpVersion;
        public string bundledJavaVersion;
        public DeclaredPackage[] declaredPackages;
        public int declaredPackageCount;
        public string[] repositories;
        public bool resolverReturnedSuccess;
        public string[] resolvedProjectLibraries;
        public int resolvedProjectLibraryCount;
        public string failureType;
        public string verificationScope;
    }
}
