"""一次性迁移登记数据与唐僧试玩动作；游戏运行时只读取生成的资源。"""
from pathlib import Path
import json
import re
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / "造梦八荒-(4.1)"

def write(path, text):
    destination = ROOT / path
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(text, encoding="utf-8")

def clean(text):
    text = re.sub(r'^script = null\n?', '', text, flags=re.M)
    text = re.sub(r' uid="[^"]+"', '', text)
    return text.replace('res://Art/', 'res://Assets/Art/')

def ext(kind, path, key):
    return f'[ext_resource type="{kind}" path="res://{path}" id="{key}"]\n'

def scene(name, script, properties, extra="", sub=""):
    return '[gd_scene format=3]\n' + ext('Script', f'Scripts/Combat/Skills/{script}.cs', 'behavior') + extra + '\n' + sub + f'\n[node name="{name}" type="Node"]\nscript = ExtResource("behavior")\n' + properties + '\n'

def resource(name, script, properties, extra="", sub=""):
    return f'[gd_resource type="Resource" script_class="{script}" format=3]\n' + ext('Script', f'Scripts/Combat/Skills/{script}.cs', 'script') + extra + '\n' + sub + '\n[resource]\nscript = ExtResource("script")\n' + properties + '\n'

def add_behavior(skill, path):
    target = ROOT / f'Content/Skills/Wukong/{skill}.tres'
    text = target.read_text(encoding='utf-8')
    text = re.sub(r'^\[ext_resource[^\n]*id="behavior"[^\n]*\n|^BehaviorScene = [^\n]*\n', '', text, flags=re.M)
    text = text.replace('[resource]', ext('PackedScene', path, 'behavior') + '\n[resource]')
    # External resources must precede subresources.
    external = ext('PackedScene', path, 'behavior')
    text = text.replace(external, '').replace('[ext_resource', external + '[ext_resource', 1)
    target.write_text(text.rstrip() + '\nBehaviorScene = ExtResource("behavior")\n', encoding='utf-8')

write('Content/Skills/Behaviors/CloudFlight.tscn', scene('CloudFlight', 'CloudFlightBehavior', 'LiftSpeed = 430.0\nRiseKey = 87'))
hit_script = ext('Script', 'Scripts/Combat/HitDefinition.cs', 'hit')
slash_frames = ext('SpriteFrames', 'Content/Skills/Wukong/FireSlashFrames.tres', 'frames')
slash_hits = '''[sub_resource type="Resource" id="air"]
script = ExtResource("hit")
AttackMultiplier = 1.32
AttackMultiplierMax = 1.4
Knockback = Vector2(60, -90)
[sub_resource type="Resource" id="dive"]
script = ExtResource("hit")
AttackMultiplier = 1.32
DamageType = 1
Knockback = Vector2(660, 270)
[sub_resource type="Resource" id="landing"]
script = ExtResource("hit")
AttackMultiplier = 4.5
AttackMultiplierMax = 4.8
DamageType = 1
Knockback = Vector2(90, -420)
'''
write('Content/Skills/Behaviors/FireSlash.tscn', scene('FireSlash', 'FireSlashBehavior', 'Frames = ExtResource("frames")\nAirHit = SubResource("air")\nDiveHit = SubResource("dive")\nLandingHit = SubResource("landing")', hit_script + slash_frames, slash_hits))
fire_effect = scene('FireEyesImpact', 'FireEyesEffect', 'Frames = ExtResource("frames")\nHit = SubResource("damage")\nHitCount = 3\nInterval = 1.1', hit_script + ext('SpriteFrames', 'Content/Skills/Wukong/FireEyesFrames.tres', 'frames'), '[sub_resource type="Resource" id="damage"]\nscript = ExtResource("hit")\nAttackMultiplier = 0.8\nDamageType = 1\nKnockback = Vector2(0, 0)\n')
write('Content/Skills/Behaviors/FireEyesImpact.tscn', fire_effect.replace('type="Node"]', 'type="Node2D"]'))
write('Content/Skills/Behaviors/FireEyes.tscn', scene('FireEyes', 'FireEyesBehavior', 'Delay = 0.7\nRange = 2000.0\nImpactScene = ExtResource("impact")', ext('PackedScene', 'Content/Skills/Behaviors/FireEyesImpact.tscn', 'impact')))
for skill, behavior in [('jdy', 'CloudFlight'), ('hmz', 'FireSlash'), ('hyjj', 'FireEyes')]:
    add_behavior(skill, f'Content/Skills/Behaviors/{behavior}.tscn')

