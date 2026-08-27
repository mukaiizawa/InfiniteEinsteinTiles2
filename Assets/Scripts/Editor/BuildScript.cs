using System;
using System.Linq;

using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Entry points for the batchmode build pipeline (build_iet2.bat, build_iet2_demo.bat).
public static class BuildScript
{
    const string ProjectRoot = @"C:\InfiniteEinsteinTiles2";
    const string BuildRoot = ProjectRoot + @"\build";
    const string ExeName = "Infinite Einstein Tiles2.exe";
    const string AppName = "Infinite Einstein Tiles2.app";
    const string WinOutPath = BuildRoot + @"\windows\" + ExeName;
    const string MacOutPath = BuildRoot + @"\macOS\" + AppName;

    public static void BuildAllProduct()
    {
        BuildAll(demo: false);
        EditorApplication.Exit(0);
    }

    public static void BuildAllDemo()
    {
        BuildAll(demo: true);
        EditorApplication.Exit(0);
    }

    static void BuildAll(bool demo)
    {
        var namedTarget = NamedBuildTarget.Standalone;
        var originalDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        try
        {
            if (demo)
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, AddDefine(originalDefines, "DEMO"));
            BuildAddressables();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            Build(BuildTarget.StandaloneWindows64, WinOutPath, scenes);
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            Build(BuildTarget.StandaloneOSX, MacOutPath, scenes);
        }
        finally
        {
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, originalDefines);
            AssetDatabase.SaveAssets();
        }
    }

    static void BuildAddressables()
    {
        Debug.Log("[BuildScript] Building Addressables...");
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError($"[BuildScript] Addressables FAILED: {result.Error}");
            EditorApplication.Exit(1);
        }
        Debug.Log("[BuildScript] Addressables done.");
    }

    static void Build(BuildTarget target, string outputPath, string[] scenes)
    {
        Debug.Log($"[BuildScript] Building {target} -> {outputPath}");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
        });
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] FAILED: {target} - {report.summary.totalErrors} errors");
            EditorApplication.Exit(1);
        }
        Debug.Log($"[BuildScript] Done: {target}");
    }

    static string AddDefine(string existing, string define)
    {
        var list = existing.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!list.Contains(define))
            list.Add(define);
        return string.Join(";", list);
    }
}
