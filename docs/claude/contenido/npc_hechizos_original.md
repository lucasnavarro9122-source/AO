# Hechizos de NPC en el original (AO20)

Contenido · 25/09/2026. Datos completos por NPC y por hechizo: `npc_hechizos_original.json` (misma carpeta).

**Fuente** (clon público, no está en el repo):
- Servidor `ao-org/argentum-online-server` commit `08a5711bacf7a2dccc75a8633b942146b47fabcb` (24/09/2026). Las rutas `Archivo.bas:línea` son relativas a `Codigo/`.
- Datos `ao-org/Recursos` commit `552d4ed940a67497f2ca9ba357d1dd6d5b5fd348` (16/08/2026): `Dat/npcs.dat`, `Dat/Hechizos.dat`.
- Config: solo existen los `Example.*.ini` públicos. Los valores reales de producción pueden ser otros.

**Numeración:** es la misma que la del proyecto. Los 450 NPC del catálogo (`OnlineServer/Data/catalog.json.gz`) y los 141 hechizos de `Assets/Resources/AOMigrator/MagicV129/spells.json` coinciden 100 % en id y nombre con este commit. Stats: una sola diferencia (NPC 262 Sacerdote Imperial, MaxHP 9550 en el proyecto y 10000 en el clon).

## Campos de `npcs.dat` (lectura: `MODULO_NPCs.bas`, sub de carga ~1030-1240)
| Campo | Qué hace | Dónde |
|---|---|---|
| `LanzaSpells` | cantidad de hechizos (slots) | `MODULO_NPCs.bas:1147` |
| `Sp1..SpN` | id de `Hechizos.dat` por slot (solo se leen N) | `:1152` |
| `Cd1..CdN` | cooldown en **segundos** por slot. Solo lo usa la IA Support/BG | `:1153`, `AI_NPC.bas:1443` |
| `RangoSpell` | rango euclídeo (`<`). Solo lo usa la IA Support/BG | `:1150` |
| `IntervaloLanzarHechizo` | ms entre hechizos; si falta = **8000** | `:1094`, `:1506-1508` |
| `IntervaloAtaque` | ms entre golpes; si falta = 2000 | `:1509-1511` |
| `CastAnimation` | cuerpo de la animación de casteo (`DoAnimation`) | `:1050`, `modHechizos.bas:229` |
| `DisplayCastMessage` | muestra las palabras mágicas sobre la cabeza | `:1181`, `modHechizos.bas:236` |
| `DontHitVisiblePlayers` | el caster no pega melee a quien ve | `:1179` |
| `RestriccionDeAtaque` / `RestriccionDeAyuda` | máscara de a quién ataca/ayuda (0=usuarios+NPC, 1=usuarios, 2=NPC / 1=NPC, 2=usuarios, 3=ambos) | `:1176-1177`, `:1593-1611` |
| `Alineacion` | 1 Real: solo ataca Criminal/Caos/Concilio; 2 Caos: solo ataca Ciudadano/Armada/Consejo | `:1132`, `AI_NPC.bas:1385-1392` |
| `MagicBonus` | daño × (1+MagicBonus) | `:1131`, `modHechizos.bas:65` |
| `CantidadInvocaciones` | tope de criaturas que puede invocar | `:1130` |
| `Movement` | tipo de IA (`e_TipoAI`, `Declares.bas:3424`) | `:1039` |
| `Nivel` | filtro de nivel (ver abajo) | `:1038` |

**Maná de NPC: no existe.** `t_NPCStats` no tiene maná (`Declares.bas:3126`) y `NpcLanzaSpellSobreUser` no descuenta nada (`modHechizos.bas:31`). Lo único que limita es `IntervaloLanzarHechizo` (y `CdN` en la IA Support).

