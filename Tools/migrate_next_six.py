"""一次性提取第四至第九关。引用以旧场景为依据，生成后由策划直接编辑。"""
from pathlib import Path
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / '造梦八荒-(4.1)'
REBUILD = ROOT.parent / 'zmbh-rebuild/assets'

def read(path):
    return path.read_text(encoding='utf-8')

def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding='utf-8')

def ext(kind, path, key):
    return f'[ext_resource type="{kind}" path="res://{path}" id="{key}"]\n'

def clean(block):
    block = re.sub(r' uid="[^"]+"', '', block)
    block = block.replace('res://Art/', 'res://Assets/Art/')
    return re.sub(r'^script = null\n', '', block, flags=re.M)

def copy_texture(block):
    path = re.search(r'path="res://([^"]+)"', block)[1]
    relative = path.removeprefix('Art/')
    target = ROOT / 'Assets/Art' / relative
    if target.exists():
        return
    source = next((p for p in [OLD / path, REBUILD / path] if p.exists()), None)
    if source is None:
        raise FileNotFoundError(f'旧场景引用缺失：{path}')
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)

def field(script, name, default=0):
    value = re.search(rf'self\.{name}\s*=\s*([^\n]+)', script)
    return value[1].split('#')[0].strip() if value else str(default)

def array(items):
    return 'Array[Resource]([' + ', '.join(items) + '])'

