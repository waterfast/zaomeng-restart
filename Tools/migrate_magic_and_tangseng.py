"""本批一次性迁移；核对旧场景后生成显式资源，禁止作为运行时注册器。"""
from pathlib import Path
import ast
import re
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / '造梦八荒-(4.1)'
if (ROOT / 'Content/Items/tsgj.tres').exists():
    raise SystemExit('本批内容已迁入；后续直接编辑资源，禁止重复生成覆盖策划调整。')

def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding='utf-8')

def ext(kind, path, key):
    return f'[ext_resource type="{kind}" path="res://{path}" id="{key}"]\n'

def clean(text):
    return re.sub(r' uid="[^"]+"', '', text).replace('res://Art/', 'res://Assets/Art/')

# 复用此前已核对的帧提取函数，不执行原迁移脚本以免覆盖已调整内容。
tree = ast.parse((ROOT / 'Tools/register_skill_catalogs.py').read_text(encoding='utf-8'))
fn = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'extract_frames')
exec(compile(ast.Module(body=[fn], type_ignores=[]), '<frame extraction>', 'exec'))

def burst(path, frames, animation, positions, times, scales, radius, windows, multiplier, maximum=0, growth=0, flat=0, kind=1):
    count = (ROOT / frames).read_text(encoding='utf-8').count('"texture":')
    print(animation, 'frames=', count, 'windows=', windows)
    assert all(end <= count for start, end in windows)
    headers = ext('Script', 'Scripts/Combat/Skills/BurstAttackDefinition.cs', 'burst')
    headers += ext('Script', 'Scripts/Combat/HitDefinition.cs', 'hit') + ext('Script', 'Scripts/Combat/HitEvent.cs', 'window')
    headers += ext('SpriteFrames', frames, 'frames')
    sub = f'[sub_resource type="Resource" id="hit"]\nscript = ExtResource("hit")\nAttackMultiplier = {multiplier}\nAttackMultiplierMax = {maximum}\nMultiplierPerLevel = {growth}\nFlatDamage = {flat}\nDamageType = {kind}\nKnockback = Vector2(30, -30)\n'
    for i, (start, end) in enumerate(windows):
        sub += f'[sub_resource type="Resource" id="w{i}"]\nscript = ExtResource("window")\nStartFrame = {start}\nEndFrame = {end}\nHit = SubResource("hit")\n'
    props = f'Frames = ExtResource("frames")\nAnimation = &"{animation}"\nRadius = {radius}\n'
    props += 'Positions = PackedVector2Array(' + ', '.join(f'{x}, {y}' for x,y in positions) + ')\n'
    props += 'ReleaseTimes = PackedFloat32Array(' + ', '.join(map(str, times)) + ')\n'
    props += 'Scales = PackedFloat32Array(' + ', '.join(map(str, scales)) + ')\n'
    props += 'Hits = Array[ExtResource("window")]([' + ', '.join(f'SubResource("w{i}")' for i in range(len(windows))) + '])\n'
    write(path, '[gd_resource type="Resource" script_class="BurstAttackDefinition" format=3]\n' + headers + sub + '\n[resource]\nscript = ExtResource("burst")\n' + props)

for name, animation in [('dshl','CucurFire_2'),('tsgj','SwordBullet'),('zjhl','PurpleGlodCucurbitBullet')]:
    extract_frames('Scene/MagicWeapon/HitBullet.tscn', animation, f'Content/MagicWeapons/{name}_frames.tres', trim=True)
for name, animation in [('xbz','Role2Bullet3'),('jhsj','Role2Bullet6')]:
    extract_frames('Scene/Hero/RoleBullet.tscn', animation, f'Content/Skills/Tangseng/{name}_frames.tres', trim=True)