# 旧通用被动只有升级箭头，没有独立技能图标；明确复用该图标。
for skill, name, stat, description in [('kb', '狂暴', 0, '暴击属性增加；各级为2、3、4、5、6、7。'), ('yh', '永恒', 1, '每秒恢复生命；各级为2、3、4、5、6、7。'), ('hh', '辉煌', 2, '每秒恢复魔法；各级为2、3、4、5、6、7。')]:
    extras = ext('Texture2D', 'Assets/Art/Skill/uplevel.png', 'icon') + ext('Script', 'Scripts/Combat/Skills/PassiveSkillBonus.cs', 'bonus')
    bonus = f'[sub_resource type="Resource" id="bonus"]\nscript = ExtResource("bonus")\nStat = {stat}\nValues = PackedFloat32Array(2, 3, 4, 5, 6, 7)\n'
    props = f'Id = "{skill}"\nDisplayName = "{name}"\nDescription = "{description}"\nIcon = ExtResource("icon")\nMaximumLevel = 6\nLearningCosts = PackedInt32Array(5000, 10000, 15000, 20000, 25000, 30000)\nPassiveBonuses = Array[ExtResource("bonus")]([SubResource("bonus")])'
    write(f'Content/Skills/Common/{skill}.tres', resource(skill, 'SkillEntry', props, extras, bonus))
extras = ext('Texture2D', 'Assets/Art/Skill/SkillIcon/sx.png', 'icon') + ext('Script', 'Scripts/Combat/Skills/PassiveSkillBonus.cs', 'bonus')
bonuses = ''
for key, stat, values in [('critical', 0, [4 + i * 1.6 for i in range(10)]), ('lifesteal', 3, [.03 + i * .003 for i in range(10)])]:
    bonuses += f'[sub_resource type="Resource" id="{key}"]\nscript = ExtResource("bonus")\nStat = {stat}\nValues = PackedFloat32Array(' + ', '.join(f'{x:g}' for x in values) + ')\n'
write('Content/Skills/Wukong/sx.tres', resource('sx', 'SkillEntry', 'Id = "sx"\nDisplayName = "嗜血"\nDescription = "被动提高暴击和吸血，升级提高效果。"\nIcon = ExtResource("icon")\nMaximumLevel = 10\nLearningCosts = PackedInt32Array(500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000)\nPassiveBonuses = Array[ExtResource("bonus")]([SubResource("critical"), SubResource("lifesteal")])', extras, bonuses))

