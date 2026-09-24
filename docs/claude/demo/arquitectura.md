# Demo AO BATTLESERVER: arquitectura (Programación)

Sector: Programación · 24/09 · **Fase 1: solo diseño. No hay nada implementado ni se tocó `Assets/`.**
Coordinado con `red.md` (rev. 2 + §12 bis, Servidor: aceptó los 7 Δ), `pruebas.md` (QA) y `ui.md` (Interfaz). Donde hay diferencias, están marcadas con **Δ** y se avisaron.

## Resumen
- **Mapas de demo** = archivos nuevos `map_{ID}.json` con IDs de **1000 a 1999**, de **100×100 como máximo** (límite de la sala online). Se arman con una herramienta nueva, `Tools/demo_map_builder.py`, que **compone recortes de mapas originales** y **nunca escribe IDs < 1000**. Los 842 originales no se tocan.
- **Zona de arenas** (mapa 1001): parte del mapa original 324 ("Zona de Ring", 4 rings de 23×19 ya cerrados). Se le abren pasillos y gradas alrededor de cada ring para los espectadores.
- **Generador de arena**: C# puro y compartido (cliente y servidor), sin `UnityEngine`, sin `System.Random` y sin float, con PRNG SplitMix64 y `GenVersion`.
  - Es simétrico (rotación 180° o espejo) y valida caminos al colocar cada obstáculo.
  - Tiene reintentos acotados y un layout de respaldo que **siempre** es válido.
- **PvP**: solo entre rivales del mismo reto, dentro de su ring y en la fase Pelea. **El servidor decide todo** (`red.md` §4).
  - El cliente muestra, pide y bloquea de entrada lo que el servidor rechazaría.
  - En el reto no hay daño local, ni fantasma, ni se desequipa al morir.

## 1. Hechos del código actual (base del diseño)
| Tema | Estado hoy |
|---|---|
| Carga de mapas | `Resources.Load("AOMigrator/WorldV07/Maps/map_"+id)`. Sin listado fijo ni tope de ID (`AOWorldManagerV07.cs:11-12, 547-676`). Las salidas cargan `destMap` si el archivo existe (`:509-545, 1707-1722`). |
| Formato | `{mapNumber,mapName,zone,terrain,safe,xmin..ymax,cells[],sprites[],blocks[],triggers[],exits[],npcs[],objects[],lights[],particles[]}`, escrito por `Tools/map_migration.py` (`world_json`, `npc_entry`, `atomic_json`). |
| Catálogos aparte | Música `AudioV190/map_music.json`, luz y clima `WorldV07/map_environment.json`, minimapa `MinimapsV0103/map_{id}.png`. Si falta alguno hay valor por defecto: el mapa no se rompe. |
| Grilla | `AOGridMap` hasta 1000×1000, pero el servidor usa un array fijo `y*101+x` (`CoopRoom.cs:457, 498-506`) → **coordenadas de 1 a 100**. |
| Triggers | Los datos traen `4` = zona segura y `6` = zona de pelea del original, pero **ningún script los lee**. Ojo: el código de techos toma el trigger 4 como techo (`AOWorldManagerRoofV210.cs:11-13`). |
| Mapa 324 | 100×100, 8233 casillas bloqueadas, trigger 6 en 1767 casillas, sin NPC, objetos ni salidas. Interiores 23×19 en (13,11), (65,11), (13,73), (66,73) según `Retos.dat`. Borde del ring cerrado y sin obstáculos adentro. |
| Servidor | `catalog.json.gz` sale de `export_online_catalog.py`, que recorre todos los `map_*.json`: un mapa nuevo entra solo con volver a correrlo. El ID de cada NPC es su **posición en el array** (`CoopRoom.cs:470-487`); si cambia la lista de un mapa ya guardado en `world.json`, los IDs quedan desfasados. |
| PvP | No existe. Los avatares remotos son solo `AOCharacterRenderer` (`AOOnlineClientV240.cs:417-421`). `CastAlly` y `CoopRoom.Cast` rechazan hechizos dañinos. |
| Muerte | `DeathRoutine` → `UnequipAllForDeath` + fantasma + `/HOGAR` (mapa 1). No se sueltan ítems (`AODeathRespawnV160.cs:146-178`). |
| Respawn NPC | Por NPC (`respawnMin/MaxSeconds`), en el cliente y en el servidor (`CoopRoom.cs:296, 398`). |
| Código compartido | `OnlineServer/*.csproj` ya enlaza `Runtime/AOCoopProtocolV250.cs` con `Compile Include … Link`. Se usa el mismo mecanismo. |