## Dos IAs de casteo
### 1. Clásica (casi todos: `Movement` 2 MueveAlAzar, 3 FixedInPos, 4 NpcDefensa, BG tanque/jefe)
- Ciclo: `NpcAI` (`AI_NPC.bas:56`) se ejecuta cada `IntervaloNpcAI`=150 ms (`intervalos.ini:19`), pero solo si al NPC le toca moverse (`IntervaloPermiteMoverse`, `modNpcAiLoop.bas:74`; 380 ms si no define `IntervaloMovimiento`, `MODULO_NPCs.bas:1500-1502`).
- **Objetivo** (`PerseguirUsuarioCercano`, `AI_NPC.bas:123-217`): siempre un jugador (`TargetUser`). Prioridad: enemigo justo al frente, luego el enemigo atacable más cercano (con magia o melee) y luego el enemigo visible más cercano. Si el NPC era pasivo y le pegaron, solo persigue al agresor (`NPCAtacado` le pone `Hostile=1`, `Modulo_UsUaRiOs.bas:1628-1640`).
  - Visión: 15×13 tiles por eje (`AI_NPC.bas:34-35`, `Consts.bas:31-32`; `Distancia` la pisa, pero ningún NPC la define) (`EnRangoVision`, `:1399`).
  - Filtro de nivel: si el NPC tiene `Nivel`>0, ignora a jugadores con ELV−Nivel>4, salvo que estén a ≤3 tiles o sean su agresor (`EsObjetivoValido`, `:1353-1369`; `NpcDeltaLevelPenalties=4`, `NpcDeltaLevelCloseRange=3` en `Example.Configuracion.ini:38-39`).
- **Cuándo castea vs. pega** (`AI_AtacarUsuarioObjetivo`, `:708-768`):
  - `AtacaConMagia = LanzaSpells>0 y IntervaloPermiteLanzarHechizo` (`:721`). Tiene prioridad (`If/ElseIf`).
  - Melee solo si está pegado (distancia 1) y (el objetivo está invisible/oculto, o el toggle `Magic_and_Punch`=1 y el NPC no tiene `DontHitVisiblePlayers`) (`:722-726`). El toggle está en 1 en `Example.feature_toggle.ini:6-8`. Un NPC sin hechizos siempre pega.
  - `AttackFromPos` (FixedInPos, `:219-249`): si tiene hechizos, **nunca** pega (`:235-240`).
- **Rango del hechizo**: |dx|≤11 y |dy|≤9 respecto del objetivo (`NpcLanzaUnSpell`, `:1297-1298`; `NPCSpellRangeX/Y`, `Example.Configuracion.ini:14-15`). `RangoSpell` **no** se usa acá.
- **Qué hechizo**: sorteo uniforme entre los slots (`RandomNumber(1, LanzaSpells)`, `:1299`). Los `Sp` repetidos pesan más: el Dragón Negro Legendario tiene 215×5.
- **A quién**, según el `Target` del hechizo (`:1301-1330`):
  - 1 usuarios: al objetivo.
  - 2 NPC: a sí mismo si `AutoLanzar=1` (p. ej. 203 Remover Parálisis (NPCS)).
  - 3 usuarios y NPC: a sí mismo si `AutoLanzar=1`; si no, al jugador.
  - 4 terreno: área (`NpcLanzaSpellSobreArea`, `modHechizos.bas:348-425`). Centro = objetivo ±2 tiles al azar; cuadrado de `AreaRadio`×`AreaRadio` (la mayoría: 22). Pega también a invisibles, pero no a muertos ni a GM. Si `Invoca=1`, invoca hasta `CantidadInvocaciones`.
- **No castea a**: muertos, invisibles, ocultos, con inmunidad, GM o en consulta (`UsuarioAtacableConMagia`, `:1416-1426`; `modHechizos.bas:44`).
- **NPC paralizado**: no daña a jugadores (`:1300`). **Inmovilizado**: solo castea si el objetivo está en línea recta adonde mira (`NpcLanzaSpellInmovilizado`, `:628-659`).
- **Daño**: `rand(MinHP,MaxHP) × (1+MagicBonus)`, menos el % de RM del jugador (salvo `AntiRm=1`), × modificadores; mínimo 0 (`modHechizos.bas:62-87`; RM en `Modulo_UsUaRiOs.bas:3323`).
- Detalle: `IntervaloPermiteLanzarHechizo` reinicia el contador al consultarse, aunque el hechizo no salga (fuera de rango, `Sp`=0, objetivo invisible) (`modNuevoTimer.bas:253-258`).

