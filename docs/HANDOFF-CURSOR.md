# 0.9.0-dev — 20/09/2026

Base: 7a913c1. Añade límites, quitar/importar servidores, paquete portable e instalador por usuario. No reabre las cinco correcciones de revisión.

## Cambios

1. **Límites:** Ajustes lee/escribe `EC_TAG_CONN_MAX_DL` (0x1303) y `EC_TAG_CONN_MAX_UL` (0x1304) en KiB/s. 0 = ilimitado. No reinicia el motor. Si la subida es < 4 KiB/s, aMule puede recortar la bajada (comportamiento del motor).
2. **Servidores:** quitar con confirmación (`EC_OP_SERVER_REMOVE` 0x30). Importar archivo (texto, enlace `ed2k://|server|…`, `server.met`) o URL http/https. Máximo 200 entradas y 2 MiB. No se llama `EC_OP_SERVER_UPDATE_FROM_URL` para no rellenar `Ed2kServersUrl`.
3. **Portable:** `scripts/Package.ps1` copia la app y `vendor/.../amule-portable-x64` a `artifacts/portable` con `engine-manifest.json`. `EngineSession.Locate` distingue repo (`docs/engine-manifest.json`) y paquete (`engine-manifest.json` junto al exe).
4. **Instalador:** `scripts/Install.ps1` a `%LOCALAPPDATA%\AmuleModern`, acceso en el menú Inicio. `Uninstall.ps1` no toca Descargas y conserva `.local` salvo `-RemoveProfile`. Sin firma.

La descarga completa en uso real la dio por buena el usuario. No hay prueba automatizada de checksum/payload ni de Kad público.

## Validación realizada

- `scripts/Build.ps1 -Test -Publish`: 80 comprobaciones PASS, 0 advertencias.
- `scripts/Package.ps1 -Zip` y `Install.ps1`/`Uninstall.ps1` contra `artifacts/install-check` (sin accesos del menú Inicio).
- `scripts/Capture.ps1`: main, search, servers, shared, settings; cinco procesos con código 0. El perfil `.local/capture` se borra entre capturas.
- No se ha instalado en `%LOCALAPPDATA%\AmuleModern` desde esta sesión. No hay prueba automatizada de checksum P2P.
