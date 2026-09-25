# Demo AO BATTLESERVER · Arte (fase 1: diseño)

Sector Arte y Animación · 24/09. Solo diseño: no se tocó `Assets/`.

**Fuentes (analizadas con scripts, no a ojo):**
- los 842 mapas importados (`Resources/AOMigrator/WorldV07/Maps`): capas 1–4, bloqueos, triggers y objetos;
- `init/graficos.ini`, `Dat/obj.dat`, `Dat/Retos.dat`;
- los rangos `HayAgua` y `HayLava` del servidor original (`General.bas`).

**Referencias visuales** en `docs/claude/demo/arte/` (abrir solo si hace falta):
- `pisos.png`: sets de piso 4×4.
- `obstaculos.png` y `ring-y-deco.png`: GRH con su número.
- `ring-original-272.png`: render del ring con cuerdas de la Arena de Clanes.

## 0. Reglas de arte para la demo
1. **Solo GRH originales** en la fase 2: el arte nuevo cuesta 0. El HD con Higgsfield es opcional y va después (sección 5).
2. **El agua y la lava se detectan por rango de GRH en la capa 1**, igual que en el servidor original. El generador tiene que usar exactamente estos rangos:
   - **Agua:** 1505–1520, 124–139, 468–483, 2948–2963, 12628–12643, 24143–24158, 24223–24238, 24303–24318 y 44668–44683.
   - **Lava:** 16101–16116 y 26767–26782.
   - Se camina sobre ellas solo con barca: **en el ring hay que prohibir navegar** (Programación).
3. **Dentro del ring, solo obstáculos cuyo sprite mida hasta 64×128 px.** Los árboles grandes (de 160 a 384 px) tapan a los jugadores: van **solo afuera** del ring o en sus bordes.
4. Cada tile usa la misma lógica del AO: la capa 1 es el piso (sets de 16 GRH, 4×4, uno por tile según la posición); la capa 2, bordes y transiciones; la capa 3, obstáculos (con el bloqueo en `blocks`); la capa 4, techos.
5. Los sprites grandes se dibujan centrados en X y apoyados abajo, sobre el tile que bloquean, como en el original.

## 1. Sets por tema (generador de arena y pisos del dungeon)

"Piso" = inicio del set de 16 GRH. "Chico" = obstáculo apto para el interior del ring. "Grande" = solo afuera o en los bordes.

### Bosque
- **Piso:** pasto 6000–6015 (f6000); variación: pasto oscuro 6016–6031.
- **Agua:** 1505–1520 (f20). **Orilla:** 85304–85315 (f6684, set de 12: 4 lados + 4 esquinas externas + 4 internas).
- **Chico:** tocón 29258 (64×32), tocón con pasto 12579 (64×64), piedra 55255 (OBJ2676).
- **Grande:** pino 12160, frondoso 55633, sauce 55627, oscuro 55638, doble 55626, cerezo 644.
- **Mapas fuente:** 3, 7, 12, 22, 28, 29.

### Desierto
- **Piso:** arena 7704–7719 (f6021). **Transiciones de arena:** 41882–42185 (f6021).
- **Orilla arena–agua:** 85480–85491 (f6685).
- **Chico:** cactus 2348 (64×64), piedra 55255.
- **Grande:** palmeras 1878, 1880, 460 y 461; palma baja 463; arbusto seco 2231; árbol muerto 2344.
- **Mapas fuente:** 15, 16, 20, 21, 98, 202; Arena de Clanes 272/273.

### Nieve
- **Piso:** 7380–7395 (f6640), 21336–21351 (f5168); variación 13064–13079 (f6641).
- **Bordes:** 56775–57030 (f6115).
- **Chico:** bloques de piedra clara 9087–9095 (f7522, 32×32), arbusto nevado 27308 (64×64).
- **Grande:** pinos nevados 12165–12171 (f7078, 256×256).
- **Mapas fuente:** 349, 351, 354, 355, 356 (Laderas Heladas, Eleusis). Clima: nieve (`map_environment.json`).