### 2. Support / BG (`Movement` 11 SupportAndAttack, 13 BGSupportBehavior)
`AI_SupportAndAttackNpc` / `AI_BGSupportBehavior` → `TrySupportThenAttackSpells` (`AI_NPC.bas:846-927`, `1443-1687`):
1. **Ayuda** (según `RestriccionDeAyuda`): primero sacar parálisis y después curar, a jugadores/NPC aliados a distancia euclídea < `RangoSpell` (`SelectSupportSpellAndTarget`, `:1459`).
2. Si no ayudó, **ataca**: primero paraliza/inmoviliza y después hace daño (`SelectAttackSpellAndTarget`, `:1550`).
   - Objetivo: su agresor si sigue a la vista; si no, el **primer** jugador de la lista del mapa (no el más cercano) que vea, pueda atacar y esté a < `RangoSpell`.
- Cada slot respeta `CdN` (s). Sin `Cd` = siempre disponible. El `IntervaloLanzarHechizo` global también aplica.
- Estado provocado (`eTaunted`): salta la ayuda y ataca (`:1678-1681`).

### 3. Guardias contra NPC hostiles (fijo en el código)
`TryGuardAttackHostileNpc` (`AI_NPC.bas:777-808`), npcType 2/8 (Guardia Real/Caos): cada 1500 ms, a ≤8 tiles, tira 25 Paralizar (si puede) + 204 "Ira de ReyarB" (`:37-40`). No usa sus `Sp`.

`NpcLanzaUnSpellSobreNpc` (`:1336`) es código muerto (nadie la llama).

## Resumen de datos (`npcs.dat`)
- 98 NPC con `LanzaSpells>0`, 48 hechizos distintos. 66 de esos NPC aparecen en mapas del proyecto; ninguno en el mapa 1 (Ullathorpe). Los más cercanos: Kobold 594 (mapas 16, 21) y Dovu 1363 (15, 21).
- IA: 74 MueveAlAzar hostiles, 14 FixedInPos hostiles; el resto, casos especiales.
- Intervalos más comunes: 8000 (18 NPC, casi todos por defecto), 2000, 5000, 1500, 4000. El más rápido: Devorador del Inframundo 571, 800 ms.
- Rarezas del original (en `notas` del JSON):
  - 670 y 1356 tienen un `Sp`=0. 1265 tiene 4 `Sp`=0.
  - 269 y 270 tienen 9 "Invocar Mascotas" sin `CantidadInvocaciones`: nunca invocan (`modHechizos.bas:388-391`).
  - 631 Kraken Gigante Body es DummyTarget: no tiene IA.
  - 498/499 son Estáticos: no castean. 269 usa la IA GuardiaPersigueNpc (solo melee contra NPC): no usa sus `Sp`.

