from pathlib import Path
import re,shutil
root=Path('.').resolve();old=root.parent/'造梦八荒-(4.1)'; rebuild=root.parent/'zmbh-rebuild/assets'
def write(p,t):
 p=root/p;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(t,encoding='utf-8')
def clean(t):
 t=re.sub(r' uid="[^"]+"','',t);t=re.sub(r'^script = .*\n','',t,flags=re.M);return t.replace('res://Art/','res://Assets/Art/').replace('res://Font/','res://Assets/Font/')
for name,path in [('Victory','Scene/Level/victory.tscn'),('Defeat','Scene/Level/defeat.tscn'),('Details','Scene/OtherScene/AfterLevelEnd.tscn'),('Rating','Scene/OtherScene/ping_jia.tscn')]:
 blocks=re.split(r'(?=^\[)',(old/path).read_text(encoding='utf-8'),flags=re.M);out=[]
 for b in blocks:
  if not b.strip() or b.startswith('[gd_scene') or b.startswith('[connection'):continue
  if b.startswith('[ext_resource') and ('type="Script"' in b or 'type="PackedScene"' in b):continue
  if name=='Defeat' and b.startswith('[sub_resource'):continue
  if name=='Defeat' and b.startswith('[node') and not any(b.startswith(f'[node name="{n}" ') for n in ['defeat','bg','return_map','ReChallenge']):continue
  if name=='Victory' and b.startswith('[sub_resource type="Animation"'):
   header,*tracks=re.split(r'(?=tracks/\d+/type =)',b)
   b=header+''.join(re.sub(r'tracks/\d+/',f'tracks/{i}/',t) for i,t in enumerate(t for t in tracks if '/type = "method"' not in t))
  b=clean(b)
  if name=='Defeat': b=re.sub(r'^modulate = .*\n','',b,flags=re.M);b=b.replace('disabled = true','disabled = false')
  out.append(b)
 text='[gd_scene format=3]\n\n'+''.join(out)
 for res in re.findall(r'path="res://([^"]+)"',text):
  dest=root/res
  if dest.exists():continue
  relative=res.removeprefix('Assets/');candidates=[old/relative,rebuild/relative]
  source=next((p for p in candidates if p.exists()),None)
  if source:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,dest)
  elif dest.suffix.lower() in ['.ttf','.ttc']:text=text.replace('res://'+res,'res://Assets/Font/Aa文徵明琴赋小楷_mianfeiziti.com.ttf')
  else:raise FileNotFoundError(res)
 write(f'Scenes/UI/Settlement/{name}.tscn',text)
# 原悟空死亡效果已经在现有 SpriteFrames 中，补回独立播放节点。
p=root/'Scenes/Actors/Player.tscn'
if '[node name="Death"' not in p.read_text(encoding='utf-8'):
 p=root/'Scenes/Actors/Player.tscn';text=p.read_text(encoding='utf-8');pattern=r'\[sub_resource type="Animation" id="anim_death"\][\s\S]*?(?=\[sub_resource)';block=re.search(pattern,text)[0];index=len(re.findall(r'tracks/\d+/type',block))
 for path,value in [('Facing/Visual/Death:visible','true'),('Facing/Visual/RoleBody:visible','false'),('Facing/Visual/RoleEquipment:visible','false')]:
  block+=f'tracks/{index}/type = "value"\ntracks/{index}/path = NodePath("{path}")\ntracks/{index}/keys = {{\n"times": PackedFloat32Array(0),\n"transitions": PackedFloat32Array(1),\n"update": 1,\n"values": [{value}]\n}}\n';index+=1
 block+=f'tracks/{index}/type = "method"\ntracks/{index}/path = NodePath("Facing/Visual/Death")\ntracks/{index}/keys = {{\n"times": PackedFloat32Array(0),\n"transitions": PackedFloat32Array(1),\n"values": [{{"args": [&"death"], "method": &"play"}}]\n}}\n\n';text=re.sub(pattern,lambda m:block,text)
 text=text.replace('[node name="HitBox" type="Area2D"', '[node name="Death" type="AnimatedSprite2D" parent="Facing/Visual"]\nvisible = false\nposition = Vector2(0, -23)\nsprite_frames = SubResource("276")\nanimation = &"death"\noffset = Vector2(0, -90)\n\n[node name="HitBox" type="Area2D"',1);p.write_text(text,encoding='utf-8')
p=root/'Scenes/Actors/Tangseng.tscn';t=p.read_text(encoding='utf-8').replace('[node name="Death" type="AnimatedSprite2D" parent=', '[node name="Death" parent=');p.write_text(t,encoding='utf-8')
print('Migrated settlement scenes and restored Wukong death effect.')
