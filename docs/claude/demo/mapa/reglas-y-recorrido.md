# Mapa de la demo: reglas y recorrido (nube, 25/09)

**Estado:** Lucas aprobó las reglas el 25/09.
- El mapa ya construido está en la PC y **no está en GitHub**: `Tools/demo_map_builder.py`, `Tools/demo_maps/` y los mapas 1000 en adelante.
- Hay que subirlo a la rama `pc/demo-mapas` para aplicarle estas reglas.
- Dueños: Programación (builder y especificaciones), Arte (sets) y Contenido (NPC). Este documento **reemplaza la tabla de pisos** de `arte.md` §4 y el orden de `progresion.md` §4. **No cambia ningún NPC.**

## Reglas aprobadas
**Recorrido**
1. **Hub (1000):** se aparece en el centro, en zona segura. **Arenas al norte y Dungeon al sur**, cada salida con su cartel.
   - Esto resuelve la contradicción entre `arquitectura.md` (norte) y `arte.md` (este).
2. **Toda salida tiene vuelta.** Cada piso tiene un **atajo al hub** en su entrada (portal o cartel).

**Dungeon con lógica**

3. **Siempre bajando:** superficie → subsuelo → profundidades. Nada de aire libre ni torres a mitad de camino.
4. **Cada piso con un set visual distinto.**
5. **Estructura de cada piso:** entrada segura de 8 casillas sin NPC → salas con grupos → escalera de bajada al fondo, lejos de la entrada.
6. **Sala del jefe** de 12×12, sin obstáculos grandes adentro.

**Coherencia visual**

7. Solo gráficos originales y **decoración acorde**: nada de árboles en cuevas, lava solo en el volcánico, agua solo donde tenga sentido.
8. **Luz según la profundidad:** la superficie más clara y abajo cada vez más oscuro, con antorchas en los caminos.

**Arenas**

9. **4 rings alrededor de una plaza**, con gradas. Los rings están cerrados: se entra por teletransporte.
10. **Cada ring con su tema** (bosque, desierto, nieve, mazmorra) y el nombre en un cartel.

**Límites**

11. **No se tocan** stats, EXP ni drops: solo el mapa, las posiciones y la cantidad de NPC.
12. **Mapas de 100×100 como máximo;** pisos de ~70×70. Un mapa original entero puede usarse si entra en 100×100.

## Recorrido del dungeon (cómo se aplica la regla 3)
Primero se propuso reordenar solo los gráficos. Así, las arañas quedaban en una cripta y los liches en una cueva.

**Ajuste:** cada tramo de nivel conserva sus NPC (`dungeon-npcs.json`, sin cambios) y va al mapa original **donde esos NPC viven en el AO**. El orden siempre baja y cada piso tiene un set distinto.

![recorrido](recorrido_dungeon.jpg)

| Mapa demo | Piso | Nivel | NPC (sin cambios) | Mapa fuente | Por qué |
|---|---|---|---|---|---|
| 1010 | **Entrada: Cementerio de Nix** (superficie, de noche) | — | ninguno hostil | 4 | La capilla (21,44) es la bajada; en el original baja al Mausoleo. Reemplaza el mapa de paso sin contenido. |
| 1011 | **P1 Dungeon Newbie** | 1–4 | ratas, murciélagos, serpientes, escorpiones, lobos, Wolfang | 264 / 37 / 168 | Es el newbie dungeon original: el hábitat de esos NPC. Piedra azul y muros rosados. |
| 1012 | **P2 Catacumbas** | 4–10 | esqueletos, zombies, Humano No-Muerto | 40, 45, 41–44 | No-muertos bajo el cementerio. Cueva de tierra con telarañas. Son mapas chicos: el piso se arma con `stamp` de varios. |
| 1013 | **P3 Mausoleo** | 10–15 | esqueletos mágicos, liches menores, Guardián | 392 entero (~76×84) | Ya es un circuito, con 103 luces y 120 partículas. |
| 1014 | **P4 Tumba del desierto** | 15–20 | escorpiones, escarabajos, Momia | 564 | Interior de la pirámide, presentado bajo tierra. Recortar ~70×70 y abrir un circuito, porque el laberinto entero cansa. Sala central (x41–55, y28–45) para la Momia. |
| 1015 | **P5 Nido de arañas** | 20–24 | arañas, Mutante Arácnido | 291–293 + sala 323 | 323 es la sala original del jefe araña ("Nido Arácnido Boss"). Se estampa al final. |
| 1016 | **P6 Cueva de las Gorgonas** | 24–27 | liches, magos malvados, orco brujo, Medusa | 311 | La Medusa es una gorgona. Musgo con agua. Sala del jefe en la plataforma sur (x35–62, y77–89). |
| 1017 | **P7 Magma (Guarida del Dragón)** | 27–30 | dragones chicos, Vytaiz | 366 / 367 (365–368) | Roca volcánica y lava: el único piso rojo, el más profundo, iluminado por la lava. |

**Descartados:** Dungeon Veriil (140–142), Dungeon Dragon (391, 377) y Limbo (314).
- Tienen el mismo piso de musgo que la Cueva de las Gorgonas: por eso P6 y P7 se veían iguales.
- 391 y 377 son salas vacías.

## Luz (regla 8)
- `baseLight` va de más claro a más oscuro: Entrada (noche con luna) → P1 → … → P6, el más oscuro.
- P7 es oscuro, pero con la luz roja de la lava.
- Se parte de la luz del `map_environment.json` de cada mapa fuente y se oscurece paso a paso.
- Antorchas 55254 en el camino del circuito y en la entrada segura.

## Pendiente (con los archivos de la PC)
- **Hub 1000:** revisar la orientación (regla 1) y los carteles.
- **Arenas 1001:** 4 rings, plaza y gradas según el módulo de `arte.md` §2. No la copia del mapa 324 con agua.
- **Aplicar el recorrido** a `Tools/demo_maps/*.json` y regenerar con `demo_map_builder.py`.
  - Comprobar con `vista_mapa.py` y con el `--check` del builder.
  - QA D-20: toda salida tiene su vuelta.
- **Contenido:** sin cambios en los NPC; solo cambian el nombre y el mapa fuente de cada piso. La EXP, el oro y el respawn no se tocan.
- **Arte:** tomar esta tabla en vez de `arte.md` §4 para los sets de cada piso.

## Herramienta
`python Tools/demo_mapa/vista_mapa.py SALIDA.png --mapa N [--px 8]`

Vista general de un mapa, con:
- bloqueos en rojo;
- salidas en verde;
- NPC en amarillo;
- luces en cian;
- una grilla cada 10 casillas.

Sirve para mapas originales y de demo, y solo lee datos.
