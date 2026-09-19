# aMule Modern

Interfaz de escritorio para Windows en C# / Avalonia, con aMule 3.0.1 como proceso independiente y control directo mediante EC.

**Estado: primer incremento funcional, 0.1.0-dev.** La ventana muestra datos reales del motor y permite añadir enlaces, consultar la cola, pausar y reanudar. La conexión P2P está desactivada expresamente en este incremento; todavía no descarga contenido. Buscar, Compartidos, configuración, bandeja e instalador son trabajo pendiente.

## Arrancar

Doble clic en `Iniciar.cmd`. Para preparar otro checkout desde PowerShell:

```powershell
.\scripts\Setup.ps1
.\scripts\Build.ps1 -Test -Publish
.\scripts\Start.ps1
```

El SDK se instala en `.tools`, sin cambiar el SDK global. La publicación autocontenida queda en `artifacts/app`; de momento requiere permanecer en el árbol del repositorio porque localiza allí el motor. No es aún un paquete portable independiente.

La X cierra ordenadamente el motor en esta versión. No hay todavía funcionamiento en bandeja. Una segunda ventana con el mismo perfil se rechaza; no se adjunta a procesos ajenos.

## Datos

- `.local/desktop`: perfil privado del motor de la interfaz. Incoming y Temp están dentro.
- `.local/integration-*`: perfiles de pruebas con un enlace sintético de tres bytes; no descargan datos de terceros.
- `.local/capture`: perfil de comprobación visual.
- `vendor`: paquete aMule fijado y comprobado contra SHA-256 publicado.
- `artifacts`: compilaciones, capturas e informes.

Esos directorios están excluidos de Git. El perfil contiene credenciales EC y sus ACL se limitan al usuario actual en Windows. No compartirlo ni adjuntarlo a incidencias.

## Validación

`Build.ps1 -Test` comprueba paquetes de referencia, estructura anidada, entradas inválidas, TCP fragmentado, autenticación real, contraseña incorrecta, cola, pausa/reanudación, estadísticas y persistencia tras cierre. Una prueba de transferencia real eD2k/Kad queda pendiente y no está sustituida por estas comprobaciones.

Ver [plan](docs/PLAN.md), [estado](docs/STATUS.md) y [referencias](docs/SOURCES.md).

## Licencia

Código del proyecto: GPL-2.0-or-later. aMule conserva su licencia y autores. Las bibliotecas .NET/Avalonia conservan sus respectivas licencias. El motor descargado no está versionado aquí; su distribución futura irá acompañada del código fuente y avisos correspondientes.