## 2. Mapas nuevos de demo

### 2.1 IDs
| ID | Mapa | Base |
|---|---|---|
| 1000 | Hub de la demo: aparición, salida norte → Arenas y salida sur → Dungeon | Recorte de una plaza de ciudad original, más muros propios |
| 1001 | Zona de arenas: 4 rings, gradas y pasillos | Copia del mapa 324, abierta (§2.3) |
| 1002–1009 | Más zonas de arenas (reservado; ej. 1002 = copia de 372 con otros 4 temas) | 372/389/390 |
| 1010 | Entrada al dungeon | Recorte de un dungeon original |
| 1011–1019 | Pisos del dungeon por tramo de nivel (los define Contenido en `progresion.md`) | Recortes de dungeons originales |
| 1020–1999 | Libres | — |

Reglas: todo mapa de demo mide **100×100 o menos**, con coordenadas de 1 a 100. Toda salida tiene su vuelta (QA D-20). No se reutiliza un ID con otra lista de NPC sin subir `npcLayoutVersion` (§2.5).

### 2.2 Herramienta: `Tools/demo_map_builder.py` (nueva, de Programación)
Entrada: una especificación declarativa por mapa, en `Tools/demo_maps/{id}.json`, versionada en git. Salida: `map_{id}.json` más las entradas de los catálogos.

Pasos, todos deterministas (la misma especificación produce los mismos bytes):
1. **Base**: `copy` de un mapa original, o `fill` con un tile de piso y bloqueo total.
2. **`stamp`**: copia un rectángulo de un mapa original (cells de las 4 capas, blocks, triggers, objects, lights, particles) a una posición destino.
   - Remapea los `sprite` ids y une `sprites[]` sin duplicar por `(fileNum,sx,sy,w,h,frames)`.
   - Sirve para plazas, pasillos, escaleras y decoración: todo sale de gráficos originales, como pide Arte.
3. **`unblock` / `block` / `trigger`**: pintan rectángulos de flags o triggers. Así se abren las gradas, se cierran bordes y se marca el trigger 6 en los interiores de los rings.
4. **`exit`**: salidas `{x,y,destMap,destX,destY}`. El builder falla si falta la salida de vuelta.
5. **`npcs`**: toma `dungeon-npcs.json` (Contenido) y arma cada NPC con `map_migration.npc_entry`, es decir, con las stats originales de `npcs.dat`. **No cambia stats**; solo posición, cantidad y respawn.
6. **Validación** antes de escribir:
   - salidas caminables y con su vuelta;
   - spawns de NPC caminables y dentro de su zona;
   - BFS desde la aparición a todas las salidas;
   - nada fuera de 1..100.
7. **Escritura** con `atomic_json`.
   - Si el ID es **< 1000, aborta**.
   - Actualiza por merge (solo IDs ≥ 1000) `map_music.json` y `map_environment.json`.
   - Genera el minimapa (§2.4).
   - Recuerda correr `export_online_catalog.py`.
8. `--check`: regenera en memoria y compara con lo que hay en disco. Sirve para QA o CI, sin escribir nada.

