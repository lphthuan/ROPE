param()

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $projectPath 'Library\MCPRuntime'
$pythonPath = Join-Path $runtimePath 'Scripts\python.exe'
$serverPath = Join-Path $runtimePath 'Scripts\mcp-for-unity.exe'
$logPath = Join-Path $projectPath 'Logs\MCP'
New-Item -ItemType Directory -Path $logPath -Force | Out-Null

try {
    $health = Invoke-RestMethod 'http://127.0.0.1:8080/health' -TimeoutSec 3
    if ($health.message -eq 'MCP for Unity server is running' -and $health.version -eq '10.3.0') {
        Write-Output 'MCP for Unity 10.3.0 is already running at http://127.0.0.1:8080/mcp.'
        return
    }
    throw 'Port 8080 is serving a different service or MCP version.'
} catch {
    # No healthy local server: prepare the project-local runtime below.
}

$portInUse = Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue
if ($portInUse) { throw 'Port 8080 is occupied. Check the existing service before starting Unity MCP.' }

if (-not (Test-Path -LiteralPath $serverPath)) {
    $uvPath = (Get-Command uv -ErrorAction Stop).Source
    & $uvPath venv --python 3.12 $runtimePath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the MCP Python runtime.' }
    & $uvPath pip install --python $pythonPath 'mcpforunityserver==10.3.0'
    if ($LASTEXITCODE -ne 0) { throw 'Could not install MCP for Unity 10.3.0.' }
}

$env:UNITY_MCP_TELEMETRY_ENABLED = 'false'
$server = Start-Process -FilePath $serverPath `
    -ArgumentList '--transport http --http-url http://127.0.0.1:8080' `
    -WorkingDirectory $projectPath -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $logPath 'Server.stdout.log') `
    -RedirectStandardError (Join-Path $logPath 'Server.stderr.log')
$server.Id | Set-Content -LiteralPath (Join-Path $logPath 'Server.pid')
Write-Output "Started Unity MCP process $($server.Id) at http://127.0.0.1:8080/mcp."
Write-Output 'In Unity, select Tools > ROPE > Connect Unity MCP.'
