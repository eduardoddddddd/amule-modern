# Estado al 20/09/2026 — 0.2.0-dev

Repositorio: C:\Users\Edu\source\amule-modern. C# / Avalonia 12.1.2, SDK local .NET 10.0.401 y motor oficial aMule 3.0.1 fijados.

## Implementado y comprobado

- Descargas: añadir enlace, tabla, filtro, pausa, reanudación y estadísticas reales por EC.
- Ventana **Servidores**: nombre opcional, IPv4 o dominio y puerto TCP; Añadir, Añadir y conectar, Conectar al seleccionado, Desconectar y Actualizar.
- Lista persistente en server.met; validación de puerto y dominio, resolución IPv4 y prevención de duplicados. El dominio se resuelve al añadir y se guarda su IP; no hay actualización DNS dinámica todavía.
- Estado eD2k real (desconectado, conectando, conectado; HighID/LowID) separado del estado EC y Kad. El botón Desconectar se habilita tras conexión establecida: el comando del motor no cancela intentos en curso.
- Perfil propio y EC loopback; cierre ordenado del proceso. No se tocaron firewall, instalaciones personales ni servicios.
- Compilación Release sin advertencias ni errores, publicación autocontenida y 31 comprobaciones superadas.
- Prueba de sesión eD2k real contra un servidor controlado en la IP LAN del mismo equipo: recepción de login, estado conectando antes del ID, HighID confirmado y desconexión. No prueba transferencia P2P ni fiabilidad de servidores públicos.
- Pruebas de botones reales en ambas ventanas con motor real; capturas inspeccionadas; procesos de captura terminaron con código 0.

Evidencias: artifacts/servers-build-validation.txt, artifacts/servers-ui.png, artifacts/servers-ui.validation.txt, artifacts/main-ui.png y artifacts/main-ui.validation.txt (excluidas de Git).

## Correcciones de integración

aMule solo inicializa y guarda server.met cuando eD2k está habilitado al arrancar. El perfil ahora arranca con eD2k habilitado, Autoconnect=0 y Reconnect=0. Kad permanece desactivado.

El daemon 3.0.1 intenta descargar y conectar a una lista pública cuando la lista está vacía, incluso con Autoconnect=0. Se observó ese intento durante el diagnóstico. El perfil gestionado ahora fija Ed2kServersUrl vacío y Serverlist=0 para impedirlo. El motor puede registrar «Invalid URL» al arrancar con lista vacía; es el rechazo local del bootstrap, no un fallo EC. Los ensayos finales comprueban reinicio sin conectar automáticamente.

wxFileConfig interpreta barras invertidas como escapes: se corrigen IncomingDir y TempDir a rutas con barras normales. Las pruebas confirman que los archivos temporales se crean dentro del perfil. La configuración anterior se conserva como amule.conf.pre-servers.bak en el perfil privado. No se borran ni migran automáticamente archivos de directorios mal formados de ensayos anteriores.

## Pendiente

- Transferencia autorizada de un archivo, checksum e integridad, pausa y recuperación de bloques tras reinicio. La fase 0 sigue parcialmente completada.
- Búsqueda, gestión de compartidos, ajustes de carpetas y Kad, bandeja, instalador, eliminación/importación de servidores y reconexión tras fallo.
- Paquete portable autónomo: la publicación aún requiere el árbol del repositorio para localizar el motor.
- aMule Windows impide instancias simultáneas incluso con perfiles distintos. Cerrar la app antes de ejecutar pruebas; no se detienen procesos ajenos automáticamente.
- Las pruebas de servidor controlado requieren una interfaz IPv4 privada activa. Solo su perfil temporal permite servidores LAN.

## Joplin

Web Clipper disponible el 20/09/2026 mediante TDKop. Se registra este incremento en la libreta Codex, junto con las decisiones, validaciones y límites anteriores.
