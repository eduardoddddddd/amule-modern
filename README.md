# aMule Modern

Interfaz de escritorio para Windows en C# / Avalonia, con aMule 3.0.1 como proceso independiente y control directo mediante EC.

**Estado: incremento funcional 0.9.0-dev.** Puedes gestionar la cola, buscar, ver compartidos, activar Kad, limitar el ancho de banda e importar o quitar servidores. La X oculta a la bandeja; «Salir y detener» cierra aMule. Hay ZIP portable e instalador por usuario.

## Qué hace hoy

- **Descargas:** pegar un enlace `ed2k://`, ver la cola real, filtrar, seleccionar varias filas, pausar, reanudar, cancelar incompletas (borra temporales) y quitar completados de la lista (conserva el archivo).
- **Servidores:** añadir IPv4 o dominio y puerto, quitar, importar desde archivo (texto o `server.met`) o URL http/https, conectar y desconectar. Muestra estado eD2k y HighID/LowID. Importar no activa la descarga automática de listas al arrancar.
- **Buscar:** una búsqueda activa en el servidor actual o global eD2k; resultados con fuentes, filtro, selección múltiple y descarga a la cola.
- **Ajustes:** Incoming, tmp, límites de bajada/subida (KiB/s, 0 = ilimitado) y Kad. Por defecto `%USERPROFILE%\Downloads\amule-modern\incoming` y `tmp`. Activar Kad no abre el cortafuegos. Si la subida es muy baja, aMule puede recortar la bajada.
- **Compartidos:** lista lo que el motor ofrece. Incoming se comparte solo; puedes añadir o quitar carpetas extra.

eD2k se habilita al arrancar, sin autoconexión. Hay que añadir un servidor y conectar a mano. Kad empieza desactivado.

## Arrancar

Doble clic en `Iniciar.cmd`. Para preparar otro checkout desde PowerShell:

```powershell
.\scripts\Setup.ps1
.\scripts\Build.ps1 -Test -Publish
.\scripts\Start.ps1
```

El SDK se instala en `.tools`, sin cambiar el SDK global. `scripts/Build.ps1 -Publish` deja la app en `artifacts/app` (sigue usando el motor de `vendor` del repo). Paquete autónomo:

```powershell
.\scripts\Package.ps1 -Zip
.\scripts\Install.ps1
```

El ZIP queda en `artifacts/AmuleModern-portable-win-x64.zip` e incluye `amuled` y `engine-manifest.json`. La instalación por usuario copia a `%LOCALAPPDATA%\AmuleModern` y crea un acceso en el menú Inicio. No pide administrador. Desinstalar conserva Incoming/tmp y, salvo `-RemoveProfile`, el perfil `.local`.

La X oculta la ventana en la bandeja; las transferencias continúan. «Salir y detener» cierra el motor por EC. Una segunda ventana avisa a la ya abierta y puede entregar un enlace `ed2k://`. No se adjunta a procesos ajenos. aMule para Windows tampoco permite dos `amuled` a la vez, aunque los perfiles sean distintos: cierra la app antes de ejecutar pruebas.

## Datos

- `%USERPROFILE%\Downloads\amule-modern\incoming`: archivos terminados del perfil de escritorio.
- `%USERPROFILE%\Downloads\amule-modern\tmp`: parciales del perfil de escritorio.
- `.local/desktop`: perfil privado del motor (configuración, claves EC, lista de servidores). Ya no guarda Incoming/tmp.
- `.local/integration-*` y `.local/capture`: perfiles de prueba con Incoming/Temp aislados dentro del perfil.
- `vendor`: paquete aMule fijado y comprobado contra SHA-256 publicado.
- `artifacts`: compilaciones, capturas e informes.

Esos directorios (salvo Descargas) están excluidos de Git. El perfil contiene credenciales EC y sus ACL se limitan al usuario actual en Windows. No compartirlo ni adjuntarlo a incidencias.

## Validación

`Build.ps1 -Test` comprueba el protocolo EC, autenticación, cola, pausa/reanudación, cancelación, servidores (añadir, importar, quitar), límites de ancho de banda, un handshake eD2k controlado en este equipo y el ciclo de búsqueda. Los perfiles de prueba no escriben en tu carpeta Descargas. El test necesita una interfaz IPv4 privada activa.

La búsqueda y una descarga completa se han usado contra un servidor eD2k real. No hay todavía una prueba automatizada de checksum independiente ni de recuperación a mitad de transferencia.

Ver [plan](docs/PLAN.md), [estado](docs/STATUS.md) y [referencias](docs/SOURCES.md).

## Licencia

Código del proyecto: GPL-2.0-or-later. aMule conserva su licencia y autores; el paquete portable incluye `NOTICE-AMULE.md` y `engine/share/doc/amule/LICENSE.md`. Las bibliotecas .NET/Avalonia conservan sus respectivas licencias. El motor de `vendor/` no está versionado en Git; el ZIP portable sí lo copia.
