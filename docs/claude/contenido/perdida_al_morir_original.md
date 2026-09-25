# Pérdida al morir en el original (AO20)

Contenido · 25/09/2026. Solo documenta; no propone cambios de balance.

**Fuente** (clon público, no está en el repo):
- Servidor `ao-org/argentum-online-server` commit `08a5711bacf7a2dccc75a8633b942146b47fabcb` (24/09/2026). Las rutas `Archivo.bas:línea` son relativas a `Codigo/`.
- Datos `ao-org/Recursos` commit `552d4ed940a67497f2ca9ba357d1dd6d5b5fd348` (16/08/2026).
- Config: solo existen los `Example.*.ini` públicos. Los valores reales de producción pueden ser otros.

## Flujo
`UserDie` (`Modulo_UsUaRiOs.bas:1749`) → `TirarTodosLosItems` (`InvUsuario.bas:2922`), que tira primero el oro y después cada slot del inventario.

## Reglas
| # | Regla | Dónde |
|---|---|---|
| 1 | **Se cae algo solo si** el tile donde muere **no** es `ZONAPELEA` (trigger 6 = arena) **y** el mapa tiene `DropItems` activo | `Modulo_UsUaRiOs.bas:1778`; `Declares.bas:1135` |
| 2 | Solo pierden cosas los usuarios comunes. Los GM/consejeros no tiran nada | `:1779` |
| 3 | **Pendiente del Sacrificio** (amuleto `EfectoMagico=15`): si lo lleva equipado, no se cae nada y se consume el pendiente. **Hoy no hay ningún objeto con `EfectoMagico=15` en `obj.dat`**, así que la regla no hace nada | `:1780-1787`; `InvUsuario.bas:1351-1352`; `FileIO.bas:1245-1246` |
| 4 | **MapasNoDrop**: se leen de `MapasEspeciales.dat [MapasNoDrop]` y ponen `DropItems=False`. **El `MapasEspeciales.dat` actual no tiene esa sección**: se cae en todos los mapas | `GameLogic.bas:1417-1451` (`CargarMapasEspeciales`), `:1464` (`EsMapaNoDrop`); `FileIO.bas:1816-1819` |
| 5 | Otras formas de no perder nada: `DropItems` en False en los escenarios (DeathMatch, HuntNpc) o con el comando de GM `/MODMAPINFO`; mapas 58-61 durante un evento faccionario | `ScenarioDeathMatch.cls:622-627`; `ScenarioHuntNpc.cls:85-87`; `Protocol_GmCommands.bas:3055-3056`; `InvUsuario.bas:2929` |
| 6 | La **zona segura** (trigger 4) y los mapas `Seguro` **no** protegen el drop (el código no los mira); solo impiden el PvP | `Modulo_UsUaRiOs.bas:1778` |
| 7 | **Oro**: se cae `GLD − OroPorNivelBilletera × ELV`. La billetera protegida es de 1000 × nivel en el ejemplo. Los GM no tiran oro | `InvUsuario.bas:2930-2935`; `ServerConfig.cls:88`; `Example.Configuracion.ini:28` |
| 8 | El oro cae en pilas de hasta `MaxInventoryObjs` (10000) por tile. Si no encuentra tile, ese oro **se queda** con el jugador. Al pirata no le cae al agua; al resto sí puede | `TirarOro`, `InvUsuario.bas:248-308` (`:270`, `:279-285`); `Example.Configuracion.ini:30` |
| 9 | **Ítems**: recorre todos los slots y tira **la pila entera** de cada ítem que cumpla `ItemSeCae` y `PirataCaeItem`, salvo que sea un ítem newbie protegido | `InvUsuario.bas:2937-2960` |
| 10 | **Nunca se caen** (`ItemSeCae`): `NoSeCae=1`, `Intirable=1`, `Destruye=1`, `Instransferible=1` o tipo llave (9), barco (31) o montura (44). `Real`/`Caos` se evalúan pero no cambian nada: decide `NoSeCae` | `InvUsuario.bas:2891-2896`; carga en `FileIO.bas:1074-1078, 1444` |
| 11 | En `obj.dat`: 571 con `NoSeCae`, 436 `Instransferible`, 417 `Intirable` y 18 `Destruye`. En total, **1128 de 4275 objetos nunca se caen** (470 son llaves). De los 228 objetos de facción (`Real`/`Caos`), 213 no se caen por sus flags | `Recursos/Dat/obj.dat` |
| 12 | **Newbie**: nivel ≤ 12 (`LimiteNewbie`). Solo protege los objetos con `Newbie=1`. El newbie **igual pierde** el oro sobre la billetera y todo lo que no sea newbie | `InvUsuario.bas:2999-3009`; `GameLogic.bas:123-129`; `Declares.bas:382` |
| 13 | Hay 54 objetos `Newbie=1`: 47 además son intransferibles (nunca caen) y 7 se protegen solo hasta nivel 12: 3490, 3493, 3496, 3497, 3498, 3684 y 3685 | `obj.dat` |
| 14 | **Pirata**: nivel ≥ 37, navegando con Galeón: cada ítem tiene 33 % de chance de no caerse | `PirataCaeItem`, `InvUsuario.bas:2901-2920` |
| 15 | **Carros** (amuleto `EfectoMagico=12`, obj 3730-3738): protegen un % de minerales, maderas, blodium y peces según `LingO/LingP/LingH/Madera/MaderaElfica/MaderaPino/Blodium/Peces` | `DropAmmount`, `InvUsuario.bas:2967-2997` |
| 16 | **Facción o criminal**: el drop no cambia por facción ni por estado. La facción solo cambia los puntos del asesino (`ContarMuerte`) | `Modulo_UsUaRiOs.bas:1911` |
| 17 | **Equipado**: se trata igual que el resto del inventario. Si cae, `QuitarUserInvItem` lo desequipa. Después `UserDie` desequipa todo lo que quedó (arma, armadura, escudo, casco, anillos, herramienta, montura, munición, amuleto) y te baja de la montura | `InvUsuario.bas:314`; `Modulo_UsUaRiOs.bas:1790-1803` |
| 18 | **Dónde cae** (`Tilelibre`): busca en cuadrados de radio 0 a 15 alrededor del muerto, fila por fila desde arriba a la izquierda. Su propio tile está ocupado por él, así que suele empezar en (x−1, y−1). Salta los tiles con NPC o usuario, con otro objeto, con otros `ElementalTags`, los que pasarían de 10000 unidades y las salidas de mapa. El mismo objeto se apila: **un objeto por tile**. El oro se tira primero y queda más cerca | `Modulo_UsUaRiOs.bas:2080-2127`; `LegalPos` `GameLogic.bas:776-816` |
| 19 | A pie: solo tierra, con respaldo `ClosestLegalPos` (radio 12). Navegando: agua o tierra. **Si no hay lugar, el ítem se borra** ("nada de mochilas gratis") | `InvUsuario.bas:2945-2956`; `GameLogic.bas:577-612` |
| 20 | **Cuánto dura en el piso: sin límite.** No hay limpieza automática: `MakeObj` ignora su parámetro `Limpiar`, `TLimpiezaItem.cls` no se usa, `/LIMPIAR` solo muestra el mensaje (igual en v4.3.4, 07/2022), `NoSeLimpia` se lee pero no se usa y `[MapasIgnoranLimpieza]` no se lee. Queda hasta que alguien lo levante o se reinicie el servidor (`WorldSave` ya no graba objetos del mapa) | `InvUsuario.bas:435`; `Protocol_GmCommands.bas:1986-2000`; `FileIO.bas:1332`; `Admin.bas:181-203` |
| 21 | **Otras pérdidas al morir**: vida, stamina y escudo en 0; se borran efectos en el tiempo, modificadores y estados; vuelven los atributos de pociones; termina el mimetismo. Las mascotas mueren, salvo las domadas del druida con instrumento mágico de madera élfica. Cuerpo 829. **No se pierde experiencia ni nivel** | `Modulo_UsUaRiOs.bas:1754-1777, 1813-1830`; `HandleUserPetsOnDeath` `:3387`; `Declares.bas:1422` |

