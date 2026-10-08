"""一次性把原版释放轨道转换为新技能行为，生成后直接编辑资源。"""
from pathlib import Path
import re
from migrate_next_six import ROOT, OLD, read, write, ext, copy_texture, array, field

def extract_frames(source_path, animation):
    output=f'Content/Monsters/Human/Effects/{animation}.tres'
    if (ROOT/output).exists():
        return output
    blocks=re.split(r'(?=^\[)',read(OLD/source_path),flags=re.M)
    resources={re.search(r'id="([^"]+)"',b)[1]:b for b in blocks if b.startswith('[sub_resource')}
    frames=next(b for b in resources.values() if b.startswith('[sub_resource type="SpriteFrames"') and f'"name": &"{animation}"' in b)
    animations=re.findall(r'\{\s*"frames": \[.*?"speed": [^\n]+\n\}',frames,re.S)
    selected=next(a for a in animations if f'"name": &"{animation}"' in a)
    selected=selected.replace('"loop": true','"loop": false')
    needed={key for key in re.findall(r'SubResource\(\s*"([^"]+)"',selected)}
    resource_blocks=[resources[key] for key in needed]
    ids={key for key in re.findall(r'ExtResource\(\s*"([^"]+)"',''.join(resource_blocks)+selected)}
    textures=[b for b in blocks if b.startswith('[ext_resource') and re.search(r'\bid="([^"]+)"',b)[1] in ids]
    for texture in textures:
        copy_texture(texture)
    def clean(block):
        block=re.sub(r' uid="[^"]+"','',block).replace('res://Art/','res://Assets/Art/')
        return re.sub(r'^script = null\n','',block,flags=re.M)
    write(output,'[gd_resource type="SpriteFrames" format=3]\n'+''.join(clean(b) for b in textures+resource_blocks)+'\n[resource]\nanimations = ['+selected+']\n')
    return output

def animation_data(number,animation):
    blocks=re.split(r'(?=^\[)',read(OLD/f'Scene/Monster/Monster_{number}.tscn'),flags=re.M)
    library=next(b for b in blocks if b.startswith('[sub_resource type="AnimationLibrary"'))
    key=re.search(rf'"{animation}": SubResource\(\s*"([^"]+)"',library)[1]
    return next(b for b in blocks if b.startswith('[sub_resource') and f'id="{key}"' in b)

def attach_behavior(path,scene,clear_hits=True):
    f=ROOT/path;t=read(f)
    t=re.sub(r'^BehaviorScene = .*\n','',t,flags=re.M)
    if clear_hits:t=re.sub(r'^Hits = .*\n','',t,flags=re.M)
    index=t.find('[sub_resource');index=index if index>=0 else t.index('[resource]')
    t=t[:index]+ext('PackedScene',scene,'behavior')+t[index:]
    f.write_text(t+'BehaviorScene = ExtResource("behavior")\n',encoding='utf8')

def hit_subresource(number,name,key='hit'):
    t=read(ROOT/f'Content/Monsters/Human/monster_{number}_{name}.tres')
    body=re.search(r'\[sub_resource type="Resource" id="hit"\](.*?)(?=\[)',t,re.S)[1]
    return f'\n[sub_resource type="Resource" id="{key}"]\n'+body

def projectile(number,name,animation,bullet_source,release_times,speed=600):
    frames=extract_frames(bullet_source,animation)
    path=f'Content/Monsters/Human/Effects/projectile_{number}_{name}.tscn'
    text='[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/SkillProjectile.cs','projectile')+ext('Script','Scripts/Combat/HitDefinition.cs','hit')+ext('SpriteFrames',frames,'frames')
    text+=hit_subresource(number,name)
    text+=f'\n[node name="Projectile" type="Node2D"]\nscript = ExtResource("projectile")\nFrames = ExtResource("frames")\nAnimation = &"{animation}"\nHit = SubResource("hit")\nSpeed = {speed}.0\nLifetime = 2.5\nHitRadius = 40.0\nVerticalHitRadius = 80.0\n'
    write(path,text)
    behavior=f'Content/Monsters/Human/Effects/behavior_{number}_{name}.tscn'
    text='[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/ProjectileSkillBehavior.cs','behavior')+ext('PackedScene',path,'projectile')
    text+='\n[node name="ProjectileAttack" type="Node"]\nscript = ExtResource("behavior")\nProjectileScene = ExtResource("projectile")\nSpawnOffset = Vector2(60, -40)\nReleaseTimes = PackedFloat32Array('+', '.join(map(str,release_times))+')\n'
    write(behavior,text)
    attach_behavior(f'Content/Monsters/Human/monster_{number}_{name}.tres',behavior)