### Pantano
- **Piso:** barro 16404–16419 (f5114) mezclado con pasto 6000–6015.
- **Agua verde:** 12628–12643 (f5021). **Bordes:** 18944–19006 (f5114).
- **Chico:** helecho 642 (64×64), tocón 12579, fardo 7148 (32×32).
- **Grande:** árbol de pantano 11904, árbol muerto 50990, gigantes 2548, 2549 y 6597 (f7104).
- **Mapas fuente:** 228, 229, 232, 233 (Pantano); 25/26 (Ciénaga de las Cobras); 173 (Guarida del Pantano).

### Ciudad (y hub)
- **Piso:** empedrado gris 58114–58122 (f5043); adoquín oscuro 3885–3900 (f5026).
- **Bordes y muros:** 59612–59632 (f5023), 58238–58253 (f5022).
- **Chico:** barril 48886, farol 55316, columna de arenisca 2386 (32×128), muro gris 58440.
- **Decoración:**
  - estandarte azul 58712 (OBJ3674) y rojo 60298 (OBJ3675);
  - antorcha 55254 y cartelera 19543;
  - carteles "Arena I–IV" 50902, 50903, 50904 y 50908, y "Bienvenido al Newbie Dungeon" 50925 (ojo: 50876 no está vacío, dice "Penthar");
  - escalera de bajada 26940.
- **Mapas fuente:** 1 (Ullathorpe), 34, 66, 112, 266, 345.

### Dungeon (paleta general; los 7 pisos concretos están en la sección 4)
| Subtema | Piso | Peligro | Mapas fuente |
|---|---|---|---|
| Alcantarilla | baldosa gris 5349–5364 (f5026) | agua oscura 124–139 (f5040) | 329–336 (Drenaje) |
| Pirámide / mina | sillería 4071–4086 (f5063) | — | 563–567, 576 (Pirámide, Cámara de Anubis); minas 33, 50, 52 |
| Cripta | piedra con musgo 9428–9443 (f5129) | agua 124–139 | 377, 391 (Dungeon Dragón); 115, 116 (Marabel); 314 |
| Volcán | roca con grietas 17079–17094 (f5041) | lava 16101–16116 | 365–368 (Magma Dungeon) |

Comunes a los 4:
- **Muros:** bordes 57793–58080 (f5050, el 66 % de los bordes de dungeon), tope negro 19710, bloques 18248–18251.
- **Chico:** columnas 18239 y 18276 (32×128), tótem de hueso 18734 (32×96), cristal 7112.
- **Decoración de mina:** yacimientos 8611–8619 y 16886.
- **Luz:** oscuro, con antorchas 55254 cada 6–8 tiles.

## 2. Zona de arenas

### Qué hay en el original
- **Zona de Ring** (324, 372, 389, 390): 4 salas de 23×19 por mapa, de pasto 6000–6015 con trigger 6 (zona de pelea), aisladas por agua o vacío. No tienen lugar para espectadores.
- **Arena de Clanes** (272/273): sobre arena, con una grilla de corrales de postes y cuerdas (x60–81, y64–85), palmeras y agua alrededor. Ver `ring-original-272.png`.
- **DeathMatch** (mapa 49): el ring se achica con los postes OBJ3763–3768.

### Set de ring (f5067, el original de postes y cuerdas)
- **Postes:** esquinas 48850–48853; postes con amarre 48858–48867 (los OBJ3763–3768 del DeathMatch).
- **Cuerdas:** horizontales 48854–48857; verticales 48868–48869.
- **Alfombra roja con fleco:** 1753–1772 (f6028).

### Propuesta: mapa de arenas (100×100)
Módulo por ring, de adentro hacia afuera:

| Capa | Ancho | Contenido |
|---|---|---|
| Interior | 23×19 | Tema elegido por la semilla: piso base + variación (manchas de 3–6 tiles) + obstáculos "chicos" simétricos. Trigger 6. |
| Borde | 1 tile | Postes f5067 en las esquinas y cada 4–5 tiles, con cuerdas entre medio. **Bloqueado.** Es fino y no tapa la pelea. |
| Pasillo | 1 tile | Alfombra roja 1753–1772: por acá caminan los espectadores. |
| Grada | 2 tiles | Empedrado 58114. En la fila externa, sillas 586 y bancos 2620 alternados (bloqueados, decorativos). |
| Calle | 4 tiles | Adoquín 3885 con faroles 55316 entre rings. |

