# aMule Modern

Interfaz de escritorio para Windows en C# / Avalonia, con aMule 3.0.1 como proceso independiente y control directo mediante EC.

**Estado: incremento funcional 0.6.0-dev.** Puedes gestionar la cola, conectar a un servidor eD2k, buscar archivos y elegir las carpetas Incoming/tmp. La X oculta a la bandeja y el motor sigue; «Salir y detener» cierra aMule. Kad permanece desactivado. Compartidos e instalador son trabajo pendiente.

## Qué hace hoy

- **Descargas:** pegar un enlace `ed2k://`, ver la cola real, filtrar, seleccionar varias filas, pausar, reanudar, cancelar incompletas (borra temporales) y quitar completados de la lista (conserva el archivo).
- **Servidores:** añadir IPv4 o dominio y puerto, guardar, conectar al seleccionado y desconectar. Muestra estado eD2k (desconectado / conectando / conectado) y HighID/LowID.
- **Buscar:** una búsqueda activa en el servidor actual o global eD2k; resultados con fuentes, filtro, selección múltiple y descarga a la cola.
- **Ajustes:** Incoming y tmp. Por defecto `%USERPROFILE%\Downloads\amule-modern\incoming` y `tmp`. Puedes elegir otras carpetas; al aplicar se reinicia el motor.

eD2k se habilita al arrancar, sin autoconexión. Hay que añadir un servidor y conectar a mano.

## Arrancar

Doble clic en `Iniciar.cmd`. Para preparar otro checkout desde PowerShell:

```powershell
.\scripts\Setup.ps1
.\scripts\Build.ps1 -Test -Publish
.\scripts\Start.ps1
```

El SDK se instala en `.tools`, sin cambiar el SDK global. La publicación autocontenida queda en `artifacts/app`; de momento requiere permanecer en el árbol del repositorio porque localiza allí el motor. No es aún un paquete portable independiente.

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

`Build.ps1 -Test` comprueba el protocolo EC, autenticación, cola, pausa/reanudación de varias filas, cancelación, servidores, un handshake eD2k controlado en este equipo y el ciclo de búsqueda. Los perfiles de prueba no escriben en tu carpeta Descargas. El test necesita una interfaz IPv4 privada activa.

La búsqueda y la descarga desde resultados también se han usado contra un servidor eD2k real. Eso no sustituye todavía una prueba formal de integridad (checksum independiente, pausa a mitad de una transferencia con datos y recuperación tras reinicio).

Ver [plan](docs/PLAN.md), [estado](docs/STATUS.md) y [referencias](docs/SOURCES.md).

## Licencia

Código del proyecto: GPL-2.0-or-later. aMule conserva su licencia y autores. Las bibliotecas .NET/Avalonia conservan sus respectivas licencias. El motor descargado no está versionado aquí; su distribución futura irá acompañada del código fuente y avisos correspondientes.
