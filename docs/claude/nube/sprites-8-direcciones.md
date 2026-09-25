# Sprites en 8 direcciones (NE/NO/SE/SO) · diseño

Sector Arte y Animación · 25/09. Solo diseño: no se tocó código ni datos.
Tarjeta del tablero: "Sprites de 8 direcciones: definir el pipeline" (`docs/claude/tablero.md:35`).

**Resumen:** hoy todo es de 4 direcciones, salvo el movimiento del jugador (8 desde v266) y los skill shots (cualquier ángulo). Sin arte nuevo (opción a) el personaje igual puede mirar mejor. Las 8 direcciones reales cuestan unos 10.500 frames si se cubre todo el equipo del jugador, y hoy las dos herramientas de IA están bloqueadas. **Además, no existen en el AO original: lo decide Lucas (pregunta al final).**

---

## 1. Estado actual

### El original
- AO define **4 headings** (`E_Heading`: NORTH=1, EAST=2, SOUTH=3, WEST=4). Cada cuerpo, cabeza, casco, arma y escudo trae un GRH por heading. No hay diagonales.
- El proyecto usa la misma numeración: `AOGridMap.cs:7-10`.

### Los datos migrados (medidos con script)
| Archivo | Contenido | Direcciones |
|---|---|---|
| `Resources/AOMigrator/CharacterV111/character_visuals.json` (v0.11.1-alpha) | 1.329 bodies y 374 heads | Todos con 4. Body: N=6, E=5, S=6, O=5 frames. Head: 1 frame por dirección |
| `Resources/AOMigrator/ItemsV10/items.json` | Armas: 107 ítems / 74 `anim`. Escudos: 42 / 40. Cascos: 177 / 162. Armaduras: 466 | Todos con 4, con los mismos conteos (6/5/6/5; cascos 1) |
| `StreamingAssets/AOMigrator/CastV268/cast_animations.json` | 13 bodies de casteo de NPC | 4 |

### El renderer (`AOCharacterRenderer.cs`)
- `DirectionVisual` (`:7-15`): `heading` + frames de body, head, helmet, weapon y shield.
- `SetHeading` (`:254-271`) **rechaza cualquier valor fuera de 1–4** (`:257-259`). Al girar mientras camina conserva la fase (`:266-268`).
- `Current()` (`:490-503`), `CurrentEquipment()` (`:505-520`) y `CurrentCast()` (`:476-488`) buscan la entrada con `d.heading == heading` exacto. **No hay fallback**: si falta la dirección, no se dibuja nada (`RefreshSprites`, `:715-716`).
- `UpdateSorting` (`:289-320`): un `switch` de 4 casos decide si el arma y el escudo van delante o detrás del cuerpo.
- `FrameIndex` (`:628-694`) normaliza la fase: body, arma y escudo quedan sincronizados aunque tengan distinta cantidad de frames (5 contra 6). Sirve igual para 8 direcciones.
- Quieto = frame 0 (`:670-671`). El ataque (`combatAnimating`) y el casteo del jugador (`PlayGenericCast`, `castingDirections == null`) usan **los mismos frames de caminata** con otra fase (`:634-668`). Por eso, un ciclo por diagonal cubre caminar, atacar y castear.

### Dónde se arma el arreglo de 4 (fijo en el código)
- `AOCharacterVisualDatabaseV111.cs:246-248` (`BuildDirections`, base y armaduras).
- `AOItemDatabaseV10.cs:224-226` (`BuildEquipmentVisuals`: casco, arma y escudo).
- `AOWorldManagerV07.cs:1335-1337` (NPC), `AOSummonDatabaseV129.cs:18-19` (mascotas) y `AOCastAnimationDatabaseV268.cs:70-71` (casteo de NPC).
- Editor: `AOWorldPopulationWindow.cs:373-374`. QA: `AOModulesQA270.cs:401` exige `dirs.Length==4`.
- Las armaduras se mezclan **por índice**, no por heading: `AOInventoryV10.cs:1585` y `:1607`, y `AOOnlineClientV240.cs:428`.

