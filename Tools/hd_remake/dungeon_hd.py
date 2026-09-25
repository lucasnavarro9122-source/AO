"""Remaster HD de pisos y paredes del dungeon de la demo, piso por piso con Lucas (nube, 25/09).

Reusa ulla_piloto.py (bloques de 128, 4x, Seedream 5 Pro, validación de piezas) con tres diferencias:
- entran las paredes: las piezas con transparencia van sobre fucsia (#FF00FF) y al importar se recortan
  con la silueta original (mismo tamaño y forma);
- el piso no se repite: se generan variantes del mismo juego de 4x4 con los bordes iguales, en una textura
  nueva solo de la demo (tex_9xxxx), que el builder mezcla en el piso;
- los rectángulos negros (vacío, GRH 1) dentro del piso se rellenan con un "abismo" del tema, también en la
  textura nueva. El tex_1 del juego (el negro de todos los mapas) no se toca.

  python Tools/hd_remake/dungeon_hd.py preparar P1 [--salida DIR]   # hojas de entrada + prompts + manifest
  python Tools/hd_remake/dungeon_hd.py costo P1                      # hojas y créditos estimados

Solo lee el juego y escribe en --salida: no gasta créditos ni toca Assets."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
import ulla_piloto as up  # noqa: E402

ROOT = up.ROOT
BLOCK, SCALE = up.BLOCK, up.SCALE
MAGENTA = (255, 0, 255, 255)
CREDITS_PER_SHEET = 2.5   # Seedream 5 Pro, 2k, 3:2 (medido en las zonas de Ullathorpe)
REFS = ROOT / "docs/claude/nube/hd/referencias"

# Por piso: mapa de la demo, texturas propias (piso y paredes), el juego de 4x4 del piso, tema y referencias.
PISOS = {
    "P1": {
        "mapa": 1011, "texturas": [5095], "piso": {"tex": 5095, "sx": 512, "sy": 288}, "textura_nueva": 90001,
        "omitir": [[5095, 4, 3]],   # franja de piso (sale de las variantes) y sombras semitransparentes (sin detalle)
        "apagar": {"paredes": [0.92, 0.97, 1.0], "piso": [0.9, 0.95, 0.97]},   # la referencia ya es apagada
        "vacio_transparente": True,  # el negro del vacío dentro de las paredes sigue el degradé del original (queda negro)
        # Ronda 3 (Lucas 25/09): su referencia es este mismo mapa remasterizado (p1_objetivo_lucas.webp): igual a eso y
        # con todavía más detalle. Vacío siempre negro.
        "referencias": ["p1_objetivo_lucas.webp"],
        "colores": 64,
        "variantes_descartar": [6],   # ronda 3: el bloque 6 dibujó un bloque de piedra en 3D
        "piso_grilla": True,          # ladrillos en grilla regular: se corta sobre las juntas (ver corte_por_juntas)
        "tema": ("the SAME dungeon shown in the reference image, remastered exactly in its look: cold blue-grey carved "
                 "marble and stone walls with cracks, darker veins and worn chipped edges, finely detailed fluted pillars "
                 "and balustrades, small rubble and pebbles at the foot of the walls; add even MORE micro-detail than the "
                 "reference (hairline cracks, chips, dust, tiny pebbles, a subtle cold sparkle in the stone). Dungeon "
                 "darkness with a muted cold palette. Areas that are black in the source stay PURE BLACK (the void): "
                 "never mist, fog or color."),
        "piso_diseno": ("redraw the floor exactly like the floor of the reference image: large dark blue-grey slate "
                        "bricks in a regular running bond, fine cracks, chipped corners and a subtle speckle; keep the "
                        "brick joints on a regular grid"),
        "variantes": ["clean bricks",
                      "a few cracked bricks and small chips",
                      "scattered pebbles and dust along some joints",
                      "one worn darker brick and a network of hairline cracks",
                      "a faint cold frost sparkle in the joints",
                      "a couple of loose stones and a broken corner"],
        "abismo": None,
    },
}


def prompt_piezas(tema: str) -> str:
    return (
        "The FIRST reference image is the source: a sheet of 6 square blocks (3 columns x 2 rows) cut from the texture "
        "atlas of a top-down RPG dungeon: floors, walls, pillars and wall edges. Some neighbouring blocks form one bigger "
        "piece: keep them continuous. Redraw THIS source sheet as high-detail dark-fantasy pixel art, like modern "
        f"souls-like HD pixel-art games, as {tema} Keep EXACTLY the same composition: same pieces in the same "
        "positions, same silhouettes, shapes, edges and proportions; every MAGENTA (#FF00FF) area is transparent "
        "background and must stay flat pure magenta, never painted over. Rich micro-detail (stone grain, cracks, frost "
        "crystals, snow). The other images are STYLE references only: copy their pixel-art rendering, palette and "
        "detail density, but do NOT copy their layout, objects or lighting. Flat neutral lighting: no cast shadows, no "
        "light spots, no vignette, no glow. Orthographic top-down view as in the source. No borders, gaps or text."
    )


def prompt_variantes(tema: str, variantes: list[str], abismo: str | None, diseno: str | None = None) -> str:
    slots = "; ".join(f"block {i + 1}: {v}" for i, v in enumerate(variantes))
    n = len(variantes)
    layout = (f"{diseno}. All {n} floor blocks share EXACTLY the same stone layout and joints (so they fit together "
              "seamlessly in any order and each one repeats seamlessly)" if diseno else
              f"Every floor block keeps EXACTLY the same stone and brick layout, joints and proportions as the source "
              "(so they fit together seamlessly)")
    tail = f" Blocks {n + 1} and {n + 2}: {abismo}, seamless." if abismo else ""
    return (
        "The FIRST reference image is the source: a sheet of 6 square blocks (3 columns x 2 rows) of a top-down RPG "
        f"dungeon floor. Blocks 1 to {n} are the SAME floor tile; redraw each one as high-detail dark-fantasy "
        f"pixel art, like modern souls-like HD pixel-art games, as {tema} {layout}, and only the surface detail "
        f"changes: {slots}.{tail} The other images are STYLE references only: copy their pixel-art rendering, palette and "
        "detail density, not their layout. Flat neutral lighting: no cast shadows, no light spots, no vignette, no glow. "
        "Orthographic top-down view. No borders, gaps or text between blocks."
    )


def componentes(piso: dict) -> list[dict]:
    """Bloques de 128 de las texturas propias que usa el mapa (capas 1-4), unidos si un sprite ocupa varios."""
    m = json.loads((up.MAPS_DIR / f"map_{piso['mapa']}.json").read_text("utf-8-sig"))
    spr = {s["id"]: s for s in m["sprites"]}
    parent, uses = {}, {}

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for c in m["cells"]:
        s = spr.get(c["sprite"])
        if not s or s["fileNum"] not in piso["texturas"]:
            continue
        blocks = [(s["fileNum"], bx, by) for by in range(s["sy"] // BLOCK, (s["sy"] + s["height"] - 1) // BLOCK + 1)
                  for bx in range(s["sx"] // BLOCK, (s["sx"] + s["width"] - 1) // BLOCK + 1)]
        for b in blocks:
            parent.setdefault(b, b)
            uses[b] = uses.get(b, 0) + 1
        for b in blocks[1:]:
            parent[find(b)] = find(blocks[0])
    comps = {}
    for b in parent:
        comps.setdefault(find(b), set()).add(b)
    out = []
    for c in comps.values():
        f = next(iter(c))[0]
        xs, ys = [b[1] for b in c], [b[2] for b in c]
        out.append({"tex": f, "bx": min(xs), "by": min(ys), "w": max(xs) - min(xs) + 1, "h": max(ys) - min(ys) + 1,
                    "uses": sum(uses[b] for b in c)})
    skip = {tuple(o) for o in piso.get("omitir", [])}
    out = [c for c in out if not any((c["tex"], c["bx"] + i, c["by"] + j) in skip for i in range(c["w"]) for j in range(c["h"]))]
    return sorted(out, key=lambda c: (-(c["w"] * c["h"]), c["tex"], c["by"], c["bx"]))


def sobre_magenta(img: Image.Image) -> Image.Image:
    bg = Image.new("RGBA", img.size, MAGENTA)
    bg.alpha_composite(img.convert("RGBA"))
    return bg.convert("RGB")


def abismo_fuente(seed: int) -> Image.Image:
    """Base oscura azulada con ruido suave: le dice al modelo dónde va el abismo (el negro puro lo copia tal cual)."""
    rng = np.random.default_rng(seed)
    small = rng.normal(0, 1, (8, 8, 1))
    field = np.asarray(Image.fromarray(((small[..., 0] - small.min()) / np.ptp(small) * 255).astype(np.uint8))
                       .resize((BLOCK, BLOCK), Image.BICUBIC), np.float32)[..., None] / 255
    rgb = np.array([8, 14, 32], np.float32) + field * np.array([22, 34, 60], np.float32)
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8))


def preparar(nombre: str, out: Path):
    piso = PISOS[nombre]
    out.mkdir(parents=True, exist_ok=True)
    comps = componentes(piso)
    grandes = [c for c in comps if c["w"] > 3 or c["h"] > 2]
    if grandes:
        print("AVISO: piezas más grandes que 3x2 quedan afuera:", grandes)
    sheets = up.zone_sheets([c for c in comps if c not in grandes])
    for i, sh in enumerate(sheets):
        img = Image.new("RGBA", (3 * BLOCK, 2 * BLOCK), MAGENTA)
        for c in sh["comps"]:
            img.alpha_composite(sobre_magenta(up.source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"])).convert("RGBA"),
                                (c["x"] * BLOCK, c["y"] * BLOCK))
        img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST).convert("RGB").save(out / f"piezas_{i:02d}_entrada.png")
    # Hoja de variantes: el juego de 4x4 del piso en los primeros bloques, el abismo en los 2 últimos.
    p = piso["piso"]
    src = Image.open(up.TEX / f"tex_{p['tex']}.png").convert("RGBA").crop((p["sx"], p["sy"], p["sx"] + BLOCK, p["sy"] + BLOCK))
    var = Image.new("RGB", (3 * BLOCK, 2 * BLOCK))
    n = len(piso["variantes"])
    for i in range(n):
        var.paste(sobre_magenta(src), ((i % 3) * BLOCK, (i // 3) * BLOCK))
    for j in range(2 if piso.get("abismo") else 0):
        k = n + j
        var.paste(abismo_fuente(j), ((k % 3) * BLOCK, (k // 3) * BLOCK))
    var.resize((var.width * SCALE, var.height * SCALE), Image.NEAREST).save(out / "variantes_entrada.png")
    (out / "piezas_prompt.txt").write_text(prompt_piezas(piso["tema"]) + "\n", "utf-8")
    (out / "variantes_prompt.txt").write_text(prompt_variantes(piso["tema"], piso["variantes"], piso.get("abismo"),
                                                               piso.get("piso_diseno")) + "\n", "utf-8")
    manifest = {"piso": nombre, **{k: piso[k] for k in ("mapa", "texturas", "piso", "textura_nueva", "referencias")},
                "hojas_piezas": sheets, "variantes": n, "abismos": 2 if piso.get("abismo") else 0}
    (out / "manifest.json").write_text(json.dumps(manifest, indent=1), "utf-8")
    total = len(sheets) + 1
    print(f"{nombre}: {len(sheets)} hojas de piezas ({sum(len(s['comps']) for s in sheets)} piezas) + 1 de variantes "
          f"= {total} hojas, ~{total * CREDITS_PER_SHEET:.1f} créditos con {up.MODEL}. En {out}")


def quitar_magenta(arr: np.ndarray, fallback: np.ndarray, opaque: np.ndarray) -> tuple[np.ndarray, float]:
    """Lo que quedó fucsia dentro de la silueta, o el borde teñido de fucsia, vuelve al original ampliado."""
    a, f = arr.astype(np.int16), fallback.astype(np.int16)
    was_pink = np.minimum(f[..., 0], f[..., 2]) - f[..., 1] > 40          # rosado de verdad (el cristal violeta)
    pinkness = np.minimum(a[..., 0], a[..., 2]) - a[..., 1]
    near_clear = np.asarray(Image.fromarray((~opaque).astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(13))) > 0
    edge = opaque & near_clear                                            # franja de 6 px junto a lo transparente
    pink = opaque & ~was_pink & ((pinkness > 70) | (edge & (pinkness > 20)))
    out = arr.copy()
    out[pink] = fallback[pink]
    core = opaque & ~edge & ~was_pink & (pinkness > 70)
    return out, float(core.sum() / max(1, opaque.sum()))


def igualar_tono(pieces: list, strength: float = 0.7):
    """Acerca media y desvío por canal de cada pieza (solo piedra: lo opaco y no oscuro) a los de todas juntas.
    Así dos piezas vecinas generadas por separado no muestran el corte."""
    def stone(arr, alpha):
        lum = arr.mean(2)
        return arr[(alpha > 200) & (lum > 45)]
    allpx = np.concatenate([stone(a, al) for a, al in pieces if stone(a, al).size]) if pieces else None
    if allpx is None or not allpx.size:
        return [a for a, _ in pieces]
    gm, gs = allpx.mean(0), allpx.std(0) + 1e-6
    out = []
    for a, al in pieces:
        px = stone(a, al)
        if not px.size:
            out.append(a)
            continue
        m, sd = px.mean(0), px.std(0) + 1e-6
        f = a.astype(np.float32)
        t = (f - m) / sd * gs + gm
        mask = ((al > 200) & (f.mean(2) > 45))[..., None]
        f = np.where(mask, f * (1 - strength) + t * strength, f)
        out.append(np.clip(f, 0, 255).astype(np.uint8))
    return out


def apagar(arr: np.ndarray, sat: float, contraste: float, brillo: float) -> np.ndarray:
    f = arr.astype(np.float32)
    gray = f.mean(2, keepdims=True)
    f = gray + (f - gray) * sat
    m = f.mean((0, 1), keepdims=True)
    f = ((f - m) * contraste + m) * brillo
    return np.clip(f, 0, 255).astype(np.uint8)


def adyacencias(piso: dict):
    """Pares de bloques de pared que se tocan en el mapa (por el borde de sus sprites): (dir, arriba/izq, abajo/der)."""
    m = json.loads((up.MAPS_DIR / f"map_{piso['mapa']}.json").read_text("utf-8-sig"))
    spr = {s["id"]: s for s in m["sprites"]}
    items = []
    for c in m["cells"]:
        s = spr.get(c["sprite"])
        if s and s["fileNum"] in piso["texturas"] and c["layer"] in (2, 3) and s["width"] >= 64:
            left, bottom = c["x"] * 32 + 16 - s["width"] // 2, c["y"] * 32 + 32
            items.append((left, left + s["width"], bottom - s["height"], bottom, (s["fileNum"], s["sx"] // BLOCK, s["sy"] // BLOCK)))
    ends = {}
    for it in items:
        ends.setdefault(("H", it[1], it[3]), []).append(it)
    pairs = {}
    for it in items:
        for o in ends.get(("H", it[0], it[3]), []):
            if o[4] != it[4]:
                pairs[("H", o[4], it[4])] = pairs.get(("H", o[4], it[4]), 0) + 1
    for it in items:                       # o encima de it: la base de o es el tope de it
        for o in items:
            if o[0] == it[0] and o[3] == it[2] and o[4] != it[4]:
                pairs[("V", o[4], it[4])] = pairs.get(("V", o[4], it[4]), 0) + 1
    return {k: v for k, v in pairs.items() if v >= 2}


def fundir_bordes(pieces: dict, pairs: dict, band: int):
    """Los bordes de piezas que se tocan en el mapa pasan a tener el mismo perfil (promedio), con fundido."""
    w = (np.arange(band, dtype=np.float32) / band)[::-1]      # 1 en el borde, 0 hacia adentro
    for (d, a, b), _ in sorted(pairs.items(), key=lambda kv: -kv[1]):
        if a not in pieces or b not in pieces:
            continue
        A, Bp = pieces[a].astype(np.float32), pieces[b].astype(np.float32)
        if d == "H":
            e = (A[:, -1] + Bp[:, 0]) / 2
            A[:, -band:] = A[:, -band:] * (1 - w[::-1][None, :, None]) + e[:, None] * w[::-1][None, :, None]
            Bp[:, :band] = Bp[:, :band] * (1 - w[None, :, None]) + e[:, None] * w[None, :, None]
        else:
            e = (A[-1] + Bp[0]) / 2
            A[-band:] = A[-band:] * (1 - w[::-1][:, None, None]) + e[None] * w[::-1][:, None, None]
            Bp[:band] = Bp[:band] * (1 - w[:, None, None]) + e[None] * w[:, None, None]
        pieces[a], pieces[b] = A.astype(np.uint8), Bp.astype(np.uint8)


def parche_rediseno(img: Image.Image, umbral: float = 85, inset: int = 8):
    """Si el piso rediseñado (oscuro) ocupa solo un rectángulo de la hoja, devuelve (x0, y0, lado) de 3x2 cuadrados."""
    dark = np.asarray(img.convert("RGB"), np.float32).mean(2) < umbral
    rows, cols = np.where(dark.mean(1) > 0.3)[0], np.where(dark.mean(0) > 0.3)[0]
    if not len(rows) or not len(cols):
        return None
    y0, y1, x0, x1 = rows.min() + inset, rows.max() - inset, cols.min() + inset, cols.max() - inset
    if (x1 - x0) > 0.9 * img.width and (y1 - y0) > 0.9 * img.height:
        return None                                        # ocupa toda la hoja: la grilla normal sirve
    side = int(min((x1 - x0) / 3, (y1 - y0) / 2))
    return x0 + ((x1 - x0) - 3 * side) // 2, y0 + ((y1 - y0) - 2 * side) // 2, side


def corte_por_juntas(cells: list, periods: int = 2):
    """Piso de ladrillos en grilla regular (aparejo corrido, ladrillo 2:1): mide la hilada, ubica las juntas y corta cada
    bloque justo sobre ellas con un número entero de períodos. Así repite solo y todas las variantes encajan, sin fundidos."""
    def fold(profile, P):
        n = len(profile) // P
        f = np.array([profile[ph:ph + n * P:P].mean() for ph in range(P)])
        return int(f.argmin()), float(profile.mean() - f.min())
    g = [np.asarray(c.convert("L"), np.float32) for c in cells]
    H = min(a.shape[0] for a in g)
    contrast = {P: np.mean([fold(a.mean(1), P)[1] for a in g]) for P in range(H // 8, H // 4)}
    top = max(contrast.values())
    py = min(P for P, c in contrast.items() if c >= 0.85 * top)            # el menor período bien marcado = una hilada
    px = 2 * py                                                            # ladrillo 2:1 (las juntas verticales son tenues)
    out = []
    for c, a in zip(cells, g):
        y0 = fold(a.mean(1), py)[0]
        x0 = fold(a[y0 + py // 4:y0 + py - py // 4].mean(0), px)[0]
        w, h = periods * px, periods * 2 * py
        if x0 + w > a.shape[1]: x0 -= px
        if y0 + h > a.shape[0]: y0 -= 2 * py
        out.append(c.crop((max(0, x0), max(0, y0), max(0, x0) + w, max(0, y0) + h)))
    return out, px, py


def aplanar_luz(arr: np.ndarray, frac: float = 0.08) -> np.ndarray:
    """Saca el degradé de luz de fondo que trae un bloque generado (conserva el detalle): divide por su versión muy
    desenfocada, calculada sobre el bloque repetido 3x3 para que el borde no cambie al repetirlo."""
    h, w = arr.shape[:2]
    big = Image.fromarray(np.tile(arr, (3, 3, 1)))
    low = np.asarray(big.filter(ImageFilter.GaussianBlur(max(h, w) * frac)), np.float32)[h:2 * h, w:2 * w]
    f = arr.astype(np.float32) / (low + 1) * low.reshape(-1, 3).mean(0)
    return np.clip(f, 0, 255).astype(np.uint8)


def bloquear_bordes(var: np.ndarray, base: np.ndarray, band: int) -> np.ndarray:
    """Los bordes de una variante pasan a ser los de la base (con fundido): cualquier combinación encaja."""
    n = var.shape[0]
    d = np.minimum.reduce([np.arange(n)[:, None].repeat(n, 1), np.arange(n)[None, :].repeat(n, 0),
                           (n - 1 - np.arange(n))[:, None].repeat(n, 1), (n - 1 - np.arange(n))[None, :].repeat(n, 0)])
    w = np.clip(1 - d / band, 0, 1)[..., None]
    return (var.astype(np.float32) * (1 - w) + base.astype(np.float32) * w).astype(np.uint8)


def importar(nombre: str, out: Path, generadas: Path, aplicar: bool, pixel: int = 2, colores: int = 48):
    piso = PISOS[nombre]
    colores = piso.get("colores", colores)
    man = json.loads((out / "manifest.json").read_text("utf-8"))
    B = BLOCK * SCALE
    res_dir = out / "resultado"
    res_dir.mkdir(parents=True, exist_ok=True)
    atlases = {}

    def atlas(t):
        if t not in atlases:
            hd = up.HD_TEX / f"tex_{t}.png"
            if hd.exists():
                atlases[t] = Image.open(hd).convert("RGBA")
            else:
                src = Image.open(up.TEX / f"tex_{t}.png").convert("RGBA")
                atlases[t] = src.resize((src.width * SCALE, src.height * SCALE), Image.NEAREST)
        return atlases[t]
    informe, accepted = [], []
    for i, sh in enumerate(man["hojas_piezas"]):
        gen = Image.open(generadas / f"piezas_{i:02d}.png").convert("RGB").resize((3 * B, 2 * B), Image.LANCZOS)
        for c in sh["comps"]:
            piece = gen.crop((c["x"] * B, c["y"] * B, (c["x"] + c["w"]) * B, (c["y"] + c["h"]) * B))
            orig = up.source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"])
            big = orig.resize(piece.size, Image.NEAREST)
            alpha = np.asarray(big.split()[3])
            # Lo semitransparente son sombras negras del original (sobre el fucsia se veían fucsia oscuro): quedan las originales.
            arr, pink = quitar_magenta(np.asarray(piece), np.asarray(big.convert("RGB")), alpha >= 250)
            semi = (alpha > 0) & (alpha < 250)
            arr = arr.copy()
            arr[semi] = np.asarray(big.convert("RGB"))[semi]
            check = Image.fromarray(arr).convert("RGBA")
            check.putalpha(Image.fromarray(alpha))                     # los dos sobre el mismo fondo
            ok, chroma, shape, black = up.piece_check(sobre_magenta(orig), sobre_magenta(check), c["w"], c["h"])
            ok = shape >= 0.4 and black <= 0.02 and pink <= 0.03
            informe.append({"tex": c["tex"], "bx": c["bx"], "by": c["by"], "ok": ok, "forma": round(shape, 2),
                            "negro": round(black, 3), "fucsia": round(pink, 3), "color": round(chroma, 2)})
            if not ok:
                print(f"  rechazada tex_{c['tex']} ({c['bx']},{c['by']}): forma {shape:.2f} negro {black * 100:.1f}% fucsia {pink * 100:.1f}%")
                continue
            accepted.append((c, arr, alpha, np.asarray(big.convert("RGB"), np.float32).mean(2)))
    toned = igualar_tono([(arr, alpha) for _, arr, alpha, _ in accepted])
    if piso.get("apagar"):
        toned = [apagar(a, *piso["apagar"]["paredes"]) for a in toned]
    singles = {(c["tex"], c["bx"], c["by"]): a for (c, *_), a in zip(accepted, toned) if c["w"] == 1 and c["h"] == 1}
    fundir_bordes(singles, adyacencias(piso), B // 20)
    toned = [singles.get((c["tex"], c["bx"], c["by"]), a) for (c, *_), a in zip(accepted, toned)]
    for (c, _, alpha, orig_lum), arr in zip(accepted, toned):
        rgb = up.pixelize(Image.fromarray(arr), pixel, colores).convert("RGBA")
        if piso.get("vacio_transparente"):
            # Donde el original era el negro del vacío, la pieza sigue su mismo degradé y deja ver el abismo de abajo:
            # así ninguna pieza pinta ladrillos donde su vecina pinta niebla.
            fade = np.where(orig_lum < 48, np.clip((orig_lum - 6) / 42, 0, 1), 1.0)
            alpha = (alpha.astype(np.float32) * fade).astype(np.uint8)
        rgb.putalpha(Image.fromarray(alpha))
        atlas(c["tex"]).paste(rgb, (c["bx"] * B, c["by"] * B))
    # Variantes del piso y abismo -> textura nueva de la demo (4 x 2 bloques de 128 a 1x).
    raw = Image.open(generadas / "variantes.png").convert("RGB")
    patch = parche_rediseno(raw) if piso.get("piso_diseno") else None
    if patch:   # el modelo dibujó el piso nuevo como un rectángulo dentro de la hoja: se cortan 3x2 cuadrados de ahí
        x0, y0, side = patch
        blocks = [np.asarray(raw.crop((x0 + (k % 3) * side, y0 + (k // 3) * side, x0 + (k % 3 + 1) * side,
                                       y0 + (k // 3 + 1) * side)).resize((B, B), Image.LANCZOS)) for k in range(6)]
        print(f"piso: rectángulo rediseñado en ({x0},{y0}), 6 cuadrados de {side} px")
    else:   # grilla 3x2; se deja afuera un margen chico por las líneas que el modelo a veces traza entre bloques
        bw, bh = raw.width / 3, raw.height / 2
        m = int(min(bw, bh) * 0.015)
        cells = [raw.crop((int((k % 3) * bw) + m, int((k // 3) * bh) + m, int((k % 3 + 1) * bw) - m,
                           int((k // 3 + 1) * bh) - m)) for k in range(6)]
        if piso.get("piso_grilla"):
            n0 = man["variantes"]
            cut, px, py = corte_por_juntas(cells[:n0])
            cells[:n0] = cut
            print(f"piso: ladrillo de {px}x{py} px; cada bloque = 2x2 períodos, cortado sobre las juntas")
        blocks = [np.asarray(c.resize((B, B), Image.LANCZOS)) for c in cells]
    n = man["variantes"]
    drop = [k - 1 for k in piso.get("variantes_descartar", [])]   # variantes que salieron mal (1 = la primera)
    if drop:
        blocks = [b for k, b in enumerate(blocks[:n]) if k not in drop] + blocks[n:]
        n -= len(drop)
    if piso.get("color_piso"):
        cp = piso["color_piso"]
        ref = Image.open(REFS / cp["ref"]).convert("RGB").crop(tuple(cp["box"]))
        blocks[:n] = [np.asarray(up.match_color(Image.fromarray(b), ref.convert("RGBA"))) for b in blocks[:n]]
    if piso.get("apagar"):
        blocks[:n] = [apagar(b, *piso["apagar"]["piso"]) for b in blocks[:n]]
    if piso.get("piso_grilla"):   # ya repiten solos (cortados sobre las juntas): sin fundidos
        floors = [aplanar_luz(np.ascontiguousarray(b)) for b in blocks[:n]]
        ref = floors[0].astype(np.float32).reshape(-1, 3)
        rm, rs = ref.mean(0), ref.std(0) + 1e-6
        for k in range(1, n):   # mismo tono que la base: si no, cada bloque de 4x4 se nota como un cuadrado
            f = floors[k].astype(np.float32)
            fm, fs = f.reshape(-1, 3).mean(0), f.reshape(-1, 3).std(0) + 1e-6
            floors[k] = np.clip((f - fm) / fs * rs + rm, 0, 255).astype(np.uint8)
    else:
        base = up.make_tileable(blocks[0], "xy")
        floors = [base] + [bloquear_bordes(blocks[k], base, B // 10) for k in range(1, n)]
    abysses = []
    if man.get("abismos"):
        # Un abismo tiene que ser oscuro: si el modelo mezcló piso en uno, se usa el otro espejado.
        dark = [b for b in blocks[n:n + 2] if np.asarray(b, np.float32).mean() < 70]
        if not dark:
            raise SystemExit("ningún bloque de abismo salió oscuro: hay que regenerar la hoja de variantes")
        abyss0 = up.make_tileable(dark[0], "xy")
        second = dark[1] if len(dark) > 1 else np.ascontiguousarray(np.flipud(np.fliplr(dark[0])))
        abysses = [abyss0, bloquear_bordes(up.make_tileable(second, "xy"), abyss0, B // 10)]
    tiles = [np.asarray(up.pixelize(Image.fromarray(t), pixel, colores)) for t in floors + abysses]
    new_hd = Image.new("RGBA", (4 * B, 2 * B), (0, 0, 0, 0))   # fila 0-1: pisos en orden (4 por fila); abismos al final
    for k, t in enumerate(tiles):
        new_hd.paste(Image.fromarray(t), ((k % 4) * B, (k // 4) * B))
    new_1x = new_hd.resize((new_hd.width // SCALE, new_hd.height // SCALE), Image.BOX)
    p = piso["piso"]   # el piso del atlas (el del mapa original) queda como la variante base
    atlas(p["tex"]).paste(Image.fromarray(tiles[0]).convert("RGBA"), (p["sx"] * SCALE, p["sy"] * SCALE))
    dest_hd = up.HD_TEX if aplicar else res_dir / "hd"
    dest_1x = up.TEX if aplicar else res_dir / "1x"
    dest_hd.mkdir(parents=True, exist_ok=True)
    dest_1x.mkdir(parents=True, exist_ok=True)
    for t, a in atlases.items():
        a.save(dest_hd / f"tex_{t}.png")
    new_id = piso["textura_nueva"]
    new_hd.save(dest_hd / f"tex_{new_id}.png")
    new_1x.save(dest_1x / f"tex_{new_id}.png")
    (out / "importar_informe.json").write_text(json.dumps(informe, indent=1), "utf-8")
    ok = sum(1 for r in informe if r["ok"])
    print(f"{nombre}: {ok}/{len(informe)} piezas aceptadas; {n} variantes de piso y {len(abysses)} de abismo en tex_{new_id}. "
          f"-> {dest_hd}" + ("" if aplicar else " (simulación: --aplicar para instalar en Assets)"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("preparar")
    p.add_argument("piso", choices=sorted(PISOS))
    p.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/dungeon")
    c = sub.add_parser("costo")
    c.add_argument("piso", choices=sorted(PISOS))
    i = sub.add_parser("importar", help="generadas/ con piezas_NN.png y variantes.png")
    i.add_argument("piso", choices=sorted(PISOS))
    i.add_argument("generadas", type=Path)
    i.add_argument("--salida", type=Path, default=ROOT.parent / "AO_HD/dungeon")
    i.add_argument("--aplicar", action="store_true", help="instalar en Assets (atlas HD y textura nueva de la demo)")
    a = ap.parse_args()
    if a.cmd == "preparar":
        preparar(a.piso, a.salida / a.piso)
    elif a.cmd == "importar":
        importar(a.piso, a.salida / a.piso, a.generadas, a.aplicar)
    else:
        comps = componentes(PISOS[a.piso])
        n = len(up.zone_sheets([x for x in comps if x["w"] <= 3 and x["h"] <= 2])) + 1
        print(f"{a.piso}: {n} hojas, ~{n * CREDITS_PER_SHEET:.1f} créditos")


if __name__ == "__main__":
    main()
