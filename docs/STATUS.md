# Estado al 20/09/2026 — 0.9.2-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Shell monoventana, tema claro/oscuro/sistema, import URL de `server.met` con nombres `ST_SERVERNAME`.
- **Pestañas de búsqueda:** cada búsqueda queda en una pestaña (máx. 12). Solo una está viva en el motor; las anteriores son instantáneas (descarga por enlace ed2k).
- **Lista de servidores:** no se pide la lista con eD2k a medias al entrar en la página (aMule devolvía vacío y la tabla se borraba). eD2k se habilita al arrancar. `RemoveDeadServer=0` y sin autodescarga de ipfilter/listas. Un cierre forzado sigue sin escribir `server.met`; hay que usar «Salir y detener».
- Smoke+integración: 92 checks PASS.

## Pendiente para 1.0

Ver README y el cierre de esta sesión: panel de detalle, categorías, `ed2k://`, inicio con Windows, firma, robustez prolongada.

## Joplin

Clipper local no respondía (:41184 conexión rechazada).