def migrate_monster(number):
    script = read(OLD / f'Script/Monster/Monster_{number}.gd')
    if number == 17:
        # 同一旧脚本按参数分小怪/首领，浅岸必须引用首领独立配置。
        script = script[:script.index('\n\telse:')] + script[script.index('\nfunc _ready'):]
    source = OLD / f'Scene/Monster/Monster_{number}.tscn'
    blocks = re.split(r'(?=^\[)', read(source), flags=re.M)
    resources = {re.search(r'id="([^"]+)"', b)[1]: b for b in blocks if b.startswith('[sub_resource')}
    textures = [b for b in blocks if b.startswith('[ext_resource type="Texture2D"')]
    for texture in textures:
        copy_texture(texture)
    body = next(b for b in blocks if b.startswith('[node name="mr_ani"'))
    frame_id = re.search(r'sprite_frames = SubResource\(\s*"([^"]+)"', body)[1]
    frame_body = clean(resources[frame_id]).split('\n', 1)[1]
    atlases = [b for b in resources.values() if b.startswith('[sub_resource type="AtlasTexture"')]
    write(f'Content/Monsters/Human/monster_{number}_frames.tres', '[gd_resource type="SpriteFrames" format=3]\n' + ''.join(map(clean, textures + atlases)) + '\n[resource]\n' + frame_body)
    library = next(b for b in resources.values() if b.startswith('[sub_resource type="AnimationLibrary"'))
    entries = re.findall(r'"([^"]+)": SubResource\(\s*"([^"]+)"', library)
    animations, lengths, original = [], {}, {}
    for name, resource_id in entries:
        if name == 'RESET':
            continue
        raw = resources[resource_id]
        original[name] = raw
        parts = re.split(r'(?=^tracks/\d+/type)', raw, flags=re.M)
        header = clean(parts[0])
        length = re.search(r'length = ([\d.]+)', header)
        lengths[name] = float(length[1]) if length else 1.0
        if name in ['wait', 'walk', 'Fly', 'Fly_wait', 'wait_1', 'walk_1'] and 'loop_mode' not in header:
            header += 'loop_mode = 1\n'
        tracks = []
        for track in parts[1:]:
            if 'NodePath("MonsterDir/mr_ani:' not in track:
                continue
            track = clean(track).replace('MonsterDir/mr_ani:', 'Facing/Visual/Body:')
            track = track.replace(':modulate"', ':self_modulate"')
            tracks.append(re.sub(r'tracks/\d+/', f'tracks/{len(tracks)}/', track))
        animations.append(header + ''.join(tracks))
    aliases = {'wait': 'Fly_wait', 'walk': 'Fly', 'jump1': 'Fly'} if number in [7, 13, 19] else {}
    if number == 20:
        aliases = {'wait': 'wait_1', 'walk': 'walk_1', 'hurt': 'hurt_1'}
    if number == 8:
        aliases = {'walk': 'wait'}
    available = {name: resource_id for name, resource_id in entries if name != 'RESET'}
    for alias, name in aliases.items():
        available[alias] = available[name]
    library_body = '_data = {\n' + ',\n'.join(f'&"{name}": SubResource("{key}")' for name, key in available.items()) + '\n}\n'
    write(f'Content/Monsters/Human/monster_{number}_animations.tres', '[gd_resource type="AnimationLibrary" format=3]\n' + ''.join(animations) + '\n[resource]\n' + library_body)
    # 用实际静止帧的图幅校准脚点，动画 offset 保留原资源轨道。
    regions = [tuple(map(float, m.split(','))) for m in re.findall(r'region = Rect2\(([^)]+)\)', ''.join(atlases))]
    height = min(140, max(45, min((v[3] for v in regions), default=90)))
    body_height = min(130, max(45, height * .8))
    scene = '[gd_scene format=3]\n' + ext('PackedScene', 'Scenes/Actors/Monster.tscn', 'base')
    scene += ext('SpriteFrames', f'Content/Monsters/Human/monster_{number}_frames.tres', 'frames') + ext('AnimationLibrary', f'Content/Monsters/Human/monster_{number}_animations.tres', 'animations')
    if number in [7, 13, 19]:
        scene += ext('Script', 'Scripts/Actors/FlyingMonster.cs', 'flight')
    scene += f'\n[sub_resource type="CapsuleShape2D" id="body"]\nradius = {min(26, body_height / 3)}\nheight = {body_height}\n\n[sub_resource type="RectangleShape2D" id="attack"]\nsize = Vector2(180, 120)\n'
    scene += f'\n[node name="HumanMonster{number}" instance=ExtResource("base")]\n'
    if number in [7, 13, 19]:
        scene += 'script = ExtResource("flight")\nGravity = 0.0\n'
    scene += f'\n[node name="BodyShape" parent="." index="0"]\nposition = Vector2(0, {-body_height/2})\nshape = SubResource("body")\n\n[node name="Visual" parent="Facing" index="0"]\nposition = Vector2(0, {-height/2})\n\n[node name="Body" parent="Facing/Visual" index="0"]\nsprite_frames = ExtResource("frames")\nanimation = &"{next(n for n in available if n.startswith("wait") or n == "Fly_wait")}"\n\n[node name="HitBox" parent="Facing" index="1"]\nposition = Vector2(-75, -45)\n\n[node name="Shape" parent="Facing/HitBox" index="0"]\nshape = SubResource("attack")\n\n[node name="HurtBox" parent="." index="2"]\nposition = Vector2(0, {-body_height/2})\n\n[node name="Shape" parent="HurtBox" index="0"]\nshape = SubResource("body")\n\n[node name="AnimationPlayer" parent="." index="3"]\nlibraries/ = ExtResource("animations")\n'
    write(f'Scenes/Actors/Human/Monster_{number}.tscn', scene)
    # 每种攻击独立资源；旧方法轨道不跨接新角色。
    skill_names = list(dict.fromkeys(re.findall(r'objattackDic\["([^"]+)"\]\s*=\s*\{', script)))
    mapped = {name: name for name in skill_names if name in available}
    if number == 8:
        mapped = {'hit1': 'bfzh'}
    if number == 12:
        mapped.update({'bshn': 'FloorSkill', 'hydd': 'FlySkill'})
    if number == 20:
        mapped = {'hit1': 'hit1_1', 'sxks': 'sxks_1', 'spzd': 'spzd_1'}
    if number == 19:
        mapped = {'slj': 'slj', 'sytx': 'sytx'}
    skill_refs, choices = '', ''
    for index, (name, animation) in enumerate(mapped.items()):
        data = re.search(rf'objattackDic\["{name}"\]\s*=\s*\{{(.*?)(?=self.objattackDic|self.fall_pro|\nfunc )', script, re.S)
        power = re.search(r'"power":\s*([\d.]+)', data[1]) if data else None
        damage = float(power[1]) if power else 0
        kind = re.search(r'"attackKind":\s*"([^"]+)"', data[1]) if data else None
        damage_type = {'magic': 1, 'real': 2}.get(kind[1] if kind else '', 0)
        length = lengths[animation]
        start = max(1, int(length * 60 * .35)); end = max(start + 1, int(length * 60 * .65))
        attack = '[gd_resource type="Resource" format=3]\n' + ext('Script', 'Scripts/Combat/SkillDefinition.cs', 'skill') + ext('Script', 'Scripts/Combat/HitDefinition.cs', 'hit') + ext('Script', 'Scripts/Combat/HitEvent.cs', 'window')
        attack += f'\n[sub_resource type="Resource" id="hit"]\nscript = ExtResource("hit")\nFlatDamage = {damage}\nAttackMultiplier = 0.0\nDamageType = {damage_type}\nKnockback = Vector2(100, -100)\n\n[sub_resource type="Resource" id="window"]\nscript = ExtResource("window")\nStartFrame = {start}\nEndFrame = {end}\nHit = SubResource("hit")\n\n[resource]\nscript = ExtResource("skill")\nId = "human_{number}_{name}"\nAnimation = &"{animation}"\nFramesPerSecond = 60\nCooldownSeconds = {1.5 if name == "hit1" else 7.0}\nHits = {array(["SubResource(\"window\")"])}\n'
        write(f'Content/Monsters/Human/monster_{number}_{name}.tres', attack)
        skill_refs += ext('Resource', f'Content/Monsters/Human/monster_{number}_{name}.tres', f's{index}')
        choices += f'\n[sub_resource type="Resource" id="choice{index}"]\nscript = ExtResource("choice")\nSkill = ExtResource("s{index}")\nMaximumRange = {float(field(script,"attackRange",85)) if name == "hit1" else 250.0}\nMaximumHeightDifference = 200.0\n'
    drops_block = re.search(r'self.fall_list\s*=\s*\[(.*?)\]\s', script, re.S)
    original_drops = re.findall(r'"name":\s*"([^"]+)"', drops_block[1]) if drops_block else []
    drops, removed = [], []
    for item_id in original_drops:
        item_path = ROOT / f'Content/Items/{item_id}.tres'
        if not item_path.exists():
            raise FileNotFoundError(f'掉落尚未注册：{item_id}')
        item = read(item_path)
        role = re.search(r'RequiredCharacterId = "([^"]+)"', item)
        if role and role[1] not in ['role_1', 'role_2']:
            removed.append(item_id)
        else:
            drops.append(item_id)
    headers = ext('Script', 'Scripts/Monsters/MonsterDefinition.cs', 'def') + ext('Script', 'Scripts/Monsters/MonsterSkillChoice.cs', 'choice') + ext('PackedScene', f'Scenes/Actors/Human/Monster_{number}.tscn', 'actor') + ext('Script', 'Scripts/Monsters/MonsterDropTable.cs', 'drops') + ext('Script', 'Scripts/Monsters/MonsterDropEntry.cs', 'drop') + skill_refs
    drop_resources = ''.join(f'\n[sub_resource type="Resource" id="drop{i}"]\nscript = ExtResource("drop")\nItemId = "{item}"\n' for i, item in enumerate(drops))
    drop_resources += f'\n[sub_resource type="Resource" id="drops"]\nscript = ExtResource("drops")\nChooseOne = true\nRollProbability = {field(script,"fall_pro")}\nEntries = {array([f"SubResource(\"drop{i}\")" for i in range(len(drops))])}\n'
    properties = {'Health': 'SHp', 'Attack': 'attack_in', 'Level': 'level', 'PhysicalDefense': 'def', 'MagicDefense': 'mdef', 'CriticalRating': 'crit', 'DodgeRating': 'miss', 'Toughness': 'Toughness', 'Accuracy': 'Htarget', 'CriticalResistance': 'Critreduce', 'Experience': 'add_exp', 'Souls': 'fall_coin'}
    definition = f'\n[resource]\nscript = ExtResource("def")\nId = "human_monster_{number}"\nDisplayName = {field(script,"my_mr_name")}\nActorScene = ExtResource("actor")\nIsBoss = {field(script,"is_boss","false")}\n'
    definition += ''.join(f'{key} = {field(script,value)}\n' for key, value in properties.items())
    definition += f'MoveSpeed = {float(field(script,"speed"))*10}\nDetectionRange = 1200.0\nSkills = {array([f"SubResource(\"choice{i}\")" for i in reversed(range(len(mapped)))])}\nDrops = SubResource("drops")\n'
    write(f'Content/Monsters/Human/monster_{number}.tres', '[gd_resource type="Resource" format=3]\n' + headers + choices + drop_resources + definition)
    if number == 17:
        path=ROOT/'Content/Monsters/Human/monster_17.tres'
        boss=read(path).replace('IsBoss = false','IsBoss = true').replace('Id = "human_monster_17"','Id = "human_monster_17_boss"')
        write('Content/Monsters/Human/monster_17_boss.tres',boss)
        full=read(OLD/'Script/Monster/Monster_17.gd');common=full[full.index('\n\telse:'):full.index('\nfunc _ready')]
        for key,value in properties.items():
            boss=re.sub(rf'^{key} = .*$',f'{key} = {field(common,value)}',boss,flags=re.M)
        boss=boss.replace('Id = "human_monster_17_boss"','Id = "human_monster_17"').replace('IsBoss = true','IsBoss = false').replace('RollProbability = 0.5','RollProbability = 0.0')
        write('Content/Monsters/Human/monster_17.tres',boss)
    return {'number': number, 'name': field(script, 'my_mr_name').strip('"'), 'source': str(source), 'drops': drops, 'removed_other_roles': removed, 'animations': list(available), 'attacks': mapped, 'pending': '特殊召唤/护盾/形态机制尚未接入；基础攻击动画和数值已提取'}

