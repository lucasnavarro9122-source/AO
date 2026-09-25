"""Genera la parte de arte de los mapas de demo: Tools/demo_maps/{id}.art.json (Arte).

Formato acordado con Programación: {"ops": [...]} con stamp, paint y erase, en coordenadas
de mapa (1..100). El builder aplica: base -> estas ops -> la estructura de Programación
(block, unblock, trigger, exit, npcs). "decorBloqueante" lista las celdas con objetos
altos que conviene bloquear (lo decide la estructura).

Uso:
  python Tools/demo_art_specs.py                 # escribe todos los .art.json
  python Tools/demo_art_specs.py --preview DIR   # además simula base + ops y dibuja DIR/{id}.png
"""

from pathlib import Path
import argparse
import copy
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Tools/demo_maps"
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"

# --- GRH originales (ver docs/claude/demo/arte.md) ---
WATER = 1505             # agua, set 4x4
PAVING = 58114           # empedrado gris, set 3x3
CARPET = 1753            # alfombra roja: relleno 2x2 = 1753..1756
POST_LEFT, POST_RIGHT, POST_MID, POST_PLAIN = 48863, 48862, 48858, 48860
ROPE_H = [48854, 48855, 48856, 48857]
ROPE_V = [48868, 48869]
BENCH = 2620              # banco de madera original (se puede pisar: sentarse)
BANNER_BLUE, BANNER_RED = 58712, 60298
TORCH = 55254
BOARD = 19543            # cartelera
ARENA_SIGNS = [(2441, 50902), (2442, 50903), (2443, 50904), (2447, 50908)]   # "Arena I..IV" (objIndex, grh)
FLOOR_SIGNS = {f"P{i}": 50848 + i for i in range(1, 8)}   # gráfico "Nº1..Nº7" (50849-50855); solo el gráfico:
                                                           # los OBJ 2406-2412 son carteles de casas y su texto no sirve
FLOOR_EXTRA_SIGNS = {"P7": (1136, 50881)}   # original completo ("Dungeon Dragon"); el de Veriil ya no va: P6 es la Cueva de las Gorgonas
NEWBIE_SIGN = 50925      # gráfico "Bienvenido al Newbie Dungeon" (el texto del objeto lo pone Contenido)
EMPTY_SIGN = 50876       # cartel vacío (arte.md §3): el texto lo pone Contenido


def sign(x, y, role, grh, obj=None):
    """Cartel = objeto clicable (ObjType 8) que pone la estructura. Con objIndex va el objeto original completo
    (su texto es el original); sin objIndex, solo el gráfico con el texto de textos-hub.md.
    Arte solo propone posición y gráfico: no se pinta, para no duplicarlo."""
    entry = {"x": x, "y": y, "rol": role, "grh": grh}
    if obj:
        entry["objIndex"] = obj
    else:
        entry["texto"] = "textos-hub.md"
    return entry
THEME_FLOOR = {"Bosque": (6000, 4), "Desierto": (7704, 4), "Nieve": (7380, 4), "Mazmorra": (9428, 4)}


def rect(x0, y0, x1, y1):
    return [x0, y0, x1 - x0 + 1, y1 - y0 + 1]


def perimeter(x0, y0, x1, y1):
    cells = [(x, y0) for x in range(x0, x1 + 1)] + [(x, y1) for x in range(x0, x1 + 1)]
    cells += [(x0, y) for y in range(y0 + 1, y1)] + [(x1, y) for y in range(y0 + 1, y1)]
    return cells


def paint(layer, cells=None, box=None, **tile):
    op = {"op": "paint", "layer": layer, **tile}
    if box is not None:
        op["rect"] = box
    else:
        op["cells"] = [list(c) for c in sorted(set(cells), key=lambda c: (c[1], c[0]))]
    return op


def rope_border(x0, y0, x1, y1):
    """Borde de cuerdas y postes como el ring original de la Arena de Clanes (mapa 272)."""
    grh = {}
    for y in (y0, y1):
        grh[(x0, y)], grh[(x1, y)] = POST_LEFT, POST_RIGHT
        x = x0 + 1
        while x < x1:
            span = min(4, x1 - x)
            pieces = ROPE_H if span == 4 else ROPE_H[:span - 1] + [ROPE_H[3]]
            for i, piece in enumerate(pieces):
                grh[(x + i, y)] = piece
            x += span
            if x < x1:
                grh[(x, y)] = POST_MID
                x += 1
    for x in (x0, x1):
        for i, y in enumerate(range(y0 + 1, y1)):
            grh[(x, y)] = POST_PLAIN if (y - y0) % 5 == 0 else ROPE_V[i % 2]
    return grh


