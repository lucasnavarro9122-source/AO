---
paths:
  - "OnlineServer/**"
  - "Assets/AOMigrator/Editor/AOOnlineBuild*.cs"
  - "Assets/AOMigrator/Runtime/AOOnline*.cs"
  - "Assets/AOMigrator/Runtime/AOCoop*.cs"
  - "Tools/package_online_client.py"
  - "Tools/test_coop_*.py"
  - "Tools/export_online_catalog.py"
---

# Servidor cooperativo y distribución

- Servidor propio C# (`OnlineServer/`), **protocolo 3** en el repo (el 0.26 distribuido sigue en 2). Comparte enemigos, vida, botín, puertas y comercio; EXP y crédito de misiones para compañeros cercanos; nombres/equipo; curación/resurrección entre jugadores; persistencia.
- Desde el protocolo 3 el servidor es dueño del **oro**: billetera, banco y custodia de apuestas, con el libro `Saves/ledger.jsonl`.
- Instancia de la demo: `--demo --data SavesDemo --port 7778`, con retos, EXP por tramo y oro ×2.
- Alpha para amigos de confianza, sin paridad con el servidor original. PvP solo dentro de un reto de la demo; sin clanes ni oficios completos. Detalle en la skill `aod-servidor`. Ver `OnlineServer/README.md` y `../AO_Online/LEEME.md`.
- `../AO_Online/VERSION.txt`: cliente 0.25 (7774658), servidor 0.25.1 (d69daef). Verificar binarios y fechas antes de afirmar qué incluye el ZIP.
- Red: TCP 7777 + Tailscale. Anfitrión usa `127.0.0.1`; amigos usan la IP Tailscale del anfitrión y su propia conexión autorizada. Claves se comparten en privado.
- No publicar claves, tokens, registros que las contengan ni partidas personales. No leer `room-key.txt`.
- Al actualizar ejecutables: no reemplazar `../AO_Online/Release/Server/Saves` ni `room-key.txt`. Hay respaldos de cliente/servidor anteriores en `../AO_Online`.

## Build y empaquetado
- Menú **AO Migrator > Build private room client** → `../AO_Online/Release/Client/ArgentumOnline.exe`.
- `python Tools/package_online_client.py` → `../AO_Online/Cliente-para-amigos.zip`, verifica CRC. Guardar copia del ZIP anterior.
- `AOOnlineBuildV240.cs` reconoce marcadores en `Temp`: `refresh_online_client`, `build_online_client`, `restart_online_editor` (este último guarda escenas/assets y cierra Unity). No dejar marcadores olvidados ni pedirlos durante una partida del usuario.
- Tras un build válido: registrar en qué commit/revisión se basa.