Detalles del módulo:
- **Lados de cada equipo:** estandarte azul 58712 al centro del lado corto del equipo A y rojo 60298 en el del B, fuera del ring.
- **Esquinas:** antorchas 55254 en las 4 esquinas de la grada.
- **Entrada:** en la grada va el cartel "Arena I…VII", que existen en el original (OBJ2441, 2442, 2443, 2447, 2448, 2451 y 2452).
- **Cartel de estado de Interfaz** (`ui.md` §5): va en la fila de grada de arriba de cada ring, sin objetos altos ahí (queda libre y visible).

Medidas:
- Un módulo ocupa 23+2+2+4 = **31 × 27 tiles**.
- En 100×100 entran **4 rings (2×2) con una plaza central** para cruzar.
- 6 (2×3) ocupan 89 tiles de alto: solo entran si el mapa no deja el margen de borde habitual del AO (~7–9 tiles por lado, que Programación tiene que confirmar).
- Recomiendo 4: la sala tiene 11 conexiones.

Luz y clima: de día y sin lluvia (`rain=false`), para leer bien la pelea.

### Generador: qué aporta Arte
- **Paleta por tema:** piso base, variación y obstáculos chicos (ver arriba). La simetría, los caminos y los spawns son de Programación.
- **Agua en el ring:** solo en manchas de 2×2 o más, para que las orillas de 12 piezas encajen.
  - Si el algoritmo de autotiling no está en la fase 2, **sin agua dentro del ring**: sale el piso con obstáculos.
  - En la fase 2, Arte arma la tabla pieza→GRH de cada orilla.
- **Arcos:** el borde del ring corta los proyectiles (lo dice Servidor); visualmente alcanza con las cuerdas.

## 3. Hub de la demo (implementado en fase 2)
- **Estilo:** aldea recortada de Ullathorpe (mapa 1, x35–62, y42–84): plaza de la fuente, calle de tierra y casas, con sus bloqueos y techos originales, rodeada de pinos.
- **Aparición:** (52,58), junto a la plaza (Programación).
- **Salida "Arenas": al norte** (decisión de Cerebro, igual que `arquitectura.md`, `ui.md` y `textos-hub.md`). Camino de tierra x55–58 hasta y12, con antorchas y cartel.
- **Salida "Dungeon": al sur.** La calle termina en la puerta de dungeon 1493, con antorchas y cartel.
- **Carteles:** en los gráficos originales no hay ninguno en blanco: todos traen texto impreso (ver `arte/carteles.png`).
  - Arte solo propone posición y gráfico (`carteles` en cada `.art.json`). Los pone la estructura como objetos clicables con los textos de `textos-hub.md`.
  - Hub: "Arena I" (50902) al norte y "Bienvenido al Newbie Dungeon" (50925) al sur. Rings: "Arena I–IV". Pisos: "Nº1…Nº7" (50849–50855, OBJ 2406–2412).
  - No usar 50876: dice "Penthar".
- **Música:** de ciudad (la elige Contenido).

## 4. Pisos del dungeon (los 7 de Contenido, `progresion.md` §4–5)
Cada piso usa el set visual **de su propio mapa original**: es el hábitat de sus NPC. Datos sacados por script de esos mapas (capa 1 = piso, capa 2 = bordes, capa 3 bloqueada = obstáculos). "Luz" es `baseLight` de `map_environment.json`.

