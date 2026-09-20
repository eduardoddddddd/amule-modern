# Correcciones para continuar en Cursor — 20/09/2026

Base revisada: ae47219 (0.8.0-dev). Este cambio se limita a los cinco errores encontrados en revisión; no añade funcionalidades del plan.

## Cambios

1. **Bandeja:** el arranque del motor se ejecuta una sola vez por ventana. Hide/Show vuelve a mostrar la interfaz sin iniciar otra sesión ni deshabilitar controles.
2. **Migración:** `folders-migrated-v1` evita importar nuevamente los originales de Incoming/Temp. Si el perfil ya apunta a una biblioteca externa, se considera migrado y no se vuelven a copiar copias antiguas. Los originales se conservan. Copias nuevas usan archivo temporal y renombrado; un destino con contenido diferente provoca error, sin sobrescribirlo.
3. **Carpetas:** cambiar Temp queda bloqueado si hay descargas incompletas, incluidas las pausadas. No se implementa migración de descargas activas. Se comprueba escritura antes de detener el motor; si falla la aplicación posterior, se intenta restaurar configuración y arranque anteriores. Ajustes no se puede cerrar mientras aplica. Cambiar Incoming no mueve archivos completados existentes, y la interfaz lo explica.
4. **Compartidos:** selector independiente de carpetas; quitar una entrada no depende de seleccionar archivos. Funciona para carpetas vacías o desaparecidas, incluso cuando quedan otras entradas desaparecidas. Quitar no borra el directorio.
5. **Kad:** el estado de búsqueda activa sigue la red con la que se inició, no la conexión eD2k ni el ámbito seleccionado después. Kad sin eD2k conserva Detener; la pérdida de su red termina el estado activo.

## Validación realizada

- `scripts/Build.ps1 -Test -Publish`: compilación sin errores/advertencias, 66 comprobaciones PASS y publicación actualizada.
- Regresiones: migración repetida sin resucitar archivos; biblioteca ya migrada; cambio de Incoming; destino inválido sin parar motor; bloqueo de cambio Temp con descarga pausada; retirada de compartidos vacíos y desaparecidos.
- Capturas ejercitadas `--capture ... --exercise-ui`, principal y opciones `--search`, `--shared`, `--settings`: cuatro procesos terminaron con código 0.
- Principal: Hide/Show mantiene PID y controles, más acciones de descargas existentes.
- Compartidos: botón real Quitar, diálogo real de confirmación y retirada de carpeta vacía sin borrado.
- Kad: estados de conexión simulados en la interfaz verifican Detener, cambio de ámbito y pérdida de red. No es una prueba de búsqueda en la red Kad pública.
- Se inspeccionaron visualmente las capturas nuevas de Compartidos y Ajustes (incluye Kad). No quedaron procesos de pruebas.
- Informe de ejecución versionado: `docs/REVIEW-FIXES-VALIDATION.txt`. Capturas locales: `artifacts/fix-main.png`, `fix-search.png`, `fix-shared.png`, `fix-settings.png`.

No se ha realizado la transferencia formal con payload del plan. La restauración tras fallo de arranque está implementada, pero no se ha simulado cada fallo posible de disco/proceso; se han verificado el rechazo previo y los cambios normales.

## Continuación

Si Cursor usa este mismo repositorio, basta con releer el commit nuevo y este documento; no necesita pull. En otro clon, el commit debe publicarse/transferirse primero y después incorporarse allí. No se ha hecho push desde esta corrección.
