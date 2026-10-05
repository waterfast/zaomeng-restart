"""一次性迁移：核对旧场景引用后提取设置、装备和悟空动作，不执行旧 GDScript。"""
from pathlib import Path
import re
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / '造梦八荒-(4.1)'

def read(path):
    text = path.read_text(encoding='utf-8-sig')
    # 部分旧资源的 id 字符串末尾带换行；迁入时统一成引用中实际使用的 ID。
    return re.sub(r'\bid\s*=\s*"([^"]+)"', lambda m: 'id="'+m[1].strip()+'"', text)

def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding='utf-8')

def sections(text):
    return re.split(r'(?=^\[)', text, flags=re.M)

def clean(text):
    text = re.sub(r' uid="[^"]+"', '', text)
    text = re.sub(r' index="\d+"', '', text)
    return text.replace('res://Art/', 'res://Assets/Art/').replace('res://Font/', 'res://Assets/Font/')

# 旧设置面板的节点、位置和按钮美术完整保留，仅移除旧脚本和连接。
setting = read(OLD / 'Scene/set_menu.tscn')
setting = ''.join(b for b in sections(setting)
                  if not b.startswith('[connection') and not (b.startswith('[ext_resource') and 'type="Script"' in b))
setting = re.sub(r'^script = .*\n', '', clean(setting), flags=re.M)
setting = re.sub(r'^\[gd_scene[^\n]+', '[gd_scene format=3]', setting)
write('Scenes/UI/Settings/SetMenu.tscn', setting)

# 原版多个胶囊、矩形斜坡彼此穿插，内侧边和接缝会卡住胶囊角色。
# 美术保持原样，地形改为一条连续轮廓，顶部高度仍由旧场景坐标计算。
forest = read(ROOT / 'Scenes/Maps/Forest.tscn')
forest = ''.join(b for b in sections(forest) if not
    (b.startswith('[node ') and ('name="斜坡' in b or 'parent="wall/斜坡' in b or 'name="Terrain"' in b)))
polygon = '''[node name="Terrain" type="CollisionPolygon2D" parent="wall"]
polygon = PackedVector2Array(0, 392, 990, 392, 1076, 320, 1280, 320, 1370, 392, 1445, 392, 1554, 326, 1759, 326, 1850, 392, 4350, 392, 4474, 326, 4706, 326, 4802, 392, 5300, 392, 5300, 800, 0, 800)

'''
forest = forest.replace('[node name="BackGround"', polygon + '[node name="BackGround"')
write('Scenes/Maps/Forest.tscn', forest)

# 物品字段来自 AllEquipment.gd。随机基础值固定取中值，实例系统仍负责强化/宝石。
equipment = read(OLD / 'AllEquipment.gd')
stat_names = {'SHp':'HealthBonus', 'SMp':'ManaBonus', 'power':'Attack', 'Def':'PhysicalDefenseBonus',
              'Mdef':'MagicDefenseBonus', 'Crit':'CriticalRating', 'Miss':'DodgeBonus',
              'R_hp':'HealthRegenerationBonus', 'R_mp':'ManaRegenerationBonus', 'vampirism':'LifeStealBonus',
              'Lucky':'LuckBonus', 'Toughness':'ToughnessBonus', 'Htarget':'Accuracy',
              'CritReduce':'CriticalResistanceBonus', 'ar':'ArmorPenetrationBonus', 'sp':'MagicPenetrationBonus'}
