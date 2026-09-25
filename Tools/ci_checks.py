"""Chequeos rapidos sin Unity para CI: sintaxis de los .py y JSON validos (incluye el catalogo .gz)."""
import gzip
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]


def tracked(pattern):
    out = subprocess.run(['git', 'ls-files', pattern], cwd=ROOT, capture_output=True, text=True, check=True).stdout
    return [ROOT / p for p in out.splitlines() if (ROOT / p).is_file()]


def run():
    failures = []
    py = tracked('*.py')
    for path in py:
        try:
            compile(path.read_bytes(), str(path), 'exec')
        except SyntaxError as e:
            failures.append(f'{path.relative_to(ROOT)}: {e}')
    data = tracked('*.json')
    for path in data:
        try:
            json.loads(path.read_text(encoding='utf-8-sig'))
        except (ValueError, UnicodeDecodeError) as e:
            failures.append(f'{path.relative_to(ROOT)}: {e}')
    catalog = ROOT / 'OnlineServer/Data/catalog.json.gz'
    try:
        json.loads(gzip.decompress(catalog.read_bytes()))
    except (OSError, ValueError) as e:
        failures.append(f'{catalog.relative_to(ROOT)}: {e}')
    for line in failures:
        print('FALLA:', line)
    print(f'{len(py)} .py, {len(data)} .json + catalogo: {len(failures)} fallas')
    sys.exit(1 if failures else 0)


if __name__ == '__main__':
    run()
