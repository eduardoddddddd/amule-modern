# Referencias verificadas

- Motor fijado: https://github.com/amule-org/amule/releases/tag/3.0.1
- Control EC: https://github.com/amule-org/amule/blob/3.0.1/src/ExternalConn.cpp
- Transporte real: https://github.com/amule-org/amule/blob/3.0.1/src/libs/ec/cpp/ECSocket.cpp
- Longitudes y etiquetas: https://github.com/amule-org/amule/blob/3.0.1/src/libs/ec/cpp/ECTag.cpp
- Constantes: https://github.com/amule-org/amule/blob/3.0.1/src/libs/ec/cpp/ECCodes.h
- Protocolo explicado: https://amule-org.github.io/docs/developer/ec-protocol
- Avalonia Windows: https://docs.avaloniaui.net/docs/platform-specific-guides/windows

La documentación EC incluida en la publicación describe antiguas cabeceras variables. El código utiliza 8 bytes fijos big-endian. También se ha corregido en nuestras pruebas el ejemplo de estadísticas: su cuerpo ocupa 11 bytes, no 9. TAGLEN excluye el contador de hijos propio y cuenta el tamaño serializado completo de cada hijo. El binario real se ha usado como referencia final.

No se anuncian extensiones EC que aún no implementamos. Límite inicial: paquetes de 16 MiB y anidación de 24 niveles. La primera versión usa consultas completas de la cola; las actualizaciones incrementales quedan pendientes de validación.

## Servidores — verificación de código 20/09/2026

Código oficial fijado a 3.0.1:
- https://github.com/amule-org/amule/blob/3.0.1/src/ExternalConn.cpp — añadir/listar/conectar/desconectar y preferencias EC.
- https://github.com/amule-org/amule/blob/3.0.1/src/ServerList.cpp — inicialización y persistencia de server.met; rechazo de URL bootstrap vacía.
- https://github.com/amule-org/amule/blob/3.0.1/src/amule.cpp — bootstrap automático del daemon y conexión al terminar de descargar la lista.
- https://github.com/amule-org/amule/blob/3.0.1/src/ServerConnect.cpp — desconectar solo sesiones establecidas.
- https://github.com/amule-org/amule/blob/3.0.1/src/ServerSocket.cpp — respuesta eD2k OP_IDCHANGE.
- https://github.com/amule-org/amule/blob/3.0.1/src/Preferences.cpp — claves y valores predeterminados del perfil.

Las pruebas de ejecución se recogen en STATUS.md; la lectura de fuentes no sustituye las pruebas P2P pendientes.

## Búsqueda — verificación de código 20/09/2026

Código oficial fijado a 3.0.1:
- https://github.com/amule-org/amule/blob/3.0.1/src/ExternalConn.cpp — EC_SEARCH_START (0x26), EC_SEARCH_STOP (0x27), EC_SEARCH_RESULTS (0x28) y EC_SEARCH_DOWNLOAD (0x2a).
- https://github.com/amule-org/amule/blob/3.0.1/src/libs/ec/cpp/ECCodes.h — etiquetas de consulta (0x701 tipo, 0x702 texto) y de resultado (0x700, 0x301, 0x303, 0x30a, 0x30d).
- https://github.com/amule-org/amule/blob/3.0.1/src/SearchList.cpp — una búsqueda activa en el motor; arrancar otra detiene la anterior.

La implementación anuncia solo servidor local o global eD2k. Kad sigue desactivado en el perfil. Una búsqueda de uso real no sustituye la prueba formal de integridad de transferencia.

## Cola — verificación de código 20/09/2026

- https://github.com/amule-org/amule/blob/3.0.1/src/ExternalConn.cpp — un comando de partfile acepta varios hashes; EC_OP_PARTFILE_DELETE llama a `PartFile::Delete`; EC_OP_CLEAR_COMPLETED usa EC_TAG_ECID (0x000F).
- https://github.com/amule-org/amule/blob/3.0.1/src/DownloadQueue.cpp — `ClearCompleted` solo retira de la lista de completados; no borra Incoming.
- En detalle FULL el entero de la etiqueta 0x300 es el ECID de sesión. No se guarda entre arranques.

## Compartidos y Kad — verificación de código 20/09/2026

- https://github.com/amule-org/amule/blob/3.0.1/src/ExternalConn.cpp — `EC_OP_GET_SHARED_FILES` (0x10), `EC_OP_SHAREDFILES_RELOAD` (0x23), `EC_OP_KAD_START` (0x48), `EC_OP_KAD_STOP` (0x49). Kad start falla si la preferencia está desactivada.
- https://github.com/amule-org/amule/blob/3.0.1/src/Preferences.cpp — `shareddir-explicit.dat` / `shareddir-recursive.dat` / `shareddir.dat`. Un Reload recorta las entradas explícitas que no estén también en el union `shareddir.dat`.
- Incoming se comparte siempre. No se toca el cortafuegos al activar Kad.

## Límites, servidores y empaquetado — 20/09/2026

- https://github.com/amule-org/amule/blob/3.0.1/src/libs/ec/cpp/ECCodes.h — `EC_TAG_CONN_MAX_DL` 0x1303, `EC_TAG_CONN_MAX_UL` 0x1304 (KiB/s), `EC_OP_SERVER_REMOVE` 0x30, `EC_OP_SERVER_UPDATE_FROM_URL` 0x32.
- https://github.com/amule-org/amule/blob/3.0.1/src/ExternalConn.cpp — quitar servidor exige etiqueta 0x500; actualizar desde URL escribe `Ed2kServersUrl`.
- https://github.com/amule-org/amule/blob/3.0.1/src/Preferences.cpp — `CheckUlDlRatio` recorta la bajada si la subida es < 4 KiB/s (×3) o < 10 KiB/s (×4).
- Esta interfaz importa listas en C# (texto/`server.met`/http) y llama a añadir servidor, para no reactivar el bootstrap automático.

