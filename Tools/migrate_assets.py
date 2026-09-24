"""Recover reusable art and archive original scene authoring data, without old scripts.

Run once with: python Tools/migrate_assets.py ORIGINAL_PROJECT
Then run Godot --headless --path . --script Tools/recover_textures.gd.
Existing recovered images are left untouched.
"""
from pathlib import Path
import json
import re
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(sys.argv[1]).resolve()
ART_FOLDERS = (
    "HeroPicture", "Monster", "Level", "Game_2", "Game_3", "AddEffect",
    "AllTX", "Bullet", "Skill", "StrikeSpecialEffects", "Trap", "zhenfa", "MagicWeapon",
)

entries = []
for folder in ART_FOLDERS:
    for imported in sorted((SOURCE / "Art" / folder).rglob("*.import")):
        match = re.search(r'path="res://([^"]+\.ctex)"', imported.read_text(encoding="utf-8"))
        if not match:
            continue
        relative = imported.relative_to(SOURCE).as_posix().removesuffix(".import")
        # Store decoded data as PNG even when the original was a JPEG.
        destination = "Assets/" + str(Path(relative).with_suffix(".png")).replace("\\", "/")
        entries.append({"original": relative, "source": (SOURCE / match[1]).as_posix(),
                        "destination": "res://" + destination})
(ROOT / "Tools/texture_manifest.json").write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding="utf-8")

archive = ROOT / "Reference/OriginalScenes"
archive.mkdir(parents=True, exist_ok=True)
(ROOT / "Reference/.gdignore").write_text("", encoding="utf-8")
for folder in ("Hero", "Monster", "Level", "Skill", "Bullet", "Base"):
    for scene in (SOURCE / "Scene" / folder).rglob("*.tscn"):
        target = archive / scene.relative_to(SOURCE / "Scene")
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(scene, target)
print(f"Prepared {len(entries)} textures; original scenes archived for reference only.")
