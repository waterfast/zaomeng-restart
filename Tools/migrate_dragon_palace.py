"""一次性提取水晶宫与龙王实际引用的资源；拒绝覆盖已迁入文件。"""
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / '造梦八荒-(4.1)'
REBUILD = ROOT.parent / 'zmbh-rebuild/assets'

def write(path, text):
    target = ROOT / path
    if target.exists():
        raise RuntimeError(f'禁止覆盖已迁入资源：{target}')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding='utf-8')

def ext(kind, path, key):
    return f'[ext_resource type="{kind}" path="res://{path}" id="{key}"]\n'

def clean(text):
    return re.sub(r'^script = null\n', '', re.sub(r' uid="[^"]+"', '', text), flags=re.M).replace('res://Art/', 'res://Assets/Art/')

def blocks(path):
    return re.split(r'(?=^\[)', (OLD / path).read_text(encoding='utf-8'), flags=re.M)

def resources(bs):
    return {(('Ext' if b.startswith('[ext_resource') else 'Sub'), re.search(r'\bid="([^"]+)"', b)[1]): b for b in bs if b.startswith(('[ext_resource', '[sub_resource'))}

def referenced(text, table):
    result = {}
    def visit(value):
        for kind, resource_id in re.findall(r'(Sub|Ext)Resource\(\s*"([^"]+)"', value):
            key = (kind, resource_id)
            if key in result:
                continue
            visit(table[key])
            result[key] = table[key]
    visit(text)
    return sorted(result.values(), key=lambda b: 0 if b.startswith('[ext_resource') else 1)

def copy_textures(bs):
    for b in bs:
        if not b.startswith('[ext_resource type="Texture2D"'):
            continue
        path = re.search(r'path="res://([^"]+)"', b)[1]
        target = ROOT / path.replace('Art/', 'Assets/Art/', 1)
        if target.exists():
            continue
        source = next((p for p in [OLD/path, REBUILD/path] if p.exists()), None)
        if source is None:
            raise FileNotFoundError(path)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)

def frame_animation(path, name, output, speed=None):
    bs = blocks(path); table = resources(bs)
    frame_block = next(b for b in bs if b.startswith('[sub_resource type="SpriteFrames"') and f'"{name}"' in b)
    entry = next(m[0] for m in re.finditer(r'\{\n"frames": \[.*?\],\n"loop": .*?,\n"name": &?"([^"]+)",\n"speed": [^\n]+\n\}', frame_block, re.S) if m[1] == name)
    entry = re.sub(r'"loop": true', '"loop": false', entry)
    if speed is not None:
        entry = re.sub(r'"speed": [^\n]+', f'"speed": {speed}', entry)
    deps = referenced(entry, table); copy_textures(deps)
    write(output, '[gd_resource type="SpriteFrames" format=3]\n' + ''.join(clean(b) for b in deps) + '\n[resource]\nanimations = [' + entry + ']\n')
    return len(re.findall(r'"texture":', entry))

