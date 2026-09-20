param([string]$Prefix = $(Join-Path $env:LOCALAPPDATA 'AmuleModern'), [switch]$RemoveProfile)
$ErrorActionPreference = 'Stop'
$running = @(Get-CimInstance Win32_Process -Filter "Name='AmuleModern.exe' OR Name='amuled.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($Prefix, [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -gt 0) { throw 'Cierra aMule Modern con Salir y detener antes de desinstalar.' }
if (-not (Test-Path -LiteralPath $Prefix)) { Write-Output "No hay instalacion en $Prefix."; return }
Get-ChildItem -LiteralPath $Prefix -Force | ForEach-Object {
    if (-not $RemoveProfile -and $_.Name -eq '.local') { return }
    Remove-Item -LiteralPath $_.FullName -Recurse -Force
}
$programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
foreach ($name in @('aMule Modern.lnk', 'Desinstalar aMule Modern.lnk')) {
    $link = Join-Path $programs $name
    if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link -Force }
}
$downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\amule-modern'
Write-Output "Binarios y acceso directo eliminados de $Prefix."
Write-Output "No se toca $downloads."
if (-not $RemoveProfile -and (Test-Path -LiteralPath (Join-Path $Prefix '.local'))) {
    Write-Output "Perfil conservado en $Prefix\.local. Para borrarlo: Uninstall.ps1 -RemoveProfile."
}