def rectangle_terrain(number):
    target = ROOT / f'Scenes/Maps/Human/Map_{number}.tscn'
    blocks = re.split(r'(?=^\[)', read(target), flags=re.M)
    result, additions, shapes = [], '', ''
    count = 0
    for block in blocks:
        if block.startswith('[node ') and 'type="CollisionPolygon2D"' in block:
            points = list(map(float, re.search(r'polygon = PackedVector2Array\(([^)]+)\)', block)[1].split(',')))
            pairs = list(zip(points[::2], points[1::2]))
            name = re.search(r'name="([^"]+)"', block)[1]
            if name == 'ContinuousTerrain':
                # 按现有顶面取样，16像素宽阶梯与原图坡面对应；不存在整图实心碰撞。
                surface = pairs[:-2]
                for (x1,y1),(x2,y2) in zip(surface,surface[1:]):
                    if x2 <= x1:
                        continue
                    segments = max(1, int((x2-x1)/16)) if y1 != y2 else 1
                    for segment in range(segments):
                        left=x1+(x2-x1)*segment/segments; right=x1+(x2-x1)*(segment+1)/segments
                        top=y1+(y2-y1)*(segment+.5)/segments
                        key=f'terrain_rect_{count}';count+=1
                        shapes+=f'\n[sub_resource type="RectangleShape2D" id="{key}"]\nsize = Vector2({right-left}, 240)\n'
                        additions+=f'\n[node name="{key}" type="CollisionShape2D" parent="wall"]\nposition = Vector2({(left+right)/2}, {top+120})\nshape = SubResource("{key}")\n'
            else:
                x1=min(x for x,y in pairs);x2=max(x for x,y in pairs);y1=min(y for x,y in pairs);y2=max(y for x,y in pairs)
                key=f'boundary_rect_{count}';count+=1
                shapes+=f'\n[sub_resource type="RectangleShape2D" id="{key}"]\nsize = Vector2({x2-x1}, {y2-y1})\n'
                additions+=f'\n[node name="{name}" type="CollisionShape2D" parent="wall"]\nposition = Vector2({(x1+x2)/2}, {(y1+y2)/2})\nshape = SubResource("{key}")\n'
            continue
        result.append(block)
    text=''.join(result);index=text.index('[node ')
    write(str(target.relative_to(ROOT)),text[:index]+shapes+text[index:]+additions)

