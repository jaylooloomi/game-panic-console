using System.IO;
using PanicConsole.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 無頭（batchmode）建立切片場景並打包 Windows 64 player。
/// 用法：Unity.exe -batchmode -quit -projectPath . -executeMethod SliceBuild.BuildWin64 -logFile -
/// </summary>
public static class SliceBuild
{
    const string ScenePath = "Assets/Scenes/Slice.unity";
    const string VersusScenePath = "Assets/Scenes/Versus.unity";
    const string OutDir = "Build/PanicConsoleSlice";
    const string OutExe = OutDir + "/PanicConsoleSlice.exe";

    // 只建立場景（供測試或重建用）
    public static void BuildSceneOnly()
    {
        CreateScenes();
    }

    static void CreateScenes()
    {
        Directory.CreateDirectory("Assets/Scenes");

        var slice = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("SliceGame").AddComponent<SliceGame>();
        EditorSceneManager.SaveScene(slice, ScenePath);
        Debug.Log("[SliceBuild] scene saved: " + ScenePath);

        var versus = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("VersusGame").AddComponent<VersusGame>();
        EditorSceneManager.SaveScene(versus, VersusScenePath);
        Debug.Log("[SliceBuild] scene saved: " + VersusScenePath);
    }

    public static void BuildWin64()
    {
        CreateScenes();

        Directory.CreateDirectory(OutDir);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath, VersusScenePath },
            locationPathName = OutExe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[SliceBuild] result={summary.result} totalErrors={summary.totalErrors} sizeBytes={summary.totalSize} output={OutExe}");

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("[SliceBuild] BUILD FAILED");
            EditorApplication.Exit(1);
        }
    }
}
