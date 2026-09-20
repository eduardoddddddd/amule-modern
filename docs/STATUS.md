# Estado al 20/09/2026 — 0.9.1-dev (UI shell)

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Todo lo de 0.9.0, más **shell monoventana**, **tema claro/oscuro/sistema** e **import URL de servidores** más claro.
- Navegación lateral real: Descargas, Buscar, Servidores, Compartidos y Ajustes son páginas en la misma ventana (ya no modales).
- Tema persistente en `%LOCALAPPDATA%\amule-modern\ui.json` (Oscuro / Claro / Sistema), con diccionarios DynamicResource.
- Import URL: se acepta sin `https://`, botón Ejemplo, timeout HTTP 30 s, rechazo de HTML y mensajes con URL de ejemplo `https://upd.emule-security.org/server.met` (HEAD 200 comprobado).
- Capturas 0.9 exercise-ui: main, search, servers, shared, settings — PASS.
- Smoke protocolario: 23 checks PASS (incluye normalización de URL sin esquema).
- **Fix shell 0.9.1:** al cambiar de página, Buscar/Servidores/Compartidos quedaban en `closed` y los botones no hacían nada al volver. Reattach reapertura el timer y refresca estado.
- **Fix server.met nombres:** el parser ignoraba `ST_SERVERNAME`; al añadir se guardaba la IP como nombre y aMule no la sustituía al conectar. Reimportar actualiza nombres vacíos/IP. EnableEd2k solo en la primera visita a Servidores.

## Pendiente

- Panel de detalle de descarga; categorías; densidad compacta.
- Asociación `ed2k://` e inicio con Windows.
- Instalador firmado, Win10/ARM64, robustez prolongada.

## Joplin

Clipper local no respondía en esta sesión (conexión rechazada en :41184). Notas 0.9 previas: revisión `d3bb65f1a9824505be1c250f64164158`.

## Correcciones de revisión

0.8/0.9: ver [HANDOFF-CURSOR.md](HANDOFF-CURSOR.md).
