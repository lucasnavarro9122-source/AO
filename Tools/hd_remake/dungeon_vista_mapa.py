"""Vista general de un mapa entero con la luz Original (dungeon_vista_p1.render_original), en tramos.

  python Tools/hd_remake/dungeon_vista_mapa.py SALIDA.jpg [mapa]
"""
import sys
from PIL import Image
sys.path.insert(0, 'Tools/hd_remake')
import dungeon_vista_p1 as v
import preview_luces as pl
pl.S, pl.HD, pl.T = v.K, True, v.T
m = pl.load(int(sys.argv[2]) if len(sys.argv) > 2 else 1011)
x0, y0, x1, y1 = 10, 7, 90, 95
TILE = 24                                        # px por casilla en la vista general
out = Image.new('RGB', ((x1 - x0) * TILE, (y1 - y0) * TILE))
CW, CH = 20, 22
for cy in range(y0, y1, CH):
    for cx in range(x0, x1, CW):
        w, h = min(CW, x1 - cx), min(CH, y1 - cy)
        img = v.to_img(v.render_original(m, cx, cy, w, h)).resize((w * TILE, h * TILE), Image.LANCZOS)
        out.paste(img, ((cx - x0) * TILE, (cy - y0) * TILE))
out.save(sys.argv[1], quality=85)
print(out.size)