bs = blocks('Scene/Monster/Monster_6.tscn'); table = resources(bs)
body = next(b for b in bs if b.startswith('[node name="mr_ani"'))
body_id = re.search(r'sprite_frames = SubResource\(\s*"([^"]+)"', body)[1]
deps = referenced(table[('Sub',body_id)], table); copy_textures(deps)
write('Content/Monsters/DragonKingFrames.tres', '[gd_resource type="SpriteFrames" format=3]\n' + ''.join(clean(b) for b in deps) + '\n[resource]\n' + clean(table[('Sub',body_id)]).split('\n',1)[1])
special = next(b for b in bs if b.startswith('[node name="SpecialEffect"'))
sid = re.search(r'sprite_frames = SubResource\(\s*"([^"]+)"', special)[1]
deps = referenced(table[('Sub',sid)], table); copy_textures(deps)
write('Content/Monsters/DragonKingCastFrames.tres', '[gd_resource type="SpriteFrames" format=3]\n' + ''.join(clean(b) for b in deps) + '\n[resource]\n' + clean(table[('Sub',sid)]).split('\n',1)[1])
library = next(b for b in bs if b.startswith('[sub_resource type="AnimationLibrary"'))
animations = []
for name, key in re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"', library):
    if name == 'RESET':
        continue
    parts = re.split(r'(?=^tracks/\d+/type)', table[('Sub',key)], flags=re.M)
    header = clean(parts[0])
    factor = 2 if name == 'qnhb' else 1
    if factor == 2:
        header = re.sub(r'length = ([\d.]+)', lambda m: f'length = {float(m[1])/factor}', header)
    if name in ['wait', 'walk'] and 'loop_mode' not in header:
        header += 'loop_mode = 1\n'
    tracks = []
    for track in parts[1:]:
        if 'NodePath("MonsterDir/mr_ani:' not in track and 'NodePath("MonsterDir/SpecialEffect:' not in track:
            continue
        track = clean(track).replace('MonsterDir/mr_ani:', 'Facing/Visual/Body:').replace('MonsterDir/SpecialEffect:', 'Facing/Visual/Cast:').replace(':modulate"', ':self_modulate"')
        if factor == 2:
            track = re.sub(r'"times": PackedFloat32Array\(([^)]+)\)', lambda m: '"times": PackedFloat32Array(' + ', '.join(str(float(v)/factor) for v in m[1].split(',')) + ')', track)
        tracks.append(re.sub(r'tracks/\d+/', f'tracks/{len(tracks)}/', track))
    # 旧寒冰蓄力光效需要在中断动作中归零。
    if name in ['wait', 'walk', 'hurt', 'death', 'hit1', 'htsl', 'qnbx']:
        i = len(tracks)
        tracks.append(f'tracks/{i}/type = "value"\ntracks/{i}/path = NodePath("Facing/Visual/Cast:frame")\ntracks/{i}/keys = {{\n"times": PackedFloat32Array(0),\n"transitions": PackedFloat32Array(1),\n"update": 1,\n"values": [0]\n}}\n')
    i = len(tracks)
    is_cast = name == 'qnhb'
    visibility_times = '0,1.9' if is_cast else '0'
    visibility_transitions = '1,1' if is_cast else '1'
    visibility_values = 'true,false' if is_cast else 'false'
    tracks.append(f'tracks/{i}/type = "value"\ntracks/{i}/path = NodePath("Facing/Visual/Cast:visible")\ntracks/{i}/keys = {{\n"times": PackedFloat32Array({visibility_times}),\n"transitions": PackedFloat32Array({visibility_transitions}),\n"update": 1,\n"values": [{visibility_values}]\n}}\n')
    if not is_cast:
        i += 1
        tracks.append(f'tracks/{i}/type = "value"\ntracks/{i}/path = NodePath("Facing/Visual/Cast:animation")\ntracks/{i}/keys = {{\n"times": PackedFloat32Array(0),\n"transitions": PackedFloat32Array(1),\n"update": 1,\n"values": ["empty"]\n}}\n')
    animations.append(header + ''.join(tracks))
write('Content/Monsters/DragonKingAnimations.tres', '[gd_resource type="AnimationLibrary" format=3]\n' + ''.join(animations) + '\n[resource]\n' + re.sub(r'"RESET": SubResource\([^\n]+\n', '', clean(library).split('\n',1)[1]))
# 身体独立继承基础演员；不借用另一个 Boss 的随身技能节点。
scene = '''[gd_scene format=3]
''' + ext('PackedScene','Scenes/Actors/Monster.tscn','base') + ext('SpriteFrames','Content/Monsters/DragonKingFrames.tres','frames') + ext('SpriteFrames','Content/Monsters/DragonKingCastFrames.tres','cast') + ext('AnimationLibrary','Content/Monsters/DragonKingAnimations.tres','animations') + '''
[sub_resource type="CapsuleShape2D" id="body"]
radius = 25.0
height = 130.0
[node name="DragonKing" instance=ExtResource("base")]
IsBoss = true
[node name="BodyShape" parent="." index="0"]
position = Vector2(0, -65)
shape = SubResource("body")
[node name="Visual" parent="Facing" index="0"]
position = Vector2(0, -72.5)
[node name="Body" parent="Facing/Visual" index="0"]
sprite_frames = ExtResource("frames")
animation = &"wait"
[node name="Cast" type="AnimatedSprite2D" parent="Facing/Visual" index="1"]
visible = false
sprite_frames = ExtResource("cast")
animation = &"empty"
[node name="HurtBox" parent="." index="2"]
position = Vector2(0, -65)
[node name="Shape" parent="HurtBox" index="0"]
shape = SubResource("body")
[node name="AnimationPlayer" parent="." index="3"]
libraries/ = ExtResource("animations")
'''
write('Scenes/Actors/DragonKing.tscn', scene)
for name, speed in [('Monster6Bullet3',10), ('qnhb_2',16.8), ('htsl',25)]:
    frame_animation('Scene/Monster/MonsterBullet.tscn', name, f'Content/Monsters/DragonKing_{name}_frames.tres', speed)
