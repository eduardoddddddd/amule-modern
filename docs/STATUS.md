# Estado al 20/09/2026 — 0.6.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Todo lo de 0.5.0, más ciclo de ventana y reconexión EC.
- La X oculta a la bandeja; el motor sigue y las transferencias no se detienen. La primera vez se explica.
- «Salir y detener» (barra lateral y menú de bandeja) cierra por EC y espera la salida. No mata `amuled`.
- Las capturas `--capture` siguen cerrando de verdad (ShutdownMode por defecto; no quedan en bandeja). Comprobado: sale en ~6 s, sin procesos residuales.
- Si el socket EC se rompe, se sustituye el cliente y se autentica de nuevo contra el mismo `amuled`. No se lanza otro motor.
- Una segunda instancia de escritorio avisa a la ya abierta (mutex `Local\AmuleModern.desktop`) y puede dejar un `ed2k://` pendiente. No se adjunta a un `amuled` ajeno.
- aMule para Windows sigue sin permitir dos `amuled` a la vez, aunque los perfiles sean distintos.

## Pendiente

- Prueba formal de transferencia con payload.
- Compartidos, Kad, instalador, límites de ancho de banda, eliminación/importación de servidores.
- Paquete portable autónomo.

## Joplin

0.2.0: `b34863d12bea450d8269209b232c4f3a`. 0.3.0: `e00ec0b576824f2b9f71a9bf868991f6`. 0.4.0: `a85d51d6cb06458ca9a0f3148eb5fce3`. 0.5.0: `27faa32e47db4af4a108ac0914b3e43c`. 0.6.0: `359f06918f54472d9d6153ffa77db6f2`.
