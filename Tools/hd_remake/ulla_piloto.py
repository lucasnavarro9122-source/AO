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


# ---------------------------------------------------------------------------------------------
# Zonas completas: Ullathorpe (1) y alrededores (2, 5, 8, 11). Todo lo usado en capas 1 y 2.
ZONE_MAPS = (1, 2, 5, 8, 11)
ZONE_SKIP = {5023, 5041}  # 5023: manchas de sombra (las hace la luz); 5041: atlas de 2048 (a 4x pasa el máximo)
PILOT_DONE = {(6000, 0, 0), (5087, 0, 4), (5087, 0, 0), (6215, 1, 0), (6215, 1, 1), (6215, 0, 2), (20, 0, 0), (5026, 2, 1)}
ZONE_PROMPT = (
    "The FIRST reference image is the source: a sheet of 6 square blocks (3 columns x 2 rows) cut from the texture "
    "atlas of a top-down RPG: ground tiles, transition pieces or floors. Some neighbouring blocks form one bigger piece: "
    "keep them continuous. Redraw THIS source sheet as high-detail dark-fantasy pixel art, like modern souls-like HD "
    "pixel-art games, slightly darker and moodier than the source. Keep EXACTLY the same composition: same blocks in "
    "the same positions, same shapes, edges, patches and color zones in every block; solid black empty slots stay solid "
    "black. Rich micro-detail appropriate to each material (grass blades, tiny flowers, pebbles, soil grain, stone "
    "cracks, wood grain). The other images are STYLE references only: copy their pixel-art rendering and detail "
    "density, but do NOT copy their layout, night lighting, lanterns, glow, fog, objects, buildings or characters. Flat "
    "neutral lighting: no cast shadows, no light spots, no vignette, no glow. Orthographic top-down view, no "
    "perspective. No borders, gaps or text between blocks."
)
MAPS_DIR = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"


