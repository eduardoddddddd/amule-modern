param([switch]$Test, [switch]$Publish)
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$dotnetExe = Join-Path $repoPath '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) { throw 'Ejecuta primero scripts/Setup.ps1.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $repoPath
try {
    & $dotnetExe restore AmuleModern.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración de dependencias fijadas.' }
    & $dotnetExe build AmuleModern.slnx -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
    if ($Test) {
        & $dotnetExe run --project tests/Layout -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de columnas.' }
        & $dotnetExe run --project tests/GridUi -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Falló la prueba visual de columnas.' }
        & $dotnetExe run --project tests/Smoke -c Release --no-build -- --integration
        if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas.' }
    }
    if ($Publish) {
        & $dotnetExe publish src/Desktop -c Release -r win-x64 --self-contained true -o artifacts/app --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación.' }
    }
} finally { Pop-Location }