# 提取唐僧实际动作轨道；不接旧脚本、音乐、翅膀和全局方法。
source = (OLD / 'Scene/Hero/Role_2/Role_2.tscn').read_text(encoding='utf-8')
blocks = re.split(r'(?=^\[)', source, flags=re.M)
resources = {re.search(r'\bid="([^"]+)"', b)[1]: b for b in blocks if b.startswith('[sub_resource')}
library = next(b for b in blocks if b.startswith('[sub_resource type="AnimationLibrary"'))
names = dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)', library))
selected = ['wait', 'run', 'jump1', 'jump2', 'drop', 'hurt', 'death', 'hit1', 'blb', 'tjgl']
animations = []
for name in selected:
    original = resources[names[name]]
    length = re.search(r'^length = ([\d.]+)', original, re.M)
    result = f'[sub_resource type="Animation" id="anim_{name}"]\nlength = {length[1] if length else "1.0"}\n'
    if name in ['wait', 'run']: result += 'loop_mode = 1\n'
    index = 0
    for track in re.split(r'(?=tracks/\d+/type =)', original)[1:]:
        path = re.search(r'NodePath\("([^"]+)"\)', track)[1]
        if '/type = "method"' in track and path != 'Action/Death': continue
        if path.startswith(('Action/RoleBody:', 'Action/RoleEquipment:', 'Action/Death')):
            path = path.replace('Action/', 'Facing/Visual/')
        elif path == 'base_damagebox/HitBox/HitBox:disabled':
            path = 'Facing/HitBox:Active'
            track = re.sub(r'"values": \[([^\]]+)\]', lambda m: '"values": [' + m[1].replace('true', 'TEMP').replace('false', 'true').replace('TEMP', 'false') + ']', track)
        elif path.startswith('base_damagebox/HitBox/HitBox:') and path.endswith(('shape', 'position')):
            path = path.replace('base_damagebox/HitBox/HitBox:', 'Facing/HitBox/Shape:')
        else: continue
        track = re.sub(r'NodePath\("[^"]+"\)', f'NodePath("{path}")', track)
        track = re.sub(r'tracks/\d+/', f'tracks/{index}/', track)
        result += clean(track)
        index += 1
    if name == 'hit1':
        result += f'''tracks/{index}/type = "method"
tracks/{index}/path = NodePath(".")
tracks/{index}/keys = {{
"times": PackedFloat32Array(0.2),
"transitions": PackedFloat32Array(1),
"values": [{{"args": [], "method": &"SpawnAttackEffect"}}]
}}
'''
    animations.append(result)
