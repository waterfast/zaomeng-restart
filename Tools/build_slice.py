"""One-off extraction of original tracks into self-contained, editable Godot scenes.
Never run this after hand-editing scenes unless you intend to regenerate them.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Reference/OriginalScenes"

def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8")

def blocks(text):
    return re.split(r'(?=^\[)', text, flags=re.M)

def clean(text):
    text = re.sub(r' uid="[^"]+"', '', text)
    text = re.sub(r'^script = null\n', '', text, flags=re.M)
    return text.replace('res://Art/', 'res://Assets/Art/')

def value_track(index, path, values='false'):
    return f'''tracks/{index}/type = "value"
tracks/{index}/path = NodePath("{path}")
tracks/{index}/interp = 1
tracks/{index}/keys = {{
"times": PackedFloat32Array(0),
"transitions": PackedFloat32Array(1),
"update": 1,
"values": [{values}]
}}
'''

def actor_scene(hero):
    source = SOURCE / ('Hero/Role_1/Role1.tscn' if hero else 'Monster/Monster_1.tscn')
    sections = blocks(source.read_text(encoding='utf-8'))
    resources = {re.search(r'id="([^"]+)"', b)[1]: b for b in sections if b.startswith('[sub_resource ')}
    originals = {re.search(r'\bid="([^"]+)"', b)[1]: b for b in sections if b.startswith('[ext_resource ')}
    library = next(b for b in sections if b.startswith('[sub_resource type="AnimationLibrary"'))
    names = dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)', library))
    selected = ['wait', 'run', 'jump1', 'hit1', 'hurt', 'death'] if hero else ['wait', 'walk', 'hit1', 'hurt', 'death']
    animations = []
    for name in selected:
        src = resources[names[name]]
        tracks = re.split(r'(?=tracks/\d+/type =)', src)
        speed = 1.0
        for track in tracks[1:]:
            if ':speed_scale' in track:
                speed = float(re.search(r'"values": \[([\d.]+)\]', track)[1])
        length = float(re.search(r'^length = ([\d.]+)', src, re.M)[1]) / speed
        result = f'[sub_resource type="Animation" id="anim_{name}"]\nresource_name = "{name}"\nlength = {length:.8f}\n'
        if name in ('wait', 'run', 'walk', 'jump1'): result += 'loop_mode = 1\n'
        index = 0
        for track in tracks[1:]:
            path = re.search(r'NodePath\("([^"]+)"\)', track)[1]
            is_active = path.endswith('HitBox/HitBox:disabled')
            keep = (path.startswith(('Action/RoleBody:', 'Action/RoleEquipment:', 'Action/SpecialEffect:', 'MonsterDir/mr_ani:'))
                    or is_active or path.endswith(('HitBox/HitBox:shape', 'HitBox/HitBox:position')))
            if not keep or '/type = "method"' in track: continue
            path = path.replace('Action/', 'Facing/Visual/').replace('MonsterDir/mr_ani:', 'Facing/Visual/Body:')
            path = path.replace('base_damagebox/HitBox/HitBox:', 'Facing/HitBox/Shape:').replace('BaseDamageBox/HitBox/HitBox:', 'Facing/HitBox/Shape:')
            if is_active:
                path = 'Facing/HitBox:Active'
                track = re.sub(r'"values": \[([^\]]+)\]', lambda m: '"values": [' + m[1].replace('true', 'TEMP').replace('false', 'true').replace('TEMP', 'false') + ']', track)
            track = re.sub(r'NodePath\("[^"]+"\)', f'NodePath("{path}")', track)
            track = re.sub(r'tracks/\d+/', f'tracks/{index}/', track)
            track = re.sub(r'"times": PackedFloat32Array\(([^)]+)\)', lambda m: '"times": PackedFloat32Array(' + ', '.join(f'{float(t)/speed:.8f}' for t in m[1].split(',')) + ')', track)
            result += clean(track).strip() + '\n'
            index += 1
        # Death has different old visibility rules. Always close attacks on every non-attack clip.
        if name != 'hit1':
            result += value_track(index, 'Facing/HitBox:Active')
            index += 1
        if hero:
            result += value_track(index, 'Facing/Visual/SpecialEffect:visible', 'true' if name == 'hit1' else 'false')
        animations.append(result)

    needed = {'276', '277'} if hero else {'19', '26'}
    pending = list(needed)
    for animation in animations:
        pending.extend(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', animation))
    needed = set()
    while pending:
        key = pending.pop()
        if key in needed: continue
        needed.add(key)
        pending.extend(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', resources[key]))
    subresources = ''.join(clean(resources[key]) for key in resources if key in needed)
    ext_ids = set(re.findall(r'ExtResource\(\s*"([^"]+)"\s*\)', subresources))
    if hero: ext_ids.update(('9', '10'))
    ext = ''.join(clean(originals[key]) for key in originals if key in ext_ids)
    actor = 'Player' if hero else 'Monster'
    ext += f'''[ext_resource type="Script" path="res://Scripts/Actors/{actor}.cs" id="actor"]
[ext_resource type="Script" path="res://Scripts/Combat/HitBox.cs" id="hitbox"]
[ext_resource type="Script" path="res://Scripts/Combat/HurtBox.cs" id="hurtbox"]
[ext_resource type="Resource" path="res://Content/{actor}Hit.tres" id="hit"]
'''
    library = '[sub_resource type="AnimationLibrary" id="library"]\n_data = {\n' + ',\n'.join(f'"{n}": SubResource("anim_{n}")' for n in selected) + '\n}\n'
    height = 90 if hero else 60
    shapes = f'''[sub_resource type="CapsuleShape2D" id="body_shape"]
radius = {20 if hero else 16}.0
height = {height}.0
'''
    nodes = f'''[node name="{actor}" type="CharacterBody2D"]
collision_layer = 2
collision_mask = 1
script = ExtResource("actor")
Team = {0 if hero else 1}
MaxHealth = {120 if hero else 60}.0
MoveSpeed = {220 if hero else 85}.0
NormalAttack = ExtResource("hit")
MoveAnimation = &"{'run' if hero else 'walk'}"
AirAnimation = &"{'jump1' if hero else 'wait'}"

[node name="BodyShape" type="CollisionShape2D" parent="."]
position = Vector2(0, {-height/2})
shape = SubResource("body_shape")

[node name="Facing" type="Node2D" parent="."]

[node name="Visual" type="Node2D" parent="Facing"]
position = Vector2(0, {-70 if hero else -30})
'''
    if hero:
        nodes += '''
[node name="RoleBody" type="Sprite2D" parent="Facing/Visual"]
texture = ExtResource("9")
hframes = 6
vframes = 14

[node name="RoleEquipment" type="Sprite2D" parent="Facing/Visual"]
texture = ExtResource("10")
hframes = 6
vframes = 14

[node name="SpecialEffect" type="AnimatedSprite2D" parent="Facing/Visual"]
visible = false
scale = Vector2(0.4, 0.4)
sprite_frames = SubResource("276")
animation = &"wait"
'''
    else:
        nodes += '''
[node name="Body" type="AnimatedSprite2D" parent="Facing/Visual"]
sprite_frames = SubResource("19")
animation = &"wait"
'''
    nodes += f'''
[node name="HitBox" type="Area2D" parent="Facing"]
position = Vector2(0, {-70 if hero else -30})
collision_layer = 0
collision_mask = 4
monitorable = false
script = ExtResource("hitbox")

[node name="Shape" type="CollisionShape2D" parent="Facing/HitBox"]
position = Vector2({-45 if hero else -21}, {-17 if hero else -5})
shape = SubResource("{'277' if hero else '26'}")

[node name="HurtBox" type="Area2D" parent="."]
position = Vector2(0, {-height/2})
collision_layer = 4
collision_mask = 0
monitoring = false
script = ExtResource("hurtbox")

[node name="Shape" type="CollisionShape2D" parent="HurtBox"]
shape = SubResource("body_shape")

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
callback_mode_process = 0
libraries = {{
"": SubResource("library")
}}

[node name="HealthBar" type="ProgressBar" parent="."]
offset_left = -35.0
offset_top = {-height-28}.0
offset_right = 35.0
offset_bottom = {-height-21}.0
mouse_filter = 2
show_percentage = false
'''
    content = ext + subresources + ''.join(animations) + library + shapes + nodes
    count = len(re.findall(r'^\[(?:ext|sub)_resource ', content, re.M)) + 1
    write(f'Scenes/Actors/{actor}.tscn', f'[gd_scene load_steps={count} format=3]\n\n' + content)

def map_scene():
    sections = blocks((SOURCE / 'Level/Level_1.tscn').read_text(encoding='utf-8'))
    ext = ''.join(clean(b) for b in sections if b.startswith('[ext_resource') and ('Texture2D' in b or 'XP.tscn' in b))
    ext = ext.replace('res://Scene/Level/XP.tscn', 'res://Scenes/Maps/Slope.tscn')
    sub = ''.join(clean(b) for b in sections if b.startswith('[sub_resource'))
    nodes = '''[node name="Forest" type="Node2D"]

[node name="wall" type="StaticBody2D" parent="."]
position = Vector2(0, 112)
collision_layer = 1
collision_mask = 0
'''
    for b in sections:
        if not b.startswith('[node ') or 'parent="wall' not in b: continue
        if re.search(r'name="stop\d', b): continue # Old scripted wave barriers.
        b = re.sub(r' index="\d+"', '', b)
        if 'name="wall_left"' in b: b = b.replace('parent="wall"', 'type="CollisionShape2D" parent="wall"')
        nodes += clean(b)
    nodes += '''
[node name="RightBoundary" type="CollisionShape2D" parent="wall"]
position = Vector2(5568, -419.5)
shape = SubResource("1")

[node name="BackGround" type="ParallaxBackground" parent="."]

[node name="End" type="ParallaxLayer" parent="BackGround"]
motion_scale = Vector2(0, 0)
'''
    for b in sections:
        if not b.startswith('[node ') or 'parent="BackGround' not in b: continue
        if 'name="End"' in b: continue
        nodes += clean(re.sub(r' index="\d+"', '', b))
    write('Scenes/Maps/Forest.tscn', '[gd_scene load_steps=15 format=3]\n\n' + ext + sub + nodes)
    write('Scenes/Maps/Slope.tscn', clean((SOURCE / 'Level/XP.tscn').read_text(encoding='utf-8')))

for actor, damage in [('Player', 12), ('Monster', 8)]:
    write(f'Content/{actor}Hit.tres', f'''[gd_resource type="Resource" script_class="HitDefinition" load_steps=2 format=3]
[ext_resource type="Script" path="res://Scripts/Combat/HitDefinition.cs" id="1"]
[resource]
script = ExtResource("1")
Damage = {damage}.0
Knockback = Vector2(60, 0)
Hitstun = 0.22
''')
actor_scene(True)
actor_scene(False)
map_scene()
print('Created Player, Monster, Forest and attack resources.')
