using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

/// <summary>Explicitly selects the installed Android build tools required by the current ad SDKs.</summary>
public static class QTAndroidBuildTools
{
    private const string BeforeReport = "Docs/SDKIntegration/QT-20261009/toolchain-before.json";
    private const string AfterReport = "Docs/SDKIntegration/QT-20261009/toolchain-after.json";
    private const string GradleProperties = "Assets/Plugins/Android/gradleTemplate.properties";
    private const string DaemonJavaProperty = "org.gradle.java.home";

    [MenuItem("Tools/QT SDK/Use Installed Android Build Tools")]
    public static void Run()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string jdk = Path.Combine(profile, ".jdks/jbr-17.0.14");
        string gradle = Path.Combine(profile,
            ".gradle/wrapper/dists/gradle-8.13-bin/5xuhj0ry160q40clulazy9h7d/gradle-8.13");
        RequireFile(Path.Combine(jdk, "bin/javac.exe"));
        RequireFile(Path.Combine(gradle, "bin/gradle.bat"));
        RequireFile(Path.Combine(gradle, "lib/gradle-launcher-8.13.jar"));
        if (!SamePath(ReadGradleDaemonJavaHome(), jdk))
            throw new InvalidOperationException("The project Gradle properties must select the installed JDK 17 for the daemon.");

        ToolchainSnapshot previous = Capture();
        if (!File.Exists(BeforeReport)) WriteReport(BeforeReport, previous);
        try
        {
            // Unity 2022 keeps its supported JDK 11; org.gradle.java.home selects JDK 17 for the Gradle daemon.
            AndroidExternalToolsSettings.gradlePath = gradle;
            if (!SamePath(AndroidExternalToolsSettings.jdkRootPath, previous.jdkRootPath) ||
                !SamePath(AndroidExternalToolsSettings.gradlePath, gradle))
                throw new InvalidOperationException("Unity did not retain the requested Android tool paths.");
            WriteReport(AfterReport, Capture());
        }
        catch
        {
            Apply(previous);
            throw;
        }
        Debug.Log("QT_ANDROID_BUILD_TOOLS_PASS: Unity Java=" + AndroidExternalToolsSettings.jdkRootPath +
                  "; Gradle daemon Java=" + ReadGradleDaemonJavaHome() +
                  "; Gradle=" + AndroidExternalToolsSettings.gradlePath +
                  "; original preferences saved to " + BeforeReport);
    }

    [MenuItem("Tools/QT SDK/Restore Previous Android Build Tools")]
    public static void Restore()
    {
        RequireFile(BeforeReport);
        ToolchainSnapshot previous = JsonUtility.FromJson<ToolchainSnapshot>(File.ReadAllText(BeforeReport));
        if (previous == null || string.IsNullOrEmpty(previous.jdkRootPath) ||
            string.IsNullOrEmpty(previous.gradlePath))
            throw new InvalidDataException("The saved Android toolchain preferences are incomplete.");
        Apply(previous);
        RestoreGradleDaemonJavaHome(previous.gradleDaemonJavaHome);
        WriteReport(AfterReport, Capture());
        Debug.Log("QT_ANDROID_BUILD_TOOLS_RESTORED: " + BeforeReport);
    }

    private static void Apply(ToolchainSnapshot snapshot)
    {
        AndroidExternalToolsSettings.gradlePath = snapshot.gradleUseEmbedded ? string.Empty : snapshot.gradlePath;
    }

    private static ToolchainSnapshot Capture()
    {
        return new ToolchainSnapshot
        {
            capturedUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            projectPath = Path.GetFullPath("."),
            jdkRootPath = AndroidExternalToolsSettings.jdkRootPath,
            unityJavaVersion = ReadJavaVersion(AndroidExternalToolsSettings.jdkRootPath),
            gradleDaemonJavaHome = ReadGradleDaemonJavaHome(),
            gradleDaemonJavaVersion = ReadJavaVersion(string.IsNullOrEmpty(ReadGradleDaemonJavaHome())
                ? AndroidExternalToolsSettings.jdkRootPath : ReadGradleDaemonJavaHome()),
            gradlePath = AndroidExternalToolsSettings.gradlePath,
            sdkRootPath = AndroidExternalToolsSettings.sdkRootPath,
            ndkRootPath = AndroidExternalToolsSettings.ndkRootPath,
            jdkUseEmbeddedPreferencePresent = EditorPrefs.HasKey("JdkUseEmbedded"),
            jdkUseEmbedded = EditorPrefs.GetBool("JdkUseEmbedded", true),
            gradleUseEmbeddedPreferencePresent = EditorPrefs.HasKey("GradleUseEmbedded"),
            gradleUseEmbedded = EditorPrefs.GetBool("GradleUseEmbedded", true),
            scope = "Only the shared Gradle preference changes. Unity keeps JDK 11; project org.gradle.java.home selects daemon JDK 17. SDK and NDK paths are unchanged."
        };
    }

    private static string ReadGradleDaemonJavaHome()
    {
        if (!File.Exists(GradleProperties)) return string.Empty;
        foreach (string line in File.ReadAllLines(GradleProperties))
        {
            string value = line.Trim();
            if (value.StartsWith(DaemonJavaProperty + "=", StringComparison.Ordinal))
                return value.Substring(DaemonJavaProperty.Length + 1).Trim();
        }
        return string.Empty;
    }

    private static string ReadJavaVersion(string root)
    {
        string releasePath = Path.Combine(root, "release");
        if (!File.Exists(releasePath)) return string.Empty;
        foreach (string line in File.ReadAllLines(releasePath))
            if (line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal))
                return line.Substring("JAVA_VERSION=".Length).Trim('"');
        return string.Empty;
    }

    private static void RestoreGradleDaemonJavaHome(string originalPath)
    {
        var properties = new StringBuilder();
        foreach (string line in File.ReadAllLines(GradleProperties))
            if (!line.Trim().StartsWith(DaemonJavaProperty + "=", StringComparison.Ordinal))
                properties.AppendLine(line);
        if (!string.IsNullOrEmpty(originalPath))
            properties.AppendLine(DaemonJavaProperty + "=" + originalPath.Replace('\\', '/'));
        File.WriteAllText(GradleProperties, properties.ToString(), new UTF8Encoding(false));
    }

    private static bool SamePath(string first, string second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second)) return false;
        return string.Equals(Path.GetFullPath(first).TrimEnd('/', '\\'),
            Path.GetFullPath(second).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    private static void RequireFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Required Android build tool file is missing.", path);
    }

    private static void WriteReport(string path, ToolchainSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
    }

    [Serializable]
    private sealed class ToolchainSnapshot
    {
        public string capturedUtc;
        public string unityVersion;
        public string projectPath;
        public string jdkRootPath;
        public string unityJavaVersion;
        public string gradleDaemonJavaHome;
        public string gradleDaemonJavaVersion;
        public string gradlePath;
        public string sdkRootPath;
        public string ndkRootPath;
        public bool jdkUseEmbeddedPreferencePresent;
        public bool jdkUseEmbedded;
        public bool gradleUseEmbeddedPreferencePresent;
        public bool gradleUseEmbedded;
        public string scope;
    }
}
