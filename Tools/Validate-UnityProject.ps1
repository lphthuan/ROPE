param(
    [string]$EditorPath,
    [ValidateSet('All', 'Compile', 'EditMode', 'PlayMode', 'Scenes', 'PlayerScripts')]
    [string]$Mode = 'All'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') -Raw
$editorVersion = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)').Groups[1].Value

if (-not $EditorPath) {
    $editorRoots = @('C:\Program Files\Unity\Hub\Editor', 'D:\Program Files\Unity Hub\Editors')
    $hubInstallPath = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $hubInstallPath) {
        $editorRoots += Get-Content -LiteralPath $hubInstallPath -Raw | ConvertFrom-Json
    }
    foreach ($editorRoot in $editorRoots) {
        $candidate = Join-Path $editorRoot "$editorVersion\Editor\Unity.exe"
        if (Test-Path -LiteralPath $candidate) {
            $EditorPath = $candidate
            break
        }
    }
}
if (-not $EditorPath -or -not (Test-Path -LiteralPath $EditorPath)) {
    throw "Unity $editorVersion was not found. Pass -EditorPath with the path to Unity.exe."
}
$monoCore = Join-Path (Split-Path -Parent $EditorPath) 'Data\MonoBleedingEdge\lib\mono\unityjit-win32\mscorlib.dll'
if (-not (Test-Path -LiteralPath $monoCore)) {
    throw "The Unity installation is incomplete: $monoCore is missing. Repair this Editor before running validation."
}

$outputDirectory = Join-Path $projectRoot 'Logs\Validation'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

function Invoke-UnityValidation {
    param([string]$RunName, [string[]]$UnityArguments)
    $lockPath = Join-Path $projectRoot 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $lockPath) {
        try {
            $lockProbe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
            $lockProbe.Dispose()
        } catch {
            throw 'ROPE is already open in Unity. Save the scene and close that Editor before running CLI validation.'
        }
    }
    $logPath = Join-Path $outputDirectory "$RunName.log"
    $arguments = @('-batchmode', '-projectPath', "`"$projectRoot`"", '-logFile', "`"$logPath`"") + $UnityArguments
    Write-Host "Running $RunName using Unity $editorVersion..."
    $process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        # Start-Process -Wait also waits for persistent licensing helper processes.
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "Unity $RunName failed (exit $($process.ExitCode)). See $logPath"
        }
    }
    finally {
        $process.Dispose()
    }
    Write-Host "$RunName passed. Log: $logPath"
}

if ($Mode -in @('All', 'Compile')) {
    Invoke-UnityValidation -RunName 'Compile' -UnityArguments @('-quit')
}
foreach ($testMode in @('EditMode', 'PlayMode')) {
    if ($Mode -in @('All', $testMode)) {
        $resultsPath = Join-Path $outputDirectory "$testMode.xml"
        if (Test-Path -LiteralPath $resultsPath) {
            Remove-Item -LiteralPath $resultsPath
        }
        # The test runner exits when finished; -quit would stop it before completion.
        Invoke-UnityValidation -RunName $testMode -UnityArguments @('-runTests', '-testPlatform', $testMode,
            '-testResults', "`"$resultsPath`"")
        if (-not (Test-Path -LiteralPath $resultsPath)) {
            throw "Unity did not produce $resultsPath. The test run is incomplete."
        }
        [xml]$results = Get-Content -LiteralPath $resultsPath -Raw
        $run = $results.'test-run'
        if ([int]$run.total -eq 0 -or [int]$run.failed -ne 0 -or $run.result -ne 'Passed') {
            throw "$testMode did not pass: result=$($run.result), total=$($run.total), failed=$($run.failed)."
        }
        Write-Host "$testMode tests: $($run.passed)/$($run.total) passed. Results: $resultsPath"
    }
}
if ($Mode -in @('All', 'Scenes')) {
    Invoke-UnityValidation -RunName 'Scenes' -UnityArguments @('-quit', '-executeMethod', 'ROPE.Editor.ProjectValidation.ValidateScenes')
}
if ($Mode -in @('All', 'PlayerScripts')) {
    Invoke-UnityValidation -RunName 'PlayerScripts' -UnityArguments @('-quit', '-executeMethod', 'ROPE.Editor.ProjectValidation.CompilePlayerScripts')
}
