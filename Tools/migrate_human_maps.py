"""一次性初始切片工具。最终策划资源与地形另经核对编辑，禁止重新覆盖。"""
from pathlib import Path
import re,json,shutil
ROOT=Path(__file__).resolve().parents[1]; OLD=ROOT.parent/'造梦八荒-(4.1)'
if (ROOT/'Scenes/Maps/Human').exists():
 raise RuntimeError('人间地图已经迁入。请直接编辑资源，不得重新生成覆盖。')
def write(path,t):
 p=ROOT/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(t,encoding='utf-8')
def read(path):return (ROOT/path).read_text(encoding='utf-8')
def ext(kind,path,id):return f'[ext_resource type="{kind}" path="res://{path}" id="{id}"]\n'
# 静态模板引用已核对的三种旧表现；Boss使用已有独立动作库。
configs=[('monkey','小猴子','Monster',60,10,5,50,80,0,85,1,1,58,.22),('demon_monkey','妖猴','Monster2',90,20,5,50,80,0,110,5,2,85,.18),('gorilla','大猩猩','Monster3',200,30,10,50,50,5,100,10,2,135,.18),('gorilla_boss','大猩猩','ForestBoss',300,65,5,50,80,5,120,35,10,135,0)]
for id,name,scene,hp,atk,lv,pd,md,crit,speed,xp,souls,ran,drop in configs:
 text=read(f'Scenes/Actors/{scene}.tscn')
 if scene!='Monster':
  library='ForestBossAnimations' if scene in ('ForestBoss','Monster3') else scene+'Animations'
  text=read('Content/Monsters/'+library+'.tres')
 block=next(b for b in re.split(r'(?=^\[)',text,flags=re.M) if b.startswith('[sub_resource type="Animation"') and ('anim_hit1' in b or 'resource_name = "hit1"' in b))
 active=next(b for b in re.split(r'(?=tracks/\d+/type)',block) if 'HitBox:Active' in b)
 times=[float(x) for x in re.search(r'"times": PackedFloat32Array\(([^)]+)\)',active)[1].split(',')]
 vals=re.search(r'"values": \[([^]]+)\]',active)[1].split(',')
 start=next(i for i,v in enumerate(vals) if v.strip()=='true'); end=start+1
 a=round(times[start]*60);b=round(times[end]*60)
 skill=ext('Script','Scripts/Combat/SkillDefinition.cs','skill')+ext('Script','Scripts/Combat/HitEvent.cs','event')+ext('Script','Scripts/Combat/HitDefinition.cs','hit')
 skill+='\n[sub_resource type="Resource" id="hit"]\nscript = ExtResource("hit")\nAttackMultiplier = 1.0\nKnockback = Vector2(100, -100)\nHitstun = 0.25\n'
 skill+=f'\n[sub_resource type="Resource" id="window"]\nscript = ExtResource("event")\nStartFrame = {a}\nEndFrame = {b}\nHit = SubResource("hit")\n'
 skill+=f'\n[resource]\nscript = ExtResource("skill")\nId = "{id}_strike"\nAnimation = &"hit1"\nFramesPerSecond = 60\nCooldownSeconds = 1.1\nHits = Array[Resource]([SubResource("window")])\n'
 write(f'Content/Monsters/Templates/{id}_strike.tres','[gd_resource type="Resource" format=3]\n'+skill)
 # 旧小怪随机选择一件，独立条目迁入的是当前已注册装备；每项概率平分。
 ids=['ptxzg','ptxzf'] if drop else []
 headers=ext('Script','Scripts/Monsters/MonsterDefinition.cs','def')+ext('Script','Scripts/Monsters/MonsterSkillChoice.cs','choice')+ext('PackedScene',f'Scenes/Actors/{scene}.tscn','scene')+ext('Resource',f'Content/Monsters/Templates/{id}_strike.tres','skill')
 headers+=ext('Script','Scripts/Monsters/MonsterDropTable.cs','drops')+ext('Script','Scripts/Monsters/MonsterDropEntry.cs','drop')
 subs=f'\n[sub_resource type="Resource" id="choice"]\nscript = ExtResource("choice")\nSkill = ExtResource("skill")\nMaximumRange = {ran}.0\n'
 for i,item in enumerate(ids):subs+=f'\n[sub_resource type="Resource" id="drop{i}"]\nscript = ExtResource("drop")\nItemId = "{item}"\nProbability = {drop/len(ids)}\n'
 subs+='\n[sub_resource type="Resource" id="drops"]\nscript = ExtResource("drops")\nEntries = Array[Resource](['+', '.join(f'SubResource("drop{i}")' for i in range(len(ids)))+'])\n'
 body=f'\n[resource]\nscript = ExtResource("def")\nId = "{id}"\nDisplayName = "{name}"\nActorScene = ExtResource("scene")\nIsBoss = {str(id.endswith("boss")).lower()}\nHealth = {hp}.0\nAttack = {atk}.0\nLevel = {lv}\nPhysicalDefense = {pd}.0\nMagicDefense = {md}.0\nCriticalRating = {crit}.0\nMoveSpeed = {speed}.0\nDetectionRange = 1000.0\nExperience = {xp}\nSouls = {souls}\nSkills = Array[Resource]([SubResource("choice")])\nDrops = SubResource("drops")\n'
 write(f'Content/Monsters/Templates/{id}.tres','[gd_resource type="Resource" format=3]\n'+headers+subs+body)
