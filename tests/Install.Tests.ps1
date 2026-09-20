$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repo ('.local\install-test-' + [guid]::NewGuid().ToString('N'))
$target = Join-Path $testRoot 'installed app'
$checks = 0
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw "FAIL: $message" }; $script:checks++; Write-Output "PASS: $message" }
& (Join-Path $repo 'scripts\Install.ps1') -Prefix $target
Check ((Test-Path -LiteralPath (Join-Path $target 'installation-manifest.json')) -and (Test-Path -LiteralPath (Join-Path $target 'engine\bin\amuled.exe'))) 'installation includes engine and uninstall inventory'
$sentinel = Join-Path $target 'engine\bin\personal.txt'
Set-Content -LiteralPath $sentinel -Value 'not a package file'
& (Join-Path $repo 'scripts\Install.ps1') -Prefix $target
Check (Test-Path -LiteralPath $sentinel) 'updating a package preserves unrelated files inside package directories'
$captureFile = Join-Path $testRoot 'installed-ui.png'
$capture = Start-Process -FilePath (Join-Path $target 'AmuleModern.exe') -ArgumentList @('--capture', ('"' + $captureFile + '"'), '--servers') -PassThru -WindowStyle Hidden
if (-not $capture.WaitForExit(30000)) { throw 'Installed capture did not exit in 30 seconds.' }
Check ($capture.ExitCode -eq 0 -and (Test-Path -LiteralPath $captureFile)) 'installed app in a path with spaces launches its bundled motor and exits cleanly'
& (Join-Path $target 'Uninstall.ps1') -RemoveProfile
Check ((Test-Path -LiteralPath $sentinel) -and -not (Test-Path -LiteralPath (Join-Path $target 'AmuleModern.exe'))) 'packaged uninstaller removes its installation while retaining unrelated files'
$reinstallTarget = Join-Path $testRoot 'profile-preserved'
& (Join-Path $repo 'scripts\Install.ps1') -Prefix $reinstallTarget
New-Item -ItemType Directory -Path (Join-Path $reinstallTarget '.local') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $reinstallTarget '.local\keep.txt') -Value 'profile'
& (Join-Path $reinstallTarget 'Uninstall.ps1')
& (Join-Path $repo 'scripts\Install.ps1') -Prefix $reinstallTarget
Check (Test-Path -LiteralPath (Join-Path $reinstallTarget '.local\keep.txt')) 'reinstall accepts and preserves the retained profile'
& (Join-Path $reinstallTarget 'Uninstall.ps1') -RemoveProfile
Write-Output "RESULT: $checks package/install checks passed. Fixtures retained under $testRoot."
