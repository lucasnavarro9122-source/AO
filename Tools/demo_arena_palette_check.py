"""Verifica la paleta visual de AOArenaGen (Arte) y dibuja una vista previa por tema.

Uso:
  python Tools/demo_arena_palette_check.py                 # verifica (G-09 / G-11), sale 1 si falla
  python Tools/demo_arena_palette_check.py --preview DIR   # además dibuja DIR/arena_<Tema>.png

Chequea: los 6 temas del enum, densidad min<=max dentro de 0..30, cada GRH existe en
graficos.ini con su textura, los sets de piso/agua completos y los límites de tamaño
de sprite por forma (dentro del ring nada tapa la pelea).
"""

from pathlib import Path
import argparse
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
PALETTE = ROOT / "Assets/StreamingAssets/AOMigrator/ArenaGen/arena_palette.json"
GRAFICOS = ROOT / "Archivos Originales/Recursos-master/Recursos-master/init/graficos.ini"
TEXTURES = [
    ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures/tex_{}.png",
    ROOT / "Archivos Originales/Recursos-master/Recursos-master/Graficos/{}.png",
]
THEMES = ["Bosque", "Desierto", "Nieve", "Mazmorra", "Pantano", "Ciudad"]
PATTERN_SIZE = {"4x4": 16, "3x3": 9}
KIND_LIMIT = {"Pillar": "solido1x1", "Tree": "solido1x1", "Rock": "roca2x2", "Wall": "porCelda", "Pool": "porCelda", "Deco": "deco"}
SHAPE_KINDS = ["Pillar", "Rock", "Wall", "Wall", "Wall", "Tree", "Pool", "Pool"]  # orden de los pesos en AOArenaGen


def generator_kinds():
    """{tema: ((min, max), {clases con peso > 0} | {'Deco'})} leído de Runtime/Shared/AOArenaGenV*.cs, o None."""
    files = sorted((ROOT / "Assets/AOMigrator/Runtime/Shared").glob("AOArenaGenV*.cs"))
    if not files:
        return None
    rows = re.findall(r"new ThemeRule\(\s*(\d+),\s*(\d+),\s*\d+,\s*\d+,\s*new\[\]\s*\{([^}]*)\}", files[-1].read_text(encoding="utf-8"))
    if len(rows) != len(THEMES):
        return None
    result = {}
    for name, (low, high, weights) in zip(THEMES, rows):
        values = [int(v) for v in weights.split(",")]
        result[name] = ((int(low), int(high)), {SHAPE_KINDS[i] for i, w in enumerate(values) if w > 0} | {"Deco"})
    return result


def load_grh() -> dict[int, list[int]]:
    grh = {}
    for line in GRAFICOS.read_text(encoding="latin-1").splitlines():
        match = re.match(r"Grh(\d+)=(.*)", line.strip())
        if match:
            grh[int(match.group(1))] = [int(v) for v in match.group(2).split("-") if v.strip().lstrip("-").isdigit()]
    return grh


def first_frame(grh: dict, index: int):
    """Devuelve (fileNum, sx, sy, w, h) del primer frame, o None."""
    value = grh.get(index)
    for _ in range(4):
        if not value:
            return None
        if value[0] > 1:
            value = grh.get(value[1])
            continue
        return tuple(value[1:6]) if len(value) >= 6 else None
    return None


def texture_path(file_num: int):
    for pattern in TEXTURES:
        path = Path(str(pattern).format(file_num))
        if path.exists():
            return path
    return None


def check(palette: dict, grh: dict) -> list[str]:
    errors = []
    limits = palette["limitesSprite"]
    themes = palette.get("temas", {})
    if sorted(themes) != sorted(THEMES):
        errors.append(f"temas {sorted(themes)} != enum {THEMES}")

    def sprite(index: int, where: str, limit=None):
        frame = first_frame(grh, index)
        if frame is None:
            errors.append(f"{where}: GRH {index} no existe en graficos.ini")
            return
        if texture_path(frame[0]) is None:
            errors.append(f"{where}: GRH {index} usa la textura {frame[0]}, que no existe")
        if limit and (frame[3] > limit[0] or frame[4] > limit[1]):
            errors.append(f"{where}: GRH {index} mide {frame[3]}x{frame[4]}, límite {limit[0]}x{limit[1]}")

    def tile_set(entry: dict, where: str):
        size = PATTERN_SIZE.get(entry.get("patron"))
        if size is None:
            errors.append(f"{where}: patrón desconocido {entry.get('patron')}")
            return
        for offset in range(size):
            sprite(entry["base"] + offset, where, limits["porCelda"])

    rules = generator_kinds()
    for name, theme in themes.items():
        density = theme.get("densidad", {})
        low, high = density.get("min"), density.get("max")
        if not (isinstance(low, int) and isinstance(high, int) and 0 <= low <= high <= 30):
            errors.append(f"{name}: densidad inválida {density}")
        tile_set(theme["piso"], f"{name}.piso")
        kinds = theme.get("kinds", {})
        for kind, entry in kinds.items():
            if kind not in KIND_LIMIT:
                errors.append(f"{name}.{kind}: clase desconocida (enum Kind: {sorted(KIND_LIMIT)})")
                continue
            if kind == "Pool":
                tile_set(entry, f"{name}.Pool")
                continue
            if not entry.get("grh"):
                errors.append(f"{name}.{kind}: sin variantes")
            for index in entry.get("grh", []):
                sprite(index, f"{name}.{kind}", limits[KIND_LIMIT[kind]])
        if rules is not None and name in rules:
            rule_density, used = rules[name]
            missing = sorted(used - set(kinds))
            if missing:
                errors.append(f"{name}: el generador usa {missing} y la paleta no los tiene")
            if rule_density != (low, high):
                errors.append(f"{name}: densidad {low}-{high} distinta de la del generador {rule_density[0]}-{rule_density[1]}")
    return errors


