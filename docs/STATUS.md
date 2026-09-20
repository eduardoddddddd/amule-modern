# Estado al 20/09/2026 — 0.9.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Todo lo de 0.8.0, más **límites de ancho de banda**, **quitar/importar servidores**, **ZIP portable** e **instalador por usuario**.
- Incoming se comparte solo. Puedes añadir carpetas extra explícitas (no el perfil, ni tmp, ni el sistema). aMule 3.0.1 exige el mismo path en `shareddir.dat` y `shareddir-explicit.dat`; al quitar una carpeta extra los archivos no se borran.
- Lista EC de compartidos, recarga, copiar enlace, abrir carpeta. Las pruebas aisladas no escriben en tu carpeta Descargas.
- 88 checks PASS (incluye límites altos, import HTTP acotado, lectura de archivos bloqueados).
- Desinstalación segura: inventario SHA-256; solo borra ficheros del paquete sin modificar; portable se desinstala a sí mismo. Tests: 8 uninstall + 5 install PASS.
- Cinco capturas UI de 0.9 (previas) y captura instalada con ruta con espacios.
- Límites EC `0x1303`/`0x1304` (KiB/s, 0 = ilimitado) sin reiniciar el motor. Solo el sentinel legacy `0xFFFF` se muestra como ilimitado.
- Quitar servidor `EC_OP_SERVER_REMOVE` (0x30). Importar texto, `ed2k://|server|…` o `server.met`; URL solo http/https, máximo 200 entradas / 2 MiB (corte en streaming). Fallos de import no desconectan EC.
- Paquete `artifacts/portable` con motor e `installation-manifest.json`. Instalación en `%LOCALAPPDATA%\AmuleModern`. Desinstalación no toca Descargas.
- La descarga completa en uso real queda comprobada por el usuario; no se añade una prueba formal de checksum/payload en este incremento.

## Pendiente

- Tema claro/oscuro y densidad; barra de estado persistente; panel de detalle; categorías.
- Asociación `ed2k://` e inicio con Windows.
- Instalador firmado, Win10/ARM64, robustez prolongada (suspensión, colas grandes).

## Joplin

0.2.0: `b34863d12bea450d8269209b232c4f3a`. 0.3.0: `e00ec0b576824f2b9f71a9bf868991f6`. 0.4.0: `a85d51d6cb06458ca9a0f3148eb5fce3`. 0.5.0: `27faa32e47db4af4a108ac0914b3e43c`. 0.6.0: `359f06918f54472d9d6153ffa77db6f2`. 0.7–0.8: `afc1afcb4e514638bf051609ab5d0346`. 0.9.0: `097a82159c00433c8b5f0431aef0169c`. Revisión 0.9: `d3bb65f1a9824505be1c250f64164158`.

## Correcciones de revisión

0.8 (bandeja, migración, Temp, compartidos, Kad) y 0.9 (uninstall, import, límites altos): detalles en [HANDOFF-CURSOR.md](HANDOFF-CURSOR.md).
