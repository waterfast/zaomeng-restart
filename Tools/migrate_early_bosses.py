"""一次性提取两只早期 Boss 的原图和身体动画；不迁旧脚本/方法轨道。"""
from pathlib import Path
import re

project = Path(__file__).resolve().parents[1]
legacy = project.parent / '造梦八荒-(4.1)'
for number, name, foot_offset, radius, height in [(4, 'MacaqueKing', 75.5, 28, 112), (5, 'TamarinKing', 50, 25, 90)]:
    output = project / f'Content/Monsters/{name}Frames.tres'
    if output.exists():
        raise RuntimeError(f'不能覆盖已经提取的资源：{output}')
    source = legacy / f'Scene/Monster/Monster_{number}.tscn'
    blocks = re.split(r'(?=^\[)', source.read_text(encoding='utf-8'), flags=re.M)
    textures = [block for block in blocks if block.startswith('[ext_resource type="Texture2D"')]
    subresources = {re.search(r'id="([^"]+)"', block)[1]: block for block in blocks if block.startswith('[sub_resource')}
    body_node = next(block for block in blocks if block.startswith('[node name="mr_ani"'))
    frames_id = re.search(r'sprite_frames = SubResource\(\s*"([^"]+)"', body_node)[1]
    def clean(block):
        block = re.sub(r' uid="[^"]+"', '', block)
        block = block.replace('res://Art/', 'res://Assets/Art/')
        return block.replace('script = null\n', '')
    for block in textures:
        texture_path = re.search(r'path="res://Art/([^"]+)"', block)[1]
        if not (project / 'Assets/Art' / texture_path).exists():
            raise RuntimeError(f'原场景引用缺图：{texture_path}')
    atlas = [block for block in subresources.values() if block.startswith('[sub_resource type="AtlasTexture"')]
    frames = subresources[frames_id].split('\n', 1)[1].replace('script = null\n', '')
    output.write_text('[gd_resource type="SpriteFrames" format=3]\n' + ''.join(map(clean, textures + atlas)) + '\n[resource]\n' + frames, encoding='utf-8')
    library = next(block for block in subresources.values() if block.startswith('[sub_resource type="AnimationLibrary"'))
    entries = re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"', library)
    animations = []
    for animation_name, resource_id in entries:
        if animation_name == 'RESET':
            continue
        raw = subresources[resource_id]
        parts = re.split(r'(?=^tracks/\d+/type)', raw, flags=re.M)
        header = parts[0].replace('script = null\n', '')
        if animation_name in ['wait', 'walk'] and 'loop_mode' not in header:
            header += 'loop_mode = 1\n'
        tracks = []
        for track in parts[1:]:
            if 'NodePath("MonsterDir/mr_ani:' not in track:
                continue
            track = track.replace('MonsterDir/mr_ani:', 'Facing/Visual/Body:')
            track = track.replace(':modulate"', ':self_modulate"').replace('script = null\n', '')
            track = re.sub(r'tracks/\d+/', f'tracks/{len(tracks)}/', track)
            tracks.append(track)
        animations.append(header + ''.join(tracks))
    library_body = library.split('\n', 1)[1].replace('script = null\n', '')
    library_body = re.sub(r'"RESET": SubResource\([^\n]+\n', '', library_body)
    (project / f'Content/Monsters/{name}Animations.tres').write_text('[gd_resource type="AnimationLibrary" format=3]\n' + ''.join(animations) + '\n[resource]\n' + library_body, encoding='utf-8')
    scene = f'''[gd_scene format=3]
[ext_resource type="PackedScene" path="res://Scenes/Actors/Monster.tscn" id="base"]
[ext_resource type="SpriteFrames" path="res://Content/Monsters/{name}Frames.tres" id="frames"]
[ext_resource type="AnimationLibrary" path="res://Content/Monsters/{name}Animations.tres" id="animations"]
[sub_resource type="CapsuleShape2D" id="body"]
radius = {radius}.0
height = {height}.0
[sub_resource type="RectangleShape2D" id="attack"]
size = Vector2(180, 110)
[node name="{name}" instance=ExtResource("base")]
IsBoss = true
[node name="BodyShape" parent="." index="0"]
position = Vector2(0, {-height/2})
shape = SubResource("body")
[node name="Visual" parent="Facing" index="0"]
position = Vector2(0, {-foot_offset})
[node name="Body" parent="Facing/Visual" index="0"]
sprite_frames = ExtResource("frames")
animation = &"wait"
[node name="HitBox" parent="Facing" index="1"]
position = Vector2(-75, -55)
[node name="Shape" parent="Facing/HitBox" index="0"]
shape = SubResource("attack")
[node name="HurtBox" parent="." index="2"]
position = Vector2(0, {-height/2})
[node name="Shape" parent="HurtBox" index="0"]
shape = SubResource("body")
[node name="AnimationPlayer" parent="." index="3"]
libraries/ = ExtResource("animations")
'''
    (project / f'Scenes/Actors/{name}.tscn').write_text(scene, encoding='utf-8')