# 魔化配置为独立模板，重构变体沿用此前配置掉落，不称旧版已还原。
s=read('Content/Monsters/Templates/demon_monkey.tres').replace('Id = "demon_monkey"','Id = "dark_demon_monkey"').replace('ItemId = "ptxzg"','ItemId = "jcsz"').replace('ItemId = "ptxzf"','ItemId = "jcld"')
write('Content/Monsters/Templates/dark_demon_monkey.tres',s)
for name in ['forest','forest_dark']:
 t=read(f'Content/Levels/{name}.tres');refs={1:'monkey',2:'dark_demon_monkey' if name=='forest_dark' else 'demon_monkey',3:'gorilla',4:'gorilla_boss'}
 t=t.replace('[sub_resource type="Resource" id="w1"]',''.join(ext('Resource',f'Content/Monsters/Templates/{v}.tres',f'template{k}') for k,v in refs.items())+'\n[sub_resource type="Resource" id="w1"]')
 t=re.sub(r'MonsterKinds = PackedInt32Array\(([^)]+)\)',lambda m:'Monsters = Array[Resource](['+', '.join(f'ExtResource("template{int(x)}")' for x in m[1].split(','))+'])',t)
 t=re.sub(r'^(CommonDropIds|BossDropIds) = .*\n','',t,flags=re.M)
 write(f'Content/Levels/{name}.tres',t)