Duelos (`ModRetos`): si el reto es "por los ítems", `TirarItemsEnPos` tira con las mismas reglas 10, 12 y 14 en otra posición (`ModRetos.bas:617-653`).

## Estado en el proyecto (grep, 25/09/2026, código sin commit)
Qué hace hoy:
- `AOPlayerCombatV09.DeathRoutine` (`:783-839`) llama a `AODeathRespawnV160.OnDeathStarted` (`:146-178`):
  - cancela `/HOGAR` y cierra la UI de ciudad y de quests;
  - `AOPlayerRPGV11.OnPlayerDeath` (`:1041`): stamina en 0 y vuelven los atributos base;
  - `AOInventoryV10.UnequipAllForDeath` (`:593-603`): pone en 0 arma, armadura, escudo, casco, amuleto y accesorio mágico. Los objetos siguen en el inventario;
  - `magic.ResetRuntimeForLoad`.
- `EnterGhostState`: cuerpo 829 (`DEAD_BODY_ID`, `AODeathRespawnV160.cs:6`, igual al `iCuerpoMuerto` original). El fantasma queda donde murió y vuelve con sacerdote o `/HOGAR`.
- Online: `OnlineServer/CoopRoom.cs:424` solo marca `dead`. El servidor no tira ni descuenta nada.

Diferencias con el original:
1. **No se cae nada**: ni ítems ni oro. Nadie llama a `AOLootPickupV09.Create` cuando muere el jugador; solo lo llaman la muerte de un NPC (`AONPCCombatV09.cs:463/519/610/654`), el hechizo de materializar y los drops online. El oro (`AOPlayerCombatV09.gold`, `:29`) no se toca: no hay billetera por nivel.
2. **No hay reglas de zona**: la grilla (`AOGridMap.cs:23-30`) no usa el trigger 6 `ZONAPELEA` ni el 4, y no existe `DropItems`/MapasNoDrop. No aplica mientras no haya drop.
3. **Flags de objeto**: `AOItemDatabaseV10.ItemDef` tiene `newbie` (`:53`), `untransferable` (`:102`) y `destroyOnSell` (`:103`), pero **no** `NoSeCae` ni `Intirable`. Tampoco hay `EsNewbie` (nivel ≤ 12) ni reglas de pirata o de carros.
4. **Equipado**: igual que el original, el fantasma queda sin nada equipado. La diferencia es que en el original lo equipado que puede caerse se cae.
5. **Piso** (`AOLootPickupV09`): apila el mismo ítem en el mismo tile (`:67-89`) pero **permite ítems distintos en un mismo tile** (el original, uno por tile y búsqueda en espiral). Sin vencimiento, igual que el original. **No se guarda** en la partida (`AOSaveGameV140` no lo menciona), así que desaparece al recargar. En el original desaparece al reiniciar el servidor.
6. Mascotas, efectos y experiencia: sin cambios respecto del original en lo que se pierde (no se pierde experiencia). No verifiqué mascotas ni efectos del proyecto en detalle.
