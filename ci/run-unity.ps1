<#
  REFRACT CI helper - runs the Unity editor in batch mode on the self-hosted Windows runner.

  Usage (from the repository root):
    powershell -File ci/run-unity.ps1 -Mode Tests
    powershell -File ci/run-unity.ps1 -Mode BuildWebGL

  Why a script: Unity.exe is a GUI-subsystem program, so PowerShell does not wait for it
  when it is called directly. Start-Process -Wait makes the CI step wait and lets us read
  the real exit code. The script also prints the Unity log and writes a test summary.

  Unity path: taken from $env:UNITY_PATH if set, otherwise built from
  ProjectSettings/ProjectVersion.txt and the default Unity Hub install location.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Tests', 'BuildWebGL')]
    [string]$Mode
)

$ErrorActionPreference = 'Stop'
$project = (Get-Location).Path

# ---------- locate Unity ----------
$unity = $env:UNITY_PATH
if ([string]::IsNullOrWhiteSpace($unity)) {
    $versionLine = Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    $version = ($versionLine -split ':', 2)[1].Trim()
    $unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $unity)) {
    Write-Host "::error::Unity editor not found at '$unity'. Set the UNITY_PATH environment variable."
    exit 1
}
Write-Host "Unity : $unity"
Write-Host "Mode  : $Mode"

$outDir = Join-Path $project 'ci-output'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$logFile = Join-Path $outDir "unity-$Mode.log"

# ---------- build the argument list ----------
$unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$project`"", '-logFile', "`"$logFile`"")
if ($Mode -eq 'Tests') {
    $resultsFile = Join-Path $outDir 'editmode-results.xml'
    $unityArgs += @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$resultsFile`"")
} else {
    $unityArgs += @('-executeMethod', 'BuildScript.BuildWebGL')
}

# ---------- run Unity and wait ----------
$started = Get-Date
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$exitCode = $proc.ExitCode
$elapsed = [int]((Get-Date) - $started).TotalSeconds
Write-Host "Unity exited with code $exitCode after $elapsed s"

# show the end of the Unity log (the full log is uploaded as an artifact)
if (Test-Path $logFile) {
    Write-Host '----- Unity log (last 60 lines) -----'
    Get-Content $logFile -Tail 60
    Write-Host '-------------------------------------'
}

# ---------- test summary ----------
if ($Mode -eq 'Tests') {
    if (-not (Test-Path $resultsFile)) {
        Write-Host "::error::Test results file was not created. See the Unity log above."
        exit 1
    }
    [xml]$xml = Get-Content $resultsFile
    $run = $xml.'test-run'
    $lines = @('## Unity EditMode Tests (self-hosted)', '',
               "**Total $($run.total) / Passed $($run.passed) / Failed $($run.failed)** ($elapsed s)", '',
               '| Result | Test |', '|---|---|')
    foreach ($case in $xml.SelectNodes('//test-case')) {
        $icon = if ($case.result -eq 'Passed') { 'PASS' } else { 'FAIL' }
        $lines += "| $icon | $($case.name) |"
        if ($case.result -ne 'Passed') {
            Write-Host "::error::Test failed: $($case.fullname)"
        }
    }
    if ($env:GITHUB_STEP_SUMMARY) {
        $lines -join "`n" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }
    Write-Host "Tests: total $($run.total), passed $($run.passed), failed $($run.failed)"
    if ([int]$run.failed -gt 0) { exit 1 }
}

if ($Mode -eq 'BuildWebGL' -and $exitCode -eq 0 -and $env:GITHUB_STEP_SUMMARY) {
    "## WebGL build`n`nBuilt in $elapsed s." | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
}

exit $exitCode
