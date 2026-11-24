using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CI에서 호출하는 빌드 진입점.
///   Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildWebGL
/// 환경 변수
///   BUILD_VERSION : 게임 화면에 표시할 버전 (예: v1.0.0 → 1.0.0). 없으면 프로젝트 설정 그대로.
///   BUILD_OUTPUT  : 출력 폴더 (기본 Builds/WebGL)
/// </summary>
public static class BuildScript
{
    public static void BuildWebGL()
    {
        var output = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) output = Path.Combine("Builds", "WebGL");

        var version = Environment.GetEnvironmentVariable("BUILD_VERSION");
        if (!string.IsNullOrWhiteSpace(version))
            PlayerSettings.bundleVersion = version.Trim().TrimStart('v');

        // itch.io 등 웹 호스팅에서 바로 실행되도록: gzip 압축 + 압축 해제 폴백
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

        Debug.Log($"[BuildScript] WebGL build start → {output} (version {PlayerSettings.bundleVersion})");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"[BuildScript] result={summary.result}, size={summary.totalSize} bytes, time={summary.totalTime}");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
