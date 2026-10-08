"""校准旧场景静止帧脚点，并显式补齐首领技能与 Buff 引用。"""
from pathlib import Path
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
if (ROOT / 'Content/Buffs/poison.tres').exists():
    raise RuntimeError('内容已补齐，禁止重复生成覆盖策划资源。')

def read(path): return (ROOT / path).read_text(encoding='utf-8')
def write(path, text): (ROOT / path).write_text(text, encoding='utf-8')
def reference(text, line):
    return text.replace('[sub_resource', line + '\n[sub_resource', 1)

for number in range(7, 21):
    path = f'Scenes/Actors/Human/Monster_{number}.tscn'
    scene = read(path)
    frames = read(f'Content/Monsters/Human/monster_{number}_frames.tres')
    idle = re.search(r'animation = &"([^"]+)"', scene)[1]
    if number == 12:
        idle = 'wait'
        scene = scene.replace('animation = &"Fly_wait"', 'animation = &"wait"')
    animation = next(block for block in frames.split('"frames":') if f'"name": &"{idle}"' in block)
    texture = re.search(r'"texture": (Sub|Ext)Resource\(\s*"([^"]+)"', animation)
    if texture[1] == 'Sub':
        atlas = re.search(rf'\[sub_resource type="AtlasTexture" id="{texture[2]}"\](.*?)(?=\[)', frames, re.S)[1]
        ext_id = re.search(r'ExtResource\(\s*"([^"]+)"', atlas)[1]
        source = re.search(rf'path="res://([^"]+)" id="{ext_id}"', frames)[1]
        x, y, width, height = map(float, re.search(r'Rect2\(([^)]+)\)', atlas)[1].split(','))
        image = Image.open(ROOT / source).convert('RGBA').crop((x, y, x + width, y + height))
    else:
        source = re.search(rf'path="res://([^"]+)" id="{texture[2]}"', frames)[1]
        image = Image.open(ROOT / source).convert('RGBA')
        width, height = image.size
    bottom = image.getchannel('A').getbbox()[3]
    library = read(f'Content/Monsters/Human/monster_{number}_animations.tres')
    animation_id = re.search(rf'&"{idle}": SubResource\("([^"]+)"', library)[1]
    block = re.search(rf'\[sub_resource type="Animation" id="{animation_id}"\](.*?)(?=\[sub_resource|\[resource)', library, re.S)[1]
    # 轨道被分段后值在后面的键块，直接从路径后的第一条 values 取初始偏移。
    offset = re.search(r':offset".*?"values": \[Vector2\([^,]+, ([^)]+)\)', block, re.S)
    offset_y = float(offset[1]) if offset else 0
    foot = bottom - height / 2 + offset_y
    scene = re.sub(r'(\[node name="Visual"[^\n]+\]\n)position = Vector2\(0, [^)]+\)', rf'\g<1>position = Vector2(0, {-foot})', scene)
    write(path, scene)
    print(f'脚点 {number}: {-foot}')

def buff(name, title, fields):
    write(f'Content/Buffs/{name}.tres', f'''[gd_resource type="Resource" format=3]
[ext_resource type="Script" path="res://Scripts/Combat/Buffs/BuffDefinition.cs" id="buff"]
[resource]
script = ExtResource("buff")
Id = "{name}"
DisplayName = "{title}"
Description = "{title}"
{fields}
''')

buff('poison', '中毒', 'Duration = 18.0\nDamagePerPulse = 15.0')
buff('burning', '灼烧', 'Duration = 4.0\nDamageRatioPerPulse = 0.01\nPulseDamageType = 1')
buff('bleeding', '流血', 'Duration = 5.0\nPulseInterval = 0.5\nDamagePerPulse = 20.0')
buff('slowed', '减速', 'Duration = 3.0\nMoveSpeedMultiplier = 0.6')
buff('stunned', '眩晕', 'Duration = 3.0\nPreventsActions = true\nMoveSpeedMultiplier = 0.0')

