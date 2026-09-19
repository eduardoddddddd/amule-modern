$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$appPath = Join-Path $repoPath 'artifacts\app\AmuleModern.exe'
if (-not (Test-Path -LiteralPath $appPath)) { & (Join-Path $PSScriptRoot 'Build.ps1') -Publish }
# User-facing desktop application: this window is intentionally visible.
Start-Process -FilePath $appPath -WorkingDirectory $repoPath -WindowStyle Normal