### Casters en mapas del proyecto (`*` = intervalo por defecto)
| NPC | Nombre | Sp (×repetidos) | ms | IA | Mapas |
|---|---|---|---|---|---|
| 262 | Sacerdote Imperial | 53 | 8000* | Azar (guardia) | 29,31,59,60… |
| 269 | Guardia de entradas | 9 25 | 8000* | GuardiaNpc pasivo | 37,40,45,310… |
| 270 | Mago del Caos | 9 25 | 8000* | Azar | 195,197,286,295 |
| 513 | Hechicera | 25 41 | 1500 | Azar | 311,313 |
| 526 | Filibustero Viejo | 51 | 4000 | Azar | 100,441 |
| 528 | Hechicera Maldita | 25 53 | 1400 | Azar | 313 |
| 529 | Chamán Ártico | 25 51 | 2500 | Azar | 309,310 |
| 533 | Mago Malvado | 41 25 | 4000 | Azar | 111,113,114,146 |
| 534 | Bruja | 25 41 | 4000 | Azar | 111,140,144,145 |
| 538 | Euríale | 25 51 | 8000* | Azar | 314 |
| 540 | Medusa | 51 26 | 7000 | Azar | 48,140,141,142… |
| 544 | Vytaiz | 25 206 | 1500 | Azar | 314 |
| 549 | Gran Dragón Rojo | 4 | 3000 | Azar | 391 |
| 555 | Orco Brujo | 43 | 2000 | Azar | 111,113,114,116… |
| 556 | Fuego Fatuo | 51 | 4500 | Azar pasivo | 110,112 |
| 564 | Capitan Wynne | 256 | 8000* | Azar | 311,312 |
| 571 | Devorador del Inframundo | 213 214 215 216 217 218 219 228 | 800 | Azar | 368 |
| 575 | Azhran | 25 131 | 7000 | Azar | 48 |
| 586 | Galeón Fantasmal | 256 | 5000 | Azar | 385 |
| 587 | Pequeño Dragón Rojo | 41 | 8000* | Azar | 115,391 |
| 588 | Pirata Jack | 53 | 5000 | Azar | 201,284 |
| 590 | Dama Oscura | 25 51 | 1500 | Azar | 313 |
| 594 | Kobold | 43 | 3000 | Azar | 16,21,203 |
| 595 | Pequeño Dragón Azul | 41 | 8000* | Azar | 391 |
| 606 | Araña Roja | 210 | 5000 | Azar | 327,334,382 |
| 607 | Araña Verde | 211 | 4000 | Azar | 321,325,328,329… |
| 609 | Lagarto | 205 | 3500 | Azar | 321,325,327,328… |
| 631 | Kraken Gigante Body | 32 26 100 | 5000 | Fijo (Dummy, no castea) | 184 |
| 632-639 | Kraken Gigante 2/3/5/6/7/9/11/12 | 32 26 100 | 5000 | Fijo | 184 |
| 640 | Dragón Negro Legendario | 215×5 15×3 218 216 217×5 293×3 213×2 | 1500 | Fijo | 377 |
| 641 | Araña New | 4 | 1500 | Fijo | 184,323 |
| 670 | Scramer Anciano | 25 0 | 3000 | Azar | 274 |
| 675 | Líder de la Manada | 247 | 8000* | Azar | 819 |
| 750 | Frogian Azulino | 205 | 1000 | Azar | 199 |
| 751 | Frogian Hechicero | 41 25 | 1000 | Azar | 199 |
| 754 | Espíritu Poseído | 51 | 8000* | Support | 594,595 |
| 922/923/924 | Dragón Dorado / Cárdeno / Rojo | 4 | 1500 | Azar | 377 |
| 926 | Hada Oscura | 25 | 8000* | Azar | 111,113 |
| 949 | Eztbron Vientogélido | 213 216×2 218 219 217×4 214×4 215×3 | 1900 | Azar | 307 |
| 992 | Kraken Gigante | 215×2 214×2 217×3 213 218 | 1700 | Azar | 285 |
| 994 | Alma en Llamas | 53 | 2000 | Azar | 366 |
| 1002 | Esqueleto Mágico | 2 | 2000 | Azar | 392 |
| 1011 | Asesina Élfica | 25 51 | 2500 | Azar | 236,237 |
| 1016 | Aksha | 25 51 | 2500 | Azar | 303,304 |
| 1020 | Banshee | 55 | 2000 | Azar | 366 |
| 1024 | Jinete Blanco | 53 | 2000 | Azar | 367 |
| 1043 | Reina Gorgona | 213 214×2 215 216 217×2 218 219 232 | 1300 | Azar | 315 |
| 1044 | Titan Lugubre | 213×2 216×2 218×2 219×2 217×4 214×4 215×4 | 1900 | Azar | 173 |
| 1056 | Har Nareth | 215×2 214×2 217×3 213 218 | 1700 | Azar | 334 |
| 1153 | Ghanly | 25 41 | 3000 | Azar | 434,457 |
| 1235 | Asesino Impieadoso | 203 236 53 25 | 2000 | Azar | 228 |
| 1308 / 1310 | Bisporus / Comatus | 267 | 3500 | Fijo | 184 (1310 también 595) |
| 1309 | Niscalo | 267 | 3500 | Fijo pasivo | 184,595 |
| 1354 | Eyewarden Ártico | 26 | 8000* | Azar | 837 |
| 1363 | Dovu | 267 | 8000* | Fijo | 15,21,101,202 |
| 1385 | Pirata Cañonero | 256 | 8000* | Azar | 100 |

