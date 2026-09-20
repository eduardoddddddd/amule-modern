param([string]$Prefix = $PSScriptRoot, [switch]$RemoveProfile)
$ErrorActionPreference = 'Stop'
$installRoot = [IO.Path]::GetFullPath($Prefix).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath $installRoot -PathType Container)) { throw 'No existe la carpeta de instalacion indicada.' }
if ($installRoot -eq [IO.Path]::GetPathRoot($installRoot).TrimEnd('\', '/')) { throw 'No se puede desinstalar una unidad completa.' }
function Assert-SafePath([string]$path) {
    $full = [IO.Path]::GetFullPath($path)
    if ($full -ne $installRoot -and -not $full.StartsWith($installRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Ruta fuera de la instalacion.' }
    $check = $full
    while ($check) {
        if ((Test-Path -LiteralPath $check) -and ((Get-Item -LiteralPath $check -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "No se desinstala a traves de enlaces o junctions: $check" }
        $parent = Split-Path -Parent $check
        if ($parent -eq $check) { break }
        $check = $parent
    }
    return $full
}
$manifestPath = Assert-SafePath (Join-Path $installRoot 'installation-manifest.json')
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Falta el inventario de instalacion. No se borrara nada; actualiza el paquete primero.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.appId -ne 'AmuleModern' -or $manifest.schema -ne 1 -or @($manifest.files).Count -lt 3) { throw 'Inventario de instalacion no valido.' }
$entries = @()
foreach ($entry in $manifest.files) {
    $relative = [string]$entry.path
    if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)|:' -or $relative -match '^\.local([\\/]|$)' -or $entry.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Entrada de inventario no valida.' }
    $target = Assert-SafePath (Join-Path $installRoot $relative)
    if ($target -eq $installRoot -or (Test-Path -LiteralPath $target -PathType Container)) { throw 'El inventario solo puede contener archivos.' }
    $entries += [pscustomobject]@{ Path = $target; Hash = $entry.sha256 }
}
foreach ($required in @('AmuleModern.exe', 'engine-manifest.json', 'Uninstall.ps1')) {
    if (-not ($entries.Path -contains (Join-Path $installRoot $required))) { throw "Inventario incompleto: $required" }
}
$running = @(Get-CimInstance Win32_Process -Filter "Name='AmuleModern.exe' OR Name='amuled.exe'" -ErrorAction SilentlyContinue | Where-Object {
    $_.ExecutablePath -and $_.ExecutablePath.StartsWith($installRoot + '\', [StringComparison]::OrdinalIgnoreCase)
})
if ($running.Count) { throw 'Cierra aMule Modern con Salir y detener antes de desinstalar.' }
# Validate every removal before deleting the first file. Modified and unrelated files stay.
$remove = @(); $preserved = @()
foreach ($entry in $entries) {
    if (-not (Test-Path -LiteralPath $entry.Path -PathType Leaf)) { continue }
    if ((Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash -eq $entry.Hash) { $remove += $entry.Path }
    else { $preserved += $entry.Path }
}
$profilePath = Assert-SafePath (Join-Path $installRoot '.local')
if ($RemoveProfile -and (Test-Path -LiteralPath $profilePath)) {
    Get-ChildItem -LiteralPath $profilePath -Recurse -Force | ForEach-Object { $null = Assert-SafePath $_.FullName }
}
foreach ($path in $remove) { Remove-Item -LiteralPath $path -Force }
if ($RemoveProfile -and (Test-Path -LiteralPath $profilePath)) { Remove-Item -LiteralPath $profilePath -Recurse -Force }
if ($preserved.Count -eq 0) { Remove-Item -LiteralPath $manifestPath -Force }
# Only prune empty directories that held package-owned files; never recurse-delete them.
$parents = @($entries | ForEach-Object { Split-Path -Parent $_.Path } | Sort-Object -Unique)
foreach ($parentPath in ($parents | Sort-Object Length -Descending)) {
    $dir = $parentPath
    while ($dir -ne $installRoot -and (Test-Path -LiteralPath $dir -PathType Container)) {
        $null = Assert-SafePath $dir
        if (@(Get-ChildItem -LiteralPath $dir -Force).Count) { break }
        Remove-Item -LiteralPath $dir
        $dir = Split-Path -Parent $dir
    }
}
$programs = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
foreach ($name in @('aMule Modern.lnk', 'Desinstalar aMule Modern.lnk')) {
    $link = Join-Path $programs $name
    if (Test-Path -LiteralPath $link) {
        $ws = New-Object -ComObject WScript.Shell
        $shortcut = $ws.CreateShortcut($link)
        if ($shortcut.WorkingDirectory -eq $installRoot -and ($shortcut.TargetPath -eq (Join-Path $installRoot 'AmuleModern.exe') -or $shortcut.Arguments -eq "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $installRoot 'Uninstall.ps1')`"")) { Remove-Item -LiteralPath $link -Force }
    }
}
Write-Output "Desinstalados los archivos inventariados y sin modificar de $installRoot."
Write-Output "Archivos modificados conservados: $($preserved.Count). Los archivos ajenos se conservan."
if (-not $RemoveProfile) { Write-Output 'Perfil .local conservado.' }
