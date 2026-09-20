$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $repo ('.local\uninstall-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$uninstaller = Join-Path $repo 'scripts\Uninstall.ps1'
$checks = 0
function Check([bool]$condition, [string]$message) { if (-not $condition) { throw "FAIL: $message" }; $script:checks++; Write-Output "PASS: $message" }
function New-Fixture([string]$name) {
    $root = Join-Path $testRoot $name
    New-Item -ItemType Directory -Path $root | Out-Null
    Set-Content -LiteralPath (Join-Path $root 'AmuleModern.exe') -Value 'fixture, not executable'
    Set-Content -LiteralPath (Join-Path $root 'engine-manifest.json') -Value '{}'
    Set-Content -LiteralPath (Join-Path $root 'owned.dll') -Value 'original owned file'
    Copy-Item -LiteralPath $uninstaller -Destination (Join-Path $root 'Uninstall.ps1')
    $files = @(Get-ChildItem -LiteralPath $root -File | ForEach-Object { @{ path=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
    @{ appId='AmuleModern'; schema=1; files=$files } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'installation-manifest.json')
    return $root
}
$portable = New-Fixture 'portable'
Set-Content -LiteralPath (Join-Path $portable 'personal.txt') -Value 'not owned'
Set-Content -LiteralPath (Join-Path $portable 'owned.dll') -Value 'user modification'
New-Item -ItemType Directory -Path (Join-Path $portable '.local') | Out-Null
Set-Content -LiteralPath (Join-Path $portable '.local\keep.txt') -Value 'profile'
# Redirect only this test process's default install location to a disposable sentinel.
$previousLocalAppData = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = Join-Path $testRoot 'mock-localappdata'
    $otherInstall = Join-Path $env:LOCALAPPDATA 'AmuleModern'
    New-Item -ItemType Directory -Path $otherInstall -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $otherInstall 'keep.txt') -Value 'other installation'
    & (Join-Path $portable 'Uninstall.ps1')
    Check (-not (Test-Path -LiteralPath (Join-Path $portable 'AmuleModern.exe'))) 'default target is the folder containing the portable uninstaller'
    Check (Test-Path -LiteralPath (Join-Path $otherInstall 'keep.txt')) 'a different installation remains untouched'
    Check ((Test-Path -LiteralPath (Join-Path $portable 'personal.txt')) -and (Test-Path -LiteralPath (Join-Path $portable 'owned.dll'))) 'unowned and modified files are preserved'
    Check (Test-Path -LiteralPath (Join-Path $portable '.local\keep.txt')) 'profile is preserved by default'
} finally { $env:LOCALAPPDATA = $previousLocalAppData }
$unowned = Join-Path $testRoot 'not-an-install'
New-Item -ItemType Directory -Path $unowned | Out-Null
Set-Content -LiteralPath (Join-Path $unowned 'keep.txt') -Value 'unrelated'
$rejected = $false
try { & $uninstaller -Prefix $unowned } catch { $rejected = $true }
Check ($rejected -and (Test-Path -LiteralPath (Join-Path $unowned 'keep.txt'))) 'missing inventory is rejected without deleting files'
$traversal = New-Fixture 'traversal'
$manifestPath = Join-Path $traversal 'installation-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.files += [pscustomobject]@{ path='..\not-an-install\keep.txt'; sha256=(Get-FileHash -LiteralPath (Join-Path $unowned 'keep.txt')).Hash }
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath
$rejected = $false
try { & $uninstaller -Prefix $traversal } catch { $rejected = $true }
Check ($rejected -and (Test-Path -LiteralPath (Join-Path $traversal 'AmuleModern.exe')) -and (Test-Path -LiteralPath (Join-Path $unowned 'keep.txt'))) 'traversal is rejected before any package file is removed'
$junction = New-Fixture 'junction'
New-Item -ItemType Junction -Path (Join-Path $junction 'escape') -Target $unowned | Out-Null
$manifestPath = Join-Path $junction 'installation-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.files += [pscustomobject]@{ path='escape\keep.txt'; sha256=(Get-FileHash -LiteralPath (Join-Path $unowned 'keep.txt')).Hash }
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath
$rejected = $false
try { & $uninstaller -Prefix $junction } catch { $rejected = $true }
Check ($rejected -and (Test-Path -LiteralPath (Join-Path $unowned 'keep.txt')) -and (Test-Path -LiteralPath (Join-Path $junction 'AmuleModern.exe'))) 'junction escape is rejected before deletion'
$removable = New-Fixture 'remove-profile'
New-Item -ItemType Directory -Path (Join-Path $removable '.local') | Out-Null
Set-Content -LiteralPath (Join-Path $removable '.local\profile.txt') -Value 'fixture'
Set-Content -LiteralPath (Join-Path $removable 'personal.txt') -Value 'preserve'
& $uninstaller -Prefix $removable -RemoveProfile
Check (-not (Test-Path -LiteralPath (Join-Path $removable '.local')) -and (Test-Path -LiteralPath (Join-Path $removable 'personal.txt'))) 'explicit profile removal preserves unrelated root files'
Write-Output "RESULT: $checks uninstall checks passed. Fixtures retained under $testRoot."