item_ids = ['ptxzf', 'dsyj', 'dslj', 'jcsz', 'dshl', 'jcld']
for item_id in item_ids:
    start = re.search(r'"'+item_id+r'":\s*\{', equipment).end()
    end = equipment.index('"强化属性"', start)
    data = equipment[start:end]
    def field(name):
        return re.search(r'"'+name+r'":\s*"([^"]*)"', data)[1]
    slot = {'武器':0,'防具':1,'饰品':2,'法宝':6}[field('类型')]
    # 由旧背包的实际图标路径映射；法宝图标位于另一个文件夹。
    icon = f'Assets/Art/BackPack/AllItems/{item_id}.png'
    if not (ROOT / icon).exists():
        matches = list((ROOT / 'Assets').rglob(item_id+'.png'))
        if not matches:
            matches = list((ROOT.parent / 'zmbh-rebuild/assets').rglob(item_id+'.png'))
            if not matches:
                raise FileNotFoundError(item_id)
            (ROOT / icon).parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(matches[0], ROOT / icon)
        else:
            icon = matches[0].relative_to(ROOT).as_posix()
    color = field('颜色')
    rgb = ', '.join(str(round(int(color[i:i+2],16)/255,6)) for i in (0,2,4))
    values = {'Id':item_id, 'DisplayName':field('名字'), 'Description':field('描述'),
              'Quality':field('品质'), 'OwnerCaption':field('所属')}
    lines = [f'{k} = {json.dumps(v,ensure_ascii=False)}' for k,v in values.items()]
    lines += [f'Slot = {slot}', 'MaxStack = 1', 'Icon = ExtResource("2")',
              f'QualityColor = Color({rgb}, 1)', f'SellPrice = {re.search(r"\"售价\":\s*(\d+)",data)[1]}']
    if field('所属') == '悟空': lines.append('RequiredCharacterId = "role_1"')
    for old_name, new_name in stat_names.items():
        expr = re.search(r'"'+old_name+r'":\s*((?:randi_range\([^)]*\)|[^,])+)',data)[1].strip()
        expr = re.sub(r'randi_range\((\d+),(\d+)\)',lambda m:str((int(m[1])+int(m[2]))/2),expr)
        value = sum(float(n.strip()) for n in expr.split('+'))
        if value: lines.append(f'{new_name} = {value:g}')
    write(f'Content/Items/{item_id}.tres', '[gd_resource type="Resource" script_class="EquipmentDefinition" format=3]\n\n'
          '[ext_resource type="Script" path="res://Scripts/Equipment/EquipmentDefinition.cs" id="1"]\n'
          f'[ext_resource type="Texture2D" path="res://{icon}" id="2"]\n\n[resource]\nscript = ExtResource("1")\n'+'\n'.join(lines)+'\n')
catalog = read(ROOT / 'Content/Items/ItemCatalog.tres')
catalog = re.sub(r'^\[ext_resource[^\n]+id="new_[^"]+"\]\n', '', catalog, flags=re.M)
catalog = re.sub(r', ExtResource\("new_[^"]+"\)', '', catalog)
new_ext = ''.join(f'[ext_resource type="Resource" path="res://Content/Items/{id}.tres" id="new_{id}"]\n' for id in item_ids)
catalog = catalog.replace('[resource]',new_ext+'\n[resource]')
catalog = catalog.replace('ExtResource("14")])','ExtResource("14"), '+', '.join(f'ExtResource("new_{id}")' for id in item_ids)+'])')
catalog = re.sub(r' load_steps=\d+', '', catalog)
write('Content/Items/ItemCatalog.tres',catalog)

print('Migrated settings, continuous forest terrain and six equipment definitions.')

boss = sections(read(OLD / 'Scene/MonsterBlood/BossBlood.tscn'))
keep_names = ['BossBlood', 'BloodUnder', 'MonsterName', 'BloodValue']
nodes = ''.join(b for b in boss if b.startswith('[node ') and re.search(r'name="([^"]+)"',b)[1] in keep_names)
nodes = re.sub(r'^script = .*\n','',nodes,flags=re.M)
used_ext = set(re.findall(r'ExtResource\(\s*"([^"]+)"\s*\)',nodes))
ext = ''.join(b for b in boss if b.startswith('[ext_resource') and re.search(r'\bid="([^"]+)"',b)[1] in used_ext)
write('Scenes/UI/Level/BossBlood.tscn','[gd_scene format=3]\n\n'+clean(ext+nodes))

# 补齐之前切片中遗漏的三个常规技能动画，以及火魔斩的角色动作。
source = sections(read(ROOT / 'Reference/OriginalScenes/Hero/Role_1/Role1.tscn'))
resources = {re.search(r'\bid="([^"]+)"',b)[1]:b for b in source if b.startswith('[sub_resource')}
ext_resources = {re.search(r'\bid="([^"]+)"',b)[1]:b for b in source if b.startswith('[ext_resource')}
library = next(b for b in source if b.startswith('[sub_resource type="AnimationLibrary"'))
names = dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)',library))
def prefix_refs(text):
    text = re.sub(r'(SubResource|ExtResource)\(\s*"([^"]+)"\s*\)',lambda m:f'{m[1]}("extra_{m[2]}")',text)
    return re.sub(r' id="([^"]+)"',lambda m:f' id="extra_{m[1]}"',clean(text))
