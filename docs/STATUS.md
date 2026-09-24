# Estado al 23/09/2026 — 0.9.2-dev

## Detalle de descarga

La fila seleccionada muestra lo que el detalle FULL ya envía: hash, enlace, prioridad, fuentes (transfiriendo, no actuales, A4AF y completas), última recepción y AICH. La ruta se compone aquí: Incoming y el nombre si está completa; si no, `NNN.part` y `.part.met` en tmp. FULL no trae la lista de pares.

## Asociación ed2k

En Ajustes, «Abrir enlaces ed2k://» registra el esquema para el usuario y guarda el programa anterior. «Quitar asociación» lo restaura, o borra la clave si no había ninguna. Si otro programa ya es el predeterminado de Windows, no se cambia esa elección. En macOS el `.app` declara el esquema y la misma acción usa Launch Services.

## Columnas y densidad

Descargas, Buscar, Servidores y Compartidos guardan ancho, orden y visibilidad en `ui.json` (la misma ficha que el tema). La primera columna de cada tabla no se oculta. Un ancho inválido no sustituye el valor de la ventana. La densidad cómoda (44 px) o compacta (36 px) vale para todas las tablas. Guardar el tema no borra el diseño de columnas.

## Estado al 21/09/2026

## macOS arm64

La misma aplicación Avalonia arranca en Apple Silicon con el `amuled` 3.0.1 del DMG oficial universal2 (hash fijado en `docs/engine-manifest.json`). `AmuleModern.app` usa el icono del proyecto y se puede anclar al Dock. El motor de esa copia no muestra el icono de aMule y no reemplaza una instalación ya existente. Smoke de integración en macOS 26.3: 91 comprobaciones PASS. Sin notarizar.

## Estado al 20/09/2026

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Shell monoventana, tema claro/oscuro/sistema, import URL de `server.met` con nombres `ST_SERVERNAME`.
- **Pestañas de búsqueda:** cada búsqueda queda en una pestaña (máx. 12). Solo una está viva en el motor; las anteriores son instantáneas (descarga por enlace ed2k).
- **Lista de servidores:** no se pide la lista con eD2k a medias al entrar en la página (aMule devolvía vacío y la tabla se borraba). eD2k se habilita al arrancar. `RemoveDeadServer=0` y sin autodescarga de ipfilter/listas. Una copia propia de servidores y de la cola se reescribe en marcha; si el proceso muere, el siguiente arranque las vuelve a poner. SIGTERM y SIGINT cierran el motor por EC.
- Smoke+integración: 92 checks PASS.

## Pendiente para 1.0

Ver README: sesión larga, categorías, inicio con Windows, checksum formal y firma.

## Joplin

Clipper local no respondía (:41184 conexión rechazada).
