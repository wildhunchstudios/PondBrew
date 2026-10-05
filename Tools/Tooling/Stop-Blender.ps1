$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$sessionDir = Join-Path $projectRoot '.tooling\blender-session'
$ready = Get-Content -LiteralPath (Join-Path $sessionDir 'ready.json') -Raw | ConvertFrom-Json
if ($ready.projectRoot -ne $projectRoot -or $ready.host -ne '127.0.0.1' -or $ready.port -ne 19876) { throw 'Session ownership mismatch.' }
$process = Get-Process -Id $ready.pid -ErrorAction SilentlyContinue
if (-not $process) { Write-Output 'Owned Blender session already stopped.'; exit 0 }
if ($process.ProcessName -ne 'blender') { throw 'PID was reused by another process; refusing stop.' }
Set-Content -LiteralPath (Join-Path $sessionDir 'stop.request') -Value 'Stop this isolated tooling session.'
if (-not $process.WaitForExit(15000)) { throw 'Clean stop timed out; no force-kill attempted.' }
Write-Output "Owned Blender PID $($ready.pid) stopped; exit code is not available from this attached process handle."