animations = []
for name in ['hytj','zz','hyjj','hmz__']:
    src = resources[names[name]]
    speed = 1.0
    tracks = re.split(r'(?=tracks/\d+/type =)',src)[1:]
    for t in tracks:
        if 'RolePlayer:speed_scale' in t:
            speed = float(re.search(r'"values": \[([\d.]+)\]',t)[1])
    length_match = re.search(r'^length = ([\d.]+)',src,re.M)
    length = (float(length_match[1]) if length_match else 1.0) / speed
    result = f'[sub_resource type="Animation" id="extra_anim_{name}"]\nresource_name = "{name}"\nlength = {length:g}\n'
    index = 0
    for t in tracks:
        if '/type = "method"' in t: continue
        path = re.search(r'NodePath\("([^"]+)"\)',t)[1]
        if not (path.startswith(('Action/RoleBody:','Action/RoleEquipment:','Action/SpecialEffect:')) or
                (path.startswith('base_damagebox/HitBox/HitBox:') and path.endswith(('shape','position')))):
            continue
        path = path.replace('Action/', 'Facing/Visual/').replace('base_damagebox/HitBox/HitBox:', 'Facing/HitBox/Shape:')
        t = re.sub(r'NodePath\("[^"]+"\)',f'NodePath("{path}")',t)
        t = re.sub(r'tracks/\d+/',f'tracks/{index}/',t)
        t = re.sub(r'"times": PackedFloat32Array\(([^)]+)\)',lambda m:'"times": PackedFloat32Array('+', '.join(f'{float(x)/speed:g}' for x in m[1].split(','))+')',t)
        result += prefix_refs(t).strip()+'\n'
        index += 1
    animations.append(result)
