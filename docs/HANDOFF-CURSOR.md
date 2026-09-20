# Correcciones de importacion y paquete para Cursor — 20/09/2026

Base: ce3355d (0.9.0-dev). Se corrigen los seis problemas de la ultima revision, sin ampliar funcionalidades.

## Cambios

1. Desinstalacion por inventario: Package.ps1 genera installation-manifest.json (identidad, esquema, rutas relativas y SHA-256). Uninstall.ps1 valida todas las rutas antes de borrar, rechaza traversal y junctions, y solo elimina archivos inventariados que conservan su hash. Archivos ajenos y modificados se conservan. .local solo se elimina con -RemoveProfile. Los accesos directos solo se retiran si apuntan a esa instalacion.
2. El destino por defecto del desinstalador es su propia carpeta ($PSScriptRoot). No apunta silenciosamente a LOCALAPPDATA. Desde el repositorio debe indicarse -Prefix. Paquetes antiguos sin inventario se rechazan sin borrar nada: actualizar primero. El instalador admite actualizar paquetes anteriores reconocidos y reinstalar conservando .local; no elimina recursivamente carpetas al actualizar.
3. HTTP: lectura por bloques, corte en 2 MiB + un byte de deteccion, y plazo total de 15 segundos para peticiones, redirecciones y cuerpo. Se validan tambien los destinos de redireccion. Un transporte inyectable permite probar respuestas sin Content-Length y cuerpos bloqueados sin acceso a Internet.
4. Archivos locales: lectura asincrona acotada y manejo de errores de apertura/permisos/archivo desaparecido. Ya no se hace ReadAllBytes fuera del control de errores de la interfaz.
5. Se separan los errores al obtener la lista de los errores del transporte EC. HTTP 404, permisos y tiempo agotado muestran un mensaje y permiten seguir usando Servidores. Un fallo EC real sigue invalidando el cliente.
6. Los limites de 80000 y 102400 KiB/s se conservan al leer. Solo 0 y el sentinel heredado exacto 65535 se interpretan como ilimitados.

## Validacion realizada

- Build.ps1 -Test -Publish: 88 comprobaciones PASS; compilacion sin errores ni advertencias.
- Incluye cuerpo HTTP ilimitado, plazo durante cuerpo bloqueado, HTTP 404, redireccion local rechazada, archivos grandes/bloqueados y roundtrip real de 80000 KiB/s contra amuled.
- Interfaz Servidores: captura --exercise-ui con salida 0. Errores de HTTP, permisos y cancelacion inyectados; archivo inexistente real; controles siguen habilitados y EC sigue respondiendo. Se mantienen alta/importacion/borrado del servidor de prueba.
- tests/Uninstall.Tests.ps1: 8 PASS, solo fixtures .local. Preserva otra instalacion, archivos ajenos/modificados y perfil; rechaza inventario ausente, traversal y junction; comprueba RemoveProfile explicito.
- tests/Install.Tests.ps1: 5 PASS. Paquete real instalado en ruta temporal con espacios, actualizado conservando archivo ajeno, ejecutado con su motor incluido y cerrado ordenadamente. Desinstalacion y reinstalacion con perfil conservado verificadas.
- Package.ps1 -Zip: ZIP y artifacts/portable regenerados con inventario y desinstalador corregido. No se instalo en el destino habitual del usuario.
- Evidencia versionada en IMPORT-PACKAGE-FIXES-VALIDATION.txt; captura local artifacts/import-fixes-ui.png. No se prueba transferencia P2P completa ni Kad publico en este cambio.

## Continuacion

En esta misma carpeta Cursor solo necesita releer el nuevo commit y este documento. En otro clon, publicar/transferir el commit antes de incorporarlo. No se hace push automaticamente.

Pruebas: cerrar la app con Salir y detener, ejecutar Build.ps1 -Test -Publish; generar Package.ps1 -Zip antes de Install.Tests.ps1. Los scripts de pruebas retienen fixtures de diagnostico dentro de .local y no modifican las descargas del usuario.
