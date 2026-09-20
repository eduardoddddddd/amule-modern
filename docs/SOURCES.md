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