### Cómo se decide la dirección
- **Al moverse (jugador):** `AOTestPlayer.cs:231` → `FacingHeading` (`:278-291`). En diagonal conserva la dirección previa si coincide con un eje. Si no coincide, elige N/S. El comentario del código ya dice que es porque los gráficos son de 4.
- **Clic / MOBA:** `AOActionBarV260.cs:127` gira hacia el objetivo adyacente con `Heading()` (`:144`, prioriza E/O). El pathfinding ya es de 8 (costo 14 en diagonal y 10 recto, `:165-166`).
- **NPC:** `AONPCMovementV08.cs:916-935` (`HeadingToward`, prioriza el eje horizontal). Solo se mueven en 4.
- **Servidor:** `OnlineServer/CoopRoom.cs:100` y `:154` recortan el heading a 1–4. Los NPC se mueven en 4 (`:452-468`, `:519`).
- **Jugador remoto:** `AOOnlineClientV240.cs:412-414` (`MoveAvatar` → `SetHeading` con el heading de la red).
- **Al castear un skill shot:** `AOPlayerMagicV120.cs:298-322`. La dirección es un vector libre (del personaje al mouse, `:309-310`) y el proyectil sale en `:321`. **El personaje no gira**: no hay `SetHeading`, así que puede disparar hacia atrás sin darse vuelta. `AOCastAnimationRuntimeV268.PlayPlayer` (`:5-11`) solo reproduce el casteo genérico en la dirección que ya tenía.
- **El heading también es gameplay:**
  - golpe cuerpo a cuerpo al tile de enfrente: `AOPlayerCombatV09.cs:491-494` y `:644-647`;
  - interactuar: `AOTestPlayer.cs:299-302`;
  - bloqueos por dirección de la grilla: `AOGridMap.cs:117-122`;
  - guardado: `RestoreHeading` recorta a 1–4 (`AOTestPlayer.cs:54-61`).

## 2. Por qué se pide
- **Movimiento en diagonal:** desde v266 el jugador **se mueve en 8 direcciones sobre la grilla**, de tile a tile y sin cortar esquinas (`AOTestPlayer.cs:222-246`, `AOGridMap.CanStep` `:135-147`), con la velocidad de la diagonal normalizada (`:147-157`). Pero el sprite muestra una cardinal: al caminar al SE se lo ve mirando al S o al E ("de costado").
- **Skill shots:** los 14 hechizos con `particleviaje` salen en **cualquier ángulo** (`AOSkillShotProjectileV267.cs:24-41` y `:72`). El personaje no mira hacia donde tira.
- **No lo necesitan:** los NPC, las mascotas y el servidor (todos en 4). Alcanza con cubrir **lo que ve el jugador en su personaje y en los de sus compañeros**.

## 3. Opciones de pipeline

Bloqueos de hoy: **Higgsfield sin créditos** y **ComfyUI/SpriteForge sin instalar** (instalarlo y bajar los modelos, de varias decenas de GB, necesita el OK de Lucas). Los personajes siguen en 1×: el remaster 4× de personajes todavía no existe (el piloto HD está bloqueado).