### 2.3 Zona de arenas (1001)
- Parte de una **copia de 324**. Se desbloquea el espacio entre los rings: columnas 37–63, filas 31–71 y una franja de 3 casillas alrededor de cada ring. Ahí se estampa piso de plaza (recortes de ciudad) y queda la **grada**.
- **El borde de cada ring queda cerrado** (ALL_SIDES), sin puertas: al reto se entra y se sale con `warp` del servidor. Así se cumple G-07 y ningún espectador puede entrar caminando.
- Interior de cada ring: piso fijo según el **tema del ring**, trigger 6 y sin obstáculos en el JSON (los obstáculos los pone el generador en tiempo de juego, §3.7).
- Tema por ring (propuesta; se puede cambiar en la especificación): Sala 1 bosque, Sala 2 desierto, Sala 3 nieve, Sala 4 mazmorra. Pantano y ciudad van en 1002.
- Salida al hub en el centro sur. En la grada no hace falta trigger: fuera de un reto nadie puede dañar a nadie (§4.1). **No se usa el trigger 4** por el problema del techo.

### 2.4 Catálogos y herramientas que pisan datos
| Qué | Acción |
|---|---|
| `map_music.json` / `map_environment.json` | El builder los mergea. **Pedido a Contenido:** que `music_migration.py` y `map_environment_migration.py` conserven las entradas ≥ 1000; hoy las borran si se vuelven a correr. |
| Minimapa | El builder dibuja un PNG simple desde bloqueos, agua y piso. **Pedido a Arte:** si quiere uno fiel, un renderer desde el JSON. **Pedido a Arte/Contenido (dueño de `import_all_minimaps.py`):** que se salte los IDs ≥ 1000; hoy falla si no coinciden uno a uno con los BMP. |
| `door_catalog.json` | Solo si se estampan puertas (`objType` 6). Se copian las entradas por `objIndex`, que ya existen. |
| `catalog.json.gz` del servidor | Volver a correr `export_online_catalog.py` (Servidor). |
| `AOMapTourQA` | Opcional: sumar 1000, 1001 y 1010 a su lista (QA). |

### 2.5 Servidor y `world.json`
- **Δ Pedido a Servidor:** el builder escribe `npcLayoutVersion` (hash de la lista de NPC) en el JSON y el catálogo lo exporta. Si el servidor ve que no coincide con lo guardado en `world.json`, **reinicia los NPC de ese mapa** en vez de enlazarlos por índice. Evita el desfase de IDs al retocar un piso.

### 2.6 Entrada, hogar y guardados
- **Personajes de demo separados** (recomendado; decide Lucas, `ui.md` §1): se guardan en otra carpeta, `AO_BattleDemo/`.
  - Hoy el juego usa `AO_Demo/save_slot_1.json`; el nombre es engañoso pero **no se cambia**.
  - Así la demo **no puede tocar** los guardados reales.
- Aparición: hub 1000. Mientras se está en la demo, el hogar (`AODeathRespawnV160`, hoy fijo en mapa 1 (57,44)) y `ReturnHomeFromUI` apuntan al hub. Se hace configurable, sin tocar la lista de 6 ciudades de `AOHomeCityV200`.
- **Modo prueba** (pedido de Servidor y QA): `--ao-test-profile <carpeta>` en la línea de comandos → `AOSaveGameV140.RootOverride` usa esa carpeta para todos los guardados (partida, spellbook `ao_magic_v129_spellbook.json`, `.bak`), nunca LocalLow.
  - El prefijo de PlayerPrefs vive en archivos de Interfaz (`AOPlayerSettingsV230`) y Servidor (identidad online): pedido a cada uno.

## 3. Generador de arena

### 3.1 Contrato
Archivo nuevo `Assets/AOMigrator/Runtime/Shared/AOArenaGenV###.cs` (número al implementar). **C# puro**: sin `UnityEngine`, sin `System.Random`, sin float ni double, sin LINQ ni `Dictionary` con orden de iteración significativo. Solo `System`. El servidor lo enlaza como hoy enlaza `AOCoopProtocolV250`.

