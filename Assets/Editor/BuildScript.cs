using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// CI에서 호출하는 빌드 진입점.
///   Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildWebGL
///   Unity.exe -batchmode -projectPath . -executeMethod BuildScript.BuildWindows
/// 환경 변수
///   BUILD_VERSION : 게임 화면에 표시할 버전 (예: v1.0.0 → 1.0.0). 없으면 프로젝트 설정 그대로.
///   BUILD_OUTPUT  : 출력 폴더 (기본 Builds/WebGL, Builds/Windows)
/// </summary>
public static class BuildScript
{
    public static void BuildWebGL()
    {
        // itch.io 등 웹 호스팅에서 바로 실행되도록: gzip 압축 + 압축 해제 폴백
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        // WebAssembly 2023 대상: 구형 MVP 대상일 때 실행되는 wasm-opt 변환(--signext-lowering)에서
        // Emscripten 최적화 도구가 Assertion 오류로 비정상 종료하는 문제가 있어 최신 표준으로 빌드한다.
        // (Chrome 91+, Firefox 89+, Safari 16.4+ 지원 — Unity도 이 옵션을 권장)
        PlayerSettings.WebGL.wasm2023 = true;

        var output = OutputDir(Path.Combine("Builds", "WebGL"));
        Build(BuildTarget.WebGL, output, output);
    }

    /// <summary>Windows 64비트 PC 빌드. 결과 폴더를 그대로 itch.io windows 채널에 올린다.</summary>
    public static void BuildWindows()
    {
        var output = OutputDir(Path.Combine("Builds", "Windows"));
        Build(BuildTarget.StandaloneWindows64, Path.Combine(output, "REFRACT.exe"), output);
    }

    private static string OutputDir(string fallback)
    {
        var output = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
        return string.IsNullOrWhiteSpace(output) ? fallback : output;
    }

    private static void Build(BuildTarget target, string locationPath, string outputDir)
    {
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION");
        if (!string.IsNullOrWhiteSpace(version))
            PlayerSettings.bundleVersion = version.Trim().TrimStart('v');

        // 러너가 작업 폴더를 지우지 않으므로(clean: false), 이전 빌드 파일이 섞여 배포되지 않게 비운다
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

        Debug.Log($"[BuildScript] {target} build start → {locationPath} (version {PlayerSettings.bundleVersion})");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = locationPath,
            target = target,
            options = BuildOptions.None
        });

        var summary = report.summary;
        Debug.Log($"[BuildScript] result={summary.result}, size={summary.totalSize} bytes, time={summary.totalTime}");

        if (summary.result == BuildResult.Succeeded)
            RemoveDoNotShipFolders(outputDir);

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    /// <summary>
    /// Burst가 만드는 "*_DoNotShip" 디버그 폴더는 배포하면 안 되므로 빌드 폴더에서 지운다.
    /// </summary>
    private static void RemoveDoNotShipFolders(string outputDir)
    {
        if (!Directory.Exists(outputDir)) return;
        foreach (var dir in Directory.GetDirectories(outputDir, "*DoNotShip*"))
        {
            Directory.Delete(dir, recursive: true);
            Debug.Log($"[BuildScript] removed {dir}");
        }
    }
}