| Opción | Qué es | Costo | Calidad / fidelidad | Riesgo |
|---|---|---|---|---|
| **a. Reusar las 4** | Sin sprites nuevos: elegir mejor la cardinal. Al lanzar un skill shot, mirar al sector de 90° del apuntado, con histéresis de ~10° para que no parpadee cerca de 45°. En diagonal, seguir con `FacingHeading`. | 0 en arte. Cambio chico de Programación. | 100 % fiel (son los GRH originales). La diagonal se sigue viendo "de costado". | Bajo. Ojo: espejar no sirve para crear diagonales (el E espejado da un O con la mano cambiada). |
| **b1. IA local: SpriteForge (ComfyUI)** | La cardinal S y la E como referencia, prompt "caminar en diagonal sureste (walking southeast)" → hoja de 8 frames de 512×512 con alpha. | Gratis por imagen (GPU propia, 2–4 min por hoja). Costo de instalación: bloqueado. | Media. Sale en HD. Para 1× hay que bajarla (27×47) y cuantizar a la paleta original, con retoque a mano. | Alto: coherencia entre frames y con las cardinales, y **separar capas** (la IA dibuja el personaje entero; AO usa 5 capas). |
| **b2. IA en la nube: Higgsfield (Seedream 4.5)** | Igual, pero por hoja: 1 imagen = 1 tira diagonal de 6 frames. Nunca frame por frame. | Piloto ≈ 20 créditos. Todo el equipo del jugador ≈ 2–4k créditos (≈ US$50–200, estimado con la tarifa del remaster). | Media-alta en HD. Mismo problema al bajar a 1×. | Bloqueado (sin créditos). Mismo problema de capas. |
| **c1. A mano (Aseprite / LibreSprite)** | Un pixel artist dibuja la diagonal partiendo de la S y la E (técnica estándar del 3/4). | Horas de persona. Gratis si lo hace alguien del grupo; si no, hay que cotizarlo. | La más alta y la más fiel: 1×, paleta original y mismo esqueleto. | Bajo en el piloto. No escala a ~10k frames. |
| **c2. Híbrida (recomendada si se hace)** | La IA hace el boceto (b1 o b2) → se baja a 1× → cuantización a la paleta del GRH original → limpieza a mano (c1). | Menos horas que c1. Créditos solo si se usa b2. | Alta. | Medio. Es la única que escala y queda cerca del original. |
| **c3. Junto con el remaster HD** | Cuando se remasterice un personaje a 4×, pedir las 8 direcciones en el mismo lote. | Se comparte el costo del remaster. | La mejor coherencia en HD: el modelo ve las cardinales HD. | Depende del remaster (bloqueado). No sirve para 1×. |
| Descartadas | Deformar la S (skew/shear): en pixel art se ve roto. Modelo 3D renderizado: cambia el estilo de AO. Tomar diagonales de otra versión de AO: no conocemos ninguna, y la regla es no mezclar versiones. | — | — | — |

**Volumen si se cubre todo lo que ve el jugador** (NPC afuera):
- 237 cuerpos (6 base + 231 de armaduras), 74 armas y 40 escudos: 4 diagonales × 6 frames = **~8.400 frames**.
- 374 cabezas y 162 cascos: 4 × 1 = **~2.150 frames**.
- Si el piloto aprueba espejar cabezas y cascos (NE→NO, SE→SO), eso se reduce a la mitad.

**Recomendación:**
1. Ya, con costo 0: **opción a**.
2. Diagonales reales solo si Lucas las aprueba: **como opción desactivable** (por defecto, 4 = fiel), empezando por el piloto con **c1/c2** en 1×, porque no depende de las herramientas bloqueadas.
3. Escalar después con SpriteForge (gratis por imagen) solo sobre los sets del jugador, y en HD hacerlo junto al remaster (c3).

## 4. Formato de datos propuesto

**Regla:** no tocar `character_visuals.json`, `items.json` ni `cast_animations.json`. Los genera el migrador desde el original: regenerarlos borraría lo agregado a mano, y quedan como "fuente fiel".

- **Archivo aparte (overlay)**, igual al patrón de CastV268: `StreamingAssets/AOMigrator/Directions8V<nnn>/directions8.json` + PNG en `Frames/`. Se carga en runtime, sin `.meta` ni reimportar en Unity (como `AOCastAnimationDatabaseV268.cs:100-141`). Cerebro asigna el número de versión.
- **Headings nuevos:** 5=NE, 6=SE, 7=SO, 8=NO (en sentido horario, después de los 4 originales). **Solo visuales**: nunca llegan al servidor, a la grilla ni al guardado.
- **Claves** iguales a los índices del original:
  - `body` por id de body;
  - `head` por id de cabeza;
  - `weapon`, `shield` y `helmet` por `anim` del ítem. Varios ítems comparten `anim` (107 armas → 74 `anim`).