needed = set(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', ''.join(animations)))
shapes = ''.join(clean(resources[key]) for key in needed)
write('Content/Skills/Tangseng/Animations.tres', '[gd_resource type="AnimationLibrary" format=3]\n\n' + shapes + ''.join(animations) + '\n[resource]\n_data = {\n' + ',\n'.join(f'"{name}": SubResource("anim_{name}")' for name in selected) + '\n}\n')

def extract_frames(source_path, selected_name, target, trim=False):
    source = (OLD / source_path).read_text(encoding='utf-8')
    blocks = re.split(r'(?=^\[)', source, flags=re.M)
    frames = next(b for b in blocks if b.startswith('[sub_resource type="SpriteFrames"') and f'"name": &"{selected_name}"' in b)
    data = frames[frames.index('animations = [') + len('animations = ['):]
    depth, start, animation = 0, None, ''
    for i, ch in enumerate(data):
        if ch == '{':
            if depth == 0: start = i
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0 and start is not None:
                candidate = data[start:i + 1]
                if f'"name": &"{selected_name}"' in candidate: animation = candidate
    resource_blocks = {re.search(r'\bid="([^"]+)"', b)[1]: b for b in blocks if b.startswith('[sub_resource')}
    pending = re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', animation)
    needed = set()
    while pending:
        key = pending.pop()
        if key in needed: continue
        needed.add(key)
        pending.extend(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', resource_blocks[key]))
    sub = ''.join(resource_blocks[key] for key in resource_blocks if key in needed)
    ext_ids = set(re.findall(r'ExtResource\(\s*"([^"]+)"\s*\)', sub + animation))
    external = ''.join(b for b in blocks if b.startswith('[ext_resource') and re.search(r'\bid="([^"]+)"', b)[1] in ext_ids)
    if trim:
        # 旧帧图含画布内移动，新运行时负责弹道，只调整资源区域，不改原图。
        paths = {m[2]: ROOT / m[1].replace('res://Art/', 'Assets/Art/') for m in re.finditer(r'path="([^"]+)" id="([^"]+)"', external)}
        images = {key: Image.open(path).convert('RGBA') for key, path in paths.items()}
        def trim_region(match):
            x, y, width, height = map(int, match[2].split(', '))
            bounds = images[match[1]].crop((x, y, x + width, y + height)).getbbox()
            if bounds is None: return match[0]
            left, top, right, bottom = bounds
            return match[0].replace(f'Rect2({match[2]})', f'Rect2({x + left}, {y + top}, {right - left}, {bottom - top})')
        sub = re.sub(r'atlas = ExtResource\(\s*"([^"]+)"\s*\)\s*region = Rect2\(([^)]+)\)', trim_region, sub)
    write(target, '[gd_resource type="SpriteFrames" format=3]\n\n' + clean(external + sub) + '\n[resource]\nanimations = [' + animation + ']\n')

extract_frames('Scene/Hero/RoleBullet.tscn', 'Role2Bullet2', 'Content/Skills/Tangseng/IceDragonFrames.tres', trim=True)
extract_frames('Scene/Hero/RoleBullet.tscn', 'Role2Bullet1', 'Content/Skills/Tangseng/NormalFrames.tres', trim=True)
extract_frames('Scene/Hero/RoleSpecialEffect.tscn', 'tjgl', 'Content/Skills/Tangseng/HealingFrames.tres')
extract_frames('Scene/Hero/Role_2/Role_2.tscn', 'death', 'Content/Skills/Tangseng/DeathFrames.tres')
write('Content/Skills/Tangseng/HealingVisual.tscn', '[gd_scene format=3]\n' + ext('SpriteFrames', 'Content/Skills/Tangseng/HealingFrames.tres', 'frames') + '\n[sub_resource type="CanvasItemMaterial" id="glow"]\nblend_mode = 1\n\n[node name="Healing" type="AnimatedSprite2D"]\nmaterial = SubResource("glow")\noffset = Vector2(0, -50)\nself_modulate = Color(1, 1, 0.611765, 1)\nsprite_frames = ExtResource("frames")\nanimation = &"tjgl"\nautoplay = "tjgl"\nshow_behind_parent = true\n')
for kind, frames, multiplier, scale, speed in [('IceDragon', 'IceDragonFrames', .55, 1, 600), ('Normal', 'NormalFrames', .26, 1, 480)]:
    projectile = scene(kind, 'SkillProjectile', f'Speed = {speed}.0\nLifetime = 1.4\nHitRadius = 55.0\nVisualScale = {scale}\nHit = SubResource("damage")\nFrames = ExtResource("frames")\nAnimation = &"Role2Bullet{2 if kind == "IceDragon" else 1}"', hit_script + ext('SpriteFrames', f'Content/Skills/Tangseng/{frames}.tres', 'frames'), f'[sub_resource type="Resource" id="damage"]\nscript = ExtResource("hit")\nAttackMultiplier = {multiplier}\nDamageType = 1\nKnockback = Vector2(90, -60)\n')
    write(f'Content/Skills/Tangseng/{kind}Projectile.tscn', projectile.replace('type="Node"]', 'type="Node2D"]'))
write('Content/Skills/Behaviors/IceDragon.tscn', scene('IceDragon', 'ProjectileSkillBehavior', 'Delay = 0.5\nProjectileScene = ExtResource("projectile")', ext('PackedScene', 'Content/Skills/Tangseng/IceDragonProjectile.tscn', 'projectile')))
write('Content/Skills/Behaviors/HealingRain.tscn', scene('HealingRain', 'HealSkillBehavior', 'Delay = 0.7\nHealthRatio = 0.2\nMissingHealthMultiplier = 0.6\nVisualScene = ExtResource("visual")', ext('PackedScene', 'Content/Skills/Tangseng/HealingVisual.tscn', 'visual')))
for skill, animation, cooldown, mana, behavior in [('blb', 'blb', 2.4, 15, 'IceDragon'), ('tjgl', 'tjgl', 4.8, 35, 'HealingRain')]:
    write(f'Content/Skills/Tangseng/{skill}.tres', '[gd_resource type="Resource" script_class="SkillDefinition" format=3]\n' + ext('Script', 'Scripts/Combat/SkillDefinition.cs', 'skill') + ext('PackedScene', f'Content/Skills/Behaviors/{behavior}.tscn', 'behavior') + f'\n[resource]\nscript = ExtResource("skill")\nId = "{skill}"\nAnimation = &"{animation}"\nFramesPerSecond = 10\nCooldownSeconds = {cooldown}\nBaseManaCost = {mana}\nBehaviorScene = ExtResource("behavior")\n')
write('Content/Attacks/Tangseng/hit1.tres', '[gd_resource type="Resource" script_class="AttackStep" format=3]\n' + ext('Script', 'Scripts/Combat/AttackStep.cs', 'step') + hit_script + ext('PackedScene', 'Content/Skills/Tangseng/NormalProjectile.tscn', 'projectile') + '\n[sub_resource type="Resource" id="damage"]\nscript = ExtResource("hit")\nAttackMultiplier = 0.26\nDamageType = 1\n\n[resource]\nscript = ExtResource("step")\nAnimation = &"hit1"\nFramesPerSecond = 10\nHit = SubResource("damage")\nEffectScene = ExtResource("projectile")\nEffectOffset = Vector2(-45, -45)\nEffectLifetime = 0.0\n')

# 核对旧场景压缩纹理与原导入映射，再引用已恢复的原始图片。
body_paths = []
for key in ['107', '108']:
    ctex = re.search(r'load_path = "res://([^"]+)"', resources[key])[1]
    imported = next(p for p in (OLD / 'Art/HeroPicture').rglob('*.import') if ctex in p.read_text(encoding='utf-8'))
    path = 'Assets/' + imported.relative_to(OLD).as_posix().removesuffix('.import')
    assert (ROOT / path).exists(), path
    body_paths.append(path)
actor = '[gd_scene format=3]\n' + ext('PackedScene', 'Scenes/Actors/Player.tscn', 'player') + ext('AnimationLibrary', 'Content/Skills/Tangseng/Animations.tres', 'animations') + ext('Resource', 'Content/Attacks/Tangseng/hit1.tres', 'attack') + ext('Texture2D', body_paths[0], 'body') + ext('Texture2D', body_paths[1], 'equipment')
actor += ext('SpriteFrames', 'Content/Skills/Tangseng/DeathFrames.tres', 'death')
actor += '''
[node name="Tangseng" instance=ExtResource("player")]
EquippedSkill1 = null
EquippedSkill2 = null
EquippedSkill3 = null
EquippedSkill4 = null
EquippedSkill5 = null
NormalCombo = Array[Resource]([ExtResource("attack")])
DoubleJumpEffect = null
[node name="Visual" parent="Facing" index="0"]
position = Vector2(0, -41)
[node name="RoleBody" parent="Facing/Visual" index="0"]
texture = ExtResource("body")
hframes = 6
vframes = 13
offset = Vector2(-15, -15)
[node name="RoleEquipment" parent="Facing/Visual" index="1"]
texture = ExtResource("equipment")
hframes = 6
vframes = 13
offset = Vector2(-15, -15)
[node name="SpecialEffect" parent="Facing/Visual" index="3"]
visible = false
[node name="SpecialEffect2" parent="Facing/Visual" index="2"]
visible = false
[node name="Death" parent="Facing/Visual" index="4"]
visible = false
position = Vector2(0, -22)
sprite_frames = ExtResource("death")
animation = &"death"
offset = Vector2(0, -120)
[node name="AnimationPlayer" parent="." index="3"]
libraries/ = ExtResource("animations")
'''
write('Scenes/Actors/Tangseng.tscn', actor)

old_ui = (OLD / 'Script/Skill/zd_skill.gd').read_text(encoding='utf-8')
def old_list(variable, role):
    data = old_ui[old_ui.index(f'var {variable} ='):]
    return re.findall(r'"([^"]+)"', re.search(r'"角色' + str(role) + r'"\s*:\s*\[([^\]]+)\]', data)[1])

def catalog(folder, role, display_name, description, actor_path, portrait, ids, names, descriptions, orders, passives, reserved):
    external = ext('Script', 'Scripts/Combat/Skills/SkillCatalog.cs', 'catalog') + ext('Script', 'Scripts/Combat/Skills/SkillEntry.cs', 'entry') + ext('PackedScene', actor_path, 'actor') + ext('Texture2D', portrait, 'portrait')
    sub, entries = '', []
    for skill, name, desc, order in zip(ids, names, descriptions, orders):
        external += ext('Resource', f'Content/Skills/{folder}/{skill}.tres', f'action_{skill}') + ext('Texture2D', f'Assets/Art/Skill/SkillIcon/{skill}.png', f'icon_{skill}')
        sub += f'[sub_resource type="Resource" id="entry_{skill}"]\nscript = ExtResource("entry")\nId = "{skill}"\nDisplayName = {json.dumps(name, ensure_ascii=False)}\nDescription = {json.dumps(desc, ensure_ascii=False)}\nIcon = ExtResource("icon_{skill}")\nMaximumLevel = 1\nLearningCosts = PackedInt32Array({100 + order * 300})\nAction = ExtResource("action_{skill}")\n'
        entries.append(f'SubResource("entry_{skill}")')
    for skill, path in passives:
        external += ext('Resource', path, f'passive_{skill}')
        entries.append(f'ExtResource("passive_{skill}")')
    write(f'Content/Skills/{folder}/Catalog.tres', '[gd_resource type="Resource" script_class="SkillCatalog" format=3]\n' + external + '\n' + sub + f'\n[resource]\nscript = ExtResource("catalog")\nCharacterId = "role_{role}"\nDisplayName = "{display_name}"\nDescription = "{description}"\nPortrait = ExtResource("portrait")\nSelectionAnimation = &"Role_{role}"\nActorScene = ExtResource("actor")\nReservedKeys = PackedInt64Array(' + ', '.join(map(str, reserved)) + ')\nSkills = Array[ExtResource("entry")]([' + ', '.join(entries) + '])\n')

shared = [(skill, f'Content/Skills/Common/{skill}.tres') for skill in ['kb', 'yh', 'hh']]
descriptions = old_list('Skill_Infor', 1)[:9]
descriptions[6] = '向前冲刺并打击触碰到的敌人。'
catalog('Wukong', 1, '孙悟空', '善用棍法，灵活多变。', 'Scenes/Actors/Player.tscn', 'Assets/Art/HeroPicture/RoleProperiesBox/swk.png', old_list('Skill_list', 1)[:9], old_list('Skill_Name', 1)[:9], descriptions, range(9), [('sx', 'Content/Skills/Wukong/sx.tres')] + shared, [87])
catalog('Tangseng', 2, '唐僧', '试玩：冰龙波、天降甘露和三个通用被动，其余技能尚未迁入。', 'Scenes/Actors/Tangseng.tscn', 'Assets/Art/HeroPicture/RoleProperiesBox/tsz.png', ['blb', 'tjgl'], ['冰龙波', '天降甘露'], ['短暂引导后释放冰龙冲击，造成魔法伤害并击退敌人。', '短暂引导后恢复生命，已损失生命越多，治疗效果越高。'], [0, 3], shared, [])
write('Content/Skills/Registry.tres', resource('Registry', 'SkillCatalogRegistry', 'Catalogs = Array[ExtResource("catalog_type")]([ExtResource("wukong"), ExtResource("tangseng")])', ext('Script', 'Scripts/Combat/Skills/SkillCatalog.cs', 'catalog_type') + ext('Resource', 'Content/Skills/Wukong/Catalog.tres', 'wukong') + ext('Resource', 'Content/Skills/Tangseng/Catalog.tres', 'tangseng')))
print('Registered Wukong, Tangseng prototype, passive bonuses and behavior scenes.')
