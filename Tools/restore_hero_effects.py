"""一次性按旧场景引用提取悟空、唐僧特效；运行时只读取显式资源。

后续编辑生成的资源，不重复运行覆盖策划调整。
"""
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / '造梦八荒-(4.1)'
DEST = 'Content/Skills/Tangseng/Effects'


def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding='utf-8')


def ext(kind, path, key):
    return f'[ext_resource type="{kind}" path="res://{path}" id="{key}"]\n'


def clean(text):
    text = re.sub(r' uid="[^"]+"| unique_id=\d+', '', text)
    text = re.sub(r'^script = null\n?', '', text, flags=re.M)
    return text.replace('res://Art/', 'res://Assets/Art/').replace('res://Font/', 'res://Assets/Font/')


def resources(path):
    blocks = re.split(r'(?=^\[)', (OLD / path).read_text(encoding='utf-8'), flags=re.M)
    subs = {re.search(r'\bid="([^"]+)"', b)[1]: b for b in blocks if b.startswith('[sub_resource')}
    external = {re.search(r'\bid="([^"]+)"', b)[1]: b for b in blocks if b.startswith('[ext_resource')}
    library = next(b for b in blocks if b.startswith('[sub_resource type="AnimationLibrary"'))
    names = dict(re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"\s*\)', library))
    return blocks, subs, external, names


def frame_animation(block, names):
    data = block[block.index('animations = [') + len('animations = ['):]
    depth, start, result = 0, 0, []
    for i, ch in enumerate(data):
        if ch == '{':
            if depth == 0: start = i
            depth += 1
        elif ch == '}':
            depth -= 1
            if depth == 0:
                candidate = data[start:i + 1]
                if any(f'"name": &"{name}"' in candidate for name in names): result.append(candidate)
    return block.split('animations = [')[0] + 'animations = [' + ',\n'.join(result) + ']\n'


def recover_assets(text):
    for path in re.findall(r'path="res://(Assets/[^\"]+)"', text):
        target = ROOT / path
        if target.exists(): continue
        original = path.removeprefix('Assets/')
        candidates = [OLD / original, ROOT.parent/'zmbh-rebuild/assets'/original,
                      ROOT.parent/'zmbh-rebuild/assets'/path]
        source = next((p for p in candidates if p.exists()), None)
        if source is None: raise FileNotFoundError(path)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)


def effect(source, animation, target, lifetime, hit=None, hit_times=(), radius=100, extra=''):
    blocks, subs, external, names = resources(source)
    original = subs[names[animation]]
    # 独立场景的 AnimationPlayer 统一以根为轨道原点，仅保留美术属性。
    tracks, sprite_names = [], {}
    for track in re.split(r'(?=tracks/\d+/type =)', original)[1:]:
        path = re.search(r'NodePath\("([^"]+)"\)', track)[1]
        if '/type = "method"' in track or path.split(':')[0] not in ['BulletPlayer', 'SpecialAN', 'smb']:
            continue
        node, prop = path.split(':', 1)
        if prop == 'animation': sprite_names[node] = re.findall(r'"([^\"]+)"', track.split('"values": ')[1])
        track = track.replace(path, 'Visual/' + path)
        tracks.append(re.sub(r'tracks/\d+/', f'tracks/{len(tracks)}/', track))
    nodes = []
    for node, animations in sprite_names.items():
        b = next(b for b in blocks if b.startswith(f'[node name="{node}" '))
        frame_id = re.search(r'sprite_frames = SubResource\(\s*"([^"]+)"', b)[1]
        subs[frame_id] = frame_animation(subs[frame_id], animations)
        b = re.sub(r'parent="[^"]+"', 'parent="Visual"', b)
        b = re.sub(r'^animation = &"[^"]+"', f'animation = &"{animations[0]}"', b, flags=re.M)
        b = re.sub(r'^metadata/[^\n]+\n?', '', b, flags=re.M)
        nodes.append(b)
    playback = 1.5 if animation == 'Role2Bullet5' else 1
    visual_tracks = ''.join(tracks)
    if playback != 1:
        visual_tracks = re.sub(r'"times": PackedFloat32Array\(([^)]*)\)',
                              lambda m: '"times": PackedFloat32Array(' + ', '.join(f'{float(v)/playback:.7g}' for v in m[1].split(',') if v.strip()) + ')', visual_tracks)
    anim = '[sub_resource type="Animation" id="clip"]\nlength = ' + str(lifetime) + '\n' + visual_tracks
    needed, pending = set(), re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', anim + ''.join(nodes))
    while pending:
        key = pending.pop()
        if key in needed: continue
        needed.add(key)
        pending.extend(re.findall(r'SubResource\(\s*"([^"]+)"\s*\)', subs[key]))
    sub = ''.join(subs[key] for key in subs if key in needed)
    ids = set(re.findall(r'ExtResource\(\s*"([^"]+)"\s*\)', sub + anim + ''.join(nodes)))
    headers = ''.join(external[key] for key in external if key in ids)
    headers += ext('Script', 'Scripts/Combat/Skills/AnimatedSkillEffect.cs', 'runtime')
    damage = ''
    if hit:
        headers += ext('Script', 'Scripts/Combat/HitDefinition.cs', 'hit')
        damage = '[sub_resource type="Resource" id="damage"]\nscript = ExtResource("hit")\n' + hit
    result = '[gd_scene format=3]\n' + headers + sub + damage + anim
    result += '[sub_resource type="AnimationLibrary" id="library"]\n_data = {"effect": SubResource("clip")}\n'
    result += '[node name="Effect" type="Node2D"]\nscript = ExtResource("runtime")\nLifetime = ' + str(lifetime) + '\n' + extra
    if hit:
        result += 'Hit = SubResource("damage")\nRadius = ' + str(radius) + '\nHitTimes = PackedFloat32Array(' + ', '.join(map(str, hit_times)) + ')\n'
    result += '[node name="Visual" type="Node2D" parent="."]\n' + ''.join(nodes)
    result += '[node name="AnimationPlayer" type="AnimationPlayer" parent="."]\nlibraries/ = SubResource("library")\n'
    result = clean(result)
    recover_assets(result)
    write(target, result)