```csharp
public static class AOArenaGen {
  public const int GenVersion = 1, W = 23, H = 19;
  public enum Theme { Bosque, Desierto, Nieve, Mazmorra, Pantano, Ciudad }
  public enum Cell : byte { Floor, Solid, Water, Deco }  // Solid: no se camina y corta proyectiles · Water: no se camina y deja pasar proyectiles · Deco: se camina, solo visual
  public static ArenaLayout Generate(int seed, Theme theme);          // nunca lanza excepción ni se cuelga
}
public sealed class ArenaLayout {
  public int GenVersion, Seed, Attempt; public AOArenaGen.Theme Theme; public byte Symmetry; // 0 = rot180, 1 = espejo
  public byte[] Cells;          // W*H, índice y*W+x, (0,0) = esquina superior izquierda del interior
  public byte[] Variant;        // W*H, 0..255: el cliente usa palette[v % count] (visual, no afecta reglas)
  public int[] SpawnA, SpawnB;  // 5 pares (x,y) cada uno, en orden 1v1..5v5
  public byte[] Serialize();    // bytes canónicos → SHA-256 para la tabla dorada
  public bool Walkable(int x,int y); public bool BlocksProjectile(int x,int y);
  public bool SegmentClear(int x0,int y0,int x1,int y1); // recorrido entero de celdas (supercover), para validar skill shots
}
public static class ArenaValidator { /* G-03..G-09: Symmetric, Connected, MinCutAtLeast2, SpawnsValid, Density */ }
```

### 3.2 PRNG
SplitMix64, todo con `ulong` y `unchecked`, idéntico en Mono y en .NET.
- Estado inicial: `mix((ulong)(uint)seed) ^ mix(((ulong)GenVersion << 32) | ((ulong)theme << 8) | (ulong)attempt)`.
- `NextBounded(n)`: multiplicación de 64 bits con rechazo (Lemire), solo enteros.
- Cada intento (`attempt` 0..7) arranca de un estado derivado. No se reusa el estado del intento anterior.

### 3.3 Algoritmo
1. **Zonas fijas**:
   - **bolsillos de spawn**: columnas 0–3 (equipo A) y 19–22 (equipo B), siempre `Floor`;
   - **fila central** `y=9`: se permite como mucho 1 obstáculo por mitad, para que no se tape el eje.
2. **Simetría**: `Symmetry = NextBounded(2)`.
   - Rot180: `(x,y)↔(22−x, 18−y)`.
   - Espejo: `(x,y)↔(22−x, y)`.
   - Se trabaja sobre la **mitad oeste** (`x ≤ 10`, más la mitad superior de la columna 11 en rot180) y cada colocación se aplica también en espejo. La celda central (11,9) se deja siempre libre.
3. **Densidad objetivo**: `target = min + NextBounded(max − min + 1)`, en % de las 437 celdas, según el tema (§3.5).
4. **Estampas** según los pesos del tema: pilar 1×1, roca 2×2, muro 1×3 o 3×1, L de 3, árbol 1×1 (Solid) y charco 2×1 o 2×2 (Water).
   - Posición al azar dentro de la mitad; hasta **64 tiradas** por arena.
   - Una estampa se **acepta** si:
     - no pisa zonas fijas ni otra estampa;
     - deja 1 celda de separación con las demás (evita laberintos; se puede ajustar por tema);
     - **después de colocarla y espejarla, el área caminable sigue conexa** (BFS de 4 vecinos, igual que el movimiento de AO; son ≤ 437 celdas).
   - Si no se acepta, se descarta y se sigue.
   - Se corta al llegar a `target`.
5. **Decoración** `Deco` (pasto alto, huesos, grietas): se camina, no cambia reglas y va en simetría.
6. **Variantes**: `Variant[i] = NextBounded(256)` para cada celda que no es `Floor`. También van en simetría: la celda espejada usa la misma variante.
7. **Spawns**: fijos en los bolsillos.
   - A: `(2,9),(2,7),(2,11),(2,5),(2,13)`. B: la imagen de A según la simetría.
   - Siempre caminables, distintos, con ≥ 2 vecinos libres y a ≥ 17 casillas del rival (G-06).
   - El servidor **alterna los lados en cada ronda**, como el original: el equipo A usa `SpawnB` en las rondas pares. No hace falta otro layout.