def zone_components():
    """Bloques de 128 usados en capas 1-2, unidos cuando un mismo sprite ocupa varios (deben ir contiguos)."""
    parent, uses = {}, {}
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for n in ZONE_MAPS:
        m = json.loads((MAPS_DIR / f"map_{n}.json").read_text("utf-8-sig"))
        spr = {s["id"]: s for s in m["sprites"]}
        for c in m["cells"]:
            if c["layer"] not in (1, 2) or c["sprite"] not in spr: continue
            s = spr[c["sprite"]]; f = s["fileNum"]
            if f in ZONE_SKIP: continue
            blocks = [(f, bx, by) for by in range(s["sy"] // BLOCK, (s["sy"] + s["height"] - 1) // BLOCK + 1)
                      for bx in range(s["sx"] // BLOCK, (s["sx"] + s["width"] - 1) // BLOCK + 1)]
            for b in blocks:
                parent.setdefault(b, b); uses[b] = uses.get(b, 0) + 1
            for b in blocks[1:]: parent[find(b)] = find(blocks[0])
    comps = {}
    for b in parent: comps.setdefault(find(b), set()).add(b)
    out, props = [], 0
    for c in comps.values():
        if all(b in PILOT_DONE for b in c): continue
        f = next(iter(c))[0]; xs = [b[1] for b in c]; ys = [b[2] for b in c]
        comp = {"tex": f, "bx": min(xs), "by": min(ys), "w": max(xs) - min(xs) + 1, "h": max(ys) - min(ys) + 1,
                "uses": sum(uses[b] for b in c)}
        # Solo piso: piezas opacas. Con transparencia son objetos o carteles (capa 2), fuera del alcance.
        alpha = np.asarray(source_region(f, comp["bx"], comp["by"], comp["w"], comp["h"]).split()[3])
        if (alpha > 250).mean() < 0.97:
            props += 1; continue
        out.append(comp)
    if props: print(f"({props} piezas con transparencia -objetos, carteles- quedan fuera: el alcance es piso y agua)")
    return sorted(out, key=lambda c: (-(c["w"] * c["h"]), c["tex"], c["by"], c["bx"]))


def zone_sheets(comps=None):
    sheets = []
    for comp in (zone_components() if comps is None else comps):
        w, h = comp["w"], comp["h"]
        if w > 3 or h > 2:
            print("AVISO: pieza más grande que 3x2, queda sin rehacer:", comp); continue
        for sh in sheets:
            spot = next(((x, y) for y in range(0, 3 - h) for x in range(0, 4 - w)
                         if all((x + i, y + j) not in sh["busy"] for i in range(w) for j in range(h))), None)
            if spot: break
        else:
            sh = {"busy": set(), "comps": []}; sheets.append(sh); spot = (0, 0)
        sh["busy"].update((spot[0] + i, spot[1] + j) for i in range(w) for j in range(h))
        sh["comps"].append(dict(comp, x=spot[0], y=spot[1]))
    return [{"comps": s["comps"]} for s in sheets]


def source_region(tex, bx, by, w, h):
    src = Image.open(TEX / f"tex_{tex}.png").convert("RGBA")
    region = Image.new("RGBA", (w * BLOCK, h * BLOCK), (0, 0, 0, 0))
    region.alpha_composite(src.crop((bx * BLOCK, by * BLOCK, min(src.width, (bx + w) * BLOCK), min(src.height, (by + h) * BLOCK))))
    return region


def tile_axes(block: Image.Image) -> str:
    """Si el bloque original se repite sin costura en X y/o en Y, el remaster también."""
    a = np.asarray(block.convert("RGBA"), np.float32)
    if a[..., 3].min() < 250: return ""
    rgb = a[..., :3]
    inner_x = np.abs(np.diff(rgb, axis=1)).mean(); inner_y = np.abs(np.diff(rgb, axis=0)).mean()
    wrap_x = np.abs(rgb[:, 0] - rgb[:, -1]).mean(); wrap_y = np.abs(rgb[0] - rgb[-1]).mean()
    return ("x" if wrap_x <= inner_x * 1.6 else "") + ("y" if wrap_y <= inner_y * 1.6 else "")


def round_names(ronda):
    pre = "hoja" if ronda == 0 else f"r{ronda}_hoja"
    return pre, f"zonas_manifest{'' if ronda == 0 else f'_r{ronda}'}.json"


def zones_prepare(out: Path, ronda: int = 0):
    out.mkdir(parents=True, exist_ok=True)
    pre, manifest = round_names(ronda)
    pend = out / "zonas_pendientes.json"
    if ronda > 0 and not pend.exists():
        print("No hay piezas pendientes de la ronda anterior."); return
    comps = None if ronda == 0 else [{k: c[k] for k in ("tex", "bx", "by", "w", "h", "uses")} for c in json.loads(pend.read_text("utf-8"))]
    sheets = zone_sheets(comps)
    for i, sh in enumerate(sheets):
        img = Image.new("RGBA", (3 * BLOCK, 2 * BLOCK), (0, 0, 0, 255))
        for c in sh["comps"]:
            img.alpha_composite(source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"]), (c["x"] * BLOCK, c["y"] * BLOCK))
        img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST).convert("RGB").save(out / f"{pre}_{i:02d}_entrada.png")
    (out / "zonas_prompt.txt").write_text(ZONE_PROMPT + "\n", "utf-8")
    (out / manifest).write_text(json.dumps(sheets, indent=1), "utf-8")
    comps = sum(len(s["comps"]) for s in sheets)
    print(f"{len(sheets)} hojas de 3x2 ({comps} piezas) en {out}. Costo estimado con {MODEL}: {len(sheets) * 2.5:.1f} créditos.")


def piece_check(orig: Image.Image, piece: Image.Image, w: int, h: int):
    """Compara con su original sin mirar el brillo (el remaster es más oscuro): reparto de color, forma y negro nuevo."""
    small = (w * 4, h * 4)
    a = np.asarray(orig.convert("RGB").resize(small, Image.BOX), np.float32) + 1
    b = np.asarray(piece.convert("RGB").resize(small, Image.BOX), np.float32) + 1
    chroma = float(np.abs(a / a.sum(2, keepdims=True) - b / b.sum(2, keepdims=True)).sum(2).mean())
    la, lb = a.mean(2), b.mean(2)
    shape = float(np.corrcoef(la.ravel(), lb.ravel())[0, 1]) if la.std() > 8 and lb.std() > 1 else 1.0
    fa = np.asarray(orig.convert("RGB").resize((w * 16, h * 16), Image.BOX), np.float32)
    fb = np.asarray(piece.convert("RGB").resize((w * 16, h * 16), Image.BOX), np.float32)
    black = float(((fb.max(2) < 14) & (fa.max(2) > 30)).mean())
    ok = chroma <= 0.19 and black <= 0.01 and shape >= 0.45
    return ok, chroma, shape, black


def match_color(piece: Image.Image, orig: Image.Image) -> Image.Image:
    """Transferencia de color por pieza: media y desvío por canal iguales a los del original (solo píxeles opacos)."""
    a = np.asarray(piece.convert("RGB"), np.float32).reshape(-1, 3)
    o = np.asarray(orig.convert("RGBA"), np.float32).reshape(-1, 4)
    ref = o[o[:, 3] > 250, :3] if (o[:, 3] > 250).any() else o[:, :3]
    out = (a - a.mean(0)) / (a.std(0) + 1e-6) * ref.std(0) + ref.mean(0)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8).reshape(piece.height, piece.width, 3))


def zones_import(out: Path, generated: Path, pixel: int, colors: int, apply: bool, ronda: int = 0, color: bool = False):
    pre, manifest = round_names(ronda)
    sheets = json.loads((out / manifest).read_text("utf-8"))
    pending, accepted = [], 0
    B = BLOCK * SCALE
    atlases = {}
    def atlas(t):
        if t not in atlases:
            hd = HD_TEX / f"tex_{t}.png"
            if apply and hd.exists():
                atlases[t] = Image.open(hd).convert("RGBA")
            else:
                src = Image.open(TEX / f"tex_{t}.png").convert("RGBA")
                atlases[t] = src.resize((src.width * SCALE, src.height * SCALE), Image.NEAREST)
        return atlases[t]
    for i, sh in enumerate(sheets):
        path = generated / f"{pre}_{i:02d}.png"
        if not path.exists():
            print(f"falta {path.name}: sus piezas quedan pendientes"); pending += sh["comps"]; continue
        gen = Image.open(path).convert("RGB").resize((3 * B, 2 * B), Image.LANCZOS)
        for c in sh["comps"]:
            piece = gen.crop((c["x"] * B, c["y"] * B, (c["x"] + c["w"]) * B, (c["y"] + c["h"]) * B))
            orig = source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"])
            ok, chroma, shape, black = piece_check(orig, piece, c["w"], c["h"])
            if color:  # negro y forma se miden en la pieza cruda; el color, ya igualado
                piece = match_color(piece, orig)
                chroma = piece_check(orig, piece, c["w"], c["h"])[1]
                ok = chroma <= 0.19 and black <= 0.01 and shape >= 0.45
            if not ok:
                print(f"  rechazada tex_{c['tex']} ({c['bx']},{c['by']}): color {chroma:.2f} forma {shape:.2f} negro {black * 100:.0f}%")
                pending.append(c); continue
            accepted += 1
            arr = np.asarray(piece)
            if c["w"] == 1 and c["h"] == 1:
                axes = tile_axes(orig)
                if axes: arr = make_tileable(arr, axes)
            rgb = pixelize(Image.fromarray(arr), pixel, colors)
            alpha = orig.split()[3].resize(rgb.size, Image.NEAREST)
            res = rgb.convert("RGBA"); res.putalpha(alpha)
            dst = atlas(c["tex"])
            dst.paste(res, (c["bx"] * B, c["by"] * B))
        print(f"hoja {i:02d}: revisada")
    (out / "zonas_pendientes.json").write_text(json.dumps(pending, indent=1), "utf-8")
    print(f"Aceptadas {accepted}, pendientes {len(pending)} (zonas_pendientes.json: siguiente ronda con --ronda {ronda + 1}).")
    dest = HD_TEX if apply else out / "atlas_hd"
    dest.mkdir(parents=True, exist_ok=True)
    for t, a in atlases.items():
        a.save(dest / f"tex_{t}.png")
    print(f"{len(atlases)} atlas HD -> {dest}" + ("" if apply else " (simulación; --aplicar para instalarlos)"))


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
    z = sub.add_parser("zonas-preparar"); z.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/ullathorpe_zonas")
    z.add_argument("--ronda", type=int, default=0, help="0 = todo; N = rehacer lo pendiente de la ronda anterior")
    zi = sub.add_parser("zonas-importar"); zi.add_argument("generadas", type=Path, help="carpeta con hoja_NN.png")
    zi.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/ullathorpe_zonas")
    zi.add_argument("--pixel", type=int, default=2); zi.add_argument("--colores", type=int, default=48)
    zi.add_argument("--aplicar", action="store_true"); zi.add_argument("--ronda", type=int, default=0)
    zi.add_argument("--igualar-color", action="store_true", help="media y desvío por canal de cada pieza = los del original")
    a = parser.parse_args()
    if a.cmd == "preparar": prepare(a.salida)
    elif a.cmd == "importar": import_generated(a.familia, a.imagen, a.salida, a.pixel, a.colores, a.aplicar)
    elif a.cmd == "zonas-preparar": zones_prepare(a.salida, a.ronda)
    elif a.cmd == "zonas-importar": zones_import(a.salida, a.generadas, a.pixel, a.colores, a.aplicar, a.ronda, a.igualar_color)
    else:
        for n, f in FAMILIES.items(): print(n, "-", ", ".join(s[4] for s in f["sets"]))


if __name__ == "__main__":
    sys.exit(main())
