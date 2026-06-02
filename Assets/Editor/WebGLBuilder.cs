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

        // WebGL の既定スタック（512KB）は小さく、AI のミニマックス再帰で
        // "Maximum call stack size exceeded" になる。スタックを 8MB に拡大する。
        // 注意：INITIAL_MEMORY(32MB) より十分小さくしないとリンクに失敗する。
        // 過去ビルドが ProjectSettings に値を永続化するため、毎回正規化する
        // （既存の STACK_SIZE 指定を除去してから付与し直す）。
        string args = PlayerSettings.WebGL.emscriptenArgs ?? "";
        args = System.Text.RegularExpressions.Regex.Replace(args, @"\s*-sSTACK_SIZE=\S+", "").Trim();
        PlayerSettings.WebGL.emscriptenArgs = (args + " -sSTACK_SIZE=8388608").Trim(); // 8 MB

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
            PostProcessIndexHtml(System.IO.Path.Combine(outputPath, "index.html"));
            Debug.Log($"WebGLBuilder: 成功 ({summary.totalSize} bytes) -> {outputPath}");
            if (exitOnFinish) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"WebGLBuilder: 失敗 ({summary.result})");
            if (exitOnFinish) EditorApplication.Exit(1);
        }
    }

    // 生成された index.html を後処理して、Full HD(16:9) を維持したまま
    // 画面に収まる（レターボックス）表示にする。再ビルドのたびに適用される。
    private static void PostProcessIndexHtml(string indexPath)
    {
        if (!System.IO.File.Exists(indexPath))
        {
            Debug.LogWarning($"WebGLBuilder: index.html が見つかりません: {indexPath}");
            return;
        }

        string html = System.IO.File.ReadAllText(indexPath);

        // 1) 内部レンダー解像度を 1920x1080 に固定（DOM サイズに追従させない）
        if (html.Contains("// config.matchWebGLToCanvasSize = false;"))
            html = html.Replace("// config.matchWebGLToCanvasSize = false;",
                                 "config.matchWebGLToCanvasSize = false;");

        // 2) 16:9 を保ったまま画面に収める CSS を <head> に注入（重複注入は避ける）
        const string marker = "id=\"webgl-fit-style\"";
        if (!html.Contains(marker))
        {
            string style =
                "    <style id=\"webgl-fit-style\">\n" +
                "      html, body { height: 100%; margin: 0; background: #231F20; overflow: hidden; }\n" +
                "      #unity-container.unity-desktop, #unity-container.unity-mobile {\n" +
                "        position: fixed; inset: 0; transform: none;\n" +
                "        display: flex; align-items: center; justify-content: center;\n" +
                "        width: 100%; height: 100%;\n" +
                "      }\n" +
                "      #unity-canvas, .unity-mobile #unity-canvas {\n" +
                "        width: min(100vw, calc(100vh * 16 / 9)) !important;\n" +
                "        height: min(100vh, calc(100vw * 9 / 16)) !important;\n" +
                "      }\n" +
                "      #unity-footer { display: none; }\n" +
                "    </style>\n";
            html = html.Replace("</head>", style + "  </head>");
        }

        System.IO.File.WriteAllText(indexPath, html);
        Debug.Log("WebGLBuilder: index.html を後処理（Full HD固定＋画面フィット）しました。");
    }
}