### 3.4 Validación final, reintentos y respaldo
- Al final se corre `ArenaValidator` completo:
  - simetría;
  - conexidad y ninguna celda caminable aislada (se garantiza por construcción);
  - **min-cut ≥ 2** entre los bolsillos A y B (Edmonds-Karp con capacidad 1 por nodo; 2 aumentos alcanzan);
  - densidad en rango;
  - spawns.
- Si falla, `attempt++` (hasta 8).
- Si fallan los 8, se usa el **layout de respaldo**: 4 pilares fijos en simetría. Siempre es válido y queda marcado con `Attempt = 255` para que QA lo cuente (G-13).
- **Semillas límite** (G-12): toda semilla int32 es válida; `seed` pasa a `uint` antes de mezclarse. Nada puede lanzar excepción: los bucles tienen tope fijo.
- Costo estimado: ≤ 64 BFS de 437 celdas más 2 flujos por intento → mucho menos de 1 ms. El tope de G-13 es 5 ms.

### 3.5 Temas y densidad
Tabla en el mismo archivo (constantes, entran en el hash):

| Tema | Densidad % | Solid (pesos) | Water | Deco |
|---|---|---|---|---|
| Bosque | 10–18 | árbol 4, roca 2, pilar 1 | charco 1 | 3 |
| Desierto | 8–14 | roca 3, pilar 2, muro 1 | — | 2 |
| Nieve | 8–15 | roca 2, pilar 2, árbol 2 | hielo = Water 1 | 2 |
| Mazmorra | 12–20 | pilar 3, muro 3, L 2 | — | 2 |
| Pantano | 10–18 | árbol 2, roca 1 | charco 4 | 3 |
| Ciudad | 8–14 | muro 2, pilar 3 | fuente 1 | 1 |

Arte fija los límites finales (G-09) y la paleta GRH por categoría (G-11), en un JSON aparte que **no** entra en el generador. El generador solo decide categoría y variante.

### 3.6 Hash, tabla dorada y versión
- `Serialize()`: `"AOAG"`, luego `GenVersion, Seed, Attempt, Theme, Symmetry, W, H` (int32 little-endian), después `Cells`, `Variant` y los spawns.
- Tabla dorada: `Tools/demo_arena_golden.json` con 50 semillas × 6 temas → SHA-256. La validan una prueba .NET (QA) y una de Unity.
- Cualquier cambio en el algoritmo, las constantes o los temas **sube `GenVersion`** y regenera la tabla a propósito.

### 3.7 Cómo se aplica
- **Cliente**: `AOGridMap` suma una **capa superpuesta por ring**, sin tocar la grilla base:
  - `ApplyArenaOverlay(ringId, originX, originY, layout)` / `ClearArenaOverlay(ringId)`.
  - Solid → `ALL_SIDES` (bloquea el paso y `ProjectileBlockedBetween` ya lo toma como pared).
  - Water → `FLAG_WATER` sin costa (bloquea el paso, deja pasar proyectiles: igual que el agua de hoy).
  - Los obstáculos visibles son sprites hijos del ring, con la paleta del tema y ordenados como los objetos del mapa.
  - Se aplica con `duelStart` o `duelRingState` (semilla y tema) y se limpia al liberarse la sala. **Los espectadores ven lo mismo** porque generan lo mismo.
- **Servidor**: corre el mismo `Generate` y marca la misma superposición en su grilla (`CoopRoom`) para validar posiciones, spawns y los `SegmentClear` de los skill shots.
- Si la `GenVersion` del cliente no coincide, no puede pelear y como espectador ve el aviso (`red.md` §6).

### 3.8 Enganche para QA
`Editor/AODemoArenaQA` (de QA, con la API de Programación): carga el mapa 1001, aplica 3 semillas y compara flag por flag con el layout (G-20). La hoja de contacto `demo_arenas_contact.png` puede salir de la prueba .NET (dibujo por categoría), sin Unity.

## 4. PvP local dentro del ring

