# Estado al 20/09/2026 — 0.9.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Todo lo de 0.8.0, más **límites de ancho de banda**, **quitar/importar servidores**, **ZIP portable** e **instalador por usuario**.
- Incoming se comparte solo. Puedes añadir carpetas extra explícitas (no el perfil, ni tmp, ni el sistema). aMule 3.0.1 exige el mismo path en `shareddir.dat` y `shareddir-explicit.dat`; al quitar una carpeta extra los archivos no se borran.
- Lista EC de compartidos, recarga, copiar enlace, abrir carpeta. Las pruebas aisladas no escriben en tu carpeta Descargas.
- 80 checks PASS, incluyendo límites, ratio de aMule, importar/quitar servidores y persistencia al reiniciar.
- Cinco capturas `--exercise-ui` (principal, búsqueda, servidores, compartidos, ajustes) con salida 0.
- ZIP portable ~122 MiB en `artifacts/AmuleModern-portable-win-x64.zip`. Instalación de prueba a un prefijo temporal (sin menú Inicio): exe + `engine/bin/amuled.exe` + manifiesto; desinstalación no toca Descargas.
- Límites EC `0x1303`/`0x1304` (KiB/s, 0 = ilimitado) sin reiniciar el motor. aMule puede recortar la bajada si la subida es < 4 KiB/s.
- Quitar servidor `EC_OP_SERVER_REMOVE` (0x30). Importar texto, `ed2k://|server|…` o `server.met`; URL solo http/https, máximo 200 entradas / 2 MiB. No se usa `EC_OP_SERVER_UPDATE_FROM_URL` para no reactivar `Ed2kServersUrl`.
- Paquete `artifacts/portable` con el motor incluido. Instalación en `%LOCALAPPDATA%\AmuleModern`. Desinstalación no toca Descargas.
- La descarga completa en uso real queda comprobada por el usuario; no se añade una prueba formal de checksum/payload en este incremento.

## Pendiente

- Tema claro/oscuro y densidad; barra de estado persistente; panel de detalle; categorías.
- Asociación `ed2k://` e inicio con Windows.
- Instalador firmado, Win10/ARM64, robustez prolongada (suspensión, colas grandes).

## Joplin

0.2.0: `b34863d12bea450d8269209b232c4f3a`. 0.3.0: `e00ec0b576824f2b9f71a9bf868991f6`. 0.4.0: `a85d51d6cb06458ca9a0f3148eb5fce3`. 0.5.0: `27faa32e47db4af4a108ac0914b3e43c`. 0.6.0: `359f06918f54472d9d6153ffa77db6f2`. 0.7–0.8: `afc1afcb4e514638bf051609ab5d0346`. 0.9.0: `097a82159c00433c8b5f0431aef0169c`.

## Correcciones de revisión (sin ampliar funciones)

Corregidos: restauración desde bandeja, migración repetida de temporales, cambio de Temp con descargas pendientes, retirada de carpetas compartidas vacías/desaparecidas y estado de búsqueda Kad sin eD2k. Detalles y límites de validación para Cursor en [HANDOFF-CURSOR.md](HANDOFF-CURSOR.md).
