# Actualización para todos los sectores: todo lo de la nube (25/09)

CEREBRO: pasale a cada sector su parte de este documento (SendMessage a "AO BATTLESERVER: <Sector>"). Contenido está archivado: sus tareas las asignás vos.
- **Rama:** `origin/claude/nifty-thompson-r3ulpf`, último commit `5bf287a`.
- **Base:** tu `edcd99d5`. Todo lo de la madrugada (hasta `439bea9`) ya lo integraste en `acca325`.

## Cómo integrar (CEREBRO, una sola vez)
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/aod_unity_lock.ps1 take -Sector "CEREBRO" -Motivo "merge de la nube"
git fetch origin; git merge origin/claude/nifty-thompson-r3ulpf
```
- El merge no toca `tablero.md`, `pruebas.md` ni `r4-checklist.md`, tus cambios sin commit de esta tarde. Si Git frena por algún otro archivo con cambios sin commit, commitealo o guardalo aparte antes.
- Después: compilar en Unity (consola sin `error CS`), `aod-verificar`, liberar el candado y avisar a cada sector.
- Distribución: servidor 0.27 y cliente **juntos**, porque cambian el protocolo (campos nuevos, compatibles con clientes viejos) y el catálogo.

## Ya en la PC (de la madrugada, integrado en `acca325`)
Según el tablero ya está aplicado: sesión fantasma, EOT, giro del skill shot, visión 15×13, stats originales de NPC, pérdida al morir (V275/V286), HD en el juego (V279), Spell Tester 1.3, CI y seguridad del servidor.

Queda abierto de entonces:
- **HD, rondas de zonas:** las sigue Higgsfield/Arte en la PC (`docs/claude/nube/hd/zonas/`).
- **Contenido:** `add_item_drop_flags.py` (unificar nombres con `noSeCae`/`intirable`).
- **Contenido:** borrar `docs/claude/contenido/npc-hechizos.json`. Ya está marcado "reemplazado por `npc_hechizos_original.json`" y ahora el juego usa `MagicV129/npc_spells.json`.

---

## Lo nuevo, por sector (commits `e9c90aa` … `5bf287a`)

### CEREBRO
- **Integrar y repartir** con este documento.
- **Skill nuevo `aod-cerebro`** (`.claude/skills/aod-cerebro/SKILL.md`, más una línea en `CLAUDE.md`): es el puente entre la nube y vos.
  - La nube puede ver el estado de los chats y escribirte con una Routine, siempre con el OK de Lucas.
  - Vos respondés por GitHub, con la receta de subida a `pc/<tema>` o en `docs/claude/nube/buzon/`.
  - Toca archivos compartidos (`CLAUDE.md`, `.claude/`): este es el aviso.
- **Módulos provisorios V902:** `AONpcSpellRulesV902` (Shared), `AONPCSpellCasterV902` y `AONPCSummonLinkV902`. Renumeralos a V290 y siguientes si querés, y subí la "próxima versión libre".
- Las decisiones de Lucas de esta tarde están en `docs/claude/demo/mapa/reglas-y-recorrido.md`: 12 reglas del mapa demo, aprobadas.

### Programación
**Mapa demo** (`docs/claude/nube/reporte-mapa-demo.md`):
- se cambiaron `Tools/demo_maps/1010.json` (entrada en el Cementerio de Nix, mapa 4), los carteles de 1011–1017 y `oneWayExits` del 1017;
- `demo_map_builder.py --check` da igual.

**IA mágica de NPC** (`docs/claude/nube/npc-magia.md`), con archivos nuevos y enganches chicos:
- Nuevos:
  - `Runtime/Shared/AONpcSpellRulesV902.cs`;
  - `Runtime/AONPCSpellCasterV902.cs`;
  - `Runtime/AONPCSummonLinkV902.cs`.
- Enganches:
  - `AONPCMovementV08.ThinkHostileAgainstPlayer`: primero la magia, después el melee;
  - `AOPlayerMagicV120`: `ApplyNpcSpell`, `ApplyToPlayer(skipHp)` y, en el skill shot, `NotifyLocalSkillShot`;
  - `AOWorldManagerV07.SpawnSummonedNpc`;
  - `AOOnlineClientV240`: `npcCast`, `hurt` con `spell`, criaturas invocadas y skill shot remoto;
  - `AOSkillShotProjectileV267.LaunchVisual`, el proyectil solo visual para los compañeros;
  - `AOCoopProtocolV250`: `castX` y `castY`.
- Falta: **compilar en Unity**, porque en la nube solo se revisó la sintaxis.

### Servidor y Multiplayer
- **Nuevo** `OnlineServer/CoopRoom.NpcMagic.cs`: magia de NPC, IA de apoyo, invocaciones y `npcCast`.
- **Enganches en `CoopRoom.cs`:**
  - `TickMap`;
  - `Hit`, que llama a `NpcDied`;
  - `BindMap`, que descarta las invocadas guardadas;
  - los campos de `NpcRecord`;
  - el reenvío de `castX`/`castY`.
- **Retos** (`CoopRoom.Duels.cs`):
  - Paralizar o Inmovilizar a un rival lo congela `Duration/2` s, y la sala no deja que se mueva;
  - "Remover parálisis" se puede tirar a un compañero;
  - la parálisis se limpia en cada ronda.
- **Catálogo** regenerado:
  - cambian los 9 mapas de la demo y se agrega `npcSpells`;
  - `export_online_catalog.py` escribe el gzip con el byte de sistema fijo (Windows), así la nube y la PC dan los mismos bytes.
- **Pruebas:**
  - nueva `Tools/test_npc_magic.py`, ya en `.github/workflows/pruebas.yml`;
  - `test_duel_server` tiene un caso de parálisis;
  - `test_coop_server` prueba el reenvío de `castX`/`castY`;
  - las 7 pasan en la nube y en GitHub Actions.
- **Guía** `docs/claude/nube/guia-amigos.md`: demo en el 7778, con firewall y Tailscale para el 7778, y no pisar `SavesDemo`. Llevarlo a `../AO_Online/LEEME.md`.

### Contenido y Fidelidad AO (archivado → lo asigna CEREBRO)
- **`dungeon-npcs.json`:** cambian solo los mapas fuente, las escaleras, las zonas y la luz de P1–P7. Los NPC, la EXP y el respawn quedan igual. Se volvieron a medir los recorridos y `modelo_progresion.py --check` da OK.
- **`textos-hub.md`:** nombres nuevos de los pisos (Dungeon Newbie, Catacumbas, Tumba del desierto, Cueva de las Gorgonas) y luz según la profundidad.
- **Datos nuevos:** `Tools/export_npc_spells.py` → `MagicV129/npc_spells.json`.
  - Son 98 NPC que lanzan hechizos, con `RangoSpell`, `CdN`, ayuda, ataque y tope de invocaciones, más 5 criaturas invocables.
  - Sale de `npcs.dat`, sin cambios de stats.
- **Revisar el balance:** ahora los NPC usan su magia original, así que el dungeon se va a sentir más difícil. Los tiempos del modelo no cuentan el daño mágico que recibe el jugador.

### Interfaz y Controles
- **Retos:** a un rival se le pueden tirar Paralizar e Inmovilizar, y "Remover parálisis" a un compañero. Revisar que la barra y los mensajes lo muestren bien.
- **Mensajes nuevos en el chat:**
  - "X lanzó Y." cuando te llega un hechizo de un NPC en la sala;
  - sin conexión, "X: <hechizo> te causa N de daño.".
- **Skill shot:** los compañeros ven el proyectil. No hay que tocar nada, pero conviene mirarlo en MOBA.

### Arte y Animación (y Higgsfield)
- **Mapa demo:**
  - la entrada es el cementerio;
  - P2 son catacumbas de tierra, P6 la cueva con lago y P7 el Magma;
  - P1, P3, P4 y P5 están enteros, sin cortes;
  - `demo_art_specs.py`: `OUTDOOR` queda vacío, `spec_1010` pasa al cementerio y se saca el cartel de Veriil.
  - Revisar en Unity la luz nueva, los carteles y los minimapas.
- **Magia de NPC:** se usan `AOSpellFXV120.PlayFromNpc` y `AOCastAnimationRuntimeV268.PlayNpc`. Revisar cómo se ven los lanzamientos de NPC y las criaturas invocadas; la Hiena Demoníaca usa las texturas de `MagicV129`.
- **Imágenes** en `docs/claude/demo/mapa/`: `antes_despues.jpg` y `recorrido_dungeon.jpg`.

### QA y Releases
- La lista completa de qué probar en Unity está en `docs/claude/nube/estado-qa-2509.md`:
  - mapa demo;
  - magia de NPC, sin conexión y en la sala;
  - invocaciones;
  - IA de apoyo;
  - retos con parálisis;
  - skill shot visible.
- **Antes de cada prueba:** `aod-respaldo`, y nunca con personajes de Lucas.
- **Release 0.27:** cliente y servidor juntos, porque cambian el catálogo y los mapas de la demo.
