using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// CLI batchmode から呼び出して WebGL ビルドをリポジトリ直下の docs/ に出力する。
//   Unity ... -batchmode -nographics -executeMethod WebGLBuilder.BuildWebGL -quit
internal static class WebGLBuilder
{
    // 開いている Editor からビルドする場合は menu: Build > WebGL (docs)
    [MenuItem("Build/WebGL (docs)")]
    public static void BuildWebGLFromMenu() => Build(exitOnFinish: false);

    // CLI batchmode から -executeMethod WebGLBuilder.BuildWebGL で呼ぶ用
    public static void BuildWebGL() => Build(exitOnFinish: true);

    private static void Build(bool exitOnFinish)
    {
        // 解像度を Full HD (1920x1080) 固定にする。
        PlayerSettings.defaultWebScreenWidth = 1920;
        PlayerSettings.defaultWebScreenHeight = 1080;

        // GitHub Pages 対策：brotli/gzip の Content-Encoding を返さないため、
        // 圧縮を無効化しないとロードに失敗する。無圧縮が最も確実。
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        // 念のため解凍フォールバックも有効化（保険）。
        PlayerSettings.WebGL.decompressionFallback = true;

        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) scenes.Add(s.path);

        if (scenes.Count == 0)
        {
            Debug.LogError("WebGLBuilder: Build Settings に有効なシーンがありません。");
            if (exitOnFinish) EditorApplication.Exit(1);
            return;
        }

        // <projectPath>/docs に出力（Application.dataPath は <projectPath>/Assets）
        string outputPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", "docs"));

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"WebGLBuilder: 成功 ({summary.totalSize} bytes) -> {outputPath}");
            if (exitOnFinish) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"WebGLBuilder: 失敗 ({summary.result})");
            if (exitOnFinish) EditorApplication.Exit(1);
        }
    }
}