frame_animation('Scene/Bullet/Fly_bullet.tscn','Monster6Bullet','Content/Monsters/DragonKing_projectile_frames.tres',10)

script=(OLD/'Script/Monster/Monster_6.gd').read_text(encoding='utf-8')
for tier, text in [('low',script[:script.index('\n\telse:')]),('high',script[script.index('\n\telse:'):script.index('\nfunc _ready')])]:
    def field(name):
        return re.search(rf'self\.{name}\s*=\s*([^\n]+)',text)[1].strip()
    skill_refs=''; choices=''
    for index,(name,caption,cd) in enumerate([('htsl','洪涛水龙',12),('qnbx','千年冰雪',19),('qnhb','千年寒冰',16),('hit1','龙王普攻',1.5)]):
        data = re.search(rf'objattackDic\["{name}"\] = \{{(.*?)(?=self.objattackDic|self.fall_pro)',text,re.S)[1]
        power = re.search(r'"power":\s*(\d+)',data)[1]
        kind = {'physics':0,'magic':1,'real':2}[re.search(r'"attackKind": "([^"]+)"',data)[1]]
        kb = list(map(float,re.search(r'"hurtBack":\[([^]]+)\]',data)[1].split(',')))
        hit=ext('Script','Scripts/Combat/HitDefinition.cs','hit')
        buff=''
        if name == 'qnbx' or name == 'qnhb' and tier == 'high':
            hit+=ext('Script','Scripts/Combat/Buffs/BuffDefinition.cs','buff')
            buff='[sub_resource type="Resource" id="buff"]\nscript = ExtResource("buff")\n'
            if name == 'qnbx':
                buff+=f'Id = "dragon_freeze_{tier}"\nDisplayName = "冻结"\nDuration = {4 if tier=="low" else 5}.0\nPreventsActions = true\nMoveSpeedMultiplier = 0.0\n'
            else:
                buff+='Id = "dragon_bleed"\nDisplayName = "流血"\nDuration = 5.0\nPulseInterval = 0.5\nDamagePerPulse = 20.0\nPulseDamageType = 2\n'
        hit+=buff+'[sub_resource type="Resource" id="hit"]\nscript = ExtResource("hit")\n'+f'FlatDamage = {power}.0\nAttackMultiplier = 0.0\nDamageType = {kind}\nKnockback = Vector2({abs(kb[0])*10}, {kb[1]*10})\nHitstun = 0.13\n'+('AppliedBuff = SubResource("buff")\n' if buff else '')
        behavior_path=f'Scenes/Skills/DragonKing/{tier}_{name}.tscn'
        if name == 'hit1':
            projectile=f'Scenes/Skills/DragonKing/{tier}_projectile.tscn'
            write(projectile,'[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/SkillProjectile.cs','script')+ext('SpriteFrames','Content/Monsters/DragonKing_projectile_frames.tres','frames')+hit+'''[node name="DragonProjectile" type="Node2D"]
script = ExtResource("script")
Speed = 3400.0
Lifetime = 1.0
HitRadius = 40.0
VerticalHitRadius = 70.0
Hit = SubResource("hit")
Frames = ExtResource("frames")
Animation = &"Monster6Bullet"
VisualRotationDegrees = -90.0
''')
            behavior='[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/ProjectileSkillBehavior.cs','script')+ext('PackedScene',projectile,'projectile')+'''[node name="DragonShot" type="Node"]
script = ExtResource("script")
ProjectileScene = ExtResource("projectile")
ReleaseTimes = PackedFloat32Array(0.4)
SpawnOffset = Vector2(100, -65)
'''
        else:
            anim={'qnbx':'Monster6Bullet3','qnhb':'qnhb_2','htsl':'htsl'}[name]
            if name=='qnbx':
                start=0; times=[1.2 if tier=='low' else 1.6]*5; xs=[-182,-91,0,91,182]; radius=38; window=(0,40); offset=-70; scale=1
            elif name=='qnhb':
                start=1.85;times=[0,.2,.2,.4,.4,.6,.6,.8,.8];xs=[0,-110,110,-290,290,-400,400,-510,510];radius=60;window=(4,8);offset=-45;scale=1.2
            else:
                start=.8;times=[0];xs=[0];radius=70;window=(3,38);offset=-100;scale=1
            behavior='[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/GroundAttackSequenceBehavior.cs','script')+ext('Script','Scripts/Combat/HitEvent.cs','window')+ext('SpriteFrames',f'Content/Monsters/DragonKing_{anim}_frames.tres','frames')+hit
            behavior+=f'[sub_resource type="Resource" id="window"]\nscript = ExtResource("window")\nStartFrame = {window[0]}\nEndFrame = {window[1]}\nHit = SubResource("hit")\n'
            behavior+='[node name="GroundSequence" type="Node"]\nscript = ExtResource("script")\n'+f'StartTime = {start}\nReleaseTimes = PackedFloat32Array('+','.join(map(str,times))+')\nOffsets = PackedVector2Array('+','.join(f'{x},0' for x in xs)+f')\nRadius = {radius}.0\nFrames = ExtResource("frames")\nAnimation = &"{anim}"\nVisualOffset = Vector2(0,{offset})\nVisualScale = Vector2({scale},{scale})\nHits = Array[Resource]([SubResource("window")])\n'
            if name=='htsl' or name=='qnbx' and tier=='high':
                behavior+='TargetEnemy = true\n'
            if name=='htsl':
                behavior+='SampleTargetOnRelease = true\n'
        if name == 'htsl':
            behavior = behavior.replace('[node name=', '[sub_resource type="CanvasItemMaterial" id="additive"]\nblend_mode = 1\n[node name=', 1)
            behavior += 'VisualMaterial = SubResource("additive")\n'
        if name == 'qnhb':
            behavior = behavior.replace('VisualOffset = Vector2(0,-45)', 'VisualOffset = Vector2(0,0)')
            behavior += 'AlignVisualToGround = true\n'
        write(behavior_path,behavior)
        skill_path=f'Content/Monsters/DragonKing/{tier}_{name}.tres'
        write(skill_path,'[gd_resource type="Resource" format=3]\n'+ext('Script','Scripts/Combat/SkillDefinition.cs','skill')+ext('PackedScene',behavior_path,'behavior')+f'[resource]\nscript = ExtResource("skill")\nId = "dragon_{tier}_{name}"\nDisplayName = "{caption}"\nAnimation = &"{name}"\nFramesPerSecond = 60\nCooldownSeconds = {cd}\nBehaviorScene = ExtResource("behavior")\n')
        skill_refs+=ext('Resource',skill_path,f's{index}')
        choices+=f'[sub_resource type="Resource" id="choice{index}"]\nscript = ExtResource("choice")\nSkill = ExtResource("s{index}")\nMaximumRange = {600 if name!="hit1" else 160}.0\nMaximumHeightDifference = 200.0\n'
    ids=re.findall(r'"name":"([^"]+)"',text)
    for item in ids:
        if not (ROOT/f'Content/Items/{item}.tres').exists():
            raise FileNotFoundError(item)
    # 当前共享工程只将悟空和唐僧装备纳入可玩掉落。
    ids = [item for item in ids if not (owner := re.search(r'RequiredCharacterId = "([^"]+)"', (ROOT/f'Content/Items/{item}.tres').read_text(encoding='utf-8'))) or owner[1] in ['role_1', 'role_2']]
    headers=ext('Script','Scripts/Monsters/MonsterDefinition.cs','def')+ext('Script','Scripts/Monsters/MonsterSkillChoice.cs','choice')+ext('Script','Scripts/Monsters/MonsterDropTable.cs','table')+ext('Script','Scripts/Monsters/MonsterDropEntry.cs','entry')+ext('PackedScene','Scenes/Actors/DragonKing.tscn','actor')+skill_refs
    drops=''.join(f'[sub_resource type="Resource" id="drop{i}"]\nscript = ExtResource("entry")\nItemId = "{item}"\n' for i,item in enumerate(ids))
    drops+='[sub_resource type="Resource" id="drops"]\nscript = ExtResource("table")\nChooseOne = true\nRollProbability = 0.4\nEntries = Array[Resource](['+', '.join(f'SubResource("drop{i}")' for i in range(len(ids)))+'])\n'
    definition=f'[resource]\nscript = ExtResource("def")\nId = "dragon_king_{tier}"\nDisplayName = {field("my_mr_name")}\nActorScene = ExtResource("actor")\nIsBoss = true\n'
    for key,value in {'Health':'SHp','Attack':'attack_in','Level':'level','PhysicalDefense':'def','MagicDefense':'mdef','CriticalRating':'crit','Luck':'lucky','Accuracy':'Htarget','Experience':'add_exp','Souls':'fall_coin'}.items():
        definition+=f'{key} = {field(value)}\n'
    definition+='MoveSpeed = 110.0\nDetectionRange = 600.0\nSkills = Array[Resource](['+', '.join(f'SubResource("choice{i}")' for i in range(4))+'])\nDrops = SubResource("drops")\n'
    write(f'Content/Monsters/DragonKing/{tier}.tres','[gd_resource type="Resource" format=3]\n'+headers+choices+drops+definition)

