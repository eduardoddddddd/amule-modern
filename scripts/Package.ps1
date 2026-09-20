param([switch]$Zip)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$engineSrc = Join-Path $repoPath 'vendor\amule-3.0.1\amule-portable-x64\bin\amuled.exe'
if (-not (Test-Path -LiteralPath $engineSrc)) { throw 'Ejecuta primero scripts/Setup.ps1 para obtener el motor.' }
& (Join-Path $PSScriptRoot 'Build.ps1') -Publish
$portable = Join-Path $repoPath 'artifacts\portable'
if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Recurse -Force }
New-Item -ItemType Directory -Path $portable | Out-Null
Copy-Item -Path (Join-Path $repoPath 'artifacts\app\*') -Destination $portable -Recurse
$engineDest = Join-Path $portable 'engine'
Copy-Item -Path (Join-Path $repoPath 'vendor\amule-3.0.1\amule-portable-x64') -Destination $engineDest -Recurse
Copy-Item -LiteralPath (Join-Path $repoPath 'docs\engine-manifest.json') -Destination (Join-Path $portable 'engine-manifest.json')
Copy-Item -LiteralPath (Join-Path $repoPath 'LICENSE.md') -Destination (Join-Path $portable 'LICENSE.md')
Copy-Item -LiteralPath (Join-Path $repoPath 'docs\NOTICE-AMULE.md') -Destination (Join-Path $portable 'NOTICE-AMULE.md')
$docs = Join-Path $portable 'docs'
New-Item -ItemType Directory -Path $docs | Out-Null
Copy-Item -LiteralPath (Join-Path $repoPath 'docs\PLAN.md') -Destination (Join-Path $docs 'PLAN.md')
Copy-Item -LiteralPath (Join-Path $repoPath 'docs\STATUS.md') -Destination (Join-Path $docs 'STATUS.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination (Join-Path $portable 'Uninstall.ps1')
Set-Content -LiteralPath (Join-Path $portable 'Iniciar.cmd') -Value "@echo off`r`nstart `"`" `"%~dp0AmuleModern.exe`"`r`n" -Encoding ASCII
if (-not (Test-Path -LiteralPath (Join-Path $portable 'AmuleModern.exe'))) { throw 'El paquete portable no contiene AmuleModern.exe.' }
if (-not (Test-Path -LiteralPath (Join-Path $portable 'engine\bin\amuled.exe'))) { throw 'El paquete portable no contiene el motor.' }
if (-not (Test-Path -LiteralPath (Join-Path $portable 'engine-manifest.json'))) { throw 'Falta engine-manifest.json en el paquete portable.' }
$inventory = @(Get-ChildItem -LiteralPath $portable -Recurse -File -Force | ForEach-Object {
    [ordered]@{ path = $_.FullName.Substring($portable.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[ordered]@{ appId = 'AmuleModern'; schema = 1; files = $inventory } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $portable 'installation-manifest.json') -Encoding UTF8
if ($Zip) {
    $zipPath = Join-Path $repoPath 'artifacts\AmuleModern-portable-win-x64.zip'
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $compressed = $false
    foreach ($attempt in 1..5) {
        try {
            Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zipPath -Force
            $compressed = $true
            break
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    if (-not $compressed) {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::CreateFromDirectory($portable, $zipPath)
    }
    Write-Output "Paquete portable: $zipPath"
}
Write-Output "Directorio portable: $portable"