# 窗口取实际帧数范围，视觉播放一次；多段伤害分别结算。
burst('Content/MagicWeapons/dshl_burst.tres','Content/MagicWeapons/dshl_frames.tres','CucurFire_2',[(0,0),(-230,0),(230,0)],[0.2]*3,[1]*3,65,[(3,6),(7,10),(11,14)],.09,.12,flat=90,kind=0)
burst('Content/MagicWeapons/tsgj_burst.tres','Content/MagicWeapons/tsgj_frames.tres','SwordBullet',[(0,0),(-90,0),(90,0),(-180,0),(180,0),(-270,0),(270,0)],[.2,.2,.2,.3,.3,.4,.4],[1]*7,42,[(2,5),(6,9)],.15,.2,flat=200)
burst('Content/MagicWeapons/zjhl_burst.tres','Content/MagicWeapons/zjhl_frames.tres','PurpleGlodCucurbitBullet',[(110,-30)],[.2],[1],110,[(2,5),(6,9),(10,13)],.13,.17,kind=0)
burst('Content/Skills/Tangseng/xbz_burst.tres','Content/Skills/Tangseng/xbz_frames.tres','Role2Bullet3',[(80,-15),(175,-20),(275,-25)],[.5,.7,.9],[1,1.3,1.5],65,[(1,6)],.9,growth=.075)
burst('Content/Skills/Tangseng/jhsj_burst.tres','Content/Skills/Tangseng/jhsj_frames.tres','Role2Bullet6',[(0,-10)],[.6],[1],180,[(1,4),(5,8),(9,12)],1,growth=.1)

for name, mana, cooldown in [('dshl',50,6),('tsgj',40,15),('zjhl',62,17)]:
    headers = ext('Script','Scripts/Equipment/MagicWeaponAbility.cs','ability') + ext('Script','Scripts/Combat/SkillDefinition.cs','action') + ext('Script','Scripts/Combat/Skills/SkillGrowth.cs','growth') + ext('Resource',f'Content/MagicWeapons/{name}_burst.tres','burst')
    write(f'Content/MagicWeapons/{name}.tres','[gd_resource type="Resource" script_class="MagicWeaponAbility" format=3]\n'+headers+f'\n[sub_resource type="Resource" id="growth"]\nscript = ExtResource("growth")\nInitialManaCost = {mana}\n[sub_resource type="Resource" id="action"]\nscript = ExtResource("action")\nId = "magic_{name}"\nGrowth = SubResource("growth")\nCooldownSeconds = {cooldown}\n\n[resource]\nscript = ExtResource("ability")\nAction = SubResource("action")\nBurst = ExtResource("burst")\n')

