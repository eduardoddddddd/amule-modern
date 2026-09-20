# Estado al 20/09/2026 — 0.5.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Todo lo de 0.4.0, más **Ajustes** de carpetas Incoming y tmp.
- Perfil de escritorio: `%USERPROFILE%\Downloads\amule-modern\incoming` y `tmp` (carpeta Descargas conocida de Windows). Rutas en amule.conf con `/`.
- Si el perfil desktop todavía apuntaba a `.local/desktop/Incoming|Temp`, se copian archivos existentes a la nueva ubicación y se actualiza la configuración. No se borran las carpetas antiguas.
- Perfiles `integration-*` y `capture` siguen aislados dentro de `.local` para no ensuciar Descargas.
- Cambiar carpetas desde la UI detiene el motor, escribe amule.conf y lo vuelve a arrancar. Incoming y tmp no pueden ser la misma carpeta.
- Las pruebas comprueban la forma de las rutas de usuario y que el perfil aislado no usa la librería de Descargas.

## Pendiente

- Prueba formal de transferencia con payload.
- Compartidos, Kad, bandeja, instalador, límites de ancho de banda, eliminación/importación de servidores y reconexión.
- Paquete portable autónomo.

## Joplin

0.2.0: `b34863d12bea450d8269209b232c4f3a`. 0.3.0: `e00ec0b576824f2b9f71a9bf868991f6`. 0.4.0: `a85d51d6cb06458ca9a0f3148eb5fce3`. 0.5.0: `27faa32e47db4af4a108ac0914b3e43c`.
