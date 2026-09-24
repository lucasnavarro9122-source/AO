# Recuperación de Unity, OneDrive y traspaso a otra IA

## Recuperación de Unity (24/09/2026)
- Unity quedó corriendo sin ventana accesible. Se respaldaron escenas, ajustes y código en `MigrationReports/editor-recovery-20260924-001627`, se reinició el editor y se recuperó la ventana.
- No se confirmó el guardado de la escena en memoria antes del cierre; se conservó la escena de recuperación en disco. Las partidas JSON quedaron intactas.
- Existe `Assets/_Recovery`: no eliminar sin revisar.

## OneDrive
- El proyecto está en OneDrive; hubo fallos de acceso a `Temp/BurstOutput`. Antes de mover esa caché verificar rutas y conservar copia. Nunca borrar carpetas calculadas sin comprobarlas.

## Pasar el proyecto a otra IA
- Con acceso local/IDE: abrir la carpeta del proyecto; `CLAUDE.md` alcanza como contexto.
- Chat sin disco: adjuntar `CLAUDE.md`, `.claude/rules/` y los archivos necesarios. Para una copia: `Assets` con `.meta`, `Packages`, `ProjectSettings`, `OnlineServer`, `Tools`.
- Excluir `Library`, `Temp`, `Logs`, `obj`, `bin`, `Builds`, credenciales y guardados personales. Respaldos privados aparte.
