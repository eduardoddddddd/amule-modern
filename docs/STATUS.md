# Estado al 19/09/2026

## Incremento 0.1.0-dev

Repositorio local creado en `C:\Users\Edu\source\amule-modern`.

Herramientas fijadas: SDK .NET 10.0.401 dentro de `.tools/dotnet`; Avalonia 12.1.2; aMule 3.0.1 Windows x64. Se comprobó el SHA-256 del paquete contra el digest de la publicación oficial, registrado en `engine-manifest.json`. No se cambió el SDK global.

## Hecho y verificado

- Cliente EC escrito en C#: paquetes, etiquetas anidadas, autenticación con salt, límites y solicitudes serializadas con timeout.
- Comunicación real con `amuled.exe`, contraseña incorrecta rechazada, consulta de cola y estadísticas.
- Añadir enlace, pausar y reanudar; persistencia de cola y estado pausado tras cierre ordenado y reinicio.
- Proceso y perfil aislados; EC en loopback; ACL del perfil comprobada para el usuario actual. No se modificaron reglas del firewall ni asociaciones.
- Ventana Avalonia con tabla real, filtro, añadir enlace, pausa/reanudación, estadísticas y apertura de carpeta.
- Publicación autocontenida `artifacts/app/AmuleModern.exe` y lanzador `Iniciar.cmd`.
- Compilación Release sin errores ni advertencias.
- 18 comprobaciones de protocolo/integración superadas.
- Prueba de interfaz a través de eventos reales de botones: añadir, pausar, reanudar y filtrar, usando el perfil de captura separado. Render inspeccionado visualmente, salida de proceso 0 y cierre ordenado del motor.
- Antes del arranque final no quedaban procesos de pruebas de AmuleModern/amuled.

Evidencias locales excluidas de Git: `artifacts/build-validation.txt`, `artifacts/ui-tested.png`, `artifacts/ui-tested.validation.txt`. La fila de la captura es una entrada de prueba real en la cola, sin bytes descargados.

## Límites actuales

**La fase 0 del plan está parcialmente completada.** Se ha probado control y persistencia, pero no una transferencia de datos entre pares ni recuperación de una descarga con bloques recibidos.

- eD2k/Kad están desactivados en el perfil inicial. Añadir enlaces crea entradas en la cola, todavía no descarga contenido.
- Buscar y Compartidos aparecen desactivados. Ajustes, selección múltiple, tema claro, bandeja, instalador y conexión remota están pendientes.
- Cerrar la ventana cierra el motor; todavía no se oculta en bandeja.
- La aplicación detecta errores EC y conserva la ventana para diagnóstico. La reconexión automática tras fallo aún no está implementada.
- El ejecutable publicado debe permanecer dentro del repositorio para localizar el motor; todavía no es un paquete portable autónomo.
- Una segunda instancia con el mismo perfil se rechaza. Recuperación de un motor huérfano y enfoque de la ventana existente pendientes.
- No hay pruebas de larga duración ni cifras de rendimiento de listas grandes.

## Siguiente incremento

1. Elegir carpetas desde la interfaz y configurar conexión P2P sin tocar instalaciones previas.
2. Completar la prueba controlada de transferencia e integridad, pausa y recuperación de bloques tras reinicio.
3. Implementar pantalla Conexión y búsqueda única eD2k/Kad sobre la versión fijada.
4. Continuar con reconexión, bandeja y selección múltiple.

## Documentación en Joplin

TDKop `joplin_status` volvió a devolver conexión rechazada por Web Clipper en `127.0.0.1:41184`. No se creó nota ni se utilizó el puente alternativo. Documento pendiente en `JOPLIN-PENDING.md`.