def burst(number,name,animation,release_times,positions,radius=100,target=False):
    frames=extract_frames('Scene/Monster/MonsterBullet.tscn',animation)
    frame_count=len(re.findall(r'"duration":',read(ROOT/frames)))
    behavior=f'Content/Monsters/Human/Effects/behavior_{number}_{name}.tscn'
    text='[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/BurstSkillBehavior.cs','behavior')+ext('Script','Scripts/Combat/Skills/BurstAttackDefinition.cs','burst')+ext('Script','Scripts/Combat/HitDefinition.cs','hit')+ext('Script','Scripts/Combat/HitEvent.cs','window')+ext('SpriteFrames',frames,'frames')
    text+=hit_subresource(number,name)
    text+=f'\n[sub_resource type="Resource" id="window"]\nscript = ExtResource("window")\nStartFrame = 0\nEndFrame = {frame_count}\nHit = SubResource("hit")\n'
    text+=f'\n[sub_resource type="Resource" id="burst"]\nscript = ExtResource("burst")\nFrames = ExtResource("frames")\nAnimation = &"{animation}"\nRadius = {radius}.0\nPositions = PackedVector2Array('+', '.join(str(v) for xy in positions for v in xy)+')\nReleaseTimes = PackedFloat32Array('+', '.join(map(str,release_times))+')\nScales = PackedFloat32Array('+', '.join('1' for _ in positions)+')\nHits = '+array(['SubResource("window")'])+'\n'
    text+=f'\n[node name="BurstAttack" type="Node"]\nscript = ExtResource("behavior")\nBurst = SubResource("burst")\nTargetEnemy = {str(target).lower()}\n'
    write(behavior,text);attach_behavior(f'Content/Monsters/Human/monster_{number}_{name}.tres',behavior)

def refine_windows():
    for f in (ROOT/'Content/Monsters/Human').glob('monster_*_*.tres'):
        match=re.match(r'monster_(\d+)_(.+)\.tres',f.name)
        if not match or match[2]=='boss':continue
        number=int(match[1]);t=read(f)
        if 'Scripts/Combat/SkillDefinition.cs' not in t:continue
        animation=re.search(r'Animation = &"([^"]+)"',t)[1]
        raw=animation_data(number,animation)
        track=next((tr for tr in re.split(r'(?=^tracks/\d+/type)',raw,flags=re.M)[1:] if 'BaseDamageBox/HitBox/HitBox:disabled' in tr),None)
        if track:
            times=list(map(float,re.search(r'"times": PackedFloat32Array\(([^)]+)\)',track)[1].split(',')))
            values=re.search(r'"values": \[([^]]+)\]',track)[1].split(',')
            windows=[(round(times[i]*60),round(times[i+1]*60)) for i,v in enumerate(values[:-1]) if v.strip()=='false']
            if windows:
                t=re.sub(r'StartFrame = \d+',f'StartFrame = {windows[0][0]}',t)
                t=re.sub(r'EndFrame = \d+',f'EndFrame = {windows[0][1]}',t)
            else:t=re.sub(r'^Hits = .*\n','',t,flags=re.M)
        # 原版power是伤害，不是attack_in；使用比例才能正确应用当前难度倍率。
        actor=read(ROOT/f'Content/Monsters/Human/monster_{number}.tres')
        attack=float(re.search(r'^Attack = ([\d.]+)',actor,re.M)[1])
        damage=float(re.search(r'FlatDamage = ([\d.]+)',t)[1])
        if attack>0 and damage>0:
            t=re.sub(r'FlatDamage = [\d.]+','FlatDamage = 0.0',t)
            t=t.replace('AttackMultiplier = 0.0',f'AttackMultiplier = {damage/attack}')
        f.write_text(t,encoding='utf8')

