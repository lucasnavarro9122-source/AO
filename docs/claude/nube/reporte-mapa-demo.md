# Reporte a los sectores: mapa demo con sentido (nube, 25/09 tarde)

Pedido de Lucas: "analizá el mapa demo y modificalo para que tenga más sentido". Aprobó 12 reglas.
- Detalle: `docs/claude/demo/mapa/reglas-y-recorrido.md`.
- Imagen: `docs/claude/demo/mapa/antes_despues.jpg`.
- Base: `pc/demo-mapas` (edcd99d5), integrado a `claude/nifty-thompson-r3ulpf`.

## Cómo traerlo a la PC (CEREBRO)
```powershell
git fetch origin; git merge origin/claude/nifty-thompson-r3ulpf
```
- El merge toca archivos de la demo que en la PC estaban limpios: `Tools/demo_maps/`, `dungeon-npcs.json`, mapas 1000 y 1010–1017, minimapas, `map_music`, `map_environment` y `catalog.json.gz`.
- **No toca** `tablero.md`, `pruebas.md` ni `r4-checklist.md`, que en la PC tienen cambios sin commit.
- Tomar el candado de Unity al integrar: se escriben `Assets/`.

## Qué cambió (sin tocar ningún NPC ni sus stats)
| Mapa | Antes | Ahora |
|---|---|---|
| 1010 Entrada | pasillo de paso del Newbie | **Cementerio de Nix** en la superficie; la capilla baja al P1 |
| 1011 P1 | Madriguera recortada | **Dungeon Newbie** entero |
| 1012 P2 | Cementerio al aire libre | **Catacumbas** (mapa 40) |
| 1013 P3 | Mausoleo recortado | Mausoleo entero |
| 1014 P4 | Pirámide recortada, 338 casillas hasta el jefe | **Tumba del desierto**: se entra por la puerta real y la Momia está en la cámara funeraria |
| 1015 P5 | Nido recortado | Nido entero |
| 1016 P6 | Torre de Veriil (mismo musgo que el P7) | **Cueva de las Gorgonas** (311): Medusa en el lago |
| 1017 P7 | sala vacía de musgo (391) | **Magma** (366): lava, roca y Vytaiz en la cámara norte |

- La luz baja según la profundidad, de B4BED2 a 646E82; el P7 es rojo por la lava.
- El hub y las arenas ya cumplían las reglas: no se tocaron. En el hub solo cambia la llegada al 1010.

## Por sector
- **Programación:**
  - `Tools/demo_maps/1010.json` es nuevo (base: mapa 4) y 1011–1017 tienen carteles nuevos.
  - Los mapas se escribieron con `write_assets` sin pasar por `lock_allows_write`, porque el Unity de la PC no se usó; está documentado.
  - `--check` da igual.
- **Contenido:**
  - `dungeon-npcs.json`: cambian solo `nombre`, `origen`, `mapa` (fuente, recorte, escaleras, sala del jefe, música y luz) y los centros y el orden de las `zonas`.
  - En los pisos que cambiaron de fuente, sacamos los campos que medían el recorrido viejo (`recorridoMaxTiles`, etc.): volver a medirlos.
  - `textos-hub.md` quedó actualizado (§3, §4 y §6).
  - `modelo_progresion.py --check` da OK.
- **Arte:**
  - `demo_art_specs.py`: `OUTDOOR` queda vacío; `spec_1010` pasa al cementerio (pinos en los cortes y un cartel al camino, 50876); se saca el cartel de Veriil.
  - Revisar en Unity la luz nueva y los carteles.
- **Servidor:**
  - `catalog.json.gz` se regeneró: cambian solo los 9 mapas de la demo, y el resto es idéntico byte a byte (con el byte de sistema de Windows).
  - Cambia `npcLayoutVersion`, así que el servidor reinicia los NPC de esos mapas.
  - Publicar servidor y cliente **juntos**.
- **QA:**
  - Pasan en la nube: `ci_checks`, el build del servidor y 6 pruebas (coop, NPC fijos, retos, robustez, visión y pérdida al morir).
  - **Falta en Unity:** recorrido completo 1000 → 1010 → P1…P7 → portal (D-20), la luz y los minimapas.

## Herramienta nueva
`Tools/demo_mapa/vista_mapa.py SALIDA.png --mapa N | --archivo map_N.json` dibuja la vista general de un mapa con bloqueos, salidas, NPC, luces y una grilla.
