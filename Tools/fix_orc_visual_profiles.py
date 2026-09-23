"""Restore the Orc bodies used by the downloaded Recursos-master data."""

from __future__ import annotations

import json
import os
from pathlib import Path

from PIL import Image

from npc_visual_migration import SOURCE, body_frames, integer, sections


ROOT = Path(__file__).resolve().parent.parent
VISUALS = ROOT / "Assets/Resources/AOMigrator/CharacterV111/character_visuals.json"
TEXTURES = ROOT / "Assets/Resources/AOMigrator/CharacterV111/Textures"


def main() -> None:
    data = json.loads(VISUALS.read_text(encoding="utf-8"))
    character_data = json.loads(
        (SOURCE / "init/HeadAndBodyData.json").read_text(encoding="utf-8-sig")
    )
    source_bodies = sections(SOURCE / "init/cuerpos.dat")
    molds = sections(SOURCE / "init/moldes.ini")
    by_id = {body["id"]: body for body in data["bodies"]}

    # HeadAndBodyData.json in the same download assigns 248 to male Orcs
    # and 249 to female Orcs. The old profiles pointed at absent 582/581.
    for gender, body_id in ((1, 248), (2, 249)):
        source_gender = "male" if gender == 1 else "female"
        if character_data["Orc"][source_gender]["body"] != body_id:
            raise ValueError(f"Unexpected Orc {source_gender} body")
        frames_by_heading, source = body_frames(body_id, source_bodies, molds, {})
        if not source or len(frames_by_heading) != 4:
            raise ValueError(f"Missing source body {body_id}")

        for frames in frames_by_heading.values():
            for frame in frames:
                texture_path = TEXTURES / f"tex_{frame['fileNum']}.png"
                if not texture_path.is_file():
                    raise FileNotFoundError(texture_path)
                with Image.open(texture_path) as texture:
                    if frame["sx"] < 0 or frame["sy"] < 0 or \
                            frame["sx"] + frame["width"] > texture.width or \
                            frame["sy"] + frame["height"] > texture.height:
                        raise ValueError(f"Body {body_id} frame outside {texture_path}")

        if body_id not in by_id:
            body = {
                "id": body_id,
                "headOffsetX": integer(source, "headoffsetx"),
                "headOffsetY": integer(source, "headoffsety"),
                "bodyShiftX": by_id[248]["bodyShiftX"],
                "directions": [
                    {"heading": heading, "frames": frames_by_heading[heading]}
                    for heading in range(1, 5)
                ],
            }
            data["bodies"].append(body)
            by_id[body_id] = body

        profile = next(
            (item for item in data["profiles"]
             if item["raceId"] == 6 and item["genderId"] == gender),
            None,
        )
        if profile is None:
            raise ValueError(f"Missing Orc profile for gender {gender}")
        profile["bodyId"] = body_id

    temporary = VISUALS.with_name(VISUALS.name + ".tmp")
    temporary.write_text(
        json.dumps(data, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    os.replace(temporary, VISUALS)
    print("Orc bodies restored: male 248, female 249")


if __name__ == "__main__":
    main()
