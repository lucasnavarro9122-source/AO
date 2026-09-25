"""Vista del juego sin Unity, fiel a los dos modos de luz, contra la imagen de Lucas (nube, 25/09).

  python Tools/hd_remake/dungeon_vista_p1.py SALIDA [--mapa-json RUTA] [--nombre p1] [--ventana X Y W H]

- Original (AOMapLighting + AOMapVertexLit): cada sprite = textura x luz de las 4 esquinas de SU casilla (la de apoyo),
  interpolada sobre todo el sprite; tope 1 (no hay sobrebrillo). La luz de cada esquina: ambiente del mapa y cada luz
  redonda la lleva a su color (Color32.Lerp por distancia^2 / radio^2).
- Mejorada (AOLighting2DV283): textura x (luz global + luces 2D sumadas, por píxel) y la viñeta propia. Sin bloom:
  el posproceso está apagado en el juego (PostProcessing = false).
Usa las texturas HD instaladas (Resources/AOMigratorHD) y el mapa armado (Assets o --mapa-json).
Las partículas del mapa (aditivas, no las toca la luz) van como foto fija aproximada: en el juego se mueven.
Escribe NOMBRE_luces.jpg (Original | Mejorada | su imagen) y NOMBRE_original.jpg / NOMBRE_mejorada.jpg a tamaño real."""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import preview_luces as pl  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
REF = ROOT / "docs/claude/nube/hd/referencias/p1_objetivo_lucas.webp"
K = 4                       # 4x
T = 32 * K


def decode(packed):
    packed &= 0xFFFFFF
    return np.array([(packed >> 16) & 255, (packed >> 8) & 255, packed & 255], np.float32) / 255


def corners_original(m):
    """AOMapLighting.Build: 4 colores por casilla (0 = abajo-izq, 1 = arriba-izq, 2 = abajo-der, 3 = arriba-der)."""
    base = m["env"].get("baseLight", 0)
    amb = decode(base) if base else np.array([120, 120, 120], np.float32) / 255
    W = m["xmax"] - m["xmin"] + 1
    H = m["ymax"] - m["ymin"] + 1
    c = np.tile(amb, (H, W, 4, 1)).astype(np.float32)
    for L in m["lights"]:
        rng = L["range"] - 99 if L["range"] >= 100 else L["range"]
        if rng <= 0 or rng > 128 or L["range"] < 100:
            continue   # las cuadradas (range < 100) no se usan en la demo
        col = decode(L["color"])
        radius2 = (rng * 32 + 16) ** 2
        for y in range(max(m["ymin"], L["y"] - rng), min(m["ymax"], L["y"] + rng) + 1):
            for x in range(max(m["xmin"], L["x"] - rng), min(m["xmax"], L["x"] + rng) + 1):
                for k in range(4):
                    vx = x * 32 + (32 if k >= 2 else 0)
                    vy = y * 32 + (32 if (k & 1) == 0 else 0)
                    d2 = (L["x"] * 32 + 16 - vx) ** 2 + (L["y"] * 32 + 16 - vy) ** 2
                    if d2 <= radius2:
                        cur = c[y - m["ymin"], x - m["xmin"], k]
                        c[y - m["ymin"], x - m["xmin"], k] = col + (cur - col) * (d2 / radius2)
    return c


def tint(size, cs):
    """Color de la luz sobre un sprite (AOMapVertexLit): bilineal de las 4 esquinas sobre todo el sprite."""
    w, h = size
    tx = np.linspace(0, 1, w, dtype=np.float32)[None, :, None]
    ty = np.linspace(1, 0, h, dtype=np.float32)[:, None, None]        # fila 0 = arriba
    lower = cs[0] + (cs[2] - cs[0]) * tx
    upper = cs[1] + (cs[3] - cs[1]) * tx
    return lower + (upper - lower) * ty


def draw_list(m, x0, y0, w, h, margin=4):
    """Qué se dibuja y en qué orden, como AOWorldManagerV07: capa 1, capa 2 (por fila), capa 3 y personajes (por fila),
    capa 4. Cada elemento: (imagen RGBA, x, y de la casilla de apoyo)."""
    spr = {s["id"]: s for s in m["sprites"]}
    inside = lambda c: x0 - margin <= c["x"] < x0 + w + margin and y0 - margin <= c["y"] < y0 + h + margin + 4  # noqa: E731
    cells = [c for c in m["cells"] if inside(c) and c["sprite"] in spr]
    out = []
    for layer in (1, 2):
        for c in sorted((c for c in cells if c["layer"] == layer), key=lambda c: (c["y"], c["x"])):
            s = pl.crop(spr[c["sprite"]])
            if s:
                out.append((s, c["x"], c["y"]))
    row = [(c["y"], c["x"], "c", c) for c in cells if c["layer"] == 3]
    row += [(n["y"], n["x"], "n", n) for n in m["npcs"] if inside(n)]
    for _, _, kind, c in sorted(row, key=lambda r: (r[0], r[1])):
        s = pl.crop(spr[c["sprite"]]) if kind == "c" else pl.npc_img(c)
        if s:
            out.append((s, c["x"], c["y"]))
    for c in cells:
        if c["layer"] == 4:
            s = pl.crop(spr[c["sprite"]])
            if s:
                out.append((s, c["x"], c["y"]))
    return out


