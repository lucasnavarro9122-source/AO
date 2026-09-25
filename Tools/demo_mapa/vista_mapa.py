"""Vista general de un mapa para diseñar la demo (sin Unity).

  python Tools/demo_mapa/vista_mapa.py SALIDA.png --mapa 392 [--px 8] [--sin-capas]
  python Tools/demo_mapa/vista_mapa.py SALIDA.png --archivo otra/carpeta/map_1011.json

Dibuja las capas 1-4 como el juego (reusa preview_luces) y encima:
- rojo: casilla bloqueada; verde: salida (con su destino); punto amarillo: NPC; cruz cian: luz;
- grilla cada 10 casillas con coordenadas, para elegir recortes (x, y de 1 a 100).
Solo lee los mapas; no escribe nada fuera de SALIDA."""
import argparse, json, sys
from pathlib import Path
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'hd_remake'))
import preview_luces as pl  # noqa: E402


def render(num, px=8, capas=True, archivo=None):
    if archivo:
        m = json.loads(Path(archivo).read_text('utf-8-sig')); m['env'] = {}
        num = m.get('mapNumber', num)
    else:
        m = pl.load(num)
    w, h = m['xmax'] - m['xmin'] + 1, m['ymax'] - m['ymin'] + 1
    if capas:
        base, _ = pl.albedo(m, m['xmin'], m['ymin'], w, h, chars=False, margin=2)
        img = base.resize((w * px, h * px), Image.BILINEAR).convert('RGBA')
    else:
        img = Image.new('RGBA', (w * px, h * px), (40, 40, 40, 255))
    over = Image.new('RGBA', img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    cx = lambda x: (x - m['xmin']) * px
    cy = lambda y: (y - m['ymin']) * px
    for b in m['blocks']:
        if b['flags']:
            d.rectangle((cx(b['x']), cy(b['y']), cx(b['x']) + px - 1, cy(b['y']) + px - 1), fill=(255, 0, 0, 70))
    for L in m['lights']:
        x, y = cx(L['x']) + px // 2, cy(L['y']) + px // 2
        d.line((x - 3, y, x + 3, y), fill=(0, 255, 255, 220)); d.line((x, y - 3, x, y + 3), fill=(0, 255, 255, 220))
    for n in m['npcs']:
        x, y = cx(n['x']) + px // 2, cy(n['y']) + px // 2
        d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(255, 220, 0, 255), outline=(0, 0, 0, 255))
    for e in m['exits']:
        d.rectangle((cx(e['x']), cy(e['y']), cx(e['x']) + px - 1, cy(e['y']) + px - 1), fill=(0, 255, 0, 200))
    for v in range(10, 101, 10):
        d.line((cx(v), 0, cx(v), img.height), fill=(255, 255, 255, 90))
        d.line((0, cy(v), img.width, cy(v)), fill=(255, 255, 255, 90))
        d.text((cx(v) + 2, 2), str(v), fill=(255, 255, 255, 255))
        d.text((2, cy(v) + 2), str(v), fill=(255, 255, 255, 255))
    img.alpha_composite(over)
    top = Image.new('RGBA', (img.width, 16), (0, 0, 0, 255))
    ImageDraw.Draw(top).text((4, 2), f"{num} {m['mapName'].strip()} | NPC {len(m['npcs'])} | salidas "
                             + ', '.join(f"({e['x']},{e['y']})->{e['destMap']}" for e in m['exits'])[:120], fill=(255, 255, 255, 255))
    out = Image.new('RGBA', (img.width, img.height + 16)); out.paste(top, (0, 0)); out.paste(img, (0, 16))
    return out


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('salida', type=Path)
    ap.add_argument('--mapa', type=int)
    ap.add_argument('--archivo', type=Path, help='un map_N.json fuera de Assets (por ejemplo, de otra rama)')
    ap.add_argument('--px', type=int, default=8)
    ap.add_argument('--sin-capas', action='store_true')
    a = ap.parse_args()
    if not a.mapa and not a.archivo: ap.error('falta --mapa o --archivo')
    render(a.mapa, a.px, not a.sin_capas, a.archivo).convert('RGB').save(a.salida)
    print(a.salida)
