# Estado al 20/09/2026 — 0.8.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Todo lo de 0.6.0, más **Compartidos (0.7)** y **Kad (0.8)**.
- Incoming se comparte solo. Puedes añadir carpetas extra explícitas (no el perfil, ni tmp, ni el sistema). aMule 3.0.1 exige el mismo path en `shareddir.dat` y `shareddir-explicit.dat`; al quitar una carpeta extra los archivos no se borran.
- Lista EC de compartidos, recarga, copiar enlace, abrir carpeta. Las pruebas aisladas no escriben en tu carpeta Descargas.
- Kad se activa/desactiva en Ajustes (preferencia + `EC_OP_KAD_START/STOP`). Buscar admite ámbito Kad cuando Kad está conectado. No se toca el cortafuegos; «en ejecución» no implica nodos públicos ni HighID Kad.
- 58 checks PASS. Capturas `--shared` y `--settings` salen de verdad, sin procesos residuales.

## Pendiente

- Prueba formal de transferencia con payload.
- Instalador, límites de ancho de banda, eliminación/importación de servidores.
- Paquete portable autónomo.

## Joplin

0.2.0: `b34863d12bea450d8269209b232c4f3a`. 0.3.0: `e00ec0b576824f2b9f71a9bf868991f6`. 0.4.0: `a85d51d6cb06458ca9a0f3148eb5fce3`. 0.5.0: `27faa32e47db4af4a108ac0914b3e43c`. 0.6.0: `359f06918f54472d9d6153ffa77db6f2`. 0.7–0.8: `afc1afcb4e514638bf051609ab5d0346`.