### 4.1 Regla de hostilidad (compartida y pura)
`AODuelRules.CanHarm(atk, tgt, duel)` es verdadero **solo si**:
- los dos son participantes del mismo reto y de equipos contrarios;
- están vivos en el reto;
- la fase es `Pelea`;
- los dos están dentro del interior de su ring.

En cualquier otro caso sigue la regla de hoy: no hay daño entre jugadores. El cliente la usa para bloquear de entrada con un mensaje; el servidor la usa para rechazar (`red.md` §4).

### 4.2 Estado del reto en el cliente
Módulo nuevo `AODuelClientV###` (Programación). Lo alimentan los eventos de Servidor (`duelStart`, `duelRoundStart`, `hurt`, `died`, `duelEnd`, `warp`, `duelRingState`) y expone:
- `InDuel`, `Phase` (Conteo, Pelea, EntreRondas, Fin), `RingId`, `Team`;
- `IsEnemy(playerId)`, `IsInsideRing(x,y)`;
- `DownInRound` (caído esperando la próxima ronda).

Interfaz lee de ahí el marcador, el conteo y la atenuación de la hotbar.

### 4.3 Acciones contra otro jugador
- **Cuerpo a cuerpo** (`AOPlayerCombatV09.TryAttack`): si `InDuel` y en la casilla de enfrente hay un avatar enemigo → `attack {id: jugador}`. Hoy solo busca NPC (`:496-517`).
- **Hechizo con clic** (`AOPlayerMagicV120.TryTargetMouse`): rama nueva antes de `CastAlly` (`:289`). Si el objetivo es un jugador y `CanHarm` → `cast {id: jugador}`. Si no → "No podés intervenir en un reto." o la regla de hoy.
- **Skill shot** (`AOSkillShotProjectileV267`): además de NPC, busca **avatares enemigos del reto** por radio (`AOOnlineClientV240` expone posición e id).
  - Los aliados no frenan el proyectil.
  - Al impactar: `SetSkillShotFlight` y `cast {id, amount}`.
  - El servidor confirma con `SegmentClear`.
  - Hoy todo está tipado a `AONPCCombatV09`: se agrega un objetivo neutro `(id, pos, kind)`.
- **Invocaciones y mascotas**: bloqueadas en el reto (no hay mascotas en el ring).

### 4.4 Vida, maná y muerte en el reto
- En el reto **no se aplica daño local**: `AOPlayerCombatV09.SetServerVitals(hp, maxHp)` y `AOPlayerRPGV11.SetServerMana(mana)` muestran lo que manda el servidor.
- El daño de NPC, EOT y veneno local no corre dentro del ring: el servidor lleva los estados.
- **`died` del reto ≠ muerte normal**:
  - no llama a `DeathRoutine`: **no desequipa, no hay fantasma, no aparece el cartel ESPÍRITU ni `/HOGAR`**;
  - se muestra un estado "caído" (sprite de muerto, sin control) hasta `duelRoundStart`, que revive y limpia.
- **Guardado**: la vida, el maná y el estado de muerto del reto nunca se persisten.
  - En `duelEnd` el **servidor manda la vida y la posición previas al reto**; el cliente no guarda un snapshot propio.
  - Se restauran **solo** vida, maná, estados y posición. **Nunca inventario, oro ni experiencia**: si no, vuelven las pociones usadas en el reto y se duplican objetos.
  - Servidor ignora `hp` y `dead` del snapshot del cliente durante el reto (aceptado, `red.md` §12 bis).
- Pociones: `use {item}` va al servidor (`red.md` §4). Localmente solo se bloquea el uso directo mientras `InDuel`.

### 4.5 Conteo y bloqueos
- **Conteo**: movimiento congelado (`AOTestPlayer` no toma órdenes ni rutas del clic derecho), sin ataque ni casteo. Interfaz atenúa la hotbar.
- **Bloqueados durante todo el reto** (`AODuelRules.Blocks(acción)`, consultado en cada punto de entrada):
  - invocar;
  - ocultarse o volverse invisible (se revisa `invisibility` al lanzar);
  - montar;
  - `/HOGAR` y `/regresar`;
  - comerciar, banco y ventanas de ciudad;
  - soltar y levantar objetos;
  - misiones.