# 对照旧 Main_Backpack 的物品图引用，而非技能图/详情展示图。
old_bag = (OLD/'Scene/BackPack/Main_Backpack.tscn').read_text(encoding='utf-8')
for name,title,skill,description,rarity,quality,color,price in [
    ('tsgj','天煞古剑','万剑归宗','召唤七道落剑，造成多段魔法伤害。基础版暂不附加冰冻。',2,'精良','0, 0.54, 1',80),
    ('zjhl','紫金葫芦','伏妖神火','向前方释放火焰，造成多段物理伤害。基础版暂不附加灼烧。',4,'邪灵','0.4, 0.4, 0.4',320)]:
    bag_script=(OLD/'Script/BackPack/Main_Backpack.gd').read_text(encoding='utf-8')
    assert 'res://Art/BackPack/AllItems/' in bag_script and 'str(name_)' in bag_script
    assert (OLD/f'Art/BackPack/AllItems/{name}.png.import').exists()
    for target in [f'Assets/Art/BackPack/AllItems/{name}.png']:
        if not (ROOT/target).exists():
            source=ROOT.parent/'zmbh-rebuild/assets'/target.removeprefix('Assets/')
            if not source.exists(): source=ROOT.parent/'zmbh-rebuild/assets'/target
            assert source.exists(), source
            (ROOT/target).parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(source,ROOT/target)
    write(f'Content/Items/Presentation/{name}.tres','[gd_resource type="Resource" script_class="MagicWeaponPresentation" format=3]\n'+ext('Script','Scripts/Equipment/MagicWeaponPresentation.cs','script')+ext('Texture2D',f'Assets/Art/MagicWeapon/Skill_Icon/{name}.png','icon')+f'\n[resource]\nscript = ExtResource("script")\nAnimation = &"{name}"\nSkillName = "{skill}"\nSkillDescription = "{description}"\nSkillIcon = ExtResource("icon")\n')
    write(f'Content/Items/{name}.tres','[gd_resource type="Resource" script_class="EquipmentDefinition" format=3]\n'+ext('Script','Scripts/Equipment/EquipmentDefinition.cs','script')+ext('Texture2D',f'Assets/Art/BackPack/AllItems/{name}.png','icon')+ext('Texture2D',f'Assets/Art/BackPack/AllItems/{name}.png','ground')+ext('Resource',f'Content/Items/Presentation/{name}.tres','presentation')+ext('Resource',f'Content/MagicWeapons/{name}.tres','ability')+f'\n[resource]\nscript = ExtResource("script")\nId = "{name}"\nDisplayName = "{title}"\nDescriptionKey = "ITEM_{name.upper()}_DESCRIPTION"\nIcon = ExtResource("icon")\nGroundIcon = ExtResource("ground")\nSlot = 6\nRarity = {rarity}\nQuality = "{quality}"\nQualityColor = Color({color}, 1)\nOwnerCaption = "通用"\nSellPrice = {price}\nMagicWeaponPresentation = ExtResource("presentation")\nMagicWeaponAbility = ExtResource("ability")\n'+('CriticalRating = 5\nManaRegenerationBonus = 3\n' if name=='zjhl' else ''))

p=ROOT/'Content/Items/dshl.tres';s=p.read_text(encoding='utf-8');s=s.replace('[resource]',ext('Resource','Content/MagicWeapons/dshl.tres','ability')+'\n[resource]');s+='MagicWeaponAbility = ExtResource("ability")\n';p.write_text(s,encoding='utf-8')
p=ROOT/'Content/Items/Presentation/dshl.tres';s=p.read_text(encoding='utf-8').replace('（并令其灼烧）','。基础版暂不附加灼烧');p.write_text(s,encoding='utf-8')
p=ROOT/'Content/Items/ItemCatalog.tres';s=p.read_text(encoding='utf-8');s=s.replace('[resource]',ext('Resource','Content/Items/tsgj.tres','magic_tsgj')+ext('Resource','Content/Items/zjhl.tres','magic_zjhl')+'\n[resource]');s=s.replace('Definitions = Array[ExtResource("definition_script")]([','Definitions = Array[ExtResource("definition_script")]([ExtResource("magic_tsgj"), ExtResource("magic_zjhl"), ');p.write_text(s,encoding='utf-8')

# 只追加新动作，保留当前唐僧动画已有的改动。
source=(OLD/'Scene/Hero/Role_2/Role_2.tscn').read_text(encoding='utf-8')
blocks=re.split(r'(?=^\[)',source,flags=re.M)
resources={re.search(r'\bid="([^"]+)"',b)[1]:b for b in blocks if b.startswith('[sub_resource')}
library=next(b for b in blocks if b.startswith('[sub_resource type="AnimationLibrary"'))
names=dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)',library))
new_animations=''
for name in ['xbz','jhsj']:
    original=resources[names[name]]
    length=re.search(r'^length = ([\d.]+)',original,re.M)
    result=f'[sub_resource type="Animation" id="anim_{name}"]\nlength = {length[1] if length else "1.0"}\n'
    i=0
    for track in re.split(r'(?=tracks/\d+/type =)',original)[1:]:
        path=re.search(r'NodePath\("([^"]+)"\)',track)[1]
        if '/type = "method"' in track or not path.startswith(('Action/RoleBody:','Action/RoleEquipment:')): continue
        track=track.replace(path,path.replace('Action/','Facing/Visual/'))
        result+=clean(re.sub(r'tracks/\d+/',f'tracks/{i}/',track));i+=1
    result+=f'tracks/{i}/type = "value"\ntracks/{i}/path = NodePath("Facing/HitBox:Active")\ntracks/{i}/keys = {{"times": PackedFloat32Array(0), "transitions": PackedFloat32Array(1), "update": 1, "values": [false]}}\n'
    print(name,'actor duration=',length[1] if length else 1)
    new_animations+=result
