"""Piloto del remaster de piso y agua de Ullathorpe con Higgsfield (Arte).

  preparar            arma las imágenes de entrada (4x, vecino más cercano) y los prompts por familia
  importar FAM IMG    toma la imagen generada: 4x exacto, pixel art (paleta limitada), sin costuras,
                      y arma el atlas HD (tex_<n>.png a 4x; lo no rehecho queda como el original ampliado)
  familias            lista las familias del piloto

Las texturas HD van a un árbol espejo (Resources/AOMigratorHD/...) y nunca pisan los originales.
El juego todavía no las carga: falta el cargador HD en Unity (ver docs/claude/nube/hd/ullathorpe.md).
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
TEX = ROOT / "Assets/Resources/AOMigrator/WorldV07/Textures"
HD_TEX = ROOT / "Assets/Resources/AOMigratorHD/WorldV07/Textures"
BLOCK, SCALE = 128, 4   # un set 4x4 de GRH de 32 px = 128 px; el remaster es 4x
MODEL = "seedream_v5_pro"  # elegido por Lucas el 25/09 (2k, 3:2): más oscuro, respeta el dibujo y el detalle buscado

STYLE = (
    "Remaster this top-down RPG ground texture into high-detail dark-fantasy pixel art, like modern "
    "'souls-like' HD pixel-art games. Keep EXACTLY the same layout: same regions, same path shapes and "
    "positions, same color zones and proportions. Rich micro-detail, crisp pixel clusters, limited palette, "
    "no blur, no text, no frame. Orthographic top-down view, no perspective. FLAT NEUTRAL LIGHTING: no cast "
    "shadows, no light spots, no vignette, no glow (lighting is added in the game engine)."
)
FAMILIES = {
    "pasto_caminos": {
        "cols": 3,
        "sets": [  # (textura, bloque x, bloque y, repetición, nombre)
            (6000, 0, 0, "xy", "pasto base (29.000 casillas)"),
            (5087, 0, 4, "xy", "pasto con piedras y flores"),
            (5087, 0, 0, "xy", "pasto seco"),
            (6215, 1, 0, "x", "camino de tierra horizontal"),
            (6215, 1, 1, "y", "camino de tierra vertical"),
            (6215, 0, 2, "", "curva de camino"),
        ],
        # Prompt aprobado por Lucas el 25/09 con Seedream 5 Pro (job 5562a4c5-8624-4bd7-ad1a-b73d9c9802c4).
        "prompt": "The FIRST reference image is the source: a top-down RPG ground texture sheet with 6 square blocks "
                  "in a 3 columns x 2 rows grid. Top row: dark green grass; grass with pebbles and small plants; dry "
                  "straw-colored grass. Bottom row: a horizontal dirt path crossing grass; a vertical dirt path crossing "
                  "grass; a curved dirt path. Redraw this exact sheet as high-detail dark-fantasy pixel art, like modern "
                  "souls-like HD pixel-art games. Keep EXACTLY the same composition: same 3x2 grid, same blocks in the "
                  "same positions, same path shapes, widths and positions, same color zones. Each block is a seamless "
                  "tileable ground texture. Rich micro-detail: individual grass blades, tiny white and yellow wildflowers, "
                  "small pebbles, moss, soil grain; dirt paths of packed soil with small stones and grass tufts on the "
                  "edges; the grass in every block matches the base grass. The other three images are STYLE references "
                  "only: copy their pixel-art rendering, micro-detail density and natural palette, but NOT their night "
                  "lighting, lanterns, glow, fog, objects, buildings or characters. Flat neutral daylight lighting: no "
                  "cast shadows, no light spots, no vignette, no glow. Orthographic top-down view, no perspective. No "
                  "borders, gaps or text between blocks.",
    },
    "agua": {
        "cols": 1,
        "sets": [(20, 0, 0, "xy", "agua de estanque")],
        "prompt": "The FIRST reference image is the source: a top-down RPG water surface texture (one square tile). "
                  "Redraw it as high-detail dark-fantasy pixel art, like modern souls-like HD pixel-art games, matching "
                  "the approved grass remaster: deep blue-teal water with subtle ripple patterns and a few tiny sparkles, "
                  "same overall color zones as the source. Seamless tileable texture, no shore, no objects, no reflections "
                  "of objects. The other three images are STYLE references only: copy their pixel-art rendering and "
                  "detail density, but NOT their night lighting, lanterns, glow, fog, objects or characters. Flat neutral "
                  "lighting: no cast shadows, no light spots, no vignette. Orthographic top-down view. No borders or text.",
    },
    "madera": {
        "cols": 1,
        "sets": [(5026, 2, 1, "xy", "tablones de madera")],
        "prompt": "The FIRST reference image is the source: a top-down RPG wooden floor texture (one square tile of "
                  "horizontal planks). Redraw it as high-detail dark-fantasy pixel art, like modern souls-like HD pixel-art "
                  "games, matching the approved grass remaster: weathered horizontal planks with wood grain, knots, cracks "
                  "and nail heads; keep the same plank direction, count and colors as the source. Seamless tileable "
                  "texture. The other three images are STYLE references only: copy their pixel-art rendering and detail "
                  "density, but NOT their night lighting, lanterns, glow, fog, objects or characters. Flat neutral "
                  "lighting: no cast shadows, no light spots, no vignette. Orthographic top-down view. No borders or text.",
    },
}


def block(tex_num, bx, by):
    return Image.open(TEX / f"tex_{tex_num}.png").convert("RGBA").crop((bx * BLOCK, by * BLOCK, (bx + 1) * BLOCK, (by + 1) * BLOCK))


def grid_size(fam):
    cols = fam["cols"]; rows = -(-len(fam["sets"]) // cols)
    return cols, rows


def prepare(out: Path):
    out.mkdir(parents=True, exist_ok=True)
    manifest = {}
    for name, fam in FAMILIES.items():
        cols, rows = grid_size(fam)
        sheet = Image.new("RGBA", (cols * BLOCK, rows * BLOCK), (0, 0, 0, 255))
        for i, (t, bx, by, _, _) in enumerate(fam["sets"]):
            sheet.alpha_composite(block(t, bx, by), ((i % cols) * BLOCK, (i // cols) * BLOCK))
        big = sheet.resize((sheet.width * SCALE, sheet.height * SCALE), Image.NEAREST)
        big.convert("RGB").save(out / f"{name}_entrada.png")
        (out / f"{name}_prompt.txt").write_text(fam["prompt"] + "\n", "utf-8")
        manifest[name] = {"size": list(big.size), "cols": cols, "rows": rows,
                          "sets": [{"tex": t, "block": [bx, by], "tile": tile, "name": n} for t, bx, by, tile, n in fam["sets"]]}
        print(f"{name}: {len(fam['sets'])} sets -> {name}_entrada.png {big.size[0]}x{big.size[1]} (1 generación)")
    (out / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), "utf-8")
    print(f"Listo en {out}. Subir a Higgsfield cada *_entrada.png con su *_prompt.txt y las 3 referencias de estilo de Lucas.")


def make_tileable(a: np.ndarray, axes: str, band=0.22) -> np.ndarray:
    """Mezcla con la copia corrida media vuelta: el borde queda continuo al repetir (solo en los ejes pedidos)."""
    out = a.astype(np.float32)
    h, w = a.shape[:2]
    for axis, n, active in ((1, w, "x" in axes), (0, h, "y" in axes)):
        if not active: continue
        rolled = np.roll(out, n // 2, axis=axis)
        t = np.abs(np.arange(n) - (n - 1) / 2) / ((n - 1) / 2)          # 0 al centro, 1 en el borde
        wgt = np.clip((t - (1 - 2 * band)) / (2 * band), 0, 1)           # solo cerca del borde usa la copia corrida
        wgt = wgt[None, :, None] if axis == 1 else wgt[:, None, None]
        out = out * (1 - wgt) + rolled * wgt
    return np.clip(out, 0, 255).astype(np.uint8)


def pixelize(img: Image.Image, pixel: int, colors: int) -> Image.Image:
    small = img.resize((img.width // pixel, img.height // pixel), Image.BOX)
    q = small.convert("RGB").quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
    return q.resize(img.size, Image.NEAREST)


def import_generated(name: str, image: Path, out: Path, pixel: int, colors: int, apply: bool):
    fam = FAMILIES[name]
    cols, rows = grid_size(fam)
    target = (cols * BLOCK * SCALE, rows * BLOCK * SCALE)
    gen = Image.open(image).convert("RGB")
    if abs(gen.width / gen.height - target[0] / target[1]) > .03:
        print(f"AVISO: la imagen generada es {gen.size}, proporción distinta de {target}; se ajusta igual.")
    gen = gen.resize(target, Image.LANCZOS)
    out.mkdir(parents=True, exist_ok=True)
    B = BLOCK * SCALE
    atlases = {}
    for i, (t, bx, by, tile, label) in enumerate(fam["sets"]):
        tile_img = gen.crop(((i % cols) * B, (i // cols) * B, (i % cols + 1) * B, (i // cols + 1) * B))
        arr = make_tileable(np.asarray(tile_img), tile) if tile else np.asarray(tile_img)
        tile_img = pixelize(Image.fromarray(arr), pixel, colors)
        tile_img.save(out / f"{name}_{t}_{bx}_{by}.png")
        if tile:
            reps = (3 if "x" in tile else 1, 3 if "y" in tile else 1)
            prev = Image.new("RGB", (B * reps[0], B * reps[1]))
            for yy in range(reps[1]):
                for xx in range(reps[0]): prev.paste(tile_img, (xx * B, yy * B))
            prev.resize((prev.width // 2, prev.height // 2), Image.NEAREST).save(out / f"{name}_{t}_{bx}_{by}_repetido.png")
        if t not in atlases:
            src = Image.open(TEX / f"tex_{t}.png").convert("RGBA")
            atlases[t] = src.resize((src.width * SCALE, src.height * SCALE), Image.NEAREST)
        atlases[t].paste(tile_img.convert("RGBA"), (bx * B, by * B))
        print(f"  {label}: tex_{t} bloque ({bx},{by}) -> {B}x{B}, repetición '{tile or 'no'}'")
    dest = HD_TEX if apply else out / "atlas_hd"
    dest.mkdir(parents=True, exist_ok=True)
    for t, atlas in atlases.items():
        path = dest / f"tex_{t}.png"
        if apply and path.exists():
            atlas = Image.open(path).convert("RGBA")  # conservar lo rehecho antes en otras familias
            for i, (tt, bx, by, _, _) in enumerate(fam["sets"]):
                if tt == t: atlas.paste(Image.open(out / f"{name}_{t}_{bx}_{by}.png").convert("RGBA"), (bx * B, by * B))
        atlas.save(path)
        print(f"  atlas HD tex_{t}.png {atlas.size[0]}x{atlas.size[1]} -> {path.relative_to(ROOT) if apply else path}")
    if not apply:
        print("Simulación: los atlas quedaron en la carpeta de salida. Para instalarlos en el árbol HD: --aplicar")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("preparar"); p.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/ullathorpe_piloto")
    i = sub.add_parser("importar"); i.add_argument("familia", choices=FAMILIES); i.add_argument("imagen", type=Path)
    i.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/ullathorpe_piloto/resultado")
    i.add_argument("--pixel", type=int, default=2, help="tamaño del pixel de arte en px HD (2 = pixel art visible a 4x)")
    i.add_argument("--colores", type=int, default=48, help="paleta por set")
    i.add_argument("--aplicar", action="store_true", help="instalar los atlas en Resources/AOMigratorHD")
    sub.add_parser("familias")
    a = parser.parse_args()
    if a.cmd == "preparar": prepare(a.salida)
    elif a.cmd == "importar": import_generated(a.familia, a.imagen, a.salida, a.pixel, a.colores, a.aplicar)
    else:
        for n, f in FAMILIES.items(): print(n, "-", ", ".join(s[4] for s in f["sets"]))


if __name__ == "__main__":
    sys.exit(main())
