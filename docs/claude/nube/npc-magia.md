# IA mágica de NPC (nube, 25/09): los NPC lanzan hechizos como en el original

Antes ningún NPC lanzaba hechizos. Ahora los 98 NPC que tienen `LanzaSpells` en `npcs.dat` los usan, sin conexión y en la sala.
- Reglas: `docs/claude/contenido/npc_hechizos_original.md`, sacadas del servidor original ao-org 08a5711.
- Números de módulo **provisorios, V902**: CEREBRO los renumera a la próxima versión libre si quiere.

## Qué hace (IA clásica: `AI_AtacarUsuarioObjetivo` + `NpcLanzaUnSpell`)
- **Sin maná:** lo único que limita es `IntervaloLanzarHechizo` (8000 ms si falta). El intervalo se gasta aunque el hechizo no salga, como en el original.
- **Rango:** |dx| ≤ 11 y |dy| ≤ 9 respecto del jugador objetivo.
- **Qué hechizo:** sorteo uniforme entre los slots (`Sp1..SpN`). Los repetidos pesan más y un slot vacío gasta el turno.
- **A quién**, según el `Target` del hechizo:
  - 1: al jugador.
  - 2: a sí mismo, con `AutoLanzar`.
  - 3: a sí mismo con `AutoLanzar`; si no, al jugador.
  - 4: área. Centro en el objetivo ±2 y cuadrado de `AreaRadio`, con mitad `CInt(r/2)`.
- **Daño:** `rand(MinHp, MaxHp) × (1 + MagicBonus)`, menos el % de resistencia mágica del jugador (armadura, anillo, escudo y casco), salvo `AntiRm`. Nunca baja de 0.
- **Otros efectos** (parálisis e inmovilización con `Duration/2`, fuego, veneno, ceguera, maldición, atributos, EOT): se reusa `ApplyToPlayer`, el mismo código que ya aplica los hechizos entre jugadores.
- **Magia primero:** el NPC pega cuerpo a cuerpo solo si no lanzó ese turno, está pegado y no tiene `DontHitVisiblePlayers`. Un NPC sin hechizos pega como siempre.
- **NPC paralizado:** no daña a jugadores.

**No incluido todavía:**
- la IA de apoyo (`Movement` 11/13: curar aliados primero, 3 NPC);
- las invocaciones;
- el filtro de nivel del NPC;
- en la sala, el chequeo de invisibilidad, porque el servidor no sabe quién está invisible (sin conexión sí se chequea);
- que los **otros** jugadores vean la animación de lanzamiento: hoy la ve el que recibe el hechizo.

## Archivos
| Archivo | Qué |
|---|---|
| `Tools/export_npc_spells.py` (nuevo) | `npcs.dat` → `Assets/Resources/AOMigrator/MagicV129/npc_spells.json` (98 NPC). `--check` compara. |
| `Runtime/Shared/AONpcSpellRulesV902.cs` (nuevo) | Reglas puras, compartidas por Unity y el servidor. |
| `Runtime/AONPCSpellCasterV902.cs` (nuevo) | Sin conexión: el turno mágico del NPC, con la animación de lanzamiento (`AOSpellFXV120.PlayFromNpc`) y el daño. |
| `Runtime/AONPCMovementV08.cs` | Enganche en `ThinkHostileAgainstPlayer`: primero `TryCast` y, si no lanzó, el melee de siempre. |
| `Runtime/AOPlayerMagicV120.cs` | `ApplyNpcSpell` y el parámetro `skipHp` en `ApplyToPlayer`, para no repetir el daño. |
| `Runtime/AOOnlineClientV240.cs` | Evento `hurt` con `spell` (y `npc` = id de red del NPC): muestra el hechizo y aplica los otros efectos. Los clientes viejos solo ven el daño. |
| `OnlineServer/CoopRoom.NpcMagic.cs` (nuevo) | La sala decide el hechizo, tira el daño y manda el `hurt`. |
| `OnlineServer/CoopRoom.cs` | Enganche en `TickMap` y el campo `nextCast`. |
| `Tools/export_online_catalog.py` | El catálogo lleva `npcSpells`. El gzip queda con el byte de sistema fijo (Windows), así la PC y la nube dan los mismos bytes. |
| `Tools/test_npc_magic.py` (nuevo) | Prueba de la sala; está en `.github/workflows/pruebas.yml`. |

## Verificado en la nube
- El servidor compila con las reglas compartidas.
- `test_npc_magic` pasa, igual que las otras 6 pruebas del servidor y `ci_checks`.
- El C# de Unity se revisó solo por sintaxis (Roslyn, C# 9), con las APIs chequeadas a mano. **Falta compilar en Unity.**

## Probar en Unity (QA, con `aod-respaldo` antes)
- **P3 Mausoleo:** el Esqueleto Mágico tira Flecha Mágica cada 2 s, a distancia.
- **P6:** el Mago Malvado paraliza y tira Tormenta de Fuego; la Medusa, Descarga Eléctrica o Inmovilizar.
- **P7:** los dragones tiran Tormenta de Fuego cada 8 s; Vytaiz, Paralizar o Incinerar cada 1,5 s.
- Sin conexión y en la sala: que se vean la animación de lanzamiento, el daño y el mensaje, y que la parálisis frene al personaje.

**Aviso de balance:** no cambia stats, pero el dungeon se va a sentir más difícil, porque ahora pega la magia original. Contenido tiene que revisar los tiempos de `modelo_progresion.py`: el modelo actual no cuenta el daño mágico que recibe el jugador.
