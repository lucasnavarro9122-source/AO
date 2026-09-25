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
  - y 64: 3 nieblas de 128x64;
  - y 128: 2 haces de luz de luna de 128x256, en la capa 2 sobre piso libre (las paredes los tapan).
- **`compensar_luz` 1,18:** su imagen es la pantalla final, y el P1 tiene luz base 0xA0A0A0.

Reglas de ubicación (hoy en `dungeon_vista_p1.py`; el builder las tiene que copiar):
- variante por bloque de 4x4 con bordes compatibles, 35 % la base;
- escombros en el 24 % de las casillas junto a pared;
- niebla en el 14 %, apoyada una casilla más abajo;
- haces de luz cada ~5x7 casillas, solo si toda su huella de 4x8 es piso libre.

Costo de la ronda 5: 0 créditos (usa lo generado en la ronda 4). Total P1: ~52,5 créditos.

### Falta para que se vea en el juego
- Builder: GRH propios de la demo para `tex_90001` (variantes) y `tex_90002` (decoración), con las reglas de arriba.
- `importar P1 ... --aplicar`, con `.meta` iguales a los HD existentes (Point, sin mipmaps, sin compresión).
- Reconstruir mapas y catálogo, pruebas, paquete para CEREBRO.
