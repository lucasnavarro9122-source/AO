# Parche: un mensaje con un dato ilegible apaga la sala

Dueño: **Servidor y Multiplayer**. Preparado en la nube el 25/09 contra `384fe18`. Verificado con .NET 10 en Linux.

## Bug (ALTA, reproducido)
1. `CoopRoom.Join` hace `sessions.Add` **antes** de `RefreshPlayer`. Si el guardado trae un campo ilegible (por ejemplo `"raceId":"x"` o `1e20`), `RefreshPlayer` falla y la sesión queda **fantasma** con `map = 0`.
2. En el siguiente tick (≤ 150 ms), `Map(0)` lanza `KeyNotFoundException`. El `catch` del ticker (`Program.cs`) hace `stop.Cancel()` y **se cierra el servidor para todos**, con código de salida 0, o sea sin señal de error.
3. `Checkpoint` guardaba el snapshot **antes** de leerlo. Un guardado ilegible queda en `world.json`, y al reconectar vuelve a tirar la sala. Tampoco se arregla reiniciando.

**Cómo puede pasar sin mala intención:** un cliente de otra versión o un guardado local dañado.

## Arreglo (`servidor-sesion-fantasma.patch`)
- `Join`: primero valida (`RefreshPlayer` + `Map`) y **después** registra el personaje nuevo y la sesión. Si algo falla, el cliente recibe "Guardado inválido." y no queda nada a medias.
- `Checkpoint`: lee el estado antes de guardar el snapshot. Si no se puede leer, no se guarda.
- Ticker (`Program.cs`): una excepción en un tick se registra (como mucho una vez cada 10 s) y la sala sigue, en vez de cerrarse.
  - Esto también cubre dos hallazgos de la revisión: un guardado bloqueado por OneDrive o el antivirus dentro del tick (#3), y el límite del diario de eventos (#12).

## Verificación (en la nube)
| Prueba | Sin parche | Con parche |
|---|---|---|
| `Tools/test_server_robustness.py` (nueva) | **FALLA**: "No se pudo mantener el mundo: The given key '0'…" | PASA |
| `Tools/test_coop_server.py` | PASA | PASA |
| `Tools/test_static_npcs.py` | PASA | PASA |
| `dotnet build` | 0 errores | 0 errores |

## Aplicar en la PC
```powershell
git apply --check docs/claude/nube/parches/servidor-sesion-fantasma.patch
git apply docs/claude/nube/parches/servidor-sesion-fantasma.patch
dotnet build OnlineServer/AOOnlineServer.csproj -v:q -clp:ErrorsOnly
python Tools/test_server_robustness.py; python Tools/test_coop_server.py; python Tools/test_static_npcs.py
```
Si la noche del 24/09 cambió `CoopRoom.cs` y `--check` falla, aplicar a mano las 3 ideas de arriba. Son unas 20 líneas.

Después, publicar con el procedimiento de siempre (`Server-next`, respaldo antes, sin tocar `Saves` ni `room-key.txt`).
