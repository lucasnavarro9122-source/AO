---
name: aod-respaldo
description: Respalda los guardados de AoDuels (partidas locales, Saves del servidor y PlayerPrefs) antes de cualquier prueba en Unity, del servidor, de un build o de un cambio en guardado, identidad o persistencia. También se usa para restaurar un respaldo si una prueba dañó una partida.
---

# Respaldo de guardados

Hubo un incidente en el que una prueba sobrescribió una partida. Respaldar **siempre** antes de probar.

## Respaldar
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_backup.ps1 -Motivo "<qué vas a probar>"
```
- Crea `../Respaldos/guardados-<fecha>` con `LocalLow/`, `ServerSaves/`, `PlayerPrefs.reg` y `manifest.json` (con hashes).
- Solo copia; nunca borra ni mueve.
- Anotá la ruta resultante en tu reporte.

## Reglas
- Nunca usar personajes de Lucas como fixtures. Las pruebas usan prefijos o datos temporales (ej. `AOPlayerSettingsV230.TestPrefixOverride`, puertos e identidades aislados en `test_coop_server.py`).
- Nunca leer, copiar a un chat ni publicar `room-key.txt`.
- No borrar PlayerPrefs ni `Saves/`.

## Restaurar (solo con OK explícito de Lucas)
1. Cerrar Unity, el cliente y el servidor.
2. Hacer un respaldo del estado actual (aunque esté roto).
3. Copiar de vuelta solo los archivos afectados desde el respaldo elegido. PlayerPrefs: `reg import PlayerPrefs.reg`.
4. Comparar los hashes con `manifest.json`.