- Meditar: como en el original (**Contenido confirma** si `ModRetos.bas` lo bloquea). Atacar y castear ya la cortan desde el 24/09.

### 4.6 Espectadores
- **Caminando**: el borde del ring está cerrado en el mapa, así que nadie entra sin `warp`.
- **Hechizos con clic**: si la casilla objetivo está dentro de un ring activo y no sos participante → "No podés intervenir en un reto." Lo mismo al revés: un participante no puede apuntar afuera.
- **Proyectiles**: el borde es `ALL_SIDES` y los corta (ya funciona con `ProjectileBlockedBetween`).
- **Servidor**: rechaza cualquier acción de un no participante hacia adentro del ring y posiciones adentro (`red.md` §7).

### 4.7 Fórmulas PvP (compartidas y puras)
Archivo nuevo `Runtime/Shared/AOPvpFormulasV###.cs`, sin UnityEngine, con un RNG entero inyectable:
- `HitChance(atkPower, evaPower)`, `MeleeDamage(...)`, `SpellDamage(base, magicDefense, ...)`.
- Transcriptas de `SistemaCombate.bas` y `modHechizos.bas` **por Contenido** (fidelidad); Programación arma la API y las pruebas.
- El cliente reporta además `magicDefense` (casco, armadura, anillo y efectos). Se calcula en `AOInventoryV10` y el servidor lo acota.
- El que tira los dados es el servidor. El cliente solo usa las fórmulas para mostrar (tooltips, estimaciones).

### 4.8 Fin del reto
`duelEnd` → aplicar la vida y la posición previas que manda el servidor (§4.4) → `warp` a la grada → `ClearArenaOverlay` cuando el servidor libera la sala. El oro llega como `wallet` absoluto (Servidor). El cliente no suma ni resta oro del reto.

## 5. Quién decide qué
| Cosa | Servidor (autoritativo) | Cliente |
|---|---|---|
| Semilla, tema, layout | Elige la semilla y genera | Genera lo mismo y lo dibuja |
| Posición en el ring, `warp`, conteo | Sí | Congela, obedece `warp` |
| Impacto, daño, muerte y rondas del PvP | Sí (fórmulas compartidas) | Pide `attack`/`cast`/`use`, detecta el skill shot y lo pide con `amount` |
| Vida y maná en el reto | Sí | Solo muestra |
| Apuesta, custodia, pago y todo el oro (protocolo 3) | Sí (billetera, banco, libro) | Pide y muestra el `wallet` |
| Bloqueos del reto | Rechaza | Bloquea de entrada con un mensaje |
| Mapas de demo, NPC, respawn | Catálogo y `TickMap` | Carga el JSON |

## 6. Oro del servidor en protocolo 3 (`red.md` §3): cambios en archivos de Programación
**Online** cada uno manda un pedido y espera el `wallet`/`bank` absoluto; **offline** no cambia nada.
- `AOQuestSystemV150:~844`: recompensa → `questReward {quest}`.
- `AOCityBankV130`: depositar y retirar → `bank {±amount}`.
- Compra y venta a comerciantes: **ya pasan por el servidor** en línea (`AOCityNPCSystemV130:309/438` → `Trade`); las líneas 320/454 son solo del modo sin conexión. No hace falta cambiarlas.
- `AOPlayerCombatV09.AddGold/SpendGold`: en online solo aplican el `wallet` del servidor.

