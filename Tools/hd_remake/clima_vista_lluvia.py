"""Vista de lluvia sin Unity: la de AOMapWeather hoy vs la propuesta en capas (mismo presupuesto de 200 sprites).

  python Tools/hd_remake/clima_vista_lluvia.py SALIDA.jpg
"""
import sys, json
import numpy as np
from PIL import Image, ImageFilter
sys.path.insert(0, 'Tools/hd_remake')
import dungeon_vista_p1 as v
import preview_luces as pl
pl.S, pl.HD, pl.T = v.K, True, v.T
K = v.K
m = pl.load(1)
m['env'] = dict(m['env'], baseLight=0xF5F5F5)      # 13 h: DayColor ~ 245
x0, y0 = pl.best_window(m, 20, 15)
W, H = 20, 15
base = v.render_original(m, x0, y0, W, H)          # float 0..1, sin clima
drop = np.asarray(Image.open('Assets/Resources/AOMigrator/WorldV07/Textures/tex_15063.png').convert('RGBA').crop((57, 39, 68, 54)), np.float32) / 255
rng = np.random.default_rng(3)
PH, PW = base.shape[:2]

def add_sprite(img, spr, cx, cy, alpha, scale, blur=0.0, stretch=1.0, mode='add', tint=(1, 1, 1)):
    s = Image.fromarray((np.clip(spr, 0, 1) * 255).astype(np.uint8), 'RGBA')
    w, h = max(1, int(s.width * K * scale)), max(1, int(s.height * K * scale * stretch))
    s = s.resize((w, h), Image.BILINEAR if scale != 1 else Image.NEAREST)
    if blur:
        s = s.filter(ImageFilter.GaussianBlur(blur))
    a = np.asarray(s, np.float32) / 255
    left, top = int(cx - w / 2), int(cy - h / 2)
    x1, y1, x2, y2 = max(0, left), max(0, top), min(PW, left + w), min(PH, top + h)
    if x1 >= x2 or y1 >= y2:
        return
    sub = a[y1 - top:y2 - top, x1 - left:x2 - left]
    rgb = sub[..., :3] * np.array(tint, np.float32)
    if mode == 'add':
        img[y1:y2, x1:x2] += rgb * sub[..., 3:] * alpha
    else:
        k = sub[..., 3:] * alpha
        img[y1:y2, x1:x2] = img[y1:y2, x1:x2] * (1 - k) + rgb * k

# A: hoy (200 gotas iguales, aditivas, pantalla)
A = base.copy()
for _ in range(200):
    add_sprite(A, drop, rng.uniform(0, PW), rng.uniform(0, PH), 1.0, 1.0)

# B: propuesta (misma cuenta: 90 lejos + 70 medio + 25 cerca + 15 salpicaduras) + clima en la luz
B = base.copy()
B = B * np.array([0.80, 0.84, 0.93], np.float32)                    # día de lluvia: más frío, menos saturado
g = B.mean(2, keepdims=True); B = g + (B - g) * 0.8
for n, sc, al, st, bl, tint in ((90, 0.55, 0.30, 1.0, 0, (0.8, 0.85, 1.0)),
                                (70, 0.9, 0.55, 1.3, 0, (0.9, 0.95, 1.0)),
                                (25, 1.7, 0.45, 1.9, 1.6, (1, 1, 1))):
    for _ in range(n):
        add_sprite(B, drop, rng.uniform(0, PW), rng.uniform(0, PH), al, sc * rng.uniform(0.85, 1.15), bl, st, tint=tint)
ring = np.zeros((12, 20, 4), np.float32)
yy, xx = np.mgrid[0:12, 0:20]
r = np.sqrt(((xx - 9.5) / 9.5) ** 2 + ((yy - 5.5) / 5.5) ** 2)
ring[..., :3] = 1; ring[..., 3] = np.clip(1 - np.abs(r - 0.8) * 5, 0, 1)
for _ in range(15):
    add_sprite(B, ring, rng.uniform(0, PW), rng.uniform(0, PH), 0.35, rng.uniform(0.5, 0.9))
fog = np.asarray(Image.fromarray((rng.random((6, 8)) * 255).astype(np.uint8)).resize((PW, PH), Image.BICUBIC), np.float32)[..., None] / 255
B = B * (1 - fog * 0.18) + np.array([0.62, 0.66, 0.74], np.float32) * fog * 0.18      # bancos de bruma suaves
out = Image.new('RGB', (PW * 2 + 24, PH), (20, 20, 20))
out.paste(v.to_img(A), (0, 0)); out.paste(v.to_img(B), (PW + 24, 0))
out.thumbnail((2400, 2400), Image.LANCZOS)
out.save(sys.argv[1], quality=88)
v.to_img(A).crop((PW // 3, PH // 3, PW // 3 + 1024, PH // 3 + 768)).save(sys.argv[1].replace('.jpg', '_a_det.jpg'), quality=90)
v.to_img(B).crop((PW // 3, PH // 3, PW // 3 + 1024, PH // 3 + 768)).save(sys.argv[1].replace('.jpg', '_b_det.jpg'), quality=90)
print('ventana', x0, y0, out.size)
