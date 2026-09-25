# NPC: defensa, rango preferido y visión como el original

**Aprobado por Lucas el 25/09:** DEF y rango preferido originales; visión exacta de 15 × 13.
Dueños: **Contenido** (migración y datos), **Programación** (IA local), **Servidor** (IA online y catálogo).
Preparado en la nube contra `384fe18`. Se aplica en la PC, porque ahí están `Archivos Originales` y el catálogo que se regeneró la noche del 24/09.

## El problema
`Tools/map_migration.py` (`npc_entry`) leía de `npcs.dat` claves que no existen:

| Dato | Leía | El original usa | Fuente |
|---|---|---|---|
| Defensa | `defensa` | `DEF` | `MODULO_NPCs.bas:1126` |
| Rango preferido | `preferredrange` | `PreferedRange` (con una sola r) | `MODULO_NPCs.bas:1072` |
| Visión | `visionrange` (8 por defecto) | `Distancia`. Si es 0: **15 horizontal × 13 vertical**, contados por eje | `AI_NPC.bas:1406-1410`, `Consts.bas:31-32` |

Además, `map_migration.py` **no rehace los NPC de los mapas que ya existen**: solo agrega visuales. Por eso arreglar las claves no alcanza.

## Qué cambia (medido con los 842 mapas y los 5.427 NPC)
Se verificó que la copia de `npcs.dat` es la misma versión: los 450 NPC tienen el mismo nombre y los demás valores coinciden.

- **DEF, 28 NPC:** Gran Dragón Rojo 400. Con 40: Guardias Imperiales (260, 261), Guardias del Caos (271, 981), 9 Krakens Gigantes, Reina Gorgona y Devorador del Inframundo. Con 30: Golem de Hielo, Azhran y Matriarca Drake. Con 25: Golem de Piedra y Patriarca Drake. Vytaiz 20. Con 15: Dragones Dorado, Cárdeno y Rojo, y Golem de Piedra Mayor. Con 10: Pequeños Dragones.
  - Cada golpe físico contra ellos pierde esos puntos. Es la misma fórmula que el original (`SistemaCombate.bas:679`), local y online.
- **Rango preferido:** Scramer Pícaro pasa de 0 a 5. Grindal (quest, no hostil) pasa de 1 a 0, sin efecto en combate.
- **Visión, todos los NPC:** de 8 en cuadrado a 15 × 13 por eje. Ningún NPC define `Distancia`. Los hostiles te ven desde más lejos.
- En los mapas solo cambian esos campos, más `visionRangeX` y `visionRangeY` nuevos. Verificado: 422 mapas, 0 cambios fuera de esos campos.

## Archivos
| Archivo | Dueño | Qué hace |
|---|---|---|
| `Tools/fix_npc_stats_original.py` (nuevo) | Contenido | Corrige esos campos en `WorldV07/Maps/map_*.json`. Por defecto simula. Con `--aplicar`, primero respalda en `MigrationReports/backup_npc_stats_<fecha>/`. Saltea un NPC si su nombre no coincide con `npcs.dat` |
| `npc-stats-map-migration.patch` | Contenido | Claves correctas en `npc_entry`, para los mapas que se creen en el futuro |
| `npc-vision-15x13-juego.patch` | Programación | `AONPCMovementV08.SetVisionAxes` + los campos en `AOWorldManagerV07`. Sin datos X/Y usa `visionRange` como antes |
| `npc-vision-15x13-servidor.patch` | Servidor | `CoopRoom`: visión por eje. Con un catálogo viejo usa `visionRange` como antes |
| `Tools/test_npc_vision.py` (nuevo) | Servidor | 15 en horizontal, 13 en vertical, y el catálogo viejo sigue funcionando |

## Verificación en la nube
- La simulación y la aplicación sobre los 842 mapas reales, más `export_online_catalog.py`, dejan el catálogo con 5.427 NPC, todos con X/Y. El 549 queda con DEF 400 y visión 15/13; el 668 con rango preferido 5.
- Servidor con el parche: `test_npc_vision` **PASA**, `test_coop_server` PASA, `test_static_npcs` PASA (también con el catálogo nuevo).
- Servidor sin el parche: `test_npc_vision` **FALLA**, porque el NPC a 14 casillas en vertical te ve igual.
- C#: los 2 archivos del juego quedan sin errores de sintaxis (Roslyn, C# 9). No se pudo compilar contra UnityEngine.
- Después de probar, se restauraron los datos: esta rama no trae mapas ni catálogo regenerados.

## Pasos en la PC (en orden)
1. `aod-respaldo`: respaldo con fecha de los guardados.
2. Aplicar los 3 parches (`git apply --check` y después `git apply`):
   ```powershell
   git apply docs/claude/nube/parches/npc-stats-map-migration.patch
   git apply docs/claude/nube/parches/npc-vision-15x13-juego.patch
   git apply docs/claude/nube/parches/npc-vision-15x13-servidor.patch
   ```
3. Simular y revisar la lista:
   ```powershell
   python Tools/fix_npc_stats_original.py
   ```
   - Mirar en especial los mapas ≥ 1000 de la demo, si ya existen: sus NPC también se corrigen si el nombre coincide.
4. Aplicar y regenerar el catálogo:
   ```powershell
   python Tools/fix_npc_stats_original.py --aplicar
   python Tools/export_online_catalog.py
   ```
5. Servidor:
   ```powershell
   dotnet build OnlineServer/AOOnlineServer.csproj -v:q -clp:ErrorsOnly
   python Tools/test_npc_vision.py; python Tools/test_coop_server.py; python Tools/test_static_npcs.py
   ```
6. Unity: tomar el candado, compilar, `aod_log_errors`, `test_modules_unity.py`. En Play:
   - un Guardia Imperial recibe 40 menos por golpe;
   - un NPC hostil te ve a 15 casillas en horizontal pero no a 14 en vertical.
7. Publicar el servidor con el procedimiento de siempre (`Server-next`, sin tocar `Saves` ni `room-key.txt`) y armar el cliente nuevo. El protocolo no cambia: un cliente viejo con el servidor nuevo sigue funcionando.