def main():
    if (ROOT/'Content/Monsters/Human/Effects').exists():raise RuntimeError('技能细化已完成，禁止重新覆盖。')
    refine_windows()
    projectile(10,'dmfd','Knife','Scene/Monster/MonsterBullet.tscn',[.3])
    projectile(11,'kldw','PoisionBullet','Scene/Monster/MonsterBullet.tscn',[1.4],450)
    burst(11,'ysyb','Monster11Bullet',[.2,.8,1.4],[(0,0),(-120,0),(120,0)],90)
    projectile(12,'hydd','Monster12Bullet','Scene/Monster/MonsterBullet.tscn',[.4],500)
    projectile(14,'hit1','Monster14Bullet','Scene/Bullet/Fly_bullet.tscn',[.3])
    burst(15,'hit1','Monster15Bullet',[.3],[(0,0)],100)
    burst(15,'qlzg','Monster15Bullet',[.4],[(0,0)],120)
    projectile(18,'fhjl','Monster18Bullet_1','Scene/Monster/MonsterBullet.tscn',[.2],500)
    burst(18,'dtsz','Monster18Bullet_2',[.1,.42,.74,1.06,1.38],[(120,0),(270,0),(420,0),(570,0),(720,0)],75)
    burst(20,'spzd','Monster20Bullet_1',[1.0],[(0,0)],100,True)
    behavior='Content/Monsters/Human/Effects/stone_summon.tscn'
    write(behavior,'[gd_scene format=3]\n'+ext('Script','Scripts/Combat/Skills/SummonSkillBehavior.cs','behavior')+ext('Resource','Content/Monsters/Human/monster_7.tres','bat')+'\n[node name="SummonBats" type="Node"]\nscript = ExtResource("behavior")\nSummonedMonster = ExtResource("bat")\nDelay = 0.4\nCount = 4\n')
    attach_behavior('Content/Monsters/Human/monster_8_hit1.tres',behavior)
    f=ROOT/'Content/Monsters/Human/monster_8_hit1.tres';t=read(f).replace('CooldownSeconds = 1.5','CooldownSeconds = 10.0');f.write_text(t,encoding='utf8')
    write('Content/Buffs/stone_shield.tres','[gd_resource type="Resource" format=3]\n'+ext('Script','Scripts/Combat/Buffs/BuffDefinition.cs','buff')+'\n[resource]\nscript = ExtResource("buff")\nId = "stone_shield"\nDisplayName = "石像护盾"\nDescription = "触碰红色弱点后护盾失效五秒。"\nDuration = 0.0\nDamageReduction = 1.0\n')
    f=ROOT/'Scenes/Actors/Human/Monster_8.tscn';t=read(f);idx=t.index('[sub_resource');t=t[:idx]+ext('Script','Scripts/Monsters/StoneShieldMechanic.cs','shield_script')+ext('Resource','Content/Buffs/stone_shield.tres','shield')+t[idx:];f.write_text(t+'\n[node name="ShieldMechanic" type="Node2D" parent="."]\nscript = ExtResource("shield_script")\nShield = ExtResource("shield")\n',encoding='utf8')
    f=ROOT/'Content/Monsters/Human/monster_8.tres';t=read(f).replace('MaximumRange = 75.0','MaximumRange = 1000.0').replace('PhysicalDefense = 500','PhysicalDefense = 60').replace('MagicDefense = 500','MagicDefense = 70');f.write_text(t,encoding='utf8')

if __name__=='__main__':main()
