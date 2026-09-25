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
import warnings
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
        "apagar": {"paredes": [0.97, 1.0, 1.0], "piso": [1.0, 1.0, 1.0]},   # la referencia ya es apagada
        "vacio_transparente": True,  # el negro del vacío dentro de las paredes sigue el degradé del original (queda negro)
        # Ronda 3 (Lucas 25/09): su referencia es este mismo mapa remasterizado (p1_objetivo_lucas.webp): igual a eso y
        # con todavía más detalle. Vacío siempre negro.
        "referencias": ["p1_objetivo_lucas.webp"],
        "colores": 64,
        # Ronda 4: recortes ampliados de su imagen como referencia (piso, paredes, escombros): el piso ya sale como el
        # de la referencia, así que estilo_piso (retoque por código) queda apagado.
        "estilo_piso": None,
        "tono_piso": {"media": [55, 66, 102], "desvio": [20, 21, 25], "brillos": 0.0025},   # su piso en zonas sin luz
        "piso_grilla": True,          # ladrillos en grilla regular: se corta sobre las juntas (ver corte_por_juntas)
        # Ronda 5 (Lucas: "100 % el estilo de esta imagen, hasta las piedras del piso"): el piso sale de SU imagen,
        # ladrillo por ladrillo (ver piso_desde_referencia); no usa la hoja de variantes generada.
        "piso_ref": {"imagen": "p1_objetivo_lucas.webp", "variantes": 8, "brillo": 64, "contraste": 1.6, "saturacion": 1.05,
                     "zonas": [[160, 95, 495, 395], [200, 400, 480, 560], [200, 580, 860, 660], [200, 655, 700, 805],
                               [700, 595, 1330, 795], [200, 805, 610, 1150], [100, 1000, 200, 1150]],
                     "color_zonas": [[700, 660, 1330, 795], [200, 805, 610, 1150]]},   # sin luz de color: dan la paleta
        # Paredes: salen de su imagen, que es este mismo mapa (ver paredes_desde_referencia). "registro" se midió con
        # registrar_referencia (escala x, escala y, corrimiento x, y); las piezas que no son pared quedan afuera.
        "paredes_ref": {"imagen": "p1_objetivo_lucas.webp", "vista": [14, 22, 26, 22], "registro": [1.61, 1.59, 7, 0],
                        "omitir": [[5095, 4, 1]]},
        # Lo que quede generado (paredes y escombros) con la paleta de su imagen (una sola transformación para todas).
        "color_paredes": {"imagen": "p1_objetivo_lucas.webp", "fuerza": 0.85,
                          "cajas": [[900, 405, 1358, 560], [75, 0, 130, 800], [540, 0, 600, 390], [800, 812, 1358, 870]]},
        "color_escombros": {"imagen": "p1_objetivo_lucas.webp",     # son piedra de las paredes: misma paleta
                            "cajas": [[900, 405, 1358, 560], [75, 0, 130, 800], [540, 0, 600, 390], [800, 812, 1358, 870]]},
        # Lo sacado de su imagen es su pantalla en las zonas sin luz. El juego (luz Original, AOMapVertexLit) dibuja
        # textura x luz, con tope 1: con la base del P1 (0xA0A0A0 = 0,627) la textura va x1/0,627 = 1,6 para que lo
        # que no tiene luz se vea igual a su imagen, y bajo los haces (luz de luna ~1) llegue a su brillo.
        "compensar_luz": 1.6,
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


def estilo_piso(floors: list, estilo: dict, seed: int = 7) -> list:
    """Lleva el piso al de la referencia de Lucas: grano de la piedra marcado, color y contraste medidos en su imagen,
    juntas azul casi negro (las líneas finas claras del generado), bisel claro arriba-izquierda de cada ladrillo y un
    brillo frío de puntitos. Las variantes se mueven juntas (mismo tono)."""
    joints, sharp = [], []
    for f in floors:
        g = Image.fromarray(f).convert("L")
        opening = g.filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MaxFilter(9))
        th = np.clip(np.asarray(g, np.float32) - np.asarray(opening, np.float32), 0, 255)
        joints.append(np.clip(th / estilo.get("juntas_umbral", 28), 0, 1))
        sharp.append(np.asarray(Image.fromarray(f).filter(ImageFilter.UnsharpMask(radius=3, percent=160, threshold=2)), np.float32))
    stone = np.concatenate([sh[j < 0.3].reshape(-1, 3) for sh, j in zip(sharp, joints)])
    m, sd = stone.mean(0), stone.std(0) + 1e-6
    tm, ts = np.array(estilo["media"], np.float32), np.array(estilo["desvio"], np.float32)
    dark = np.array(estilo.get("junta_color", [16, 20, 34]), np.float32)
    rng = np.random.default_rng(seed)
    res = []
    for sh, j in zip(sharp, joints):
        o = (sh - m) / sd * ts + tm
        jj = Image.fromarray((j * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))
        jm = np.asarray(jj, np.float32)[..., None] / 255
        bev = np.clip(np.roll(np.roll(jm, 4, 0), 4, 1) - jm, 0, 1)            # justo abajo-derecha de la junta
        o = o * (1 + 0.28 * bev)
        o = o * (1 - 0.9 * jm) + dark * 0.9 * jm
        h, w = o.shape[:2]
        k = int(h * w * estilo.get("brillos", 0.002) / 4)
        ys, xs = rng.integers(0, h - 1, k), rng.integers(0, w - 1, k)
        spark = np.array(estilo.get("brillo_color", [190, 210, 255]), np.float32)
        for dy in (0, 1):
            for dx in (0, 1):
                o[ys + dy, xs + dx] = o[ys + dy, xs + dx] * 0.35 + spark * 0.65
        res.append(np.clip(o, 0, 255).astype(np.uint8))
    return res