def migrate_level(number):
    script = read(OLD / f'Script/Level/Level_{number}.gd') if number != 4 else read(OLD / 'Script/Level/level_4.gd')
    groups = re.search(r'Monster_group = \{(.*?)\}', script, re.S)[1]
    waves = [list(map(int,v.split(','))) for v in re.findall(r'"stage_\d+":\s*\[([^]]+)\]',groups)]
    target = ROOT / f'Content/Levels/human_{number}.tres'
    text = read(target)
    refs = ext('Script','Scripts/Level/WaveEncounterDefinition.cs','encounter') + ext('Script','Scripts/Level/WaveDefinition.cs','wave')
    kinds = sorted({kind for wave in waves for kind in wave})
    refs += ''.join(ext('Resource',f'Content/Monsters/Human/monster_{kind}.tres',f'm{kind}') for kind in kinds)
    if number == 7:
        refs=refs.replace('monster_17.tres','monster_17_boss.tres')
    resource=''
    ranges=[(450,950),(1400,2000),(2600,3300),(4200,4500)]
    gates=[1100,2250,3600,4950]
    for index,wave in enumerate(waves):
        resource+=f'\n[sub_resource type="Resource" id="wave{index}"]\nscript = ExtResource("wave")\nMonsters = {array([f"ExtResource(\"m{kind}\")" for kind in wave])}\nEntranceX = {[0,1110,2260,3610][index]}\nSpawnRange = Vector2{ranges[index]}\nGateX = {gates[index]}.0\nCameraRight = {gates[index]+33}\nSpawnRayTop = -200.0\nSpawnRayBottom = 900.0\n'
        if index==3:
            resource+='ReleaseGateOnClear = false\n'
    resource+='\n[sub_resource type="Resource" id="encounter"]\nscript = ExtResource("encounter")\nWaves = '+array([f'SubResource("wave{i}")' for i in range(len(waves))])+'\n'
    text=text.replace('[resource]',refs+resource+'\n[resource]').replace('MapOnly = true\n','')
    text=re.sub(r'Description = "[^"]*"','Description = "原版四波遭遇与掉落已接入；部分Boss特殊机制正在还原"',text)
    write(str(target.relative_to(ROOT)),text+'WaveEncounter = SubResource("encounter")\n')
    rectangle_terrain(number)
    spikes = [float(x) for x in re.findall(r'^\s*Global.add_Trap\(self,Vector2\((\d+),400\),"Trap_2"\)',script,re.M)]
    map_path=f'Scenes/Maps/Human/Map_{number}.tscn';text=read(ROOT/map_path)
    if spikes:
        index=text.index('[node ');text=text[:index]+ext('PackedScene','Scenes/Maps/GroundSpike.tscn','spike')+text[index:]
        for index,x in enumerate(spikes):
            text+=f'\n[node name="Spike{index}" parent="." instance=ExtResource("spike")]\nposition = Vector2({x}, 507)\nPhaseOffset = {(index%4)*.35}\n'
        write(map_path,text)
    return {'number':number,'waves':waves,'spikes_x':spikes,'source':str(OLD/f'Script/Level/Level_{number}.gd')}