bs=blocks('Scene/Level/Level_sjg.tscn'); textures=[b for b in bs if b.startswith('[ext_resource type="Texture2D"')];copy_textures(textures)
map_text='[gd_scene format=3]\n'+''.join(map(clean,textures))+'''[sub_resource type="RectangleShape2D" id="floor"]
size = Vector2(1666,1025)
[node name="CrystalPalace" type="Node2D"]
[node name="wall" type="StaticBody2D" parent="."]
collision_mask = 0
[node name="floor" type="CollisionShape2D" parent="wall"]
position = Vector2(793,990.5)
shape = SubResource("floor")
[node name="LeftBoundary" type="CollisionPolygon2D" parent="wall"]
polygon = PackedVector2Array(-100,-1000,0,-1000,0,1000,-100,1000)
[node name="RightBoundary" type="CollisionPolygon2D" parent="wall"]
polygon = PackedVector2Array(1000,-1000,1100,-1000,1100,1000,1000,1000)
[node name="BackGround" type="ParallaxBackground" parent="."]
'''
# 原版水晶宫地面世界525，整体上移47；美术与碰撞一起移动保持相对关系。
for b in bs:
    if not b.startswith('[node') or 'parent="BackGround' not in b:
        continue
    b=clean(re.sub(r' index="\d+"','',b))
    if b.startswith('[node name="End"'):
        b=b.replace('parent=', 'type="ParallaxLayer" parent=',1)+'motion_scale = Vector2(0,0)\n'
    if b.startswith('[node name="floor_2"'):
        b=b.replace('parent=', 'type="ParallaxLayer" parent=',1)
    # 子图片统一平移，父层原位保留。
    if 'type="Sprite2D"' in b:
        b=re.sub(r'position = Vector2\(([^,]+), ([^)]+)\)',lambda m:f'position = Vector2({m[1]}, {float(m[2])-46})',b)
    map_text+=b