def place(s, x, y, x0, y0):
    sw, sh = s.size
    return (x - 1) * T + T // 2 - sw // 2 - (x0 - 1) * T, y * T - sh - (y0 - 1) * T


def render_original(m, x0, y0, w, h):
    cor = corners_original(m)
    img = np.zeros((h * T, w * T, 3), np.float32)
    for s, x, y in draw_list(m, x0, y0, w, h):
        left, top = place(s, x, y, x0, y0)
        a = np.asarray(s, np.float32) / 255
        cy, cx = min(max(y - m["ymin"], 0), cor.shape[0] - 1), min(max(x - m["xmin"], 0), cor.shape[1] - 1)
        rgb = a[..., :3] * tint(s.size, cor[cy, cx])
        _paste(img, rgb, a[..., 3:], left, top)
    return particulas(img, m, x0, y0, w, h)


def lights_mejorada(m, x0, y0, w, h):
    """AOLighting2DV283.BuildMapLights + UpdateGlobal (mazmorra: baseLight x (0,85; 0,9; 1,1)); caída como la vista
    aprobada (preview_luces.light_propuesta, que es la que se copió al juego), sin el núcleo de brillo ni el bloom."""
    base = m["env"].get("baseLight", 0)
    d = decode(base) if base else np.array([.24, .30, .50], np.float32)
    amb = np.array([d[0] * .85, d[1] * .9, min(1, d[2] * 1.1)], np.float32) if base else d
    H, W = h * T, w * T
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    L = np.tile(amb, (H, W, 1))
    for l in m["lights"]:
        rng = l["range"] - 99 if l["range"] >= 100 else l["range"]
        cx, cy = (l["x"] - x0) * T + T // 2, (l["y"] - y0) * T + T // 2
        r = (max(2.5, rng * 1.25) if l["range"] >= 100 else max(3.5, rng * 1.6)) * T
        if cx < -r or cy < -r or cx > W + r or cy > H + r:
            continue
        col = decode(l["color"])
        warm = col.min() > .85
        moon = not warm and col[2] - col[0] >= .1 and col.max() - col.min() < .25   # AOLighting2DV283: luna suave
        if warm:
            col = np.array([1.0, .74, .46], np.float32)
        dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
        L += col * (np.clip(1 - dist, 0, 1) ** 2)[..., None] * (.55 if moon else 1.05)
        L += col * (np.clip(1 - dist / 1.8, 0, 1) ** 3)[..., None] * .22
    return L


def render_mejorada(m, x0, y0, w, h):
    img = np.zeros((h * T, w * T, 3), np.float32)
    for s, x, y in draw_list(m, x0, y0, w, h):
        left, top = place(s, x, y, x0, y0)
        a = np.asarray(s, np.float32) / 255
        _paste(img, a[..., :3], a[..., 3:], left, top)
    rgb = particulas(img * lights_mejorada(m, x0, y0, w, h), m, x0, y0, w, h)
    H, W, _ = rgb.shape
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx / W) * 2 - 1, (yy / H) * 2 - 1
    dark = 1 - np.clip(1 - 0.42 * (u * u + v * v) ** 1.2, 0.35, 1)       # AOLighting2DV283.VignetteSprite
    return rgb * (1 - dark[..., None])