def sequence(name, cues):
    headers = ext('Script', 'Scripts/Combat/Skills/SkillEffectSequenceBehavior.cs', 'runtime')
    headers += ext('Script', 'Scripts/Combat/Skills/SkillEffectCue.cs', 'cue')
    subs = ''
    for i, (time, scene, x, y, scale, follow, mirror, ends) in enumerate(cues):
        headers += ext('PackedScene', f'{DEST}/{scene}.tscn', f'scene{i}')
        subs += f'[sub_resource type="Resource" id="cue{i}"]\nscript = ExtResource("cue")\nTime = {time}\nScene = ExtResource("scene{i}")\nOffset = Vector2({x}, {y})\nScale = Vector2({scale}, {scale})\nFollowActor = {str(follow).lower()}\nMirror = {str(mirror).lower()}\nEndsWithCast = {str(ends).lower()}\n'
    write(f'Content/Skills/Behaviors/{name}.tscn', '[gd_scene format=3]\n'+headers+subs+f'[node name="{name}" type="Node"]\nscript = ExtResource("runtime")\nCues = Array[ExtResource("cue")](['+', '.join(f'SubResource("cue{i}")' for i in range(len(cues)))+'])\n')


if __name__ == '__main__':
    if (ROOT/f'{DEST}/HolyBall.tscn').exists(): raise SystemExit('已迁入：请直接编辑资源。')
    magic = 'DamageType = 1\nKnockback = Vector2(90, -60)\n'
    effect('Scene/Hero/RoleBullet.tscn', 'Role2Bullet3', f'{DEST}/IceFormation.tscn', .5,
           magic+'AttackMultiplier = 0.9\nMultiplierPerLevel = 0.075\n', [0,.2,.4],65)
    effect('Scene/Hero/RoleBullet.tscn', 'Role2Bullet6', f'{DEST}/ScriptureRing.tscn',1.9,
           magic+'AttackMultiplier = 1\nMultiplierPerLevel = 0.1\n', [i/10 for i in range(0,19,2)],180)
    effect('Scene/Hero/RoleSpecialEffect.tscn','jhsj',f'{DEST}/ScriptureAura.tscn',2.1)
    effect('Scene/Hero/RoleSpecialEffect.tscn','myhc',f'{DEST}/HealingAura.tscn',8,extra='HealRatio = 0.045\n')
    effect('Scene/Hero/RoleSpecialEffect.tscn','jgz',f'{DEST}/GoldenCurse.tscn',.7)
    effect('Scene/Hero/Role_2/smb_effect.tscn','smb',f'{DEST}/WaterCharge.tscn',1.5,extra='MoveSpeed = 420\n')
    effect('Scene/Hero/RoleBullet.tscn','Role2Bullet4',f'{DEST}/WaterExplosion.tscn',1.5,
           magic+'AttackMultiplier = 7.6\nMultiplierPerLevel = 0.6\n',[.4],180,extra='VerticalRadius = 250\n')
    effect('Scene/Hero/RoleBullet.tscn','Role2Bullet5',f'{DEST}/HolyBall.tscn',4,
           magic+'AttackMultiplier = 0.19\nMultiplierPerLevel = 0.02\n',[i/15 for i in range(1,60,4)],90,extra='MoveSpeed = 120\n')
    # 分身只迁入美术和移动；不接旧分身的 GDScript 或 AI。
    blocks, subs, external, names = resources('Scene/Hero/Role_2/Role_2.tscn')
    run = subs[names['run']]
    run = re.sub(r'\[sub_resource[^\n]+\n', '[sub_resource type="Animation" id="clip"]\n', run, count=1)
    tracks=[]
    for t in re.split(r'(?=tracks/\d+/type =)',run)[1:]:
        if 'Action/RoleBody:' in t or 'Action/RoleEquipment:' in t:
            tracks.append(re.sub(r'tracks/\d+/',f'tracks/{len(tracks)}/',t.replace('Action/','Visual/')))
    clone='[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/AnimatedSkillEffect.cs','runtime')
    for key, node in [('107','RoleBody'),('108','RoleEquipment')]:
        load=re.search(r'load_path = "res://([^"]+)"',subs[key])[1]
        imported=next(p for p in (OLD/'Art/HeroPicture').rglob('*.import') if load in p.read_text(encoding='utf-8'))
        clone+=ext('Texture2D','Assets/'+imported.relative_to(OLD).as_posix().removesuffix('.import'),node)
    clone+='[sub_resource type="Animation" id="clip"]\nlength = 1.2\nloop_mode = 1\n'+''.join(tracks)
    clone+='[sub_resource type="AnimationLibrary" id="library"]\n_data = {"effect": SubResource("clip")}\n[node name="WaterShadow" type="Node2D"]\nscript = ExtResource("runtime")\nLifetime = 3\nMoveSpeed = 360\nmodulate = Color(0.3, 0.9, 1, 0.6)\n[node name="Visual" type="Node2D" parent="."]\nposition = Vector2(0, -41)\n'
    for node in ['RoleBody','RoleEquipment']:
        clone+=f'[node name="{node}" type="Sprite2D" parent="Visual"]\ntexture = ExtResource("{node}")\nhframes = 6\nvframes = 13\noffset = Vector2(-15, -15)\n'
    clone+='[node name="AnimationPlayer" type="AnimationPlayer" parent="."]\nlibraries/ = SubResource("library")\n'
    write(f'{DEST}/WaterShadow.tscn',clean(clone))
    sequence('xbz',[(.5,'IceFormation',80,-15,1,False,True,False),(.7,'IceFormation',175,-20,1.3,False,True,False),(.9,'IceFormation',275,-25,1.5,False,True,False)])
    sequence('jhsj',[(0,'ScriptureAura',20,-15,1,True,True,True),(.3,'ScriptureRing',0,-10,1,True,True,False)])
    sequence('shy',[(.1,'WaterShadow',0,0,1,False,True,False)])
    sequence('myhc',[(.2,'HealingAura',0,-30,1,True,False,False)])
    sequence('jgz',[(.2,'GoldenCurse',210,0,1,False,False,False)])
    sequence('sgq',[(.7,'HolyBall',35,-20,2,False,True,False)])
    sequence('smb',[(.1,'WaterCharge',100,25,1,False,True,False),(1.1,'WaterExplosion',520,-155,1,False,True,False)])
    # 追加新角色动作；保留当前已有动作，不重生成。
    target=ROOT/'Content/Skills/Tangseng/Animations.tres'; data=target.read_text(encoding='utf-8')
    new=''
    for name in ['shy','myhc','jgz','sgq','smb_1','smb_2']:
        original=subs[names[name]]
        length=re.search(r'^length = ([\d.]+)',original,re.M)
        new+=f'[sub_resource type="Animation" id="anim_{name}"]\nlength = {length[1] if length else 1}\n'
        index=0
        for track in re.split(r'(?=tracks/\d+/type =)',original)[1:]:
            if '/type = "method"' in track or not any(f'Action/{n}:' in track for n in ['RoleBody','RoleEquipment']): continue
            new+=clean(re.sub(r'tracks/\d+/',f'tracks/{index}/',track.replace('Action/','Facing/Visual/')));index+=1
        new+=f'tracks/{index}/type = "value"\ntracks/{index}/path = NodePath("Facing/HitBox:Active")\ntracks/{index}/keys = {{"times": PackedFloat32Array(0), "transitions": PackedFloat32Array(1), "update": 1, "values": [false]}}\n'
    data=data.replace('[resource]',new+'[resource]')
    for name in ['shy','myhc','jgz','sgq','smb_1','smb_2']:
        data=data.replace('_data = {','_data = {\n"'+name+'": SubResource("anim_'+name+'"),',1)
    # 水魔爆的预备和爆发在同一演示动作中顺序播放，二次按键控制另属玩法。
    # 当前 smb_1 动作足够容纳两阶段释放；仅延长保持帧，不伪造原版身体动作。
    data=re.sub(r'(\[sub_resource type="Animation" id="anim_smb_1"\]\n)length = [^\n]+',r'\g<1>length = 1.8',data)
    target.write_text(data,encoding='utf-8')
    titles={'shy':'水幻影','smb':'水魔爆','sgq':'圣光球','myhc':'沐浴回春','jgz':'紧箍咒'}
    descriptions={'shy':'放出向前移动的水幻影。当前仅还原表现，分身技能联动尚未接入。','smb':'释放水流预备特效与水爆。当前自动接续爆发，二次按键控制尚未接入。','sgq':'释放前进的圣光球及环绕光刃，造成多段魔法伤害。','myhc':'生成随身回春光环，持续恢复生命。','jgz':'在前方生成原版金色紧箍光效。吸附控制尚未接入。'}
    catalog=ROOT/'Content/Skills/Tangseng/Catalog.tres';cat=catalog.read_text(encoding='utf-8')
    for name in titles:
        mana={'shy':30,'smb':60,'sgq':40,'myhc':35,'jgz':40}[name]
        cooldown={'shy':2.4,'smb':1.2,'sgq':1.2,'myhc':4.8,'jgz':2.4}[name]
        write(f'Content/Skills/Growth/Tangseng/{name}.tres','[gd_resource type="Resource" script_class="SkillGrowth" format=3]\n'+ext('Script','Scripts/Combat/Skills/SkillGrowth.cs','runtime')+'[resource]\nscript = ExtResource("runtime")\nInitialLearningCost = 100\nLearningCostPerLevel = 500\nInitialManaCost = '+str(mana)+'\nManaLinearGrowth = 3\nManaQuadraticGrowth = '+str(2 if name in ['shy','myhc'] else 3)+'\n')
        write(f'Content/Skills/Tangseng/{name}.tres','[gd_resource type="Resource" script_class="SkillDefinition" format=3]\n'+ext('Script','Scripts/Combat/SkillDefinition.cs','runtime')+ext('Resource',f'Content/Skills/Growth/Tangseng/{name}.tres','growth')+ext('PackedScene',f'Content/Skills/Behaviors/{name}.tscn','behavior')+f'[resource]\nscript = ExtResource("runtime")\nId = "{name}"\nDisplayName = "{titles[name]}"\nAnimation = &"'+('smb_1' if name=='smb' else name)+f'"\nFramesPerSecond = 10\nCooldownSeconds = {cooldown}\nGrowth = ExtResource("growth")\nBehaviorScene = ExtResource("behavior")\n')
        h=ext('Resource',f'Content/Skills/Tangseng/{name}.tres',f'action_{name}')+ext('Texture2D',f'Assets/Art/Skill/SkillIcon/{name}.png',f'icon_{name}')
        cat=cat.replace('[sub_resource',h+'\n[sub_resource',1)
        entry=f'[sub_resource type="Resource" id="entry_{name}"]\nscript = ExtResource("entry")\nId = "{name}"\nDisplayName = "{titles[name]}"\nDescription = "{descriptions[name]}"\nIcon = ExtResource("icon_{name}")\nMaximumLevel = 10\nAction = ExtResource("action_{name}")\n'
        cat=cat.replace('[resource]',entry+'\n[resource]')
        cat=cat.replace('ExtResource("passive_kb")',f'SubResource("entry_{name}"), ExtResource("passive_kb")')
    cat=cat.replace('试玩：冰龙波、玄冰阵、天降甘露、九环圣经和三个通用被动。','九个主动技能表现与三个通用被动；部分控制及分身玩法尚未接入。')
    catalog.write_text(cat,encoding='utf-8')
    # 悟空重复的动画字典键会覆盖表现配置：保留当前正式动作，删除未被引用的过渡动画。
    p=ROOT/'Scenes/Actors/Player.tscn';s=p.read_text(encoding='utf-8')
    s=re.sub(r'^"(?:hytj|zz|hyjj|hmz__)": SubResource\("extra_anim_[^\"]+"\),\n','',s,flags=re.M)
    blocks=re.split(r'(?=^\[)',s,flags=re.M)
    for i,b in enumerate(blocks):
        if b.startswith('[sub_resource type="Animation" id="extra_anim_'): blocks[i]='';continue
        if b.startswith('[sub_resource type="Animation" id="anim_zz"'):
            b=re.sub(r'(path = NodePath\("Facing/Visual/SpecialEffect:visible"\)[\s\S]*?"values": )\[false\]',r'\1[true]',b)
        blocks[i]=b
    p.write_text(''.join(blocks),encoding='utf-8')
    print('已提取唐僧全部主动特效，并修正悟空重斩可见性。')
