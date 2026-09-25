# Trabajo hecho en la nube (25/09) · para integrar

Rama `claude/nifty-thompson-r3ulpf`, basada en `384fe18`. **No incluye** lo que se hizo en la PC la noche del 24/09 (fase 2 de la demo, sin commitear allá).

**Regla que se siguió:** solo archivos nuevos. Ningún archivo existente de `Assets/` u `OnlineServer/` quedó modificado. Los arreglos de código van como parches para que el sector dueño los aplique en la PC. Por eso la rama se puede mergear sin conflictos, salvo que la PC haya creado archivos con los mismos nombres.

## Qué hay
| Entregable | Sector | Estado |
|---|---|---|
| `revision-codigo.md`: revisión de V260–V269 + seguridad del servidor | todos | 21 hallazgos, 1 alta (reproducida) |
| `parches/servidor-sesion-fantasma.*` + `Tools/test_server_robustness.py` | Servidor | **Aplicar primero.** Un mensaje apagaba la sala. Probado con .NET 10 |
| `parches/eot-dano-magico-doble.*` | Programación | Aplicar y probar en Unity. Afecta jugador y NPC |
| `parches/skillshot-girar-personaje.*` | Programación | Decisión de Lucas. Aplicar y probar en Unity |
| `guia-amigos.md` | Servidor | Borrador para `../AO_Online/LEEME.md`. Incluye firewall solo-Tailscale |
| `sprites-8-direcciones.md` | Arte | Decidido: quedan 4 direcciones |
| `Tools/SpellTester/index_v13.html` (v1.3) | Arte | Listo, probado en Chromium |
| `../contenido/perdida_al_morir_original.md` | Contenido → Programación | Reglas del original. Hoy no se cae nada al morir |
| `../contenido/npc_hechizos_original.{md,json}` | Contenido → Programación | 98 NPC que castean. Falta toda la IA mágica |
| `parches/npc-stats-originales.md` + `Tools/fix_npc_stats_original.py` + `Tools/test_npc_vision.py` | Contenido + Programación + Servidor | Aprobado por Lucas: DEF (28 NPC), rango preferido y visión 15×13. Corregir mapas y regenerar el catálogo **en la PC** |
| `parches/muerte-perdida-original.md` + `muerte-{juego,online}.patch` + `Tools/add_item_drop_flags.py` + `Tools/test_death_drop.py` | Programación + Servidor + Contenido | Aprobado por Lucas: pérdida al morir como el original. Aplicar en la PC |
| `Tools/ci_checks.py` | QA | JSON y Python válidos |
| `.github/workflows/pruebas.yml` | QA | **Activo** (OK de Lucas, 25/09). Sumar `test_server_robustness`, `test_npc_vision` y `test_death_drop` cuando se apliquen sus parches |
| `../demo/mapa/reglas-y-recorrido.md` + `Tools/demo_mapa/vista_mapa.py` | Programación + Arte + Contenido | Reglas del mapa demo aprobadas por Lucas (25/09) y recorrido nuevo del dungeon. Falta aplicarlo a `Tools/demo_maps/` (está solo en la PC) |

## Cómo integrar (Cerebro, en la PC)
1. Subir lo de la PC a una rama (`pc/noche-2509`) y mergear esta rama encima.
2. Aplicar los parches en este orden: servidor → EOT → skill shot. Si `git apply --check` falla, aplicarlos a mano; cada `.md` explica el cambio.
3. Verificar: `dotnet build`, `test_server_robustness.py`, `test_coop_server.py`, `test_static_npcs.py`, y en Unity `test_modules_unity.py` (con `aod-respaldo` antes).
4. Pasar al `tablero.md`:
   - Programación: EOT hecho (parche), giro del skill shot, filtro de mapa en `FindNpcHit`, IA mágica de NPC (con datos de Contenido), pérdida al morir (esperar la decisión de Lucas).
   - Servidor: parche de la sesión fantasma, `--bind` y plazo del `hello`, guardado sin bloqueo, expiración del botín, tope de personajes.
   - Interfaz: aviso de `SetSpellMacros`, chequeo de modales al arrastrar, texto "hasta 10 jugadores", `richText` del chat.
   - Arte: `PlayNearestNpc` solo para NPC.
   - Contenido: corregir `map_migration.py` (necesita el OK de Lucas, ver abajo).
5. Marcar como hechos en el tablero: Spell Tester v1.3; guía de actualización para amigos (borrador); datos de hechizos por NPC; reglas de pérdida al morir.

## Decisiones pendientes de Lucas
1. ~~`map_migration.py` lee campos que no existen en `npcs.dat`~~ → **Resuelto (25/09):** Lucas aprobó DEF y rango preferido originales y visión 15×13. Ver `parches/npc-stats-originales.md`.
2. ~~Pérdida al morir~~ → **Resuelto (25/09):** como el original. Ver `parches/muerte-perdida-original.md`.
3. ~~Workflow de GitHub Actions~~ → **Activo (25/09).**
4. **Remake con Higgsfield:** alcance elegido por Lucas (25/09): **íconos del inventario, HUD en general, piso y agua**. Se habla en detalle cuando termine lo demás.