# Arenas (1001): todo sale de Banderbill, la ciudad original con este mismo empedrado (58114), para que combine.
STREET_LAMP = 2460        # farol de hierro doble de las calles de Banderbill
PLANTERS = [21721, 21720, 21719]   # canteros de piedra con arbustos (sobre el empedrado de Banderbill)
POTTED = [4544, 4545, 4546]        # árboles en maceta de madera (4546 con manzanas)
BAR = 12307               # barra de taberna con barriles y botellas (160x128)
FOOD_TABLE = 2100         # mesa de provisiones: pan, queso, pollo (160x64)
BARREL, CAULDRON, FIREWOOD, BRAZIER = 2066, 2093, 2214, 1178
ARENA_WARM = 0xFFE6C0    # la arena tiene luz de día fija (baseLight -1): una luz naranja la mancharía de marrón
RAIL_N = {0: 58254, 1: 58255, 2: 58255}          # baranda de canal sobre el agua al norte del empedrado (poste, listón, listón)
RAIL_W = {0: 58257, 1: 58385, 2: 58382}          # agua al oeste del empedrado
RAIL_E = {0: 58318, 1: 58256, 2: 58321}          # agua al este (al sur del empedrado Banderbill no pone baranda)


def canal_rails(paved):
    """Baranda de postes de piedra y listones de madera en el borde del agua, como los canales de Banderbill."""
    rails = {}
    for x in range(1, 101):
        for y in range(1, 101):
            if (x, y) in paved:
                continue
            if (x, y + 1) in paved:
                rails[(x, y)] = RAIL_N[x % 3]
            elif (x + 1, y) in paved:
                rails[(x, y)] = RAIL_W[y % 3]
            elif (x - 1, y) in paved:
                rails[(x, y)] = RAIL_E[y % 3]
            elif (x + 1, y + 1) in paved or (x - 1, y + 1) in paved:
                rails[(x, y)] = RAIL_N[0]                # poste de piedra en la esquina
    return rails