| Piso (nv) | Mapas fuente | Piso | Bordes / muros | Obstáculos y decoración | Luz |
|---|---|---|---|---|---|
| **P1 Madriguera** (1–4) | 37, 167, 168, 264 (Newbie Dungeon) | roca de cueva 7720–7735 (f5095) | 15298–15304, 7847–7860 (f5095) | roca 16907 (64×64) adentro; formaciones 15302–15312 (128×128) solo en muros. Cartel 50925 "Bienvenido al newbie dungeon" en la entrada | 9408399, 4–9 luces |
| **P2 Cementerio** (4–10) | 4 (Cementerio de Nix, exterior) | pasto 6000–6063 + tierra 51305–51320 (f6223) + 6320–6335 (f6215) | 51879–51918 (f6223) | lápidas y cruces 14386, 14390 (f5018, 32×96), tumbas 50955, 50956, 50967 (f5036), 14395 (96×96); árboles 12160, 55627, 55633 afuera | exterior con lluvia; 22 luces, 19 partículas. **Propuesta: de noche** |
| **P3 Mausoleo** (10–15) | 392 | piso de mausoleo 27094–27301 (f6026) | 9743 (f248), 17047/17066 (f5041) | sarcófagos y tumbas 50950–50963 (f5036, 32×32 a 96×96) | propia (−8355670); 103 luces, 120 partículas: el más ambientado, copiarlo tal cual |
| **P4 Pirámide** (15–20) | 564, 567 | sillería 4071–4086 (f5063) | 19022–19033, 19143, 19171 (f5124) | tótem 18734 (32×96), columna 19203 (32×128), estatuas 18834/19110/19111 (96×96); trampas "Trampa 01" 19539 (visual) | 12566463; 8–37 luces |
| **P5 Nido de arañas** (20–24) | 291–293 (Catacumbas Lvl2) | catacumba 1602–1697, 4928–5007, 3711–3790 (f5134) | 1396–1407, 1484–1489 (f5134) | restos 1480–1482 (96×64) adentro; 1399–1403 (160×128) en muros; tope negro 19710 | 14671839–15724527; ~38 luces, ~44 partículas |
| **P6 Torre de Veriil** (24–27) | 140–142 (Dungeon Veriil) | cripta 9428–9443 (f5129) | 19694–19698, 19706–19707 (f5129), 9743 (f248) | muro y pilares dorados 19577, 19696–19701 (128×128, en muros); tope 19710 | 9408399; 32–72 luces |
| **P7 Guarida del Dragón** (27–30) | 391 (Dungeon Dragón) | cripta 9428–9443 (f5129) | 17047/17066 (f5041, volcánico), 19698, 19706–19707 | pilares dorados 19699–19701, 19708–19709 | 12566463; 37 luces |
| **Jefe final Vytaiz** | 314 (Limbo de los Justos) | cripta 9428–9443 + agua 1505–1520 (20 %) | 56134–56149 (f6679) | pilares dorados 19576/19577, 19654, 19699–19701 | propia; 75 partículas |

Notas:
- **P6 y P7 comparten el set de cripta** (`f5129`) en el original. Se distinguen con GRH originales:
  - P6: bordes `f248` y la luz gris de Veriil.
  - P7: los bordes volcánicos `f5041` que ya trae el mapa 391, más **manchas de roca volcánica 17079–17094 y lava 16101–16116 como decoración en la sala del jefe** (fuera del camino).
  - Vytaiz: como el Limbo, con agua (1505–1520) y bordes `f6679`.
- **P2 es un mapa exterior** (lluvia de día). Para que se lea como piso de dungeon: `baseLight` oscuro y farolas o antorchas en el circuito. Lo aplica Programación con `map_environment`; Arte da el valor.
- **Recortes de 70×70 (Contenido):** conviene recortarlos de los mapas fuente, respetando las capas 1–4, las luces y las partículas. El cierre del recorte se hace con los muros del mismo set (columna "Bordes / muros").
- **Sala del jefe (12×12):** solo obstáculos de hasta 64×128 adentro, igual que el ring, para que no tapen la pelea. Los grandes (de 128×128 en adelante) van en las paredes.
- **Escaleras y zona segura:** hueco oscuro 57950 (como las salidas originales) entre dos antorchas 55254. No hay cartel en blanco original.

## 5. Plan de remaster con Higgsfield (cuando haya créditos)
Regla 4×: 512 → 2048 y 1024 → 4096. Los de 2048 (f5040 agua, f5041 volcán) se parten en 4.

| Tema | Texturas | Imágenes 4× | Con 1 reintento |
|---|---|---|---|
| Ring (f5067, f6028, f6021, f6000, f7516) | 5 | 5 | 10 |
| Ciudad / hub | 12 | 12 | 24 |
| Bosque | 9 | 9 | 18 |
| Desierto | 6 | 6 | 12 |
| Nieve | 7 | 7 | 14 |
| Pantano | 9 | 9 | 18 |
| Dungeon (7 pisos, sección 4) | 15 | 18 | 36 |
| **Total** | **63** | **66** | **~132** |

Créditos y costo:
- Hay texturas compartidas entre temas (f6000, f105, f5026, f5066, f5096, f6800, f7102, f6021, f5124). Sin repetir, quedan **~55 imágenes, unos 110 créditos**.
- Seedream 4.5 cuesta ~1 crédito por imagen. Con la cuenta del remaster completo (5–6k créditos ≈ US$150–300), la demo sale **~US$3–7**.
- Falta confirmar que Seedream 4.5 saque 4096 px. Si no, las texturas de 1024 también se parten en 4, y se multiplica por ~3.