def tono_piso(floors: list, tono: dict, seed: int = 7) -> list:
    """Color y contraste del piso medidos en la referencia (zonas sin luz) y un brillo frío de puntitos; todas las
    variantes se mueven igual, así no cambia el tono entre bloques."""
    allpx = np.concatenate([f.reshape(-1, 3) for f in floors]).astype(np.float32)
    m, sd = allpx.mean(0), allpx.std(0) + 1e-6
    tm, ts = np.array(tono["media"], np.float32), np.array(tono["desvio"], np.float32)
    rng = np.random.default_rng(seed)
    out = []
    for f in floors:
        o = (f.astype(np.float32) - m) / sd * ts + tm
        h, w = o.shape[:2]
        k = int(h * w * tono.get("brillos", 0.002) / 4)
        ys, xs = rng.integers(0, h - 1, k), rng.integers(0, w - 1, k)
        for dy in (0, 1):
            for dx in (0, 1):
                o[ys + dy, xs + dx] = o[ys + dy, xs + dx] * 0.4 + np.array([185, 205, 250], np.float32) * 0.6
        out.append(np.clip(o, 0, 255).astype(np.uint8))
    return out


def _suavizar(p: np.ndarray, s: float, axis: int = 0) -> np.ndarray:
    r = int(3 * s) + 1
    k = np.exp(-np.arange(-r, r + 1) ** 2 / (2 * s * s))
    k /= k.sum()
    pad = [(0, 0)] * p.ndim
    pad[axis] = (r, r)
    return np.apply_along_axis(lambda v: np.convolve(v, k, "valid"), axis, np.pad(p, pad, mode="reflect"))


def _juntas(perfil: np.ndarray, ventana: int, umbral, sep: int, maximo: bool) -> list:
    """Posiciones de las juntas en un perfil 1D: extremos locales que pasan el umbral, separados más de sep px."""
    p = _suavizar(perfil, 1.0)
    out = []
    for i in range(2, len(p) - 2):
        w = p[max(0, i - ventana):i + ventana + 1]
        if (p[i] == w.max() and p[i] >= umbral) if maximo else (p[i] == w.min() and p[i] < umbral):
            if not out or i - out[-1] > sep:
                out.append(i)
            elif (p[i] > p[out[-1]]) if maximo else (p[i] < p[out[-1]]):
                out[-1] = i
    return out


