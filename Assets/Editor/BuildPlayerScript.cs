using UnityEditor;
using UnityEngine;
using System.IO;

public static class BuildPlayerScript
{
    // Builds an Android debug APK for quick device testing.
    // Usage (from shell):
    // Unity.exe -quit -batchmode -projectPath "<projectPath>" -executeMethod BuildPlayerScript.BuildAndroidDebug

    public static void BuildAndroidDebug()
    {
        string projectPath = Directory.GetCurrentDirectory();
        string buildDir = Path.Combine(projectPath, "Builds", "Android");
        Directory.CreateDirectory(buildDir);

        string apkPath = Path.Combine(buildDir, "XR-Chemistry-Lab-debug.apk");

        // Ensure Android is selected as build target
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("Switching active build target to Android...");
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            if (!ok)
            {
                Debug.LogError("Failed to switch build target to Android. Ensure Android Build Support is installed in your Unity Editor.");
                return;
            }
        }

        // Collect enabled scenes
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled) scenes.Add(s.path);
        }

        if (scenes.Count == 0)
        {
            Debug.LogError("No enabled scenes in Build Settings. Please add the main scene and enable it before running this build.");
            return;
        }

        // Configure development build settings
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.allowDebugging = true;

        var buildOptions = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.AcceptExternalModificationsToPlayer;

        Debug.Log($"Starting Android debug build to: {apkPath}");
        var report = BuildPipeline.BuildPlayer(scenes.ToArray(), apkPath, BuildTarget.Android, buildOptions);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {apkPath}");
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result} | {report.summary.totalErrors} errors");
        }
    }
}