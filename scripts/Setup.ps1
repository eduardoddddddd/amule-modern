$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$sdkVersion = (Get-Content -LiteralPath (Join-Path $repoPath 'global.json') -Raw | ConvertFrom-Json).sdk.version
$toolsPath = Join-Path $repoPath '.tools'
New-Item -ItemType Directory -Path $toolsPath -Force | Out-Null
$dotnetExe = Join-Path $toolsPath 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) {
    $installScript = Join-Path $toolsPath 'dotnet-install.ps1'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript
    & $installScript -Version $sdkVersion -InstallDir (Join-Path $toolsPath 'dotnet') -NoPath
    if (-not (Test-Path -LiteralPath $dotnetExe)) { throw 'No se instaló el SDK.' }
}
$actualSdk = & $dotnetExe --version
if ($actualSdk.Trim() -ne $sdkVersion) { throw "SDK inesperado: $actualSdk" }
$manifest = Get-Content -LiteralPath (Join-Path $repoPath 'docs\engine-manifest.json') -Raw | ConvertFrom-Json
$vendorPath = Join-Path $repoPath 'vendor'
New-Item -ItemType Directory -Path $vendorPath -Force | Out-Null
$archive = Join-Path $vendorPath "amule-$($manifest.version).zip"
if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -UseBasicParsing -Uri $manifest.url -OutFile $archive }
$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $manifest.sha256) { throw 'El paquete de aMule no coincide con el hash fijado.' }
$engineDirectory = Join-Path $vendorPath "amule-$($manifest.version)"
if (-not (Test-Path -LiteralPath $engineDirectory)) { Expand-Archive -LiteralPath $archive -DestinationPath $engineDirectory }
Write-Output "SDK $actualSdk y aMule $($manifest.version) preparados dentro del repositorio."