p=ROOT/'Content/Skills/Tangseng/Animations.tres';s=p.read_text(encoding='utf-8');s=s.replace('[resource]',new_animations+'\n[resource]');s=s.replace('"tjgl": SubResource("anim_tjgl")','"tjgl": SubResource("anim_tjgl"),\n"xbz": SubResource("anim_xbz"),\n"jhsj": SubResource("anim_jhsj")');p.write_text(s,encoding='utf-8')

for name,title,mana,quadratic,cd,description in [('xbz','玄冰阵',25,2,1.2,'向前方依次释放三个逐渐扩大的冰阵，造成魔法伤害。'),('jhsj','九环圣经',60,3,2.4,'释放近身圣经，对周围敌人造成多段魔法伤害。')]:
    write(f'Content/Skills/Growth/Tangseng/{name}.tres','[gd_resource type="Resource" script_class="SkillGrowth" format=3]\n'+ext('Script','Scripts/Combat/Skills/SkillGrowth.cs','script')+f'\n[resource]\nscript = ExtResource("script")\nInitialLearningCost = 100\nLearningCostPerLevel = 500\nInitialManaCost = {mana}\nManaLinearGrowth = 3\nManaQuadraticGrowth = {quadratic}\n')
    write(f'Content/Skills/Behaviors/{name}.tscn','[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/BurstSkillBehavior.cs','script')+ext('Resource',f'Content/Skills/Tangseng/{name}_burst.tres','burst')+f'\n[node name="{name}" type="Node"]\nscript = ExtResource("script")\nBurst = ExtResource("burst")\n')
    write(f'Content/Skills/Tangseng/{name}.tres','[gd_resource type="Resource" script_class="SkillDefinition" format=3]\n'+ext('Script','Scripts/Combat/SkillDefinition.cs','script')+ext('Resource',f'Content/Skills/Growth/Tangseng/{name}.tres','growth')+ext('PackedScene',f'Content/Skills/Behaviors/{name}.tscn','behavior')+f'\n[resource]\nscript = ExtResource("script")\nId = "{name}"\nDisplayName = "{title}"\nAnimation = &"{name}"\nFramesPerSecond = 10\nCooldownSeconds = {cd}\nGrowth = ExtResource("growth")\nBehaviorScene = ExtResource("behavior")\n')
    p=ROOT/'Content/Skills/Tangseng/Catalog.tres';s=p.read_text(encoding='utf-8');s=s.replace('[sub_resource type="Resource" id="entry_blb"]',ext('Resource',f'Content/Skills/Tangseng/{name}.tres',f'action_{name}')+ext('Texture2D',f'Assets/Art/Skill/SkillIcon/{name}.png',f'icon_{name}')+'\n[sub_resource type="Resource" id="entry_blb"]',1);s=s.replace('[resource]',f'[sub_resource type="Resource" id="entry_{name}"]\nscript = ExtResource("entry")\nId = "{name}"\nDisplayName = "{title}"\nDescription = "{description}分身联动与水精通尚未接入。"\nIcon = ExtResource("icon_{name}")\nMaximumLevel = 10\nAction = ExtResource("action_{name}")\n\n[resource]');s=s.replace('Skills = Array[ExtResource("entry")]([','Skills = Array[ExtResource("entry")]([SubResource("entry_'+name+'"), ');s=s.replace('试玩：冰龙波、天降甘露和三个通用被动，其余技能尚未迁入。','试玩：冰龙波、玄冰阵、天降甘露、九环圣经和三个通用被动。');p.write_text(s,encoding='utf-8')
