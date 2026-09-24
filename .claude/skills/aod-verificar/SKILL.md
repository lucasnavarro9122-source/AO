---
name: aod-verificar
description: Verifica que AoDuels compila y que las pruebas pasan antes de dar algo por terminado. Usar después de editar C#, antes de reportar a Cerebro, antes de un build y cuando alguien pregunta "¿funciona?". Incluye la compilación auxiliar, los errores del Editor.log, las pruebas de Unity y las del servidor.
---

# Verificación

No afirmar que algo funciona sin evidencia. Distinguir "compila", "pasa la prueba X" y "probado jugando".

## Nivel 1: siempre (no toca Unity)
```powershell
dotnet build AOCoopCompile.csproj -v:q -clp:ErrorsOnly 2>&1 | Select-Object -Last 15
```
Cubre solo los scripts de runtime (los ~107 warnings son normales). No cubre los scripts de Editor ni la serialización.

## Nivel 2: compilación en Unity
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_log_errors.ps1
```
- Unity recompila solo al detectar cambios; esperá a que termine.
- `SIN ERRORES` = ok. Si lista errores, arreglá primero el primero (los demás suelen ser en cascada).

## Nivel 3: pruebas en Unity (tomar el candado de Unity + `aod-respaldo` antes)
| Prueba | Qué cubre |
|---|---|
| `python Tools/test_controls_unity.py` | Controles AO/MOBA, perfiles, macros, rutas, consumibles; verifica que los guardados queden intactos |
| `python Tools/test_coop_unity.py` | Cliente online contra una sala aislada (nunca contra la sala real) |
- Requieren Unity abierto y **fuera de Play**. Usan marcadores en `Temp/`: no dejarlos olvidados.
- Después: `aod_log_errors.ps1 -DesdeUltimoPlay` para ver las excepciones de runtime.

## Nivel 4: servidor
```powershell
dotnet build OnlineServer/AOOnlineServer.csproj -v:q -clp:ErrorsOnly
python Tools/test_coop_server.py
python Tools/test_static_npcs.py
```

## Módulos sin prueba automática
V261 (drag & drop), V267 (skill shots), V268 (casteo), V269 (meditación) y V130 (EOT) todavía no tienen QA. Si los tocás, decilo explícitamente en el reporte, o pedile a QA que agregue la prueba.

## Delegar
Para no llenar el contexto, usá el subagente `verificador` (compila y prueba) y `lector-logs` (errores de logs).
