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
    const string OutDir = "Build/PanicConsoleSlice";
    const string OutExe = OutDir + "/PanicConsoleSlice.exe";

    // 只建立場景（供測試或重建用）
    public static void BuildSceneOnly()
    {
        CreateScene();
    }

    static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("SliceGame");
        go.AddComponent<SliceGame>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[SliceBuild] scene saved: " + ScenePath);
    }

    public static void BuildWin64()
    {
        CreateScene();

        Directory.CreateDirectory(OutDir);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
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
