param([string]$BlenderPath = 'C:\Program Files\Blender Foundation\Blender 5.0\blender.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'EvolutionShop\ProjectSettings\ProjectVersion.txt'))) { throw 'Not the Evolution Shop tooling root.' }
$sessionDir = Join-Path $projectRoot '.tooling\blender-session'
New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null
$readyPath = Join-Path $sessionDir 'ready.json'
if (Test-Path -LiteralPath $readyPath) {
    $prior = Get-Content -LiteralPath $readyPath -Raw | ConvertFrom-Json
    if (Get-Process -Id $prior.pid -ErrorAction SilentlyContinue) { throw 'Prior tooling PID remains alive. Inspect/stop the owned session before launching again.' }
}
foreach ($name in @('ready.json', 'stop.request', 'stopped.json')) {
    $item = Join-Path $sessionDir $name
    if (Test-Path -LiteralPath $item) { Remove-Item -LiteralPath $item }
}
$env:DISABLE_TELEMETRY = 'true'
$env:BLENDER_MCP_SAFE_MODE = '1'
$bootstrap = Join-Path $PSScriptRoot 'blender_bootstrap.py'
$process = Start-Process -FilePath $BlenderPath -ArgumentList @('--factory-startup', '--python', ('"' + $bootstrap + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $sessionDir 'stdout.log') -RedirectStandardError (Join-Path $sessionDir 'stderr.log')
$deadline = (Get-Date).AddSeconds(45)
while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $readyPath)) {
    if ($process.HasExited) { throw "Blender exited with $($process.ExitCode); inspect $sessionDir." }
    Start-Sleep -Milliseconds 300
}
if (-not (Test-Path -LiteralPath $readyPath)) { throw "Timed out waiting for owned Blender PID $($process.Id); inspect its log before retrying." }
Get-Content -LiteralPath $readyPath -Raw
