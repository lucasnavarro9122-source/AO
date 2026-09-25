# Remaster HD del dungeon (nube, 25/09)

Herramientas: `Tools/hd_remake/dungeon_hd.py` (preparar, costo, importar) y `Tools/hd_remake/dungeon_vista_p1.py` (vista sin Unity).
Referencias de Lucas: `referencias/`. Vistas por ronda: `dungeon/`.

## P1 (mapa 1011), ronda 5: 100 % el estilo de su imagen
Lucas pidió igualar su imagen (`referencias/p1_objetivo_lucas.webp`) hasta en las piedras del piso y la luz. Su imagen es este mismo mapa remasterizado, casilla por casilla, así que el material sale de ahí:

- **Piso (`piso_desde_referencia`):**
  - se le saca la luz a su imagen y se arma una biblioteca con los ladrillos de 7 zonas de piso;
  - se descartan los que caen bajo un haz de luz;
  - con esa biblioteca se arman 8 bloques con aparejo trabado y la paleta de su piso.
- **Bordes compatibles (`bordes_piso(v)`):**
  - la variante v tiene borde izquierdo `v % 2` y derecho `(v // 2) % 2`;
  - a la derecha de un bloque con borde derecho t va uno con borde izquierdo t;
  - así las filas trabadas no tienen costura. El builder lo tiene que respetar.
- **Paredes (`paredes_desde_referencia`):**
  - la vista 1x del mapa se registra contra su imagen (`registro` en `PISOS["P1"]`, medido con `registrar_referencia`);
  - cada pieza de `tex_5095` sale de la mediana de sus apariciones visibles, y el brillo de color pintado se neutraliza;
  - las 4 esquinas que su imagen no muestra se completan por analogía con parches de las piezas que sí se ven;
  - la silueta y el tamaño son los originales.
- **Decoración (`tex_90002`, 512x512 a 1x):**
  - y 0: 6 escombros de 64x64, con la piedra de sus paredes;
  - y 64: 3 jirones de niebla de 128x64;
  - y 128: 2 haces de luz de luna de 128x256;
  - x 256: halo celeste de 160 (brillos) y violeta de 224 (portal).
- **`compensar_luz` 1,6:** el juego (luz Original) dibuja textura x luz con tope 1, y el P1 tiene luz base 0xA0A0A0 (0,627). Con x1,6, lo que no tiene luz se ve como su imagen y bajo la luz de luna llega a su brillo.

## Luces del P1 (ronda 1 de luces, 25/09)
Cómo dibuja la luz el juego (estudiado en el código):
- **Original** (por defecto): `AOMapLighting` + `AOMapVertexLit`.
  - Cada sprite = textura x la luz de las 4 esquinas de la casilla donde se apoya, estirada sobre todo el sprite; tope 1.
  - Una luz de color multiplica (el azul puro oscurece) y tiñe entera cada pieza de pared alta que se apoye cerca: se ve un rectángulo.
- **Mejorada:** `AOLighting2DV283`, luces 2D sumadas por píxel más la viñeta.
  - El bloom está apagado (`PostProcessing = false`, por el bug de la cámara recortada).

Qué se hizo:
- **Brillos azules (`FLOOR_GLOW`):**
  - celeste 0x5A96FF, radio 1, dos casillas lejos de la pared (antes: azul puro al pie, teñía el friso);
  - el resplandor lo da un halo celeste pintado.
- **Haces de luna:**
  - 38 al azar, fijos por mapa y separados entre sí;
  - cada uno con una luz de luna 0xC8D2F5 donde cae al piso, así en Original el haz entero se ilumina;
  - radio 1, o sin luz, si cerca se apoya una pared alta.
- **Portal:** halo violeta grande.
- **Luz Mejorada:** las luces frías y casi blancas (azul > rojo en 0,1 o más, saturación < 0,25) van a intensidad 0,55 en vez de 1,05.
  - Si no, varias juntas se queman.
  - En todo el juego eso son solo las 32 de luna del P1 y una violeta suave del mapa 370.
  - Copiado en `preview_luces.py` y `dungeon_vista_p1.py`.
- **Vista:** `dungeon_vista_p1.py` ahora dibuja como el juego en los dos modos, con el mapa armado y las texturas instaladas. Antes multiplicaba x1,35 de más.

Todo lo arma el builder (op `hd_remaster` de `demo_art_specs.HD_REMASTER`, función `demo_map_builder.hd_remaster`):
- variantes con bordes compatibles (35 % la base);
- escombros en el 24 % de las casillas al pie de pared;
- niebla en el 14 %;
- haces y halos.

Sprites propios con id desde 900000 en la lista del mapa. Sin las texturas instaladas, no hace nada.

Instalado:
- `tex_5095`, `tex_90001` y `tex_90002` en `Resources/AOMigratorHD`;
- `tex_90001` y `tex_90002` a 1x en `Resources/AOMigrator`;
- `map_1011` reconstruido.

Los `.meta` los genera Unity (`AOHDTextureImportV279` y `AOMapTextureImport`).

`tex_5095` HD también la usan los Newbie Dungeon originales (37, 167, 168, 264; luz 0,56). En las costas 78 y 80 solo se usan los cristales, que no cambiaron.

## El resto del P1: adornos y efectos (25/09, noche)
- **Adornos (`dungeon_hd.py adornos P1`):** altar, estandarte, antorcha y rocas de las salidas selladas (dos versiones), remasterizados con Higgsfield.
  - 1 hoja, 2,5 créditos; job `a816ae67`.
  - Misma silueta y tamaño que el original; sombras originales.
  - Brillo x1,05, porque la hoja ya sale al nivel de las paredes HD.
  - La mancha marrón de escombros se recoloreó con la paleta fría del piso.
  - Van en `tex_90003`, propia de la demo: las texturas originales (5034, 105, 5066, 5041) se usan en muchos mapas y no cambian.
  - El builder cambia los sprites solo en el P1 (`reemplazos` de `HD_REMASTER`).
- **Partículas** (aditivas: brillan igual con las dos luces). Son propias de la demo, en `particle_migration.DEMO_PARTICLES`, y ya están agregadas a `particle_defs.json`:
  - 9001: chispas violetas del portal;
  - 9002: polvo de luna que cae por los haces (1 de cada 2);
  - 9003: chispas en la llama de la antorcha.

  Se descartaron las del original:
  - 52: explosión de portal; fija se quema en blanco;
  - 245: puffs grandes;
  - 246: su llama cae fuera de esta antorcha;
  - 199: motas en un área de 22 casillas, que caen sobre el vacío negro.
- **Haces:** 24 en vez de 38, más separados (de lejos parecían lluvia).
- **Vista:** `dungeon_vista_p1.py` suma las partículas como foto fija aproximada; en el juego se mueven.

### Falta (PC)
- Compilar en Unity (se cambió `AOLighting2DV283.cs`; en la nube no hay proyecto auxiliar), importar las texturas nuevas (`tex_5095` HD, `tex_90001`, `tex_90002`, `tex_90003`) y probar el P1 en Play con las dos luces: altar, portal (chispas), antorchas y haces (polvo de luna).
- Paquete para CEREBRO (con OK de Lucas).