- `ppu`: 32 en 1× (como `AOCharacterVisualDatabaseV111.cs:378`) y 128 en 4×, para que ocupe lo mismo en el mundo. Pivot abajo al centro (0.5, 0), igual que hoy (`:375-377`).
- `order` (opcional): orden de arma y escudo respecto del cuerpo en esa diagonal. Si falta, se copia el de la cardinal de fallback.

```json
{
  "version": "d8-0.1",
  "sets": [
    { "kind": "body", "id": 1, "ppu": 32,
      "directions": [
        { "heading": 6, "frames": ["body_1/se_0.png", "body_1/se_1.png", "..."],
          "order": { "weapon": 4, "shield": 3 } }
      ] },
    { "kind": "head",   "id": 1,  "directions": [ { "heading": 6, "frames": ["head_1/se.png"] } ] },
    { "kind": "weapon", "anim": 68, "directions": [ { "heading": 6, "frames": ["weapon_68/se_0.png", "..."] } ] }
  ]
}
```

**Fallback (todo o nada por personaje):**
- La diagonal se usa solo si **todas las capas visibles** del personaje la tienen: body efectivo (con armadura), head, helmet, weapon y shield, las que tengan frames en su cardinal.
- Si falta una sola, el personaje entero usa la cardinal. Mezclar un cuerpo en diagonal con un arma en cardinal se ve roto.
- La cardinal de fallback es el `heading` lógico (1–4) que ya calcula el código (`FacingHeading`, `HeadingToward`, etc.). Así el fallback es exactamente el comportamiento de hoy.
- Un set sin diagonales (NPC, fantasma BODY829, mimetismo, mascotas) sigue igual que ahora.

## 5. Cambios en el renderer (propuesta, sin código)

**Idea central:** separar el **heading lógico** (1–4: gameplay, red y guardado) del **heading visual** (1–8: solo sprites). El `Heading` público sigue siendo 1–4.

`AOCharacterRenderer` (dueño: Programación):
- Campo nuevo `visualHeading` (1–8) + propiedad `VisualHeading` + `SetVisualHeading(int)` o `SetFacing(Vector2)` (8 sectores de 45° con histéresis).
- `SetHeading` (`:254-271`) no cambia: sigue rechazando 5–8. Eso lo protege de llamadas viejas.
- Opción por personaje: `bool useDiagonals` (serializado, por defecto `false`), más una preferencia global del jugador ("Direcciones: 4 original / 8"), también en `false` por defecto. Sin las dos, el renderer se comporta igual que hoy.
- Método nuevo `ResolveHeading()`: devuelve el visual si la diagonal está completa en todas las capas (regla del punto 4) y si no, el `heading`. Se calcula al cambiar el heading o la configuración (`Configure`, `ConfigureEquipment`, `PlayCastAnimation`), no en cada frame.
- `Current()`, `CurrentEquipment()` y `CurrentCast()` (`:476-520`): buscar con el heading resuelto en vez de `heading`.
- `EffectiveBody/Weapon/Shield/HelmetFrames` (`:522-592`) y `RefreshSprites` (`:708-769`): usan esas búsquedas. No hay lógica nueva.
- `UpdateSorting` (`:289-320`): casos 5–8. Usa el `order` del overlay y, si falta, el de la cardinal de fallback.
- `FrameIndex` (`:628-694`): sin cambios. Ya sincroniza distintas cantidades de frames.
- Al girar a una diagonal, conservar la fase de la caminata, como hoy (`walkTime`, `:266-268`).

Constructores (dueño: Programación; datos: Arte):
- `BuildDirections` (`AOCharacterVisualDatabaseV111.cs:240-274`) y `BuildEquipmentVisuals` (`AOItemDatabaseV10.cs:216-248`): después de armar las 4, **agregar las entradas 5–8 del overlay si existen**. El arreglo pasa a 4 u 8 entradas, y el renderer busca por heading, no por índice.
- Revisar las mezclas por índice (`AOInventoryV10.cs:1585` y `:1607`, `AOOnlineClientV240.cs:428`): tienen que cruzar por `heading`. Si no, a la armadura le faltan diagonales y se aplica el fallback (no se rompe, pero se pierde la diagonal).

