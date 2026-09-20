# Correcciones de revisión 0.9 — 20/09/2026

Base: ce3355d. Cierra los seis hallazgos de revisión del instalador e importación; no añade funciones del plan.

## Hallazgos → corrección

1. **[Alta] Uninstall borraba el Prefijo entero:** ahora solo elimina archivos del `installation-manifest.json` cuyo SHA-256 coincide. Modificados y ajenos se conservan. Sin manifiesto no borra nada.
2. **[Alta] Uninstall portable apuntaba a LOCALAPPDATA:** por defecto usa `$PSScriptRoot` (la carpeta del propio Uninstall.ps1).
3. **[Media] Límite 2 MiB en HTTP:** lectura acotada por chunks; body sin `Content-Length` se corta al límite; timeout de 15 s (inyectable en tests).
4. **[Alta] ImportFile crash:** `ReadFileAsync` acotado; errores de archivo/permisos/URL se convierten en `ArgumentException` y no deshabilitan la ventana.
5. **[Media] Fallo HTTP mataba la UI:** mismos errores de adquisición no marcan `available=false`.
6. **[Media] Límites altos = ilimitado:** `Normalize` solo trata `0` y el sentinel legacy `0xFFFF` como ilimitado; 80000 KiB/s se lee bien.

Extras: Install no mezcla en carpetas ajenas ni atraviesa junctions; merge en actualización; inventario SHA-256 en `Package.ps1`.

## Validación

- `Build.ps1 -Test -Publish`: 88 checks PASS.
- `tests/Uninstall.Tests.ps1`: 8 PASS (portable por defecto, traversal, junction, perfil).
- `tests/Install.Tests.ps1`: 5 PASS (ruta con espacios, merge, reinstall con perfil).