def main():
    if (ROOT/'Content/Monsters/Human').exists():
        raise RuntimeError('后六关已提取，禁止重新生成覆盖。请直接修改资源。')
    monsters=[migrate_monster(number) for number in range(7,21)]
    old_trap=read(OLD/'Scene/Level/trap.tscn')
    texture=next(b for b in re.split(r'(?=^\[)',old_trap,flags=re.M) if b.startswith('[ext_resource type="Texture2D"'))
    copy_texture(texture)
    path=re.search(r'path="res://([^"]+)"',texture)[1].replace('Art/','Assets/Art/',1)
    write('Scenes/Maps/GroundSpike.tscn','[gd_scene format=3]\n'+ext('Script','Scripts/Level/Traps/GroundSpikeTrap.cs','trap')+ext('Texture2D',path,'texture')+'\n[node name="GroundSpike" type="Node2D"]\nscript = ExtResource("trap")\n\n[node name="Visual" type="Sprite2D" parent="."]\nposition = Vector2(0, -24)\nrotation = 3.14159\ntexture = ExtResource("texture")\n')
    levels=[migrate_level(number) for number in range(4,10)]
    write('文档/system/后六关迁移清单.json',json.dumps({'monsters':monsters,'levels':levels},ensure_ascii=False,indent=2))

if __name__ == '__main__':
    main()
