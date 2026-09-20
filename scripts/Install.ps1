param([string]$Prefix = $(Join-Path $env:LOCALAPPDATA 'AmuleModern'))
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
$portableRoot = Join-Path $repoPath 'artifacts\portable'
$Prefix = [IO.Path]::GetFullPath($Prefix).TrimEnd('\', '/')
if ($Prefix -eq [IO.Path]::GetPathRoot($Prefix).TrimEnd('\', '/') -or $Prefix.StartsWith($portableRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Destino de instalacion no valido.' }
$check = $Prefix
while ($check) {
    if ((Test-Path -LiteralPath $check) -and ((Get-Item -LiteralPath $check -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'No se instala a traves de enlaces o junctions.' }
    $parent = Split-Path -Parent $check
    if ($parent -eq $check) { break }
    $check = $parent
}
if (-not (Test-Path -LiteralPath (Join-Path $portableRoot 'installation-manifest.json'))) { & (Join-Path $PSScriptRoot 'Package.ps1') }
if (-not (Test-Path -LiteralPath (Join-Path $portableRoot 'AmuleModern.exe'))) { throw 'No hay paquete portable. Ejecuta scripts/Package.ps1.' }
$running = @(Get-CimInstance Win32_Process -Filter "Name='AmuleModern.exe' OR Name='amuled.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($Prefix + '\', [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -gt 0) { throw 'Cierra aMule Modern (Salir y detener) antes de instalar o actualizar.' }
New-Item -ItemType Directory -Path $Prefix -Force | Out-Null
# Refuse to merge into an unrelated directory. Legacy packages have engine-manifest.json.
if (@(Get-ChildItem -LiteralPath $Prefix -Force | Where-Object { $_.Name -ne '.local' }).Count -gt 0 -and -not (Test-Path -LiteralPath (Join-Path $Prefix 'engine-manifest.json')) -and -not (Test-Path -LiteralPath (Join-Path $Prefix 'installation-manifest.json'))) { throw 'El destino contiene archivos y no se reconoce como una instalacion de aMule Modern.' }
Get-ChildItem -LiteralPath $Prefix -Recurse -Force | ForEach-Object { if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'El destino contiene enlaces o junctions.' } }
Get-ChildItem -LiteralPath $portableRoot -Force | ForEach-Object {
    if ($_.Name -eq '.local') { return }
    $dest = Join-Path $Prefix $_.Name
    if ($_.PSIsContainer) {
        # Merge package files. Do not recursively delete user files in an existing directory.
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Get-ChildItem -LiteralPath $_.FullName -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force }
    } else {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
    }
}
$defaultPrefix = Join-Path $env:LOCALAPPDATA 'AmuleModern'
$createShortcuts = [string]::Equals($Prefix, $defaultPrefix, [StringComparison]::OrdinalIgnoreCase)
if ($createShortcuts)
{
    $programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    New-Item -ItemType Directory -Path $programs -Force | Out-Null
    $ws = New-Object -ComObject WScript.Shell
    $shortcut = $ws.CreateShortcut((Join-Path $programs 'aMule Modern.lnk'))
    $shortcut.TargetPath = Join-Path $Prefix 'AmuleModern.exe'
    $shortcut.WorkingDirectory = $Prefix
    $shortcut.Description = 'aMule Modern'
    $shortcut.Save()
    $uninstall = $ws.CreateShortcut((Join-Path $programs 'Desinstalar aMule Modern.lnk'))
    $uninstall.TargetPath = 'powershell.exe'
    $uninstall.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $Prefix 'Uninstall.ps1')`""
    $uninstall.WorkingDirectory = $Prefix
    $uninstall.Description = 'Quita aMule Modern. Conserva descargas y el perfil.'
    $uninstall.Save()
}
Write-Output "Instalado en $Prefix (por usuario, sin administrador)."
Write-Output "Las descargas siguen en Descargas\amule-modern. El perfil del motor queda en $Prefix\.local."
if ($createShortcuts) { Write-Output 'Acceso directo: menu Inicio, aMule Modern.' }