Faltan en los mapas del proyecto 32 casters (casi todos de eventos o invocaciones): 30, 276, 400, 498, 499, 572, 591, 592, 644, 655, 719, 723, 724, 925, 947, 962-964, 1025, 1026, 1051, 1057, 1220, 1226-1228, 1265, 1312, 1325, 1326, 1356 y 1372.

## Comparación con el proyecto
**Hoy ningún NPC lanza hechizos, ni en local ni online.**
- `AONPCMagicDatabaseV129.cs` carga `Resources/AOMigrator/MagicV129/npc_magic.json`, pero solo trae la **defensa** mágica del NPC (`npcIndex`, `magicResistance`, `magicDef`, `immuneToSpells`, `paralysisImmune`), que se usa cuando el jugador le tira un hechizo. No tiene `LanzaSpells`, `Sp`, `Cd`, `RangoSpell`, `IntervaloLanzarHechizo`, `MagicBonus` ni `DisplayCastMessage`.
- IA local (`AONPCCombatV09.cs`, `AONPCMovementV08.cs`) y del servidor (`OnlineServer/CoopRoom.cs` ~405-427): solo melee (`attackRange`, `attackIntervalMs`, tirada de poder de ataque). Grep de `lanzaspells|npcSpell|spellRange|IntervaloLanzarHechizo` en Runtime, Resources y OnlineServer: solo aparece el casteo del **jugador** sobre NPC (`AOOnlineClientV240.CanCastNpcSpell`).
- Lo que ya está y serviría:
  - **Hechizos**: los 48 que usan los NPC ya existen en `MagicV129/spells.json` y en el catálogo del servidor, con los mismos ids (con `target`, `autoCast`, `minHp/maxHp`, `areaRadius`, `areaAffects`, `summonNpc`, `eotId`, `antiRm`, `fxGrh`, `wav`).
  - **NPC**: 66 de los 98 casters están ubicados en mapas del catálogo.
  - **Animación**: `StreamingAssets/AOMigrator/CastV268/cast_animations.json` tiene los 15 NPC con `CastAnimation` (castBody = `CastAnimation` original). 8 de ellos castean: 640, 641, 655, 754, 1308, 1309, 1310 y 1326. `AOCastAnimationRuntimeV268.PlayNpc` existe, pero solo lo llama la prueba `AOModulesQA270.cs:411` (`PlayNearestCaster` no se usa).
- Lo que falta (nada existe):
  - La lista de hechizos por NPC (`Sp`, `Cd`), `IntervaloLanzarHechizo` (y su default de 8000), el rango 11×9, `RangoSpell` y la regla de sorteo por slot.
  - La prioridad magia > melee con `Magic_and_Punch`/`DontHitVisiblePlayers`, las reglas de objetivo (el agresor primero, filtro de nivel, alineación, invisibles), `AutoLanzar` a sí mismo, las áreas centradas ±2 y las invocaciones con tope.
  - La IA Support (curar/sacar parálisis a aliados), los guardias que tiran 25+204 a NPC hostiles y `MagicBonus`.
- Parámetros de NPC que el proyecto lee mal (`Tools/map_migration.py:185-193`, `npc_entry`):
  - `visionrange` no existe en `npcs.dat`: todos quedan con 8 (el original ve 15×13).
  - `preferredrange`: el original escribe `PreferedRange`, así que queda en 0 en los 5 NPC que lo tienen.
  - `defensa`: el original usa `DEF`. 28 de los 450 NPC del catálogo quedan con `defense` 0 contra el `DEF` original (p. ej. 549 Gran Dragón Rojo: 0 contra 400; 260/261 Guardia Imperial: 0 contra 40).
  - Los valores que están bien probablemente vienen de las plantillas: en el mapa 1 coinciden todos.