def spec_1001():
    """Zona de arenas: copia de 324 con 4 rings, pasillo de alfombra, gradas con bancos y una plaza con puestos."""
    rings = [  # interior (Retos.dat), tema (arquitectura §2.3)
        ((13, 11, 35, 29), "Bosque"), ((65, 11, 87, 29), "Desierto"),
        ((13, 73, 35, 91), "Nieve"), ((66, 73, 88, 91), "Mazmorra"),
    ]
    plaza = (37, 31, 63, 71)
    band = 4                                                                # grada: 4 casillas de empedrado + el pasillo de alfombra
    ops = [{"op": "erase", "layer": layer, "rect": rect(1, 1, 100, 100)} for layer in (2, 3, 4)]
    ops.append(paint(1, box=rect(1, 1, 100, 100), tile4x4=WATER))
    ops.append(paint(1, box=rect(*plaza), tile3x3=PAVING))
    paved = {(x, y) for x in range(plaza[0], plaza[2] + 1) for y in range(plaza[1], plaza[3] + 1)}
    layer2, layer3, blocking, signs, lights = {}, {}, [], [], []
    for index, ((ix0, iy0, ix1, iy1), theme) in enumerate(rings):
        bx0, by0, bx1, by1 = ix0 - 1, iy0 - 1, ix1 + 1, iy1 + 1           # borde (cuerdas)
        ox0, oy0, ox1, oy1 = bx0 - 1 - band, by0 - 1 - band, bx1 + 1 + band, by1 + 1 + band   # borde externo de la grada
        ops.append(paint(1, box=rect(ox0, oy0, ox1, oy1), tile3x3=PAVING))
        paved |= {(x, y) for x in range(ox0, ox1 + 1) for y in range(oy0, oy1 + 1)}
        base, side = THEME_FLOOR[theme]
        ops.append(paint(1, box=rect(ix0, iy0, ix1, iy1), **{f"tile{side}x{side}": base}))
        layer3.update(rope_border(bx0, by0, bx1, by1))
        for x, y in perimeter(bx0 - 1, by0 - 1, bx1 + 1, by1 + 1):       # pasillo
            layer2[(x, y)] = CARPET + (y % 2) * 2 + (x % 2)
        cx, cy = (ox0 + ox1) // 2, (oy0 + oy1) // 2
        # Gradas: bancos dobles (dos 2620 juntos) mirando al ring, separados para que no parezcan una cerca;
        # afuera, canteros y árboles en maceta.
        for y in range(oy0 + band, oy1 - band + 1, 2):                      # costados oeste y este: una fila cada 2
            if abs(y - cy) > 3:                                             # el estandarte (4 de alto) no tapa bancos
                for x in (ox0 + 1, ox0 + 2, ox1 - 1, ox1 - 2):
                    layer3[(x, y)] = BENCH
        for x in range(ox0 + band, ox1 - band + 1):                         # norte y sur: de a dos, con un hueco
            if (x - ox0) % 3 and abs(x - cx) > 1:
                for y in (oy0 + 2, oy1 - 2):
                    layer3[(x, y)] = BENCH
        plants = [(x, y) for y in range(oy0 + band + 2, oy1 - band, 6) if abs(y - cy) > 2 for x in (ox0, ox1)]
        plants += [(x, y) for x in range(ox0 + band + 2, ox1 - band, 6) if abs(x - cx) > 2 for y in (oy0, oy1)]
        for i, cell in enumerate(plants):
            layer3[cell] = (PLANTERS if cell[1] in (oy0, oy1) else POTTED)[i % 3]
            blocking.append(cell)
        layer3[(ox0 + 1, cy)], layer3[(ox1 - 1, cy)] = BANNER_BLUE, BANNER_RED    # equipo A (oeste) / B (este)
        top = index < 2
        plaza_side_x = ox1 if ix0 < 50 else ox0
        sign_y = oy1 if top else oy0
        corners = [(ox0, oy0), (ox1, oy0), (ox0, oy1), (ox1, oy1)]
        signs.append(sign(plaza_side_x, sign_y, f"entrada ring {index + 1}", ARENA_SIGNS[index][1], ARENA_SIGNS[index][0]))
        for corner in corners:
            if corner != (plaza_side_x, sign_y):
                layer3[corner] = TORCH
                lights.append(light(*corner, ARENA_WARM, 3))
        blocking += [(ox0 + 1, cy), (ox1 - 1, cy)] + corners
    px0, py0, px1, py1 = plaza
    for x in (px0 + 3, px1 - 3):                                            # faroles de Banderbill, con luz
        for y in range(py0 + 8, py1 - 7, 8):
            layer3[(x, y)] = STREET_LAMP
            blocking.append((x, y))
            lights.append(light(x, y, ARENA_WARM, 3))
    board = ((px0 + px1) // 2, (py0 + py1) // 2)
    layer3[board] = BOARD
    # Puestos (los vendedores los pone Programación en 1001.json, delante de cada puesto: se les habla de al lado).
    stall_y = 44
    drinks, food = (44, stall_y), (56, stall_y)
    layer3[drinks] = BAR                                                     # taberna: agua, cerveza, jugo y vino
    layer3[(drinks[0] - 3, stall_y)] = BARREL
    layer3[food] = FOOD_TABLE                                                # provisiones: pan, queso, fruta, pollo
    layer3[(food[0] - 3, stall_y)] = FIREWOOD
    layer3[(food[0] + 3, stall_y)] = CAULDRON
    blocking += [(x, stall_y) for x in range(drinks[0] - 3, drinks[0] + 3)] + [(x, stall_y) for x in range(food[0] - 3, food[0] + 4)]
    for spot in ((drinks[0] + 3, stall_y + 1), (food[0] - 3, stall_y + 1)):   # braseros al lado de cada puesto
        layer3[spot] = BRAZIER
        blocking.append(spot)
        lights.append(light(*spot, ARENA_WARM, 3))
    lights.append(light(food[0] + 3, stall_y, ARENA_WARM, 2))                # el fuego del caldero
    # Descanso: bancos frente a los puestos y a los lados de la cartelera, canteros y árboles en maceta.
    for y in (stall_y + 4,):
        for x in (drinks[0] - 3, drinks[0] - 2, drinks[0] + 2, drinks[0] + 3, food[0] - 3, food[0] - 2, food[0] + 2, food[0] + 3):
            layer3[(x, y)] = BENCH
    for y in (board[1] + 6, board[1] + 8):
        for x in (board[0] - 6, board[0] - 5, board[0] - 3, board[0] - 2, board[0] + 2, board[0] + 3, board[0] + 5, board[0] + 6):
            layer3[(x, y)] = BENCH
    greenery = {(board[0] - 3, board[1]): PLANTERS[0], (board[0] + 3, board[1]): PLANTERS[1],
                (drinks[0], stall_y + 7): POTTED[2], (food[0], stall_y + 7): POTTED[2],
                (board[0] - 8, board[1] + 10): POTTED[0], (board[0] + 8, board[1] + 10): POTTED[1],
                (board[0] - 5, py1 - 2): PLANTERS[2], (board[0] + 5, py1 - 2): PLANTERS[0]}
    for cell, grh in greenery.items():
        layer3[cell] = grh
        blocking.append(cell)
    exit_x, exit_y = 50, py1 - 1                       # salida al hub (Programación): una casilla adentro, el halo no cae al agua
    for side in ((exit_x - 2, exit_y), (exit_x + 2, exit_y)):
        layer3[side] = TORCH
        blocking.append(side)
    layer3[(exit_x, exit_y)] = PORTAL                  # la salida se ve: portal original con luz
    blocking.append(board)
    layer3.update(canal_rails(paved))                  # sobre el agua (ya bloqueada): no cambia el paso
    for grh in sorted(set(layer2.values())):
        ops.append(paint(2, [c for c, g in layer2.items() if g == grh], grh=grh))
    for grh in sorted(set(layer3.values())):
        ops.append(paint(3, [c for c, g in layer3.items() if g == grh], grh=grh))
    ops += lights + [light(exit_x, exit_y, PORTAL_LIGHT, 4),
                     light(exit_x - 2, exit_y, ARENA_WARM, 3), light(exit_x + 2, exit_y, ARENA_WARM, 3)]
    return {
        "map": 1001,
        "autor": "Arte",
        "base": "copy 324 (Programación)",
        "notas": "Rings de Retos.dat; borde de cuerdas f5067 como la Arena de Clanes (272); pasillo de alfombra 1753-1756. "
                 f"Gradas de {band} casillas de empedrado con bancos dobles (2620) mirando al ring, separados, "
                 "canteros y árboles en maceta afuera, estandarte azul (oeste, equipo A) y rojo (este, B), antorchas con luz. "
                 "Plaza: faroles de hierro, puesto de bebidas (barra 12307) y de provisiones (mesa 2100, caldero, leña), "
                 "braseros, bancos y canteros. Luces blanco cálido: la arena tiene luz de día fija. Todo el decorado es de Banderbill, la ciudad original con este empedrado; "
                 "el borde del agua lleva su baranda de canal. Los bancos se pueden pisar (sentarse); lo alto bloquea. "
                 "Fila de arriba de cada ring sin objetos altos (cartel de estado de Interfaz). Resto: agua.",
        "ops": ops,
        "decorBloqueante": [list(c) for c in sorted(set(blocking), key=lambda c: (c[1], c[0]))],
        "carteles": signs,
    }


ROAD = {  # camino de tierra vertical de 4 de ancho (Ullathorpe x51-54), por fila y % 4 del mapa fuente
    0: [6376, 6369, 6370, 6379], 1: [6380, 6373, 6374, 6383],
    2: [6368, 6377, 6378, 6371], 3: [6372, 6381, 6382, 6375],
}
GRASS = 6000
PINE = 12160
DUNGEON_DOOR = 1493      # "Puerta cerrada con llave Catas Ullathorpe" (96x96)
PORTAL = 49488           # teleport original (objeto tipo 19, 8 cuadros): toda salida de la demo lo usa (pedido de Lucas, 25/09)
PORTAL_LIGHT = 0xB47CFF  # luz violeta del portal (RRGGBB, como las luces de los mapas)
TORCH_LIGHT = 0xFFB45A   # luz cálida de antorchas y puertas


def light(x, y, color, radius):
    """Luz redonda del mapa, con radio en casillas. En AOMapLighting, range >= 100 es un halo que se
    apaga hacia el borde (radio = range - 99); range < 100 pinta un cuadrado de color plano."""
    return {"op": "light", "x": x, "y": y, "color": color, "range": 99 + radius}


def spec_1000():
    """Hub: aldea recortada de Ullathorpe (plaza de la fuente + calle sur con casas),
    camino al norte hacia las Arenas y puerta de dungeon al final de la calle sur."""
    src = (35, 42, 62, 84)                 # rectángulo del mapa 1 (sin la casa cortada ni la entrada a las catacumbas)
    dx, dy = 39, 30                        # destino de su esquina superior izquierda
    sx0, sy0, sx1, sy1 = src
    offset_x, offset_y = dx - sx0, dy - sy0
    road_x = 51 + offset_x                 # la calle x51-54 del mapa 1 queda en x55-58
    top = dy                               # borde norte del recorte
    ops = [{"op": "erase", "layer": layer, "rect": rect(1, 1, 100, 100)} for layer in (2, 3, 4)]
    ops.append(paint(1, box=rect(1, 1, 100, 100), tile4x4=GRASS))
    ops.append({"op": "stamp", "src": 1, "x": sx0, "y": sy0, "w": sx1 - sx0 + 1, "h": sy1 - sy0 + 1,
                "dx": dx, "dy": dy, "layers": [1, 2, 3, 4], "blocks": True, "triggers": True,
                "objects": True, "lights": True, "particles": True})
    north_end = 12
    ops.append({"op": "erase", "layer": 3, "rect": rect(road_x, north_end, road_x + 3, top + 3)})
    for layer in (2, 3, 4):                # resto de un edificio de piedra que queda cortado en el borde oeste
        ops.append({"op": "erase", "layer": layer, "rect": rect(dx, dy + 12, dx + 1, dy + 23)})
    road = {}
    for y in range(north_end, top + 2):
        row = ROAD[(y - offset_y) % 4]
        for i in range(4):
            road[(road_x + i, y)] = row[i]
    layer3 = {(road_x - 1, north_end): TORCH, (road_x + 4, north_end): TORCH,
              (road_x + 1, north_end): PORTAL, (road_x + 2, north_end): PORTAL}   # portal doble al ancho de la calle
    signs = [sign(road_x - 2, north_end + 2, "salida norte a las Arenas", ARENA_SIGNS[0][1])]
    south = sy1 + offset_y + 1                                # primera fila bajo el recorte
    layer3[(road_x + 2, south + 2)] = DUNGEON_DOOR
    signs.append(sign(road_x - 2, south, "salida sur al Dungeon", NEWBIE_SIGN))
    layer3[(road_x - 1, south + 2)] = TORCH
    layer3[(road_x + 4, south + 2)] = TORCH
    for y in range(south, south + 3):                         # tramo corto de calle hasta la puerta
        row = ROAD[(y - offset_y) % 4]
        for i in range(4):
            road[(road_x + i, y)] = row[i]
    trees = []
    x0, y0, x1, y1 = dx - 2, north_end - 2, dx + (sx1 - sx0) + 2, south + 5
    for x in range(x0, x1 + 1, 3):
        trees += [(x, y0), (x, y1)]
    for y in range(y0 + 3, y1, 3):
        trees += [(x0, y), (x1, y)]
    for cell in trees:
        if not (road_x - 1 <= cell[0] <= road_x + 4):
            layer3.setdefault(cell, PINE)
    for grh in sorted(set(road.values())):
        ops.append(paint(1, [c for c, g in road.items() if g == grh], grh=grh))
    for grh in sorted(set(layer3.values())):
        ops.append(paint(3, [c for c, g in layer3.items() if g == grh], grh=grh))
    ops += [light(road_x + 1, north_end, PORTAL_LIGHT, 4), light(road_x + 2, north_end, PORTAL_LIGHT, 4),
            light(road_x - 1, north_end, TORCH_LIGHT, 3), light(road_x + 4, north_end, TORCH_LIGHT, 3),
            light(road_x + 2, south + 1, TORCH_LIGHT, 4),                                  # frente a la puerta del dungeon
            light(road_x - 1, south + 2, TORCH_LIGHT, 3), light(road_x + 4, south + 2, TORCH_LIGHT, 3)]
    return {
        "map": 1000,
        "autor": "Arte",
        "base": "fill (Programación)",
        "notas": f"Recorte de Ullathorpe (mapa 1, x{sx0}-{sx1}, y{sy0}-{sy1}) en x{dx}-{dx + sx1 - sx0}, y{dy}-{dy + sy1 - sy0}, "
                 f"con sus bloqueos, techos, luces y partículas originales. Caminable: el recorte (respetando sus bloqueos) + "
                 f"camino norte x{road_x}-{road_x + 3}, y{north_end}-{top + 1} (salida a Arenas en y{north_end}) + "
                 f"calle sur x{road_x}-{road_x + 3}, y{south}-{south + 2} (puerta del dungeon en x{road_x + 2}, y{south + 2}: "
                 f"la salida va en la fila de la puerta o la de arriba). Pinos alrededor como borde.",
        "ops": ops,
        # Portals are walked into (the exit): never blocking.
        "decorBloqueante": [list(c) for c in sorted(layer3, key=lambda c: (c[1], c[0])) if layer3[c] != PORTAL],
        "carteles": signs,
    }


DUNGEON_DATA = ROOT / "docs/claude/demo/dungeon-npcs.json"   # Contenido: fuente, recorte, entrada y escaleras por piso
VOID = 1                  # negro de vacío del AO (capa 1)
HOLE = 57950              # hueco oscuro de las salidas de los dungeons originales (capa 2)
TELEPORT = 49488          # teleport original (objeto tipo 19)
CRYPT_DOOR = DUNGEON_DOOR
FLOOR_IDS = {"P1": 1011, "P2": 1012, "P3": 1013, "P4": 1014, "P5": 1015, "P6": 1016, "P7": 1017}
OUTDOOR = set()           # todos los pisos son bajo tierra (reglas-y-recorrido.md, regla 3); el cementerio es la entrada 1010
# Brillos azules al pie de las paredes (remaster del P1, pedido de Lucas 25/09). Celeste claro y no azul puro: la luz
# Original multiplica la textura, y un azul puro (0x3C78FF) la oscurecía en vez de iluminarla.
FLOOR_GLOW = {"P1": 0x5A96FF}
MOON_LIGHT = 0xC8D2F5   # luz de luna bajo los haces (fría; < 0xD9 para que la luz Mejorada no la lea como antorcha)
# Remaster HD (Tools/hd_remake/dungeon_hd.py + demo_map_builder.hd_remaster): variantes del piso y decoración.
HD_REMASTER = {
    "P1": {"op": "hd_remaster",
           "piso": {"tex": 5095, "sx": 512, "sy": 288},             # el juego de 4x4 del piso original
           "variantes": {"tex": 90001, "n": 8, "base": 35},          # 35 % la variante 0 (la del atlas)
           "decor": {"tex": 90002, "escombros": 24, "niebla": 14,     # % de las casillas al pie de una pared
                     "haces": {"uno_de": 18, "lejos": [8, 11], "luz": MOON_LIGHT},
                     # Partículas propias de la demo (particle_migration.DEMO_PARTICLES): 9001 chispas violetas del
                     # portal, 9002 polvo de luna (1 de cada 2 haces), 9003 chispas en la llama de la antorcha.
                     # Descartadas del original: 52 (explosión: fija se quema en blanco), 245 (puffs grandes), 246 (su
                     # llama cae fuera de esta antorcha) y 199 (motas en 22 casillas: caen sobre el vacío negro).
                     "particulas": {"luces": [[PORTAL_LIGHT, 9001], [TORCH_LIGHT, 9003]], "haz": 9002, "haz_uno_de": 2},
                     "halos": [[0x5A96FF, "halo"], [0xB47CFF, "halo_grande"]]},    # brillos (celeste) y portal (violeta)
           # Adornos remasterizados (dungeon_hd.py adornos): textura original, recorte a 1x -> lugares en tex_90003.
           "reemplazos": {"tex": 90003, "items": [
               [5034, 0, 0, 128, 64, [[0, 0]]],                    # altar
               [5034, 160, 32, 32, 96, [[128, 0]]],                # estandarte
               [105, 96, 32, 32, 32, [[160, 0]]],                  # antorcha
               [5066, 960, 576, 64, 64, [[0, 96], [64, 96]]],      # rocas de las salidas selladas (dos versiones)
               [5041, 512, 1280, 256, 128, [[0, 160]]]]}},         # mancha de escombros
}


def _walkable_source(source_id: int):
    data = json.loads((MAPS / f"map_{source_id}.json").read_text(encoding="utf-8"))
    floor = {(c["x"], c["y"]): c["grh"] for c in data["cells"] if c["layer"] == 1}
    blocked = {(b["x"], b["y"]) for b in data["blocks"] if b["flags"]}
    water = [(1505, 1520), (124, 139), (468, 483), (2948, 2963), (12628, 12643),
             (24143, 24158), (24223, 24238), (24303, 24318), (44668, 44683)]
    return lambda c: c in floor and floor[c] != VOID and c not in blocked and not any(a <= floor[c] <= b for a, b in water)


def spec_floor(map_id: int):
    floors = json.loads(DUNGEON_DATA.read_text(encoding="utf-8"))["pisos"]
    floor = next(f for f in floors if FLOOR_IDS.get(f["id"]) == map_id)
    m = floor["mapa"]
    r = m["recorte"]
    x0, y0, x1, y1 = r["x"], r["y"], r["x"] + r["ancho"] - 1, r["y"] + r["alto"] - 1
    inside = lambda c: x0 <= c[0] <= x1 and y0 <= c[1] <= y1   # noqa: E731
    walk = _walkable_source(m["fuente"])
    ops, layer2, layer3, lights = [], {}, {}, []
    if floor["id"] in OUTDOOR:
        gaps = set()
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                if (x in (x0, x1) or y in (y0, y1)) and walk((x, y)):
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        o = (x + dx, y + dy)
                        if not inside(o) and walk(o):
                            gaps.add(o)
        for x, y in gaps:
            layer3[(x, y)] = PINE
    else:
        outside = [rect(1, 1, 100, y0 - 1), rect(1, y1 + 1, 100, 100), rect(1, y0, x0 - 1, y1), rect(x1 + 1, y0, 100, y1)]
        outside = [box for box in outside if box[2] > 0 and box[3] > 0]
        for box in outside:
            for layer in (2, 3, 4):
                ops.append({"op": "erase", "layer": layer, "rect": box})
            ops.append(paint(1, box=box, grh=VOID))
    for key in ("escaleraSubida", "escaleraBajada"):
        stair = m.get(key)
        if not stair:
            continue
        x, y = stair["x"], stair["y"]
        # Toda escalera es el portal original (antes: el hueco negro 57950), con luz violeta y antorchas con luz.
        layer3[(x, y)] = TELEPORT
        lights.append(light(x, y, PORTAL_LIGHT, 4))
        for side in ((x - 1, y - 1), (x + 1, y - 1)):
            if inside(side) and side not in layer3:
                layer3[side] = TORCH
                lights.append(light(side[0], side[1], TORCH_LIGHT, 3))
    glow = FLOOR_GLOW.get(floor["id"])
    if glow:   # uno que otro brillo: una luz chica cada ~17 casillas al pie de una pared, siempre en el mismo lugar
        for y in range(y0 + 1, y1 + 1):
            for x in range(x0, x1 + 1):
                if walk((x, y)) and not walk((x, y - 1)) and (x * 7 + y * 13) % 17 == 0:
                    # Dos casillas más abajo y radio 1: en la luz Original cada pieza de pared toma la luz de la casilla
                    # donde se apoya (la fila de piso de abajo) en toda su altura, y con la luz al pie de la pared se
                    # teñía un rectángulo azul. El resplandor lo da el halo del builder.
                    if walk((x, y + 1)) and walk((x, y + 2)):
                        lights.append(light(x, y + 2, glow, 1))
    entry = m["entrada"]
    signs = [sign(entry["x"] + 2, entry["y"] + 1, "cartel del piso (zona segura)", FLOOR_SIGNS[floor["id"]])]
    if floor["id"] in FLOOR_EXTRA_SIGNS:
        obj, grh = FLOOR_EXTRA_SIGNS[floor["id"]]
        spot = (entry["x"] - 2, entry["y"] + 1)
        if not inside(spot):
            spot = (entry["x"] + 3, entry["y"] - 1)
        signs.append(sign(spot[0], spot[1], "cartel original junto a la entrada", grh, obj))
    for grh in sorted(set(layer2.values())):
        ops.append(paint(2, [c for c, g in layer2.items() if g == grh], grh=grh))
    for grh in sorted(set(layer3.values())):
        ops.append(paint(3, [c for c, g in layer3.items() if g == grh], grh=grh))
    ops += lights
    if floor["id"] in HD_REMASTER:
        ops.append(HD_REMASTER[floor["id"]])
    return {
        "map": map_id,
        "autor": "Arte",
        "base": f"copy {m['fuente']}",
        "nombre": f"{floor['id']} {floor['nombre']}",
        "notas": "Copia del mapa original (datos de Contenido en dungeon-npcs.json). "
                 + ("Afuera del recorte queda el cementerio; los cortes del recorte se cierran con pinos. " if floor["id"] in OUTDOOR
                    else "Afuera del recorte: negro de vacío (GRH 1) sin capas, como el borde de los dungeons originales. ")
                 + "Escaleras y final del dungeon: el teleport original (49488) con luz violeta y dos antorchas con luz.",
        "ops": ops,
        "carteles": signs,
    }


def spec_1010():
    """Entrada al dungeon en la superficie: Cementerio de Nix (mapa 4), recorte x14-87, y11-90.
    Al aire libre: los cortes del recorte se cierran con pinos. La capilla (20-21, 43) es la bajada original."""
    x0, y0, x1, y1 = 14, 11, 87, 90
    inside = lambda c: x0 <= c[0] <= x1 and y0 <= c[1] <= y1   # noqa: E731
    walk = _walkable_source(4)
    gaps = set()
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if (x in (x0, x1) or y in (y0, y1)) and walk((x, y)):
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    o = (x + dx, y + dy)
                    if not inside(o) and walk(o) and not (44 <= o[0] <= 48 and o[1] == y0 - 1):   # el camino norte queda abierto
                        gaps.add(o)
    ops = [paint(3, sorted(gaps), grh=PINE)] if gaps else []
    # El camino norte (salida a la plaza, x44-48, y11) lleva el portal original al centro; la capilla, luz en la puerta.
    ops += [paint(3, [(46, 11)], grh=PORTAL), paint(3, [(43, 11), (49, 11)], grh=TORCH),
            light(46, 11, PORTAL_LIGHT, 4), light(43, 11, TORCH_LIGHT, 3), light(49, 11, TORCH_LIGHT, 3),
            light(20, 44, TORCH_LIGHT, 4), light(21, 44, PORTAL_LIGHT, 3)]
    return {"map": 1010, "autor": "Arte", "base": "copy 4",
            "notas": "Cementerio de Nix al aire libre (entrada del dungeon). Los cortes del recorte se cierran con pinos; "
                     "la bajada es la puerta original de la capilla (con luz) y el camino norte vuelve a la plaza de la demo "
                     "por el portal original (49488) con luz y antorchas.",
            "ops": ops,
            "carteles": [sign(23, 47, "entrada del dungeon (capilla)", NEWBIE_SIGN), sign(49, 12, "camino a la plaza", EMPTY_SIGN)]}


SPECS = {1000: spec_1000, 1001: spec_1001, 1010: spec_1010, **{map_id: (lambda m=map_id: spec_floor(m)) for map_id in FLOOR_IDS.values()}}


# --- vista previa: simula base + ops (el builder real es de Programación) ---
def _tile(op, x, y):
    for key, side in (("tile4x4", 4), ("tile3x3", 3)):
        if key in op:
            return op[key] + (y % side) * side + (x % side)
    return op["grh"]


def _cells(op):
    if "rect" in op:
        x, y, w, h = op["rect"]
        return [(cx, cy) for cy in range(y, y + h) for cx in range(x, x + w)]
    return [tuple(c) for c in op["cells"]]


def simulate(base_id: int, ops: list) -> dict:
    data = copy.deepcopy(json.loads((MAPS / f"map_{base_id}.json").read_text(encoding="utf-8")))
    cells = {(c["layer"], c["x"], c["y"]): c["grh"] for c in data["cells"]}
    objects = [] if ops and ops[0]["op"] == "erase" else list(data["objects"])
    for op in ops:
        if op["op"] == "erase":
            for x, y in _cells(op):
                cells.pop((op["layer"], x, y), None)
        elif op["op"] == "paint":
            for x, y in _cells(op):
                cells[(op["layer"], x, y)] = _tile(op, x, y)
        elif op["op"] == "stamp":
            source = json.loads((MAPS / f"map_{op['src']}.json").read_text(encoding="utf-8"))
            for c in source["cells"]:
                if op["x"] <= c["x"] < op["x"] + op["w"] and op["y"] <= c["y"] < op["y"] + op["h"] and c["layer"] in op.get("layers", [1, 2, 3, 4]):
                    cells[(c["layer"], c["x"] - op["x"] + op["dx"], c["y"] - op["y"] + op["dy"])] = c["grh"]
            if op.get("objects", True):
                for o in source["objects"]:
                    if op["x"] <= o["x"] < op["x"] + op["w"] and op["y"] <= o["y"] < op["y"] + op["h"]:
                        objects.append({**o, "x": o["x"] - op["x"] + op["dx"], "y": o["y"] - op["y"] + op["dy"]})
    data["cells"] = [{"layer": l, "x": x, "y": y, "grh": g} for (l, x, y), g in cells.items()]
    data["objects"] = objects
    return data


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", type=Path)
    parser.add_argument("--ids", type=int, nargs="*")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    for map_id, build in SPECS.items():
        if args.ids and map_id not in args.ids:
            continue
        spec = build()
        target = OUT / f"{map_id}.art.json"
        target.write_text(json.dumps(spec, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
        print(f"{target.relative_to(ROOT)}: {len(spec['ops'])} ops")
        if args.preview:
            sys.path.insert(0, str(ROOT / "Tools"))
            import demo_minimap
            parts = spec["base"].split()
            base_id = int(parts[1]) if parts[0] == "copy" else 1   # "fill": se simula sobre un mapa limpiado por las ops
            full = demo_minimap.render_full(simulate(base_id, spec["ops"]), tile=16)
            args.preview.mkdir(parents=True, exist_ok=True)
            full.save(args.preview / f"{map_id}.png")
            print(f"  vista previa: {args.preview / f'{map_id}.png'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
