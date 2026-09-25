"""Compares a game-view screenshot against a reference of the same place (e.g. "Luz: Mejorada" vs "Original").

Lighting changes colors, not layout, so the comparison uses edge maps (gradient magnitude):
- corr0: correlation at the same position (layout unchanged -> high);
- shift: horizontal shift (in columns of the 192-wide edge map) that best matches the reference
  (the view moved or was duplicated -> not 0);
- halves: correlation between the left and right halves of the image itself (duplicated view -> high).
Usage: python Tools/qa_view_check.py reference.png candidate.png x y w h   (crop in image pixels)."""
import sys

import numpy as np
from PIL import Image

WIDTH = 192


def edges(path, box):
    x, y, w, h = box
    image = Image.open(path).convert('L').crop((x, y, x + w, y + h))
    image = image.resize((WIDTH, max(8, round(WIDTH * h / w))), Image.BILINEAR)
    a = np.asarray(image, dtype=np.float64)
    g = np.hypot(np.diff(a, axis=1)[:-1, :], np.diff(a, axis=0)[:, :-1])
    return (g - g.mean()) / (g.std() + 1e-9)


def corr(a, b):
    a = a - a.mean(); b = b - b.mean()
    return float((a * b).sum() / (np.sqrt((a * a).sum() * (b * b).sum()) + 1e-9))


def self_similarity(e):
    """Highest correlation of the image with itself shifted sideways by 15-85 % of its width (duplicate -> high)."""
    w = e.shape[1]
    return max(corr(e[:, :-s], e[:, s:]) for s in range(int(w * .15), int(w * .85)))


def strip_shifts(r, c, strips=3):
    """For each vertical strip of the candidate, the horizontal shift that best matches the reference."""
    w = c.shape[1]; step = w // strips; shifts = []
    for k in range(strips):
        a, b = k * step, (k + 1) * step
        best, best_s = -2.0, 0
        for s in range(-a, w - b + 1):
            value = corr(c[:, a:b], r[:, a + s:b + s])
            if value > best: best, best_s = value, s
        shifts.append(best_s)
    return shifts


def compare(reference, candidate, box):
    r, c = edges(reference, box), edges(candidate, box)
    return dict(corr0=round(corr(r, c), 3), shifts=strip_shifts(r, c),
                self=round(self_similarity(c), 3), self_ref=round(self_similarity(r), 3))


def verdict(m, min_corr=0.3, max_shift=3):
    problems = []
    moved = [s for s in m['shifts'] if abs(s) > max_shift]
    if moved: problems.append('la vista está corrida respecto de la referencia (franjas %s)' % m['shifts'])
    if m['self'] > max(0.5, m['self_ref'] + 0.25): problems.append('la imagen se repite a sí misma (duplicado, %.2f)' % m['self'])
    if m['corr0'] < min_corr: problems.append('no coincide con la referencia (corr0 %.2f)' % m['corr0'])
    return problems


if __name__ == '__main__':
    ref, cand = sys.argv[1], sys.argv[2]
    box = tuple(int(v) for v in sys.argv[3:7])
    m = compare(ref, cand, box)
    print(m, verdict(m) or 'OK')
