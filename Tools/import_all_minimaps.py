"""Import every original AO minimap into Unity Resources as a 100x100 PNG."""

from pathlib import Path
import re

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Archivos Originales/Recursos-master/Recursos-master/Minimapas"
MAPS = ROOT / "Assets/Resources/AOMigrator/WorldV07/Maps"
OUTPUT = ROOT / "Assets/Resources/AOMigrator/MinimapsV0103"


def numbered_files(folder: Path, pattern: str) -> dict[int, Path]:
    expression = re.compile(pattern)
    found = {}
    for path in folder.iterdir():
        match = expression.fullmatch(path.name)
        if match:
            number = int(match.group(1))
            if number in found:
                raise ValueError(f"Duplicate map {number}: {path}")
            found[number] = path
    return found


def main() -> None:
    originals = numbered_files(SOURCE, r"[Mm]apa(\d+)\.bmp")
    maps = numbered_files(MAPS, r"map_(\d+)\.json")
    if originals.keys() != maps.keys():
        raise ValueError(
            f"Map ID mismatch: {len(originals.keys() - maps.keys())} source-only, "
            f"{len(maps.keys() - originals.keys())} Unity-only"
        )

    OUTPUT.mkdir(parents=True, exist_ok=True)
    created = 0
    existing = 0
    for number in sorted(maps):
        target = OUTPUT / f"map_{number}.png"
        with Image.open(originals[number]) as bitmap:
            image = bitmap.convert("RGB")
        if image.size != (100, 100):
            raise ValueError(f"Unexpected minimap size for {number}: {image.size}")

        if target.exists():
            with Image.open(target) as imported:
                if ImageChops.difference(image, imported.convert("RGB")).getbbox():
                    raise ValueError(f"Existing minimap differs from source: {number}")
            existing += 1
            continue

        temporary = target.with_suffix(".png.tmp")
        try:
            image.save(temporary, format="PNG", optimize=True)
            temporary.replace(target)
        finally:
            temporary.unlink(missing_ok=True)
        created += 1

    print(f"Maps={len(maps)} created={created} verified_existing={existing}")


if __name__ == "__main__":
    main()
