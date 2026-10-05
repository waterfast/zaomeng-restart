"""核对原场景后提取紫色小怪的动作偏移，以及连击的布局、动画。"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / "造梦八荒-(4.1)"

def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")

source = (OLD / "Scene/Monster/Monster_2.tscn").read_text(encoding="utf-8")
blocks = re.split(r"(?=^\[)", source, flags=re.M)
resources = {re.search(r'\bid="([^"]+)"', b)[1]: b for b in blocks if b.startswith("[sub_resource")}
library = next(b for b in blocks if b.startswith('[sub_resource type="AnimationLibrary"'))
names = dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)', library))
animations = []
for name, key in names.items():
    original = resources[key]
    length = re.search(r'^length = ([\d.]+)', original, re.M)
    result = f'[sub_resource type="Animation" id="anim_{name}"]\nlength = {length[1] if length else "1.0"}\n'
    if name in ("wait", "walk"):
        result += 'loop_mode = 1\n'
    index = 0
    for track in re.split(r'(?=tracks/\d+/type =)', original)[1:]:
        if '/type = "method"' in track:
            continue
        path = re.search(r'NodePath\("([^"]+)"\)', track)[1]
        if path.startswith('MonsterDir/mr_ani:'):
            path = path.replace('MonsterDir/mr_ani:', 'Facing/Visual/Body:')
        elif path == 'BaseDamageBox/HitBox/HitBox:disabled':
            path = 'Facing/HitBox:Active'
            track = re.sub(r'"values": \[([^\]]+)\]', lambda m: '"values": [' + m[1].replace('true', 'TEMP').replace('false', 'true').replace('TEMP', 'false') + ']', track)
        elif path.startswith('BaseDamageBox/HitBox/HitBox:') and path.endswith(('shape', 'position')):
            path = path.replace('BaseDamageBox/HitBox/HitBox:', 'Facing/HitBox/Shape:')
        else:
            continue
        track = re.sub(r'NodePath\("[^"]+"\)', f'NodePath("{path}")', track)
        track = re.sub(r'tracks/\d+/', f'tracks/{index}/', track)
        result += re.sub(r'^script = null\n?', '', track, flags=re.M)
        index += 1
    animations.append(result)
needed = set(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', ''.join(animations)))
shapes = ''.join(re.sub(r'^script = null\n?', '', resources[key], flags=re.M) for key in needed)
write('Content/Monsters/Monster2Animations.tres', '[gd_resource type="AnimationLibrary" format=3]\n\n' + shapes + ''.join(animations) + '\n[resource]\n_data = {\n' + ',\n'.join(f'"{name}": SubResource("anim_{name}")' for name in names) + '\n}\n')
combo = (OLD / 'Scene/show_text/combo.tscn').read_text(encoding='utf-8')
combo = re.sub(r'^\[ext_resource type="Script"[^\n]*\n', '', combo, flags=re.M)
combo = re.sub(r'^script = [^\n]*\n', '', combo, flags=re.M)
combo = re.sub(r' uid="[^"]+"', '', combo)
combo = combo.replace('res://Art/', 'res://Assets/Art/').replace('load_steps=6', 'load_steps=5')
write('Scenes/UI/Level/Combo.tscn', combo)
print('Migrated Monster2 animation offsets and original Combo scene.')