write('Scenes/Maps/CrystalPalace.tscn',map_text)
for tier in ['low','high']:
    scene_path=f'Scenes/Level/CrystalPalace_{tier}.tscn'
    headers=ext('Resource','Content/Audio/10_bg4.tres','music')+ext('Script','Scripts/Level/LevelDefinition.cs','def')+ext('Script','Scripts/Level/WaveDefinition.cs','wave')+ext('Script','Scripts/Level/WaveEncounterDefinition.cs','enc')+ext('Resource',f'Content/Monsters/DragonKing/{tier}.tres','boss')+ext('PackedScene','Scenes/Maps/CrystalPalace.tscn','map')+ext('Resource','Content/Levels/Difficulties/normal.tres','normal')+ext('Resource','Content/Levels/Difficulties/hard.tres','hard')
    write(f'Content/Levels/crystal_palace_{tier}.tres','[gd_resource type="Resource" format=3]\n'+headers+f'''[sub_resource type="Resource" id="wave"]
script = ExtResource("wave")
Monsters = Array[Resource]([ExtResource("boss")])
EntranceX = 0.0
SpawnRange = Vector2(850,850)
GateX = 980.0
CameraRight = 1000
SpawnInterval = 3.0
SpawnRayTop = -200.0
SpawnRayBottom = 650.0
ReleaseGateOnClear = false
[sub_resource type="Resource" id="encounter"]
script = ExtResource("enc")
Waves = Array[Resource]([SubResource("wave")])
[resource]
script = ExtResource("def")
Music = ExtResource("music")
Id = "crystal_palace_{tier}"
DisplayName = "水晶宫"
Description = "水帘洞水面下的隐藏龙王关卡"
ProgressLevel = 2
AdvancesCampaign = false
LevelScenePath = "res://{scene_path}"
MapScene = ExtResource("map")
Difficulties = Array[Resource]([ExtResource("normal"),ExtResource("hard")])
PlayerSpawn = Vector2(200,474)
CameraRight = 1000
CameraBottom = 650
ExitPosition = Vector2(768,478)
WaveEncounter = SubResource("encounter")
''')
    write(scene_path,'[gd_scene format=3]\n'+ext('PackedScene','Scenes/Level/GameplayLevel.tscn','base')+ext('Resource',f'Content/Levels/crystal_palace_{tier}.tres','definition')+f'[node name="CrystalPalace_{tier}" instance=ExtResource("base")]\nLevelNumber = 2\nDefaultDefinition = ExtResource("definition")\n')
print('Extracted Crystal Palace, both Dragon King templates and four skills per tier')
