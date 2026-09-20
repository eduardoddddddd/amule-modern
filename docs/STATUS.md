# Estado al 20/09/2026 — 0.3.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Descargas: añadir enlace, tabla, filtro, pausa, reanudación y estadísticas reales por EC.
- Ventana **Servidores**: nombre opcional, IPv4 o dominio y puerto TCP; Añadir, Añadir y conectar, Conectar al seleccionado, Desconectar y Actualizar.
- Lista persistente en server.met; validación de puerto y dominio, resolución IPv4 y prevención de duplicados. El dominio se resuelve al añadir y se guarda su IP; no hay actualización DNS dinámica todavía.
- Estado eD2k real (desconectado, conectando, conectado; HighID/LowID) separado del estado EC y Kad. El botón Desconectar se habilita tras conexión establecida: el comando del motor no cancela intentos en curso.
- Ventana **Buscar**: una búsqueda activa (servidor actual o global eD2k), resultados con fuentes, filtro, selección múltiple, descarga a la cola y detener conservando lo recibido. Exige conexión eD2k previa.
- Perfil propio y EC loopback; cierre ordenado del proceso. No se tocaron firewall, instalaciones personales ni servicios.
- Compilación Release sin advertencias ni errores, publicación autocontenida y 35 comprobaciones superadas, incluidas búsqueda controlada y alta del resultado en la cola.
- Prueba de sesión eD2k real contra un servidor controlado en la IP LAN del mismo equipo: recepción de login, estado conectando antes del ID, HighID, término de búsqueda recibido, resultado decodificado por EC y desconexión.
- Uso real: búsqueda en servidor eD2k, fichero encontrado y descarga iniciada desde la interfaz. No sustituye aún checksum independiente ni recuperación de bloques tras reinicio a mitad de transferencia.
- Pruebas de botones reales en Descargas y Servidores con motor real. La captura automatizada de la ventana Buscar quedó pendiente de Codex.

Evidencias: artifacts/search-build-validation.txt, artifacts/servers-ui.png, artifacts/servers-ui.validation.txt, artifacts/main-ui.png y artifacts/main-ui.validation.txt (excluidas de Git).

## Correcciones de integración

aMule solo inicializa y guarda server.met cuando eD2k está habilitado al arrancar. El perfil ahora arranca con eD2k habilitado, Autoconnect=0 y Reconnect=0. Kad permanece desactivado.

El daemon 3.0.1 intenta descargar y conectar a una lista pública cuando la lista está vacía, incluso con Autoconnect=0. El perfil gestionado fija Ed2kServersUrl vacío y Serverlist=0 para impedirlo. El motor puede registrar «Invalid URL» al arrancar con lista vacía; es el rechazo local del bootstrap, no un fallo EC.

wxFileConfig interpreta barras invertidas como escapes: IncomingDir y TempDir se escriben con barras normales. Las pruebas confirman que los archivos temporales se crean dentro del perfil. La configuración anterior se conserva como amule.conf.pre-servers.bak. No se borran ni migran automáticamente directorios mal formados de ensayos anteriores en vendor/bin.

## Pendiente

- Prueba formal de transferencia: checksum independiente, pausa a mitad y recuperación de bloques tras reinicio.
- Gestión de compartidos, ajustes de carpetas y Kad, bandeja, instalador, eliminación/importación de servidores y reconexión tras fallo.
- Paquete portable autónomo: la publicación aún requiere el árbol del repositorio para localizar el motor.
- aMule Windows impide instancias simultáneas incluso con perfiles distintos. Cerrar la app antes de ejecutar pruebas; no se detienen procesos ajenos automáticamente.
- Las pruebas de servidor controlado requieren una interfaz IPv4 privada activa. Solo su perfil temporal permite servidores LAN.

## Joplin

Web Clipper disponible el 20/09/2026 mediante TDKop. 0.2.0: nota Codex `b34863d12bea450d8269209b232c4f3a`. 0.3.0 y el remoto GitHub: `e00ec0b576824f2b9f71a9bf868991f6`.