def piso_desde_referencia(cfg: dict, B: int, seed: int = 11) -> list:
    """Bloques de piso armados con los ladrillos de la imagen de Lucas (mismo relieve, grietas y craquelado).

    1. Se le saca la luz: brillo / brillo promedio de la zona (sigma 14 px), así no quedan los haces ni los reflejos.
    2. En cada zona de piso se buscan las juntas horizontales y, en cada fila, las verticales -> biblioteca de ladrillos.
    3. Cada bloque (4 filas de una casilla) se arma con ladrillos al azar, espejados y con una leve diferencia de tono;
       las filas pares cierran con junta en los bordes y las impares cruzan el borde con un ladrillo compartido
       (aparejo trabado sin costura: ver bordes_piso).
    4. El color sale de la paleta del piso de su imagen (brillo -> color medido en zonas sin luz de color)."""
    img = np.asarray(Image.open(REFS / cfg["imagen"]).convert("RGB"), np.float32)
    lum = img.mean(2)
    ratio = lum / (_suavizar(_suavizar(lum, 14, 0), 14, 1) + 1e-3)
    bricks = []
    for x0, y0, x1, y1 in cfg["zonas"]:
        z = ratio[y0:y1, x0:x1]
        rows = _juntas(z.mean(1), 4, np.median(z.mean(1)) * 0.93, 35, maximo=False)
        for ra, rb in zip(rows, rows[1:]):
            h = rb - ra
            if not 46 <= h <= 62:
                continue
            band = z[ra + int(h * .15):rb - int(h * .15)]
            cols = _juntas((band < 0.8).mean(0), 6, 0.5, 25, maximo=True)
            bricks += [(x0 + a, y0 + ra, x0 + b, y0 + rb) for a, b in zip(cols, cols[1:]) if 50 <= b - a <= 180]
    # Los ladrillos bajo un haz de luz de su imagen traen el destello pintado (rayitas claras): afuera.
    bl = np.array([lum[y0:y1, x0:x1].mean() for x0, y0, x1, y1 in bricks])
    n0 = len(bricks)
    bricks = [b for b, v in zip(bricks, bl) if v <= np.median(bl) * 1.2]
    print(f"piso: {n0 - len(bricks)} ladrillos descartados por estar bajo un haz de luz")
    pix = np.concatenate([img[y0:y1, x0:x1].reshape(-1, 3) for x0, y0, x1, y1 in cfg["color_zonas"]])
    plum = pix.mean(1)
    lut = np.full((256, 3), np.nan, np.float32)
    for v in range(256):
        m = np.abs(plum - v) < 3
        if m.sum() > 30:
            lut[v] = pix[m].mean(0)
    ok, xs = ~np.isnan(lut[:, 0]), np.arange(256)
    for c in range(3):
        lut[:, c] = np.interp(xs, xs[ok], lut[ok, c])
    rng = np.random.default_rng(seed)
    T = B // 4
    order, pos = list(rng.permutation(len(bricks))), [0]

    def siguiente():   # la biblioteca se recorre entera antes de volver a usar un ladrillo
        if pos[0] >= len(order):
            order.extend(rng.permutation(len(bricks)))
        pos[0] += 1
        return bricks[order[pos[0] - 1]]

    def brick(b, w):
        x0, y0, x1, y1 = b
        r = np.asarray(Image.fromarray(ratio[y0:y1, x0:x1]).resize((w, T), Image.BICUBIC))
        if rng.random() < .5:
            r = r[:, ::-1]
        if rng.random() < .5:
            r = r[::-1]
        return r * rng.normal(1.0, 0.07)

    def llenar(ancho):   # ladrillos enteros que cierran justo en `ancho` (el último se acorta o se escala la fila)
        picked, widths, total = [], [], 0
        while total < ancho:
            picked.append(siguiente())
            x0, y0, x1, y1 = picked[-1]
            widths.append(int(round((x1 - x0) * T / (y1 - y0))))
            total += widths[-1]
        if widths[-1] - (total - ancho) >= T * 0.85:
            widths[-1] -= total - ancho
        else:
            widths = [int(round(w * ancho / total)) for w in widths]
            widths[-1] += ancho - sum(widths)
        return [brick(b, w) for b, w in zip(picked, widths)]

    # Bordes compatibles: en las filas trabadas (1 y 3) el ladrillo que cruza el borde entre dos bloques es uno de
    # 2 ladrillos compartidos por fila (tipo 0 o 1). La variante v tiene borde izquierdo v % 2 y derecho (v // 2) % 2;
    # quien arma el mapa pone a la derecha de un bloque de borde derecho t una variante de borde izquierdo t.
    edge = {}
    natural = lambda b: int(round((b[2] - b[0]) * T / (b[3] - b[1])))
    for t in (0, 1):
        for r in (1, 3):
            b = siguiente()
            while not T * 1.2 <= natural(b) <= T * 2.4:   # sin estirar: uno que ya tenga ese largo
                b = siguiente()
            edge[t, r] = brick(b, natural(b) - natural(b) % 2)

    out = []
    for v in range(cfg.get("variantes", 8)):
        L, R = v % 2, (v // 2) % 2
        rows = []
        for r in range(4):
            if r % 2 == 0:
                rows.append(np.concatenate(llenar(B), 1))
            else:
                el, er = edge[L, r], edge[R, r]
                right_half, left_half = el[:, el.shape[1] // 2:], er[:, :er.shape[1] // 2]
                mid = llenar(B - right_half.shape[1] - left_half.shape[1])
                rows.append(np.concatenate([right_half] + mid + [left_half], 1))
        blk = np.concatenate(rows, 0)
        target = np.clip(cfg.get("brillo", 64) * np.power(np.clip(blk, 0, 4), cfg.get("contraste", 1.4)), 0, 255)
        rgb = lut[target.astype(np.uint8)]
        g = rgb.mean(2, keepdims=True)
        rgb = g + (rgb - g) * cfg.get("saturacion", 1.0)
        out.append(np.clip(rgb, 0, 255).astype(np.uint8))
    print(f"piso: {len(bricks)} ladrillos de {cfg['imagen']}; {len(out)} bloques ({pos[0]} ladrillos usados, espejados)")
    return out


def paredes_desde_referencia(cfg: dict, comps: list) -> dict:
    """Paredes sacadas de la imagen de Lucas (que es este mismo mapa remasterizado, casilla por casilla).

    1. La vista del mapa (cfg["vista"]) se dibuja a 1x como el juego y se registra contra su imagen
       (cfg["registro"] = escala x, escala y, corrimiento x, y: medido con registrar_referencia).
    2. Cada pieza de pared de la textura se busca en todas sus apariciones visibles (lo que tapa otra cosa no cuenta)
       y se toma, píxel a píxel, la mediana de las apariciones: se van los haces de luz y los reflejos de una sola.
    3. El brillo de color que quedó pintado (luces del mapa, que el juego ya pone) se neutraliza con la paleta de sus
       paredes sin luz.
    4. Lo que su imagen no muestra (esquinas que no están en la vista, partes tapadas) se completa por analogía:
       para cada parche del original se busca el parche más parecido del original que sí se vio y se copia su versión
       de la referencia (el marco usa los mismos mármoles y pilares, así que calza).
    Devuelve {(tex, bx, by): rgb a 4x} para las piezas de cfg (salvo las de cfg["omitir"])."""
    import preview_luces as pl
    x0, y0, w, h = cfg["vista"]
    sx_, sy_, dx, dy = cfg["registro"]
    T, K, B = pl.T, SCALE, BLOCK
    m = pl.load(cfg["mapa"])
    spr = {s["id"]: s for s in m["sprites"]}
    ref = Image.open(REFS / cfg["imagen"]).convert("RGB")
    W, H = w * T, h * T
    ids = np.full((H, W), -1, np.int32)
    items = []

    def draw(img, x, y, frame):
        sw, sh = img.size
        left, top = (x - 1) * T + T // 2 - sw // 2 - (x0 - 1) * T, y * T - sh - (y0 - 1) * T
        a = np.asarray(img.convert("RGBA"))[..., 3] > 128
        items.append((left, top, frame))
        ya, yb, xa, xb = max(0, top), min(H, top + sh), max(0, left), min(W, left + sw)
        if ya < yb and xa < xb:
            ids[ya:yb, xa:xb][a[ya - top:yb - top, xa - left:xb - left]] = len(items) - 1

    inside = lambda c: x0 - 3 <= c["x"] < x0 + w + 3 and y0 - 3 <= c["y"] < y0 + h + 6
    cells = [c for c in m["cells"] if inside(c)]
    for layer in (1, 2):   # mismo orden que el juego: capas 1-2, después 3 y personajes por fila, al final techos
        for c in cells:
            if c["layer"] == layer and c["sprite"] in spr and (s := pl.crop(spr[c["sprite"]])):
                draw(s, c["x"], c["y"], spr[c["sprite"]])
    row = [(c["y"], c["x"], c) for c in cells if c["layer"] == 3 and c["sprite"] in spr]
    row += [(n["y"], n["x"], n) for n in m["npcs"] if inside(n)]
    for _, _, c in sorted(row, key=lambda r: (r[0], r[1])):
        npc = "sprite" not in c
        if (s := pl.npc_img(c) if npc else pl.crop(spr[c["sprite"]])):
            draw(s, c["x"], c["y"], None if npc else spr[c["sprite"]])
    for c in cells:
        if c["layer"] == 4 and c["sprite"] in spr and (s := pl.crop(spr[c["sprite"]])):
            draw(s, c["x"], c["y"], spr[c["sprite"]])
    skip = {tuple(o) for o in cfg.get("omitir", [])}
    fl = cfg.get("piso_bloque")   # [tex, sx, sy]: el juego de 4x4 del piso no es pared
    seen = {}
    for comp in comps:
        key = (comp["tex"], comp["bx"], comp["by"])
        if key in skip:
            continue
        cx0, cy0, cx1, cy1 = comp["bx"] * B, comp["by"] * B, (comp["bx"] + comp["w"]) * B, (comp["by"] + comp["h"]) * B
        stack = []
        for idx, (left, top, s) in enumerate(items):
            if not s or s["fileNum"] != comp["tex"]:
                continue
            if fl and s["fileNum"] == fl[0] and fl[1] <= s["sx"] < fl[1] + B and fl[2] <= s["sy"] < fl[2] + B:
                continue
            ax0, ay0 = max(cx0, s["sx"]), max(cy0, s["sy"])
            ax1, ay1 = min(cx1, s["sx"] + s["width"]), min(cy1, s["sy"] + s["height"])
            if ax0 >= ax1 or ay0 >= ay1:
                continue
            vx0, vy0 = left + ax0 - s["sx"], top + ay0 - s["sy"]
            ow, oh = (ax1 - ax0) * K, (ay1 - ay0) * K
            vis = np.asarray(Image.fromarray((ids == idx).astype(np.uint8) * 255).transform(
                (ow, oh), Image.AFFINE, (1 / K, 0, vx0, 0, 1 / K, vy0), Image.NEAREST)) > 0
            if not vis.any():
                continue
            rgb = np.asarray(ref.transform((ow, oh), Image.AFFINE, (sx_ / K, 0, vx0 * sx_ + dx, 0, sy_ / K, vy0 * sy_ + dy),
                                           Image.BICUBIC), np.float32).copy()
            rgb[~vis] = np.nan
            full = np.full(((cy1 - cy0) * K, (cx1 - cx0) * K, 3), np.nan, np.float32)
            full[(ay0 - cy0) * K:(ay0 - cy0) * K + oh, (ax0 - cx0) * K:(ax0 - cx0) * K + ow] = rgb
            stack.append(full)
        if stack:
            st = np.stack(stack)
            n = (~np.isnan(st[..., 0])).sum(0)
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", RuntimeWarning)   # píxeles que no se vieron nunca: NaN -> 0
                val = np.nan_to_num(np.nanmedian(st, 0))
            seen[key] = (val, n > 0, len(stack))
    # 3. brillo de color pintado -> paleta de las paredes sin luz (brillo -> color)
    allpix = np.concatenate([v[c] for v, c, _ in seen.values()])
    exc = allpix[:, 2] - allpix[:, :2].mean(1)
    calm = allpix[exc < np.percentile(exc, 70)]
    clum = calm.mean(1)
    lut = np.full((256, 3), np.nan, np.float32)
    for v in range(256):
        sel = np.abs(clum - v) < 3
        if sel.sum() > 20:
            lut[v] = calm[sel].mean(0)
    ok, xs = ~np.isnan(lut[:, 0]), np.arange(256)
    for c in range(3):
        lut[:, c] = np.interp(xs, xs[ok], lut[ok, c])
    e0, e1 = np.percentile(exc, 80), np.percentile(exc, 97)
    for key, (v, cov, n) in seen.items():
        e = v[..., 2] - v[..., :2].mean(2)
        wgt = np.clip((e - e0) / max(e1 - e0, 1), 0, 1)[..., None] * cov[..., None]
        fixed = lut[np.clip(v.mean(2) * 0.9, 0, 255).astype(np.uint8)]
        seen[key] = (v * (1 - wgt) + fixed * wgt, cov, n)
    # 4. completar por analogía (parches de 16 px a 1x, paso 4 en la fuente y 8 en el destino)
    P, st_src, st_dst = 16, 4, 8
    srcA, srcB = [], []
    for key, (v, cov, n) in seen.items():
        comp = next(c for c in comps if (c["tex"], c["bx"], c["by"]) == key)
        o = np.asarray(up.source_region(key[0], key[1], key[2], comp["w"], comp["h"]), np.float32)
        cov1 = cov.reshape(o.shape[0], K, o.shape[1], K).all((1, 3))
        for yy in range(0, o.shape[0] - P + 1, st_src):
            for xx in range(0, o.shape[1] - P + 1, st_src):
                pa = o[yy:yy + P, xx:xx + P]
                if cov1[yy:yy + P, xx:xx + P].all() and pa[..., 3].min() > 250:
                    srcA.append(pa[..., :3].reshape(-1))
                    srcB.append(v[yy * K:(yy + P) * K, xx * K:(xx + P) * K])
    srcA = np.asarray(srcA, np.float32)
    na = (srcA ** 2).sum(1)
    win = np.outer(np.hanning(P * K + 2)[1:-1], np.hanning(P * K + 2)[1:-1])[..., None] + 1e-3
    out = {}
    for comp in comps:
        key = (comp["tex"], comp["bx"], comp["by"])
        if key in skip:
            continue
        o = np.asarray(up.source_region(*key, comp["w"], comp["h"]), np.float32)
        v, cov, _ = seen.get(key, (np.zeros((o.shape[0] * K, o.shape[1] * K, 3), np.float32),
                                   np.zeros((o.shape[0] * K, o.shape[1] * K), bool), 0))
        opaque = np.asarray(Image.fromarray(o[..., 3].astype(np.uint8)).resize((o.shape[1] * K, o.shape[0] * K), Image.NEAREST)) > 0
        if (opaque & ~cov).sum() > 0 and len(srcA):
            acc = np.zeros(v.shape, np.float32)
            wsum = np.zeros(v.shape[:2] + (1,), np.float32)
            pos = [(yy, xx) for yy in range(0, o.shape[0] - P + 1, st_dst) for xx in range(0, o.shape[1] - P + 1, st_dst)]
            pos = [(yy, xx) for yy, xx in pos if o[yy:yy + P, xx:xx + P, 3].max() > 0
                   and not cov[yy * K:(yy + P) * K, xx * K:(xx + P) * K].all()]
            if pos:
                q = np.asarray([o[yy:yy + P, xx:xx + P, :3].reshape(-1) for yy, xx in pos], np.float32)
                best = np.argmin(na[None] - 2 * q @ srcA.T, 1)
                for (yy, xx), b in zip(pos, best):
                    acc[yy * K:(yy + P) * K, xx * K:(xx + P) * K] += srcB[b] * win
                    wsum[yy * K:(yy + P) * K, xx * K:(xx + P) * K] += win
            filled = acc / np.maximum(wsum, 1e-6)
            # donde se vio, lo visto; el borde se funde unos px para que no quede costura
            cm = np.asarray(Image.fromarray(cov.astype(np.uint8) * 255).filter(ImageFilter.GaussianBlur(3)), np.float32)[..., None] / 255
            cm = np.where(wsum > 0, cm, 1.0) * cov[..., None] + np.where(wsum > 0, cm, cov[..., None]) * (~cov[..., None])
            v = v * cm + filled * (1 - cm)
        out[key] = np.clip(v, 0, 255).astype(np.uint8)
        print(f"  pared tex_{key[0]} ({key[1]},{key[2]}): vista {seen[key][2] if key in seen else 0} veces, "
              f"{(cov & opaque).sum() / max(opaque.sum(), 1) * 100:.0f} % de su imagen")
    return out


def registrar_referencia(cfg: dict) -> tuple:
    """Escala y corrimiento de la imagen de Lucas respecto de la vista a 1x: se alinea el negro del vacío."""
    import preview_luces as pl
    x0, y0, w, h = cfg["vista"]
    base, _ = pl.albedo(pl.load(cfg["mapa"]), x0, y0, w, h, chars=True, margin=3)
    o = base.convert("L")
    ref = Image.open(REFS / cfg["imagen"]).convert("RGB")
    rv = np.asarray(ref, np.float32).mean(2) < 14
    best, q = None, 4
    rq = np.asarray(Image.fromarray(rv.astype(np.uint8) * 255).resize((ref.width // q, ref.height // q), Image.BOX)) > 127
    for s in np.arange(1.40, 1.80, 0.01):   # grueso, a 1/4: una escala para los dos ejes
        for dx in range(-40, 41, 4):
            for dy in range(-40, 41, 4):
                best = _probar(o, rq, s, s, dx, dy, best, q)
    _, s, _, dx, dy = best
    best = None
    for sx in np.arange(s - 0.02, s + 0.021, 0.005):   # fino, a tamaño real
        for sy in np.arange(s - 0.02, s + 0.021, 0.005):
            for ddx in range(dx - 4, dx + 5):
                for ddy in range(dy - 4, dy + 5):
                    best = _probar(o, rv, sx, sy, ddx, ddy, best)
    return tuple(round(float(v), 4) for v in best[1:])


def _probar(o, rv, sx, sy, dx, dy, best, q=1):
    img = o.transform((rv.shape[1], rv.shape[0]), Image.AFFINE, (q / sx, 0, -dx / sx, 0, q / sy, -dy / sy),
                      Image.BILINEAR, fillcolor=128)
    b = 40 // q
    sc = (rv[b:-b, b:-b] == (np.asarray(img) < 14)[b:-b, b:-b]).mean()
    return (sc, sx, sy, dx, dy) if best is None or sc > best[0] else best


def bordes_piso(v: int) -> tuple:
    """Tipo de borde (izquierdo, derecho) de la variante v de piso_desde_referencia: a la derecha de un bloque con
    borde derecho t va uno con borde izquierdo t (el builder y la vista previa lo respetan)."""
    return v % 2, (v // 2) % 2


def color_de_referencia(arrs: list, alphas: list, cfg: dict) -> list:
    """Una sola transformación (media y desvío por canal) que lleva el conjunto de piezas a la paleta de las cajas
    de la imagen de referencia; así las piezas no cambian entre sí."""
    img = np.asarray(Image.open(REFS / cfg["imagen"]).convert("RGB"), np.float32)
    ref = np.concatenate([img[y0:y1, x0:x1].reshape(-1, 3) for x0, y0, x1, y1 in cfg["cajas"]])
    ref = ref[ref.mean(1) > 12]                                   # sin el negro del vacío
    own = np.concatenate([a.reshape(-1, 3)[(al.reshape(-1) > 250) & (a.reshape(-1, 3).mean(1) > 12)]
                          for a, al in zip(arrs, alphas)]).astype(np.float32)
    om, os_, rm, rs = own.mean(0), own.std(0) + 1e-6, ref.mean(0), ref.std(0)
    k = cfg.get("fuerza", 1.0)
    out = []
    for a in arrs:
        f = a.astype(np.float32)
        t = (f - om) / os_ * rs + rm
        out.append(np.clip(f + (t - f) * k, 0, 255).astype(np.uint8))
    return out


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
    if piso.get("color_paredes"):
        toned = color_de_referencia(toned, [alpha for _, _, alpha, _ in accepted], piso["color_paredes"])
    if piso.get("paredes_ref"):   # lo que se vio en su imagen reemplaza a lo generado (y entra aunque se haya rechazado)
        cfg = {**piso["paredes_ref"], "mapa": piso["mapa"], "piso_bloque": [piso["piso"]["tex"], piso["piso"]["sx"], piso["piso"]["sy"]]}
        comps = [c for sh in man["hojas_piezas"] for c in sh["comps"]]
        walls = paredes_desde_referencia(cfg, comps)
        have = {(c["tex"], c["bx"], c["by"]): k for k, (c, *_) in enumerate(accepted)}
        for c in comps:
            key = (c["tex"], c["bx"], c["by"])
            if key not in walls:
                continue
            orig = up.source_region(c["tex"], c["bx"], c["by"], c["w"], c["h"])
            big = orig.resize((c["w"] * B, c["h"] * B), Image.NEAREST)
            alpha = np.asarray(big.split()[3])
            arr = walls[key].copy()
            semi = (alpha > 0) & (alpha < 250)
            arr[semi] = np.asarray(big.convert("RGB"))[semi]   # sombras semitransparentes: las del original
            if key in have:
                toned[have[key]] = arr
            else:
                accepted.append((c, arr, alpha, np.asarray(big.convert("RGB"), np.float32).mean(2)))
                toned.append(arr)
    k_luz = piso.get("compensar_luz", 1.0)
    if k_luz != 1.0:
        toned = [np.clip(a.astype(np.float32) * k_luz, 0, 255).astype(np.uint8) for a in toned]
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
    if piso.get("piso_ref"):
        floors, abysses = piso_desde_referencia(piso["piso_ref"], B), []
        n = len(floors)
    else:
        floors, abysses, n = variantes_generadas(piso, man, generadas, B)
    if k_luz != 1.0:
        floors = [np.clip(f.astype(np.float32) * k_luz, 0, 255).astype(np.uint8) for f in floors]
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
    decoracion(generadas, dest_hd, dest_1x, piso.get("color_escombros"), k_luz)
    (out / "importar_informe.json").write_text(json.dumps(informe, indent=1), "utf-8")
    ok = sum(1 for r in informe if r["ok"])
    print(f"{nombre}: {ok}/{len(informe)} piezas aceptadas; {n} variantes de piso y {len(abysses)} de abismo en tex_{new_id}. "
          f"-> {dest_hd}" + ("" if aplicar else " (simulación: --aplicar para instalar en Assets)"))


def variantes_generadas(piso: dict, man: dict, generadas: Path, B: int):
    """Rondas 1-4: el piso sale de la hoja de variantes generada (3x2), cortada sobre las juntas."""
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
    if piso.get("apagar") and not piso.get("estilo_piso"):
        blocks[:n] = [apagar(b, *piso["apagar"]["piso"]) for b in blocks[:n]]
    if piso.get("piso_grilla"):   # ya repiten solos (cortados sobre las juntas): sin fundidos
        floors = [aplanar_luz(np.ascontiguousarray(b)) for b in blocks[:n]]
        ref = floors[0].astype(np.float32).reshape(-1, 3)
        rm, rs = ref.mean(0), ref.std(0) + 1e-6
        for k in range(1, n):   # mismo tono que la base: si no, cada bloque de 4x4 se nota como un cuadrado
            f = floors[k].astype(np.float32)
            fm, fs = f.reshape(-1, 3).mean(0), f.reshape(-1, 3).std(0) + 1e-6
            floors[k] = np.clip((f - fm) / fs * rs + rm, 0, 255).astype(np.uint8)
        if piso.get("estilo_piso"):
            floors = estilo_piso(floors, piso["estilo_piso"])
        if piso.get("tono_piso"):
            floors = tono_piso(floors, piso["tono_piso"])
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
    return floors, abysses, n


DECOR_ID = 90002   # decoración de la demo (escombros, nieblas y haces de luz): ver decoracion()


def escombros_desde_hoja(img: Image.Image, B: int) -> list:
    """Hoja 3x2 de escombros sobre fucsia -> 6 sprites RGBA de 64x64 (a 4x = 256): se saca el fucsia y el borde
    teñido, y se centra cada montón con su base abajo (el sprite se dibuja apoyado en la casilla)."""
    out = []
    bw, bh = img.width / 3, img.height / 2
    for k in range(6):
        cell = np.asarray(img.crop((int((k % 3) * bw), int((k // 3) * bh), int((k % 3 + 1) * bw), int((k // 3 + 1) * bh))).convert("RGB"), np.int16)
        pink = np.minimum(cell[..., 0], cell[..., 2]) - cell[..., 1]
        alpha = np.clip((90 - pink) * 4, 0, 255).astype(np.uint8)       # fucsia -> 0, piedra -> 255, borde suave
        ys, xs = np.where(alpha > 128)
        if not len(ys):
            continue
        cx, bottom = int(np.median(xs)), int(np.percentile(ys, 98))
        side = int(bw * 64 / 128 / 0.9)                                    # 64 px de juego; el montón ocupa ~90 %
        x0, y0 = max(0, cx - side // 2), max(0, bottom - side + side // 10)
        rgba = np.dstack([cell.clip(0, 255).astype(np.uint8), alpha])
        rgb = rgba[..., :3].astype(np.float32)
        rgb[pink > 20] = rgb[pink > 20] * [0.6, 1.0, 0.75]                  # quita el tinte fucsia del borde
        rgb = rgb * 1.2                                                    # un poco más claros, como en la referencia
        rgba[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
        sprite = Image.fromarray(rgba, "RGBA").crop((x0, y0, x0 + side, y0 + side)).resize((64 * SCALE, 64 * SCALE), Image.LANCZOS)
        out.append(sprite)
    return out


def _ruido(rng, H: int, W: int, octavas=(4, 8, 16, 32)) -> np.ndarray:
    """Ruido suave de varias escalas (0-1), para niebla sin bloques."""
    acc = np.zeros((H, W), np.float32)
    for k, o in enumerate(octavas):
        g = (rng.random((o, max(2, o * W // H))) * 255).astype(np.uint8)
        acc += np.asarray(Image.fromarray(g).resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(W / o / 3)),
                          np.float32) / 255 / (k + 1)
    acc -= acc.min()
    return acc / max(acc.max(), 1e-6)


def nieblas(n: int = 3, color=(200, 214, 255), seed: int = 5) -> list:
    """Jirones de niebla fría (128x64 de juego) al pie de las paredes, como los de su imagen: un par de volutas
    definidas dentro de una elipse que se desvanece del todo antes del borde."""
    rng = np.random.default_rng(seed)
    W, H = 128 * SCALE, 64 * SCALE
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    out = []
    for i in range(n):
        f = _ruido(rng, H, W, (4, 8, 16, 32, 64))
        cx, cy = W * (0.42 + 0.16 * rng.random()), H * 0.58
        env = np.clip(1 - (((xx - cx) / (W * 0.40)) ** 2 + ((yy - cy) / (H * 0.40)) ** 2), 0, 1) ** 1.3
        env *= np.sin(np.pi * xx / W) * np.sin(np.pi * yy / H)          # 0 en todo el borde
        a = np.clip((f - 0.42) * 3.2, 0, 1) * env * 0.9
        img = np.zeros((H, W, 4), np.uint8)
        img[..., :3] = color
        img[..., 3] = (a * 255).astype(np.uint8)
        out.append(Image.fromarray(img, "RGBA").filter(ImageFilter.GaussianBlur(2)))
    return out


def haces(n: int = 2, color=(200, 214, 255), seed: int = 9) -> list:
    """Haces de luz de luna como los de su imagen (128x256 de juego, casi verticales). La luz de verdad la ponen las
    luces de luna del mapa (multiplican y dejan ver la piedra); el calco suma un velo muy suave y los destellos."""
    rng = np.random.default_rng(seed)
    W, H = 128 * SCALE, 256 * SCALE
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    out = []
    for i in range(n):
        xt, xb = W * (0.66 - 0.1 * i), W * (0.34 + 0.1 * i)          # de arriba a la derecha hacia abajo a la izquierda
        cx = xt + (xb - xt) * yy / H
        wob = np.asarray(Image.fromarray((rng.random((8, 2)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), np.float32) / 255
        band = np.exp(-(((xx - cx) / (W * (0.12 + 0.05 * wob))) ** 2))
        core = np.exp(-(((xx - cx) / (W * 0.05)) ** 2))
        env = np.clip(yy / (H * 0.2), 0, 1) * np.clip((H - yy) / (H * 0.25), 0, 1)
        grain = _ruido(rng, H, W, (8, 32, 64))
        specks = (rng.random((H, W)) > 0.992).astype(np.float32)
        big = (rng.random((H, W)) > 0.9993).astype(np.float32)
        sp = np.asarray(Image.fromarray((specks * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), np.float32) / 255 * 4
        sp += np.asarray(Image.fromarray((big * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.2)), np.float32) / 255 * 12
        a = band * env * (0.14 + 0.12 * grain) + core * env * 0.10 + np.clip(sp, 0, 1) * band * env * 0.95
        img = np.zeros((H, W, 4), np.uint8)
        img[..., :3] = color
        img[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)
        out.append(Image.fromarray(img, "RGBA"))
    return out


def halos(tipos=((160, (150, 195, 255)), (224, (200, 150, 255)))) -> list:
    """Resplandor del piso bajo una luz de color: chico celeste (brillos) y grande violeta (portal). Va pintado del
    color: en la luz Original el sprite toma la luz de SU casilla (que queda fuera del radio de la luz)."""
    out = []
    for lado, color in tipos:
        W = lado * SCALE
        yy, xx = np.mgrid[0:W, 0:W].astype(np.float32)
        r = np.sqrt((xx - W / 2) ** 2 + (yy - W / 2) ** 2) / (W / 2)
        a = np.clip(1 - r, 0, 1) ** 2.0 * 0.55
        img = np.zeros((W, W, 4), np.uint8)
        img[..., :3] = color
        img[..., 3] = np.clip(a * 255, 0, 255).astype(np.uint8)
        out.append(Image.fromarray(img, "RGBA"))
    return out


def decoracion(generadas: Path, dest_hd: Path, dest_1x: Path, color: dict | None = None, k_luz: float = 1.0):
    """tex_90002 (512x512 a 1x). Lo lee el builder (DEMO_DECOR en demo_map_builder.py): si cambia, cambiar los dos.
    y 0: 6 escombros de 64x64 | y 64: 3 nieblas de 128x64 | y 128: 2 haces de luz de 128x256 (x 0 y 128)
    | x 256, y 128: halo celeste de 160x160 | x 256, y 288: halo violeta de 224x224."""
    rub = escombros_desde_hoja(Image.open(generadas / "escombros.png"), BLOCK * SCALE) if (generadas / "escombros.png").exists() else []
    if rub and color:   # piedra gris azulada como los montones de su imagen (no cristal)
        arrs = [np.asarray(r)[..., :3] for r in rub]
        alphas = [np.asarray(r)[..., 3] for r in rub]
        rub = [Image.fromarray(np.dstack([np.clip(a.astype(np.float32) * k_luz, 0, 255).astype(np.uint8), al]), "RGBA")
               for a, al in zip(color_de_referencia(arrs, alphas, color), alphas)]
    mist = nieblas()
    rays = haces()
    hd = Image.new("RGBA", (512 * SCALE, 512 * SCALE), (0, 0, 0, 0))
    for k, s in enumerate(rub):
        hd.paste(s, (k * 64 * SCALE, 0))
    for k, s in enumerate(mist):
        hd.paste(s, (k * 128 * SCALE, 64 * SCALE))
    for k, s in enumerate(rays):
        hd.paste(s, (k * 128 * SCALE, 128 * SCALE))
    glows = halos()
    hd.paste(glows[0], (256 * SCALE, 128 * SCALE))
    hd.paste(glows[1], (256 * SCALE, 288 * SCALE))
    hd.save(dest_hd / f"tex_{DECOR_ID}.png")
    hd.resize((hd.width // SCALE, hd.height // SCALE), Image.LANCZOS).save(dest_1x / f"tex_{DECOR_ID}.png")
    print(f"decoración: {len(rub)} escombros, {len(mist)} nieblas, {len(rays)} haces de luz y 2 halos en tex_{DECOR_ID}")


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