def preview(palette: dict, grh: dict, folder: Path) -> None:
    """Arena de 23x19 de muestra (misma posición para todos los temas, espejada)."""
    from PIL import Image

    cache = {}

    def image(index: int):
        frame = first_frame(grh, index)
        if frame is None:
            return None
        file_num, sx, sy, w, h = frame
        if file_num not in cache:
            path = texture_path(file_num)
            cache[file_num] = Image.open(path).convert("RGBA") if path else None
        texture = cache[file_num]
        return texture.crop((sx, sy, sx + w, sy + h)) if texture else None

    width, height, ox, oy = 23, 19, 13, 11  # interior de la Sala 1 del mapa 324
    west = [("Pillar", 6, 4), ("Tree", 8, 12), ("Rock", 5, 8), ("Wall", 9, 2), ("L", 7, 15),
            ("Pool", 9, 7), ("Deco", 6, 10), ("Deco", 10, 14)]
    folder.mkdir(parents=True, exist_ok=True)
    for name, theme in palette["temas"].items():
        canvas = Image.new("RGBA", (width * 32, height * 32), (0, 0, 0, 255))
        floor = theme["piso"]
        side = 4 if floor["patron"] == "4x4" else 3
        for y in range(height):
            for x in range(width):
                mx, my = ox + x, oy + y
                tile = image(floor["base"] + (my % side) * side + (mx % side))
                if tile:
                    canvas.alpha_composite(tile, (x * 32, y * 32))
        placed = []
        for kind, x, y in west:
            for cx in (x, width - 1 - x):
                placed.append((kind, cx, y))
        # agua y deco primero (van abajo), después sólidos ordenados por Y
        for kind, x, y in placed:
            kinds = theme["kinds"]
            water = kinds.get("Pool") if kind == "Pool" else None
            if water:
                for dx in range(2):
                    for dy in range(2):
                        mx, my = ox + x + dx, oy + y + dy
                        tile = image(water["base"] + (my % 4) * 4 + (mx % 4))
                        if tile and x + dx < width:
                            canvas.alpha_composite(tile, ((x + dx) * 32, (y + dy) * 32))
            if kind == "Deco" and kinds.get("Deco", {}).get("grh"):
                sprite = image(kinds["Deco"]["grh"][x % len(kinds["Deco"]["grh"])])
                if sprite:
                    canvas.alpha_composite(sprite, (x * 32 + 16 - sprite.width // 2, y * 32 + 32 - sprite.height))
        solids = sorted(placed, key=lambda item: item[2])
        for kind, x, y in solids:
            entry = theme["kinds"].get("Wall" if kind == "L" else kind)
            if not entry or kind in ("Pool", "Deco"):
                continue
            variant = entry["grh"][(x + y) % len(entry["grh"])]
            if kind in ("Wall", "L"):
                cells = [(x, y + i) for i in range(3)] if kind == "Wall" else [(x, y), (x, y + 1), (x + 1 if x < width // 2 else x - 1, y + 1)]
                for cx, cy in cells:
                    tile = image(variant)
                    if tile:
                        canvas.alpha_composite(tile, (cx * 32, cy * 32))
                continue
            sprite = image(variant)
            if not sprite:
                continue
            span = 2 if kind == "Rock" else 1
            left = x * 32 + span * 16 - sprite.width // 2
            top = (y + span) * 32 - sprite.height
            canvas.alpha_composite(sprite, (max(0, left), max(0, top)))
        canvas.convert("RGB").save(folder / f"arena_{name}.png")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--palette", type=Path, default=PALETTE)
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    palette = json.loads(args.palette.read_text(encoding="utf-8"))
    grh = load_grh()
    errors = check(palette, grh)
    for error in errors:
        print("ERROR", error)
    ranges = ", ".join(f"{n} {t['densidad']['min']}-{t['densidad']['max']}%" for n, t in palette["temas"].items())
    print(f"Paleta v{palette.get('version')}: {'FALLA' if errors else 'OK'} | densidad: {ranges}")
    if args.preview:
        preview(palette, grh, args.preview)
        print(f"Vista previa en {args.preview}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