needed = set()
pending = [key.removeprefix('extra_') for a in animations for key in re.findall(r'SubResource\("([^"]+)"\)',a)]
while pending:
    key = pending.pop()
    if key in needed: continue
    needed.add(key)
    pending.extend(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', resources[key]))
new_sub = ''.join(prefix_refs(resources[key]) for key in resources if key in needed)
used_ext = set(key.removeprefix('extra_') for key in re.findall(r'ExtResource\("([^"]+)"\)',new_sub))
new_ext = ''.join(prefix_refs(ext_resources[key]) for key in ext_resources if key in used_ext)
player = read(ROOT / 'Scenes/Actors/Player.tscn')
# 重跑只替换本工具生成的资源。
player = ''.join(b for b in sections(player) if not (b.startswith(('[ext_resource','[sub_resource')) and 'id="extra_' in b.splitlines()[0]))
player = re.sub(r'^"(?:hytj|zz|hyjj|hmz__)": SubResource\("extra_[^"]+"\),?\n', '', player, flags=re.M)
player = player.replace('[sub_resource', new_ext+'\n[sub_resource',1)
player = player.replace('[sub_resource type="AnimationLibrary"',new_sub+'\n'+''.join(animations)+'\n[sub_resource type="AnimationLibrary"',1)
player = player.replace('_data = {', '_data = {\n'+''.join(f'"{name}": SubResource("extra_anim_{name}"),\n' for name in ['hytj','zz','hyjj','hmz__']),1)
player = re.sub(r' load_steps=\d+', '',player)
write('Scenes/Actors/Player.tscn',player)
print('Migrated original boss bar and remaining Wukong animation tracks.')

def extract_frames(source_path, selected, target):
    blocks = sections(read(OLD / source_path))
    frames = next(b for b in blocks if b.startswith('[sub_resource type="SpriteFrames"'))
    # SpriteFrames 的每个动画都是顶层字典，内部帧还有嵌套字典。
    # 按括号深度切分，避免在帧数组内部截断资源。
    data = frames[frames.index('animations = [') + len('animations = ['):]
    animations = []
    depth = 0
    start = None
    for i, ch in enumerate(data):
        if ch == '{':
            if depth == 0: start = i
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0 and start is not None:
                animation = data[start:i+1]
                name = re.search(r'"name": &?"([^"]+)"', animation)[1]
                if name in selected: animations.append(animation)
                start = None
    needed = set()
    resources = {re.search(r'\bid="([^"]+)"',b)[1]:b for b in blocks if b.startswith('[sub_resource')}
    pending = re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', ''.join(animations))
    while pending:
        key = pending.pop()
        if key in needed: continue
        needed.add(key)
        pending.extend(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)',resources[key]))
    sub = ''.join(b for key,b in resources.items() if key in needed)
    used_ext = set(re.findall(r'ExtResource\(\s*"([^"]+)"\s*\)',sub+''.join(animations)))
    ext = ''.join(b for b in blocks if b.startswith('[ext_resource') and re.search(r'\bid="([^"]+)"',b)[1] in used_ext)
    write(target, '[gd_resource type="SpriteFrames" format=3]\n\n'+clean(ext+sub)+'\n[resource]\nanimations = ['+',\n'.join(animations)+']\n')

extract_frames('Scene/Hero/RoleBullet_1.tscn', ['hyjj'], 'Content/Skills/Wukong/FireEyesFrames.tres')
extract_frames('Scene/Hero/Role_1/hmz_effect/SpecialEffectBullet.tscn', ['hmz_pro','hmz_1','hmz_flush','hmz_2'], 'Content/Skills/Wukong/FireSlashFrames.tres')
print('Extracted original fire-eyes and fire-slash sprite animations.')

# 大猩猩不能沿用小猴子的攻击框与帧号，否则 AI 在攻击距离内也打不到玩家。
boss_source = sections(read(ROOT / 'Reference/OriginalScenes/Monster/Monster_3.tscn'))
boss_resources = {re.search(r'\bid="([^"]+)"',b)[1]:b for b in boss_source if b.startswith('[sub_resource')}
boss_library = next(b for b in boss_source if b.startswith('[sub_resource type="AnimationLibrary"'))
boss_names = dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)', boss_library))
boss_animations = []
for name, key in boss_names.items():
    src = boss_resources[key]
    length_match = re.search(r'^length = ([\d.]+)',src,re.M)
    length = float(length_match[1]) if length_match else 1.0
    result = f'[sub_resource type="Animation" id="boss_anim_{name}"]\nresource_name = "{name}"\nlength = {length:g}\n'
    if name in ['wait','walk']: result += 'loop_mode = 1\n'
    index = 0
    for track in re.split(r'(?=tracks/\d+/type =)',src)[1:]:
        if '/type = "method"' in track: continue
        path = re.search(r'NodePath\("([^"]+)"\)',track)[1]
        if path.startswith('MonsterDir/mr_ani:'):
            path = path.replace('MonsterDir/mr_ani:', 'Facing/Visual/Body:')
        elif path == 'BaseDamageBox/HitBox/HitBox:disabled':
            path = 'Facing/HitBox:Active'
            track = re.sub(r'"values": \[([^\]]+)\]',lambda m:'"values": ['+m[1].replace('true','TEMP').replace('false','true').replace('TEMP','false')+']',track)
        elif path.startswith('BaseDamageBox/HitBox/HitBox:') and path.endswith(('shape','position')):
            path = path.replace('BaseDamageBox/HitBox/HitBox:', 'Facing/HitBox/Shape:')
        else: continue
        track = re.sub(r'NodePath\("[^"]+"\)',f'NodePath("{path}")',track)
        track = re.sub(r'tracks/\d+/',f'tracks/{index}/',track)
        result += clean(track).strip()+'\n'
        index += 1
    boss_animations.append(result)
needed = set(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', ''.join(boss_animations)))
boss_shapes = ''.join(clean(boss_resources[key]) for key in needed)
write('Content/Monsters/ForestBossAnimations.tres', '[gd_resource type="AnimationLibrary" format=3]\n\n'+boss_shapes+''.join(boss_animations)+'\n[resource]\n_data = {\n'+',\n'.join(f'"{name}": SubResource("boss_anim_{name}")' for name in boss_names)+'\n}\n')
print('Migrated gorilla attack shapes and complete animation library.')