# 地图切片：继承自 BaseThroughLevel 的几何和背景，保留资源实际引用，删除旧逻辑。
report=[]
for n in range(2,11):
 source=OLD/f'Scene/Level/Level_{n}.tscn';t=source.read_text(encoding='utf-8')
 blocks=re.split(r'(?=^\[)',t,flags=re.M)
 resources=''.join(b for b in blocks if b.startswith('[sub_resource') or b.startswith('[ext_resource') and 'type="Script"' not in b and 'BaseThroughLevel' not in b)
 resources=re.sub(r' uid="[^"]+"','',resources);resources=re.sub(r'^.*script = null\n','',resources,flags=re.M)
 resources=resources.replace('res://Art/','res://Assets/Art/').replace('res://Font/','res://Assets/Font/')
 resources=resources.replace('res://Scene/Level/stage.tscn','res://Scenes/Maps/Stage.tscn').replace('res://Scene/Level/XP.tscn','res://Scenes/Maps/Slope.tscn')
 nodes='[node name="HumanMap'+str(n)+'" type="Node2D"]\n\n[node name="wall" type="StaticBody2D" parent="."]\nposition = Vector2(0, 112)\ncollision_mask = 0\n\n[node name="LeftBoundary" type="CollisionPolygon2D" parent="wall"]\npolygon = PackedVector2Array(-100, -1000, 0, -1000, 0, 1000, -100, 1000)\n\n[node name="BackGround" type="ParallaxBackground" parent="."]\n\n[node name="End" type="ParallaxLayer" parent="BackGround"]\nmotion_scale = Vector2(0, 0)\n\n[node name="floor_2" type="ParallaxLayer" parent="BackGround"]\n'
 for b in blocks:
  if not b.startswith('[node '):continue
  name=re.search(r'name="([^"]+)"',b)[1];par=re.search(r'parent="([^"]+)"',b)
  if par is None:continue
  parent=par[1]
  if name.startswith('stop') or name in ['wall','End','floor_2','Area2D']:continue
  if not (parent.startswith(('wall','BackGround','stage')) or name=='stage'):continue
  b=re.sub(r' index="\d+"','',b);b=re.sub(r'^script = .*\n','',b,flags=re.M)
  if 'type=' not in b.splitlines()[0] and 'instance=' not in b.splitlines()[0]:
   typ='TileMap' if 'layer_0/tile_data' in b else 'CollisionShape2D' if 'shape = ' in b else 'Node2D'
   b=b.replace(' parent=',f' type="{typ}" parent=',1)
  b=b.replace('collision_layer = 32','collision_layer = 1')
  nodes+=b
 # 未迁入的外部依赖不得猜测；仅实际引用路径的图片允许从重建 assets 补齐。
 for path in re.findall(r'path="res://([^"]+)"',resources):
  if not (ROOT/path).exists():
   orig=OLD/path.replace('Assets/',''); candidates=[orig,ROOT.parent/'zmbh-rebuild/assets'/path.removeprefix('Assets/')]
   found=next((f for f in candidates if f.exists()),None)
   if found is None:raise FileNotFoundError(path)
   (ROOT/path).parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(found,ROOT/path)
 mapfile=f'Scenes/Maps/Human/Map_{n}.tscn'
 write(mapfile,'[gd_scene format=3]\n\n'+resources+nodes)
 script=(OLD/f'Script/Level/Level_{n}.gd').read_text(encoding='utf-8')
 title=re.search(r'Global.CurrentLevel = "([^"]+)"',script)[1]
 # 出口旧局部坐标加 BaseThroughLevel 的112偏移，光圈组件自身上偏77。
 exitnode=next(b for b in blocks if b.startswith('[node name="exit" parent="."'))
 ex,ey=map(float,re.search(r'position = Vector2\(([^)]+)\)',exitnode)[1].split(','));ground=ey+112+77
 # 实际地面矩形顶边优先于出口图的视觉中心。
 floor=next((b for b in blocks if b.startswith('[node name="floor" type="CollisionShape2D"')),None)
 if floor:
  shape=re.search(r'SubResource\(\s*"([^"]+)"',floor)[1]
  sb=next(b for b in blocks if b.startswith('[sub_resource') and f'id="{shape}"' in b)
  fy=float(re.search(r'position = Vector2\([^,]+, ([^)]+)\)',floor)[1]); height=float(re.search(r'size = Vector2\([^,]+, ([^)]+)\)',sb)[1]);ground=fy-height/2+112
 camera=int(ex+200) if n==10 else 5000
 key={2:'water_cave',3:'peach_garden'}.get(n,f'human_{n}')
 headers=ext('Script','Scripts/Level/LevelDefinition.cs','def')+ext('PackedScene',mapfile,'map')+ext('Resource','Content/Levels/Difficulties/normal.tres','normal')+ext('Resource','Content/Levels/Difficulties/hard.tres','hard')
 data=f'\n[resource]\nscript = ExtResource("def")\nId = "{key}"\nDisplayName = "{title}"\nDescription = "地图试玩：原版地形与背景已迁入，本关怪物与 Boss 尚未迁入"\nProgressLevel = {n}\nLevelScenePath = "res://Scenes/Level/Level_{n}.tscn"\nMapScene = ExtResource("map")\nMapOnly = true\nDifficulties = Array[Resource]([ExtResource("normal"), ExtResource("hard")])\nPlayerSpawn = Vector2(380, {ground-4})\nCameraRight = {camera}\nCameraBottom = 650\nExitPosition = Vector2({ex}, {ground})\n'
 write(f'Content/Levels/{key}.tres','[gd_resource type="Resource" format=3]\n'+headers+data)
 container=read('Scenes/Level/Level_1.tscn').replace('Content/Levels/forest.tres',f'Content/Levels/{key}.tres').replace('name="Level_1"','name="Level_'+str(n)+'"').replace('LevelNumber = 1','LevelNumber = '+str(n))
 write(f'Scenes/Level/Level_{n}.tscn',container)
 report.append({'number':n,'name':title,'source':str(source),'map':mapfile,'exit':[ex,ground]})
# 地图入口显式扩展到十个
s=read('Content/Maps/Human.tres')
for n in range(4,11):
 s=s.replace('[sub_resource type="Resource" id="entry1"]',ext('Resource',f'Content/Levels/human_{n}.tres',f'level{n}')+'\n[sub_resource type="Resource" id="entry1"]')
 path='Node2D2/Node2D/level_9' if n==9 else f'level_{n}'
 s=s.replace('[resource]',f'[sub_resource type="Resource" id="entry{n}"]\nscript = ExtResource("entrance")\nButtonPath = NodePath("{path}")\nLevels = Array[Resource]([ExtResource("level{n}")])\n\n[resource]')
s=re.sub(r'LevelEntrances = .*', 'LevelEntrances = Array[Resource](['+', '.join(f'SubResource("entry{n}")' for n in range(1,11))+'])',s)
write('Content/Maps/Human.tres',s)
write('文档/system/人间地图迁移清单.json',json.dumps(report,ensure_ascii=False,indent=2))
print('Migrated ten main map entrances and first-map templates')