applications = {
    'poison': ['Content/Monsters/Human/Effects/projectile_11_kldw.tscn'],
    'burning': ['Content/Monsters/Human/Effects/projectile_12_hydd.tscn', 'Content/Monsters/Human/monster_12_bshn.tres'],
    'bleeding': ['Content/Monsters/Human/monster_20_sxks.tres', 'Content/Monsters/Human/monster_20_low_sxks.tres'],
    'slowed': ['Content/Monsters/Human/Effects/behavior_18_dtsz.tscn'],
    'stunned': ['Content/Monsters/Human/Effects/behavior_11_ysyb.tscn', 'Content/Monsters/Human/Effects/projectile_18_fhjl.tscn', 'Content/Monsters/Human/Effects/behavior_20_spzd.tscn']
}
for name, paths in applications.items():
    for path in paths:
        text = reference(read(path), f'[ext_resource type="Resource" path="res://Content/Buffs/{name}.tres" id="debuff"]')
        text = text.replace('script = ExtResource("hit")', 'script = ExtResource("hit")\nAppliedBuff = ExtResource("debuff")')
        write(path, text)

# changeHit 在旧 AI 中被注释；低血量仍使用原始攻击数值，只切换动画与伴生鲨鱼。
for name in ['hit1', 'sxks', 'spzd']:
    high = read(f'Content/Monsters/Human/monster_20_{name}.tres')
    low = high.replace(f'Id = "human_20_{name}"', f'Id = "human_20_low_{name}"').replace(f'&"{name}_1"', f'&"{name}_2"')
    write(f'Content/Monsters/Human/monster_20_low_{name}.tres', low)
scene = reference(read('Scenes/Actors/Human/Monster_20.tscn'), '[ext_resource type="Resource" path="res://Content/Monsters/Human/monster_19.tres" id="shark"]')
scene += '\nLowPhaseSummon = ExtResource("shark")\n'
write('Scenes/Actors/Human/Monster_20.tscn', scene)

scene = reference(read('Scenes/Actors/Human/Monster_12.tscn'), '[ext_resource type="Script" path="res://Scripts/Monsters/FlightPhaseMechanic.cs" id="flight_phase"]')
scene += '\n[node name="FlightPhase" type="Node2D" parent="."]\nscript = ExtResource("flight_phase")\n'
write('Scenes/Actors/Human/Monster_12.tscn', scene)
skill = read('Content/Monsters/Human/monster_12_bshn.tres').replace('&"FloorSkill"', '&"FlyBirds"')
skill = reference(skill, '[ext_resource type="Script" path="res://Scripts/Combat/ActionMotion.cs" id="motion"]')
skill = skill.replace('[resource]', '[sub_resource type="Resource" id="motion"]\nscript = ExtResource("motion")\nMode = 2\nSpeed = 600.0\n\n[resource]')
skill += 'Hits = Array[Resource]([SubResource("window")])\nMotion = SubResource("motion")\n'
write('Content/Monsters/Human/monster_12_bshn.tres', skill)
skill = read('Content/Monsters/Human/monster_12_hydd.tres')
write('Content/Monsters/Human/monster_12_ground_fire.tres', skill.replace('human_12_hydd', 'human_12_ground_fire').replace('&"FlySkill"', '&"FloorSkill"').replace('CooldownSeconds = 7.0', 'CooldownSeconds = 2.0'))
behavior = read('Content/Monsters/Human/Effects/behavior_12_hydd.tscn')
write('Content/Monsters/Human/Effects/behavior_12_hydd.tscn', behavior + 'SpreadDegrees = PackedFloat32Array(-30, 0, 30)\n')
definition = reference(read('Content/Monsters/Human/monster_12.tres'), '[ext_resource type="Resource" path="res://Content/Monsters/Human/monster_12_ground_fire.tres" id="ground_fire"]')
definition = definition.replace('MaximumRange = 250.0', 'MaximumRange = 800.0').replace('MaximumHeightDifference = 200.0', 'MaximumHeightDifference = 350.0')
definition = definition.replace('Skill = ExtResource("s0")', 'Skill = ExtResource("s0")\nRequiredMode = "ground"').replace('Skill = ExtResource("s1")', 'Skill = ExtResource("s1")\nRequiredMode = "flight"').replace('Skill = ExtResource("s2")', 'Skill = ExtResource("s2")\nRequiredMode = "flight"')
definition = definition.replace('[resource]', '[sub_resource type="Resource" id="ground_choice"]\nscript = ExtResource("choice")\nSkill = ExtResource("ground_fire")\nRequiredMode = "ground"\nMaximumRange = 800.0\nMaximumHeightDifference = 350.0\n\n[resource]')
definition = definition.replace('Skills = Array[Resource]([', 'Skills = Array[Resource]([SubResource("ground_choice"), ')
write('Content/Monsters/Human/monster_12.tres', definition)
