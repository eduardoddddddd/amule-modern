# Estado al 20/09/2026 — 0.4.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Descargas: añadir enlace, tabla, filtro, **selección múltiple**, pausa y reanudación de varias filas en un solo comando EC.
- Cancelar incompletas (EC_OP_PARTFILE_DELETE 0x1D): borra temporales; pide confirmación. Quitar completados de la lista (EC_OP_CLEAR_COMPLETED 0x53) conserva el archivo en Incoming; usa el ECID de sesión, nunca persistido.
- Ventana **Servidores** y **Buscar** como en 0.3.0. Buscar se deshabilita sin conexión eD2k.
- Perfil propio y EC loopback; cierre ordenado del proceso. No se tocaron firewall, instalaciones personales ni servicios.
- Compilación Release y pruebas de integración ampliadas: pausa múltiple, cancelación, el cancelado no reaparece tras reinicio, handshake eD2k y búsqueda controlada.
- Uso real previo: búsqueda en servidor eD2k, fichero encontrado y descarga desde la interfaz. No sustituye checksum independiente ni recuperación de bloques a mitad de una transferencia con datos.
- Limpieza local: se eliminaron las carpetas basura `sers*` en `vendor/.../bin` dejadas por rutas con barras invertidas. El motor ya escribe Incoming/Temp con `/`.

Evidencias locales (excluidas de Git): artifacts/search-build-validation.txt y capturas previas. La captura de Buscar de este incremento se genera al publicar.

## Correcciones de integración

Sin cambios respecto a 0.2.0/0.3.0: eD2k ON, Autoconnect=0, Kad OFF, Ed2kServersUrl vacío, IncomingDir/TempDir con barras normales. aMule Windows sigue impidiendo dos `amuled` a la vez.

wxFileConfig y el ECID: el identificador interno de fila solo sirve para quitar completados en la sesión actual.

## Pendiente

- Prueba formal de transferencia con payload: checksum independiente, pausa a mitad y recuperación de bloques tras reinicio. aMule Windows no permite dos motores en el mismo equipo; hace falta un peer real o un simulador de bloques.
- Gestión de compartidos, ajustes de carpetas y Kad, bandeja, instalador, eliminación/importación de servidores y reconexión tras fallo.
- Paquete portable autónomo: la publicación aún requiere el árbol del repositorio para localizar el motor.

## Joplin

0.2.0: `b34863d12bea450d8269209b232c4f3a`. 0.3.0: `e00ec0b576824f2b9f71a9bf868991f6`. 0.4.0: `a85d51d6cb06458ca9a0f3148eb5fce3`.
