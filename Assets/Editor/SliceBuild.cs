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
    const string DuelScenePath = "Assets/Scenes/Duel.unity";   // 新主模式：1v1 對戰（開場場景）
    const string ScenePath = "Assets/Scenes/Slice.unity";
    const string VersusScenePath = "Assets/Scenes/Versus.unity";
    const string BomberScenePath = "Assets/Scenes/Bomber.unity";
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

        // 主模式：1v1 對戰（建置索引 0 = 開場）
        var duel = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("DuelGame").AddComponent<DuelGame>();
        EditorSceneManager.SaveScene(duel, DuelScenePath);
        Debug.Log("[SliceBuild] scene saved: " + DuelScenePath);

        var slice = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("SliceGame").AddComponent<SliceGame>();
        EditorSceneManager.SaveScene(slice, ScenePath);
        Debug.Log("[SliceBuild] scene saved: " + ScenePath);

        var versus = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("VersusGame").AddComponent<VersusGame>();
        EditorSceneManager.SaveScene(versus, VersusScenePath);
        Debug.Log("[SliceBuild] scene saved: " + VersusScenePath);

        var bomber = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("BomberGame").AddComponent<BomberGame>();
        EditorSceneManager.SaveScene(bomber, BomberScenePath);
        Debug.Log("[SliceBuild] scene saved: " + BomberScenePath);
    }

    public static void BuildWin64()
    {
        CreateScenes();

        Directory.CreateDirectory(OutDir);
        var options = new BuildPlayerOptions
        {
            scenes = new[] { DuelScenePath, ScenePath, VersusScenePath, BomberScenePath },
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