Quién fija el heading visual:
- `AOTestPlayer.TryStepVector` (`:231-233`): además del `FacingHeading`, `SetVisualHeading` con (dx, dy).
- `AOPlayerMagicV120.TryLaunchSkillShotMouse` (`:298-322`): con el vector `direction`, 8 sectores (o 4 con la opción a). Ojo con el eje: en el mundo +y = norte; en la grilla +y = sur (`AOTestPlayer.cs:206-209`).
- `AOOnlineClientV240.MoveAvatar` (`:412-414`): diagonal visual a partir del salto de tile del remoto (x, y previo → nuevo). **No cambia el protocolo 2** y el servidor sigue recortando a 1–4. El casteo remoto va a mirar en cardinal hasta que exista `castX/castY` (pendiente de Servidor).
- `AOMimicVisualV129.cs:41` y `:51`: copiar también el heading visual.
- QA: `AOModulesQA270.cs:401` tiene que aceptar 4 u 8.

**Decisión de gameplay aparte:** ¿al lanzar un skill shot también se gira el heading **lógico** hacia la cardinal del apuntado? Hoy `AOActionBarV260.cs:127` ya lo hace con el golpe. Cambia a qué tile pega el cuerpo a cuerpo siguiente. Lo deciden Programación y Lucas. Con la opción a sola alcanza con el visual.

## 6. Plan piloto

**Qué:** el personaje por defecto con un arma común.

| Capa | Set | Hoy | Frames nuevos |
|---|---|---|---|
| Cuerpo | body 1 (Humano hombre, `tex_1005`, 27×47 px) | N6 / E5 / S6 / O5 | 4 diagonales × 6 = **24** |
| Cabeza | head 1 (`tex_420`, celda 27×64) | 1 por dirección | 4 × 1 = **4** |
| Arma | Espada Larga (ítem 2, `anim` 68, `tex_2101`, 27×47) | 6 / 5 / 6 / 5 | 4 × 6 = **24** |

- **Total: 52 frames en 1×.** Las diagonales llevan 6 frames, como N/S: comparten el paso de piernas de frente y de espaldas.
- **El frame 0 tiene que ser la pose quieto:** no hay idle aparte.
- **Variante A/B:** espejar solo la cabeza (NE→NO, SE→SO), para medir si sirve y ahorrar la mitad de cabezas y cascos. Nunca espejar el arma ni el escudo: cambia la mano y el orden de dibujo.
- **Cómo producirlo hoy:** c1 (a mano). En cuanto se destrabe, correr c2 con SpriteForge sobre los mismos 3 sets y comparar.
  - Orden: cuerpo primero, como "esqueleto" de referencia. Después la cabeza y el arma, alineadas a las manos de ese cuerpo.
  - Ese esqueleto se reusa para todas las armaduras y armas. Si no, las armas no calzan en otros cuerpos.

**Validación:**
1. **Hoja de contacto** (script en `Tools/`, con PNG a `docs/claude/nube/`): las 8 direcciones en orden N, NE, E, SE, S, SO, O, NO × 6 frames. Revisar:
   - pies en la misma línea base;
   - pivot abajo al centro;
   - misma altura (47 px);
   - colores fuera de la paleta del GRH original = 0 (en 1×).
2. **Spell Tester (v1.3):** hoy el lanzador es una imagen fija (`Tools/SpellTester/index.html:28`, `assets/ui/player.png`). Propuesta (Arte):
   - modo "lanzador 8 direcciones": carga el overlay, mira al sector del mouse mientras vuela el proyectil y muestra el cuadro frame a frame con zoom y la cruz del pivot;
   - encaja con lo que ya pide la v1.3.