## 7. Trabajo de fase 2 (Programación)
| Pieza | Archivos | Tamaño |
|---|---|---|
| Builder de mapas + especificaciones de 1000/1001/1010+ | `Tools/demo_map_builder.py`, `Tools/demo_maps/*.json` | Grande |
| Generador y validador compartidos | `Runtime/Shared/AOArenaGenV###.cs` | Medio |
| Superposición de arena en la grilla y los sprites | `AOGridMap`, `AOWorldManagerV07` (o un módulo `AOArenaOverlayV###`) | Medio |
| Estado y reglas del reto | `AODuelClientV###`, `AODuelRules` (compartido) | Medio |
| PvP en ataque, magia y skill shot | `AOPlayerCombatV09`, `AOPlayerMagicV120`, `AOSkillShotProjectileV267` | Medio |
| Vida del servidor, muerte del reto, snapshot y bloqueos | `AOPlayerCombatV09`, `AOPlayerRPGV11`, `AODeathRespawnV160`, `AOTestPlayer`, `AOInventoryV10` | Medio |
| Fórmulas PvP compartidas (con Contenido) | `Runtime/Shared/AOPvpFormulasV###.cs` | Chico |
| Oro online en misiones y banco | `AOQuestSystemV150`, `AOCityBankV130` | Chico |
| Modo prueba y hogar de demo | `AOSaveGameV140`, `AODeathRespawnV160`, `AOWorldManagerV07` | Chico |

Orden sugerido: generador + validador (QA puede probar G-01…G-13 enseguida) → builder + mapa 1001 → superposición → reto en el cliente → dungeon (cuando esté `dungeon-npcs.json`).

## 8. Pedidos a otros sectores
- **Servidor**:
  - enlazar `Runtime/Shared/*.cs` en el csproj;
  - `npcLayoutVersion` (§2.5);
  - ignorar `hp` y `dead` del snapshot durante el reto (§4.4);
  - API `attack/cast {id: jugador}` y posiciones e ids de avatares para los skill shots;
  - prefijo de identidad en modo prueba.
- **Contenido**:
  - fórmulas PvP de `SistemaCombate.bas`/`modHechizos.bas` y cómo se calcula `magicDefense`;
  - `dungeon-npcs.json` con posición por zona, cantidad y respawn;
  - que las migraciones de música y ambiente conserven los IDs ≥ 1000;
  - textos del hub.
- **Arte**:
  - paleta GRH por tema y categoría (Solid, Water, Deco) y límites de densidad;
  - recortes originales para la plaza del hub y las gradas;
  - minimapas de los mapas ≥ 1000 (o el renderer);
  - que `import_all_minimaps.py` se salte los IDs ≥ 1000.
- **Interfaz**:
  - leer `AODuelClient` (fase, marcador, caído);
  - prefijo de PlayerPrefs en modo prueba;
  - bloquear ventanas de ciudad y comercio cuando `AODuelRules.Blocks`.
- **QA**:
  - la prueba .NET del generador sobre la API de §3.1;
  - `AODemoArenaQA` (G-20);
  - auditoría D-20 de los mapas con `demo_map_builder.py --check`.

## 9. Riesgos y decisiones para Lucas
| # | Tema | Recomendación |
|---|---|---|
| 1 | Personajes de demo separados (`AO_BattleDemo/`) o los mismos de siempre | Separados: la demo no puede dañar partidas reales |
| 2 | Tema del ring: fijo por ring o elegido en cada reto | Fijo por ring (piso horneado en el mapa, menos render en tiempo de juego); el servidor igual manda el tema |
| 3 | 4 rings en 1001 alcanzan para 11 conexiones | Sí; 1002 queda reservado |
| 4 | Los hechizos con clic no piden línea de vista (como el original); los skill shots sí chocan con obstáculos | Mantener el original |
| 5 | Riesgo: cambiar la lista de NPC de un piso ya jugado online | Se resuelve con `npcLayoutVersion` |
| 6 | Riesgo: builder y migraciones pisan catálogos | Merge solo ≥ 1000 y pedidos a Contenido y Arte |

**Bug encontrado al pasar (no es de la demo):** a los EOT se les aplica `ModifyIncomingMagic` dos veces (`AOMagicEffectRuntimeV129.cs:107` y `AOPlayerCombatV09.cs:690`). Anotado en el tablero de Programación para corregirlo aparte.