def particulas(img, m, x0, y0, w, h, seed=7):
    """Foto fija aproximada de las partículas del mapa (AOMapParticleGroup): cada partícula en un momento al azar de
    su vida, sprite gris x color de sus esquinas (vienen en BGR), sumado (aditivo)."""
    defs = {d["id"]: d for d in json.loads((pl.WORLD / "particle_defs.json").read_text("utf-8-sig"))["definitions"]}
    rng = np.random.default_rng(seed)
    H, W, _ = img.shape
    for p in m.get("particles", []):
        d = defs.get(p["particle"])
        if not d or not (x0 - 12 <= p["x"] < x0 + w + 12 and y0 - 12 <= p["y"] < y0 + h + 12):
            continue
        cc = d.get("cornerColors") or [255] * 12
        color = np.array([np.mean(cc[2::3][:4]), np.mean(cc[1::3][:4]), np.mean(cc[0::3][:4])], np.float32) / 255
        fr = max(1, d.get("friction", 1))
        for _ in range(min(d["count"], 200)):
            opt = d["sprites"][rng.integers(len(d["sprites"]))]["frames"][0]
            s = pl.crop(opt)
            if s is None:
                continue
            if d.get("resize") and d.get("resizeX", 0) > 0:
                s = s.resize((d["resizeX"] * K, d["resizeY"] * K), Image.LANCZOS)
            o, v = d["origin"], d["velocity"]
            t = rng.integers(0, max(2, max(d["life"])))
            x = rng.integers(min(o[0], o[2]), max(o[0], o[2]) + 1) - 16.0
            y = rng.integers(min(o[1], o[3]), max(o[1], o[3]) + 1) - 16.0
            vx = rng.integers(min(v[0], v[1]), max(v[0], v[1]) + 1)
            vy = rng.integers(min(v[2], v[3]), max(v[2], v[3]) + 1)
            for _ in range(t):
                if d.get("gravity"):
                    vy += d.get("gravityStrength", 0)
                    if y > 0:
                        vy = d.get("bounceStrength", 0)
                mb = d.get("moveBounds") or [0, 0, 0, 0]
                if d.get("moveX"):
                    vx = rng.integers(min(mb[0], mb[1]), max(mb[0], mb[1]) + 1)
                if d.get("moveY"):
                    vy = rng.integers(min(mb[2], mb[3]), max(mb[2], mb[3]) + 1)
                x += int(vx / fr)
                y += int(vy / fr)
            a = np.asarray(s, np.float32) / 255
            glow = a[..., :3] * a[..., 3:] * color
            cx = ((p["x"] - x0) * 32 + 16 + x) * K
            by = ((p["y"] - y0 + 1) * 32 + y) * K
            left, top = int(cx - s.width / 2), int(by - s.height)
            x1, y1, x2, y2 = max(0, left), max(0, top), min(W, left + s.width), min(H, top + s.height)
            if x1 < x2 and y1 < y2:
                img[y1:y2, x1:x2] += glow[y1 - top:y2 - top, x1 - left:x2 - left]
    return img


def _paste(img, rgb, a, left, top):
    H, W, _ = img.shape
    h, w = a.shape[:2]
    x1, y1, x2, y2 = max(0, left), max(0, top), min(W, left + w), min(H, top + h)
    if x1 >= x2 or y1 >= y2:
        return
    sa = a[y1 - top:y2 - top, x1 - left:x2 - left]
    img[y1:y2, x1:x2] = img[y1:y2, x1:x2] * (1 - sa) + rgb[y1 - top:y2 - top, x1 - left:x2 - left] * sa


def to_img(arr):
    return Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("salida", type=Path)
    ap.add_argument("--mapa", type=int, default=1011)
    ap.add_argument("--mapa-json", type=Path)
    ap.add_argument("--nombre", default="p1")
    ap.add_argument("--ventana", type=int, nargs=4, default=[14, 22, 26, 22], metavar=("X", "Y", "W", "H"))
    args = ap.parse_args()
    pl.S, pl.HD, pl.T = K, True, T
    m = pl.load(args.mapa)
    if args.mapa_json:
        env = m["env"]
        m = json.loads(args.mapa_json.read_text("utf-8"))
        m["env"] = env
    x0, y0, w, h = args.ventana
    orig = to_img(render_original(m, x0, y0, w, h))
    mej = to_img(render_mejorada(m, x0, y0, w, h))
    args.salida.mkdir(parents=True, exist_ok=True)
    ref = Image.open(REF).convert("RGB")
    size = ref.size if (x0, y0, w, h) == (14, 22, 26, 22) else (w * 52, h * 52)
    a, b = orig.resize(size, Image.LANCZOS), mej.resize(size, Image.LANCZOS)
    a.save(args.salida / f"{args.nombre}_original.jpg", quality=90)
    b.save(args.salida / f"{args.nombre}_mejorada.jpg", quality=90)
    panels = [a, b] + ([ref] if size == ref.size else [])
    sheet = Image.new("RGB", (sum(p.width for p in panels) + 12 * (len(panels) - 1), size[1]), (20, 20, 20))
    xx = 0
    for p in panels:
        sheet.paste(p, (xx, 0))
        xx += p.width + 12
    sheet.thumbnail((2400, 2400), Image.LANCZOS)
    sheet.save(args.salida / f"{args.nombre}_luces.jpg", quality=88)
    print(f"-> {args.salida / (args.nombre + '_luces.jpg')} (Original | Mejorada{' | su imagen' if len(panels) == 3 else ''})")


if __name__ == "__main__":
    main()