3. **Unity**, con `aod-respaldo` antes, `useDiagonals` solo en el jugador y los datos solo del piloto:
   - caminar las 8 direcciones con el teclado y con clic (MOBA): sin salto de pivot al pasar S→SE→E y con la caminata continua al girar;
   - skill shots a 16 ángulos alrededor: sin parpadeo cerca de 45°;
   - equipar un arma **sin** diagonales: todo el personaje vuelve a la cardinal;
   - atacar y castear en diagonal: se ve el ciclo de 6 frames;
   - orden de arma y escudo en cada diagonal;
   - online con 2 clientes: el remoto se ve en diagonal y el cliente viejo sigue en 4;
   - guardar y cargar: el heading guardado sigue en 1–4 (7 guardados intactos).
4. **QA automatizada** (sector QA, estilo `AOModulesQA270`):
   - `SetHeading(5..8)` se ignora y `Heading` queda en 1–4;
   - con diagonal incompleta, `ResolveHeading()` = cardinal;
   - con `useDiagonals=false`, el render es idéntico al de hoy.

**Criterio para seguir:** Lucas lo ve en juego y lo aprueba. Recién ahí se estima el lote grande (sets del jugador, por orden de uso).

## 7. Riesgos
- **Fidelidad:** las 8 direcciones **no existen en el AO original** y la regla es "fiel al original". Mitigación:
  - opción desactivable, por defecto en 4;
  - los datos originales no se tocan (overlay aparte).
- **Coherencia de capas:** son 5 capas combinables (237 cuerpos × 74 armas × 40 escudos × 162 cascos × 374 cabezas). Si cada diagonal se hace suelta, las manos no calzan con el arma. Hace falta un esqueleto de referencia común y el fallback todo o nada.
- **Resolución mezclada:** diagonales en HD junto a cardinales en 1× se notan al girar. Hacerlas en la misma resolución que el resto (hoy 1×; en HD, junto al remaster).
- **IA:** frames incoherentes, siluetas que cambian, paleta que no es la de AO. La IA no separa capas. Con 27×47 px, casi todo termina en retoque a mano.
- **Herramientas bloqueadas:** Higgsfield sin créditos. ComfyUI sin instalar (descarga grande, requiere OK).
- **Costo al escalar:** ~10.500 frames. En la nube, ≈ 2–4k créditos (estimado); a mano, muchas horas.
- **Visual distinto de la lógica:** el personaje mira al NE y el golpe va al N o al E. Aclarar la regla (punto 5) antes de implementar.
- **Parpadeo** en los límites de 45° (mouse y pathfinding): hace falta histéresis.
- **Online:** sin cambio de protocolo. El remoto se infiere del salto de tile. El casteo remoto queda en cardinal hasta que exista `castX/castY`.
- **Guardados:** el heading visual no se guarda (no cambia el formato). Igual, respaldo antes de probar.
- **Memoria:** hasta 2× frames por personaje con diagonales. Despreciable en el piloto.
- **Sectores:**
  - renderer, constructores y jugador: Programación;
  - overlay, sprites y Spell Tester: Arte;
  - preferencia 4/8: Interfaz;
  - remotos: Servidor;
  - prueba: QA.
  - Cerebro integra.

---

## Pregunta para Lucas
**¿Querés que los personajes se vean en 8 direcciones, aunque el AO original tenga solo 4?**
1. **No:** quedan en 4 y solo se mejora cuál se elige (opción a: al tirar un skill shot el personaje mira hacia donde apunta). Costo 0 y 100 % fiel.
2. **Sí, como opción:** una preferencia "Direcciones: 4 (original) / 8", que por defecto queda en 4. Se hace el piloto de 52 frames (body 1 + head 1 + Espada Larga).
3. **Sí, siempre:** 8 direcciones para todos, con fallback a 4 donde falten sprites.

Y aparte: **al lanzar un skill shot, ¿el personaje gira de verdad** (cambia hacia dónde pega el cuerpo a cuerpo siguiente) **o solo visualmente?**