**Piloto (1 lote, unos 16 créditos):**
- **Texturas:** f6000 (pasto), f6021 (arena), f6640 (nieve), f5129 (cripta), f5114 (pantano), f5043 (empedrado), f5067 (postes y cuerdas) y f6028 (alfombra).
- **Criterios de aprobación:**
  - el set 4×4 tiene que repetir sin costuras;
  - misma silueta, paleta y lectura top-down;
  - los sprites con alfa limpio (si Higgsfield no devuelve alfa, sacar el fondo con birefnet de SpriteForge);
  - mismo pivot y tamaño relativo.
- **Controlar con:** `AOTerrainHDDiagnosticV029` y la vista lado a lado del Spell Tester.

**Orden:** ring y hub (es lo primero que se ve) → temas de arena (bosque, desierto, nieve, pantano) → dungeon.

Cada lote necesita el OK de Lucas con el costo a la vista. **Hoy está bloqueado: no hay créditos.**

## 6. Respuestas a otros sectores
- **Interfaz** (`ui.md`):
  - Minimapa y mapa grande de los mapas ≥ 1000: Arte los genera como los 842 existentes (`Resources/AOMigrator/MinimapsV0103/map_<id>.png`, 100×100, 1 px por tile) con un script de render de las capas 1–3 (fase 2).
  - En el mapa de arenas, el minimapa muestra el ring vacío (sin los obstáculos de la semilla).
  - Borde visible y lugar para el cartel: en la sección 2.
  - Ícono de Retos en el HUD: se puede reusar el cartel 50902 o el estandarte recortado. Lo decide Interfaz con Lucas.
- **Servidor** (`red.md`):
  - `fx` para espectadores: se reusan `AOSpellFXV120`, `AOCombatFeedbackV113` y `AOCastAnimation*V268`, que ya dibujan golpes, hechizos y casteos. Solo hace falta llamarlos con los datos del evento, sin arte nuevo.
  - Proyectil remoto: un modo "solo visual" de `AOSkillShotProjectileV267`. El dueño es Programación y Arte ajusta `visualScale`.

## 7. Decisiones para Lucas
1. **Borde del ring:** cuerdas y postes, estilo Arena de Clanes (recomendado: no tapa la vista), o muro de piedra, estilo Zona de Ring.
2. **Cantidad de arenas:** 4 rings con plaza (recomendado) o 6 (solo si entran con los márgenes del mapa).
3. **Agua dentro del ring:** solo si Programación hace el autotiling de orillas. Si no, el ring va solo con obstáculos.
4. **Dungeon:** salas copiadas de los mapas originales (recomendado) o generadas.

## 8. Fase 2: implementado (24/09)
- **Paleta del generador:** `Assets/StreamingAssets/AOMigrator/ArenaGen/arena_palette.json`.
  - Las claves son el enum `Kind`; densidad G-09 igual a `arquitectura.md` §3.5.
  - Verificador: `python Tools/demo_arena_palette_check.py` (G-11; `--preview DIR`).
  - Cambios por legibilidad frente a §1: Mazmorra usa piso de cripta 9428, Pantano agua 1505 y Ciudad muros oscuros 60192/60035.
  - No hay textura de hielo original: el "hielo" de Nieve es agua 1505.
  - Adentro del ring no entra ningún árbol original (todos miden 224–288 px): la clase Tree es vegetación de 64×64.
- **Mapas:** `python Tools/demo_art_specs.py` escribe `Tools/demo_maps/{id}.art.json` (ops stamp/paint/erase + `decorBloqueante`) para 1000, 1001, 1010 y 1011–1017.
  - `--preview DIR` simula el resultado.
  - El builder de Programación (`Tools/demo_map_builder.py`) los aplica.
- **Minimapas y mapa grande:** `Tools/demo_minimap.py` dibuja el mapa entero y lo achica a 100×100, como los originales. El builder lo llama al escribir.
- **Referencias:** `docs/claude/demo/arte/paleta-arenas.png` y `mapas-demo.png` (los 10 mapas armados).

