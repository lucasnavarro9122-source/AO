"""Minimapas de los mapas de demo (IDs >= 1000), con el mismo estilo que los originales.

Se dibuja el mapa entero como el cliente (capas 1-4 y objetos) y se achica a 100x100,
igual que los BMP originales del AO. La ventana "Mapa" (M)
usa el mismo PNG estirado, así que sirve también de mapa grande.

Uso:
  python Tools/demo_minimap.py                    # todos los map_{id}.json con id >= 1000
  python Tools/demo_minimap.py --ids 1000 1001
  python Tools/demo_minimap.py --calibrate 1 4 324 # compara contra los originales (no escribe)

El builder de Programación puede importar `render_minimap(map_dict)` y `write_minimap(id, image)`.
Nunca escribe IDs < 1000: esos son los minimapas originales importados.
"""

from pathlib import Path
import argparse
import json
import re
import sys

from PIL import Image

try:
    from map_migration import DEMO_MIN_MAP
except ImportError:  # se importa desde otra carpeta
    DEMO_MIN_MAP = 1000

ROOT = Path(__file__).resolve().parents[1]
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
OUTPUT = ROOT / "Assets/Resources/AOMigrator/MinimapsV0103"
GRAFICOS = ROOT / "Archivos Originales/Recursos-master/Recursos-master/init/graficos.ini"
TEXTURES = [
    ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures/tex_{}.png",
    ROOT / "Archivos Originales/Recursos-master/Recursos-master/Graficos/{}.png",
]

_grh = None
_textures = {}


def _load_grh() -> dict:
    global _grh
    if _grh is None:
        _grh = {}
        for line in GRAFICOS.read_text(encoding="latin-1").splitlines():
            match = re.match(r"Grh(\d+)=(.*)", line.strip())
            if match:
                _grh[int(match.group(1))] = [int(v) for v in match.group(2).split("-") if v.strip().lstrip("-").isdigit()]
    return _grh


def _frame(index: int):
    grh = _load_grh()
    value = grh.get(index)
    for _ in range(4):
        if not value:
            return None
        if value[0] > 1:
            value = grh.get(value[1])
            continue
        return tuple(value[1:6]) if len(value) >= 6 else None
    return None


_sprites = {}


def _sprite(index: int):
    """Primer frame del GRH como imagen RGBA (con caché), o None."""
    if index not in _sprites:
        frame = _frame(index)
        image = None
        if frame:
            file_num, sx, sy, w, h = frame
            if file_num not in _textures:
                path = next((Path(str(p).format(file_num)) for p in TEXTURES if Path(str(p).format(file_num)).exists()), None)
                _textures[file_num] = Image.open(path).convert("RGBA") if path else None
            texture = _textures[file_num]
            if texture is not None:
                image = texture.crop((sx, sy, sx + w, sy + h))
        _sprites[index] = image
    return _sprites[index]


_scaled_cache = {}


def _scaled(index: int, tile: int):
    key = (index, tile)
    if key not in _scaled_cache:
        sprite = _sprite(index)
        if sprite is not None and tile != 32:
            sprite = sprite.resize((max(1, sprite.width * tile // 32), max(1, sprite.height * tile // 32)), Image.BOX)
        _scaled_cache[key] = sprite
    return _scaled_cache[key]


def render_full(map_dict: dict, tile: int = 32) -> Image.Image:
    """Dibuja el mapa completo como el cliente: capas 1 y 2, luego capa 3 y objetos
    por fila (centrados en X y apoyados abajo), y los techos (capa 4) encima."""
    size = 100 * tile
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    by_layer = {1: [], 2: [], 3: [], 4: []}
    for cell in map_dict.get("cells", []):
        if cell["layer"] in by_layer:
            by_layer[cell["layer"]].append((cell["y"], cell["x"], cell["grh"]))
    for obj in map_dict.get("objects", []):
        if obj.get("grhIndex"):
            by_layer[3].append((obj["y"], obj["x"], obj["grhIndex"]))

    def draw(entries, centered):
        for y, x, index in sorted(entries):
            sprite = _scaled(index, tile)
            if sprite is None:
                continue
            left, top = (x - 1) * tile, (y - 1) * tile
            if centered and (sprite.width > tile or sprite.height > tile):
                left += (tile - sprite.width) // 2
                top += tile - sprite.height
            if left >= size or top >= size or left + sprite.width <= 0 or top + sprite.height <= 0:
                continue
            canvas.paste(sprite, (left, top), sprite)

    draw(by_layer[1], False)
    draw(by_layer[2], True)
    draw(by_layer[3], True)
    draw(by_layer[4], True)
    return canvas.convert("RGB")


def render_minimap(map_dict: dict) -> Image.Image:
    """PNG 100x100 (RGB): el mapa dibujado entero y achicado, como los minimapas originales."""
    return render_full(map_dict, tile=8).resize((100, 100), Image.BOX)


def write_minimap(map_id: int, image: Image.Image) -> Path:
    if map_id < DEMO_MIN_MAP:
        raise ValueError(f"El mapa {map_id} es original: su minimapa no se regenera")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    target = OUTPUT / f"map_{map_id}.png"
    temporary = target.with_suffix(".png.tmp")
    try:
        image.save(temporary, format="PNG", optimize=True)
        temporary.replace(target)
    finally:
        temporary.unlink(missing_ok=True)
    return target


def calibrate(ids: list[int]) -> None:
    for map_id in ids:
        rendered = render_minimap(json.loads((MAPS / f"map_{map_id}.json").read_text(encoding="utf-8")))
        with Image.open(OUTPUT / f"map_{map_id}.png") as original:
            original = original.convert("RGB")
            diff = sum(abs(a - b) for p, q in zip(rendered.get_flattened_data(), original.get_flattened_data()) for a, b in zip(p, q))
            print(f"map {map_id}: error medio por canal = {diff / (100 * 100 * 3):.1f} (0..255)")
            side = Image.new("RGB", (200, 100))
            side.paste(original, (0, 0))
            side.paste(rendered, (100, 0))
            yield map_id, side


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ids", type=int, nargs="*")
    parser.add_argument("--calibrate", type=int, nargs="*")
    parser.add_argument("--calibrate-out", type=Path)
    args = parser.parse_args()
    if args.calibrate:
        for map_id, side in calibrate(args.calibrate):
            if args.calibrate_out:
                args.calibrate_out.mkdir(parents=True, exist_ok=True)
                side.save(args.calibrate_out / f"calib_{map_id}.png")
        return 0
    ids = args.ids
    if not ids:
        ids = sorted(int(m.group(1)) for p in MAPS.glob("map_*.json")
                     if (m := re.fullmatch(r"map_(\d+)\.json", p.name)) and int(m.group(1)) >= DEMO_MIN_MAP)
    if not ids:
        print("No hay mapas de demo (id >= 1000) todavía.")
        return 0
    for map_id in ids:
        target = write_minimap(map_id, render_minimap(json.loads((MAPS / f"map_{map_id}.json").read_text(encoding="utf-8"))))
        print(f"map {map_id} -> {target.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
