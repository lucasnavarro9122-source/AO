# Estado QA: trabajo de la nube del 25/09 (tarde)

Rama `claude/nifty-thompson-r3ulpf`, sobre el trabajo de la PC `edcd99d5`. Para integrar: `reporte-mapa-demo.md` (merge) y, antes, el candado de Unity.

## Verificado en la nube (sin Unity)
| Qué | Cómo | Resultado |
|---|---|---|
| Datos y Python | `python Tools/ci_checks.py` | 47 .py, 1217 .json y el catálogo: 0 fallas |
| Mapas de la demo | `python Tools/demo_map_builder.py --check` | igual a lo armado |
| Hechizos de NPC | `python Tools/export_npc_spells.py --check` | igual (98 NPC) |
| Progresión | `python docs/claude/demo/modelo_progresion.py --check` | OK |
| Build del servidor | `dotnet build OnlineServer/AOOnlineServer.csproj` | 0 errores |
| Sala cooperativa | `test_coop_server` | PASS; incluye el reenvío de `castX`/`castY` |
| NPC fijos | `test_static_npcs` | PASS |
| Retos | `test_duel_server` | PASS; caso nuevo de parálisis: sin el freno del servidor falla, con el freno pasa |
| Robustez | `test_server_robustness` | PASS |
| Visión 15×13 | `test_npc_vision` | PASS |
| Pérdida al morir | `test_death_drop` | PASS |
| **IA mágica de NPC** | `test_npc_magic` (nuevo, en CI) | PASS |
| C# de Unity que se tocó | Roslyn, solo sintaxis (C# 9) y APIs revisadas a mano | 0 errores. **Falta compilar en Unity.** |

## Falta probar en Unity (con `aod-respaldo` antes y nunca con personajes de Lucas)
1. **Compilar:** consola sin `error CS` (`Tools/aod_log_errors.ps1`). Archivos nuevos:
   - `AONPCSpellCasterV902.cs`;
   - `Shared/AONpcSpellRulesV902.cs`;
   - `MagicV129/npc_spells.json`, que Unity va a crear con su `.meta`.
2. **Mapa demo** (`docs/claude/demo/mapa/`): recorrer hub → cementerio (camino norte y capilla) → P1…P7 → portal al hub (D-20).
   - Mirar la luz de cada piso, los carteles y los minimapas.
   - En el P2, la cripta con llave queda cerrada.
3. **IA mágica de NPC** (`npc-magia.md`):
   - Sin conexión: Esqueleto Mágico (P3), Mago Malvado y Medusa (P6), dragones y Vytaiz (P7).
     - Se tienen que ver la animación de lanzamiento, el efecto, el daño y el mensaje.
     - La parálisis frena al personaje.
     - Un NPC sin hechizos pega igual que antes.
   - En la sala: lo mismo con 2 clientes. El que recibe ve el hechizo y el mensaje "X lanzó Y".
4. **Retos:**
   - Paralizar o Inmovilizar a un rival lo congela `Duration/2` segundos.
   - "Remover parálisis" a un compañero lo libera.
   - La parálisis se borra al empezar una ronda nueva.
5. **Skill shot visible:** con 2 clientes, cuando uno tira un skill shot, el otro ve el proyectil (solo visual) y el otro no recibe daño por eso.
6. Lo pendiente de antes sigue en `docs/claude/tablero.md`: módulos V261–V269 y V130, HD y luces, entre otros.

## Riesgos
- **Balance:** el dungeon se va a sentir más difícil porque ahora los NPC usan la magia original. No cambian stats, pero Contenido tiene que revisar los tiempos.
- Módulos **V902 provisorios**: renumerar si la PC ya usó ese número (la próxima libre era V290).
- `AOOnlineClientV240`, `AOPlayerMagicV120`, `AONPCMovementV08` y `AOSkillShotProjectileV267` tienen enganches chicos. Si en la PC tienen cambios sin commit, el merge va a pedir resolverlos a mano.
