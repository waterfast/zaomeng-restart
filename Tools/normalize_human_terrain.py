"""一次性碰撞整理；原顶面与调整后顶面保存在人间地形调整.json。"""
from pathlib import Path
import re,json
p=Path(__file__).resolve().parents[1];changes=[]
def map_path(n):
 return p/f'Scenes/Maps/Human/Map_{n}.tscn'
if any("ContinuousTerrain" in map_path(n).read_text(encoding="utf-8") for n in [2,3,5]):
 raise RuntimeError("地形已整理，请直接编辑场景，不能覆盖现有结果。")
for n in [2,3,5]:
 f=map_path(n);t=f.read_text(encoding='utf-8');blocks=re.split(r'(?=^\[)',t,flags=re.M);resources={re.search('id="([^"]+)"',s)[1]:s for s in blocks if s.startswith('[sub_resource')};ranges=[];kept=[]
 for s in blocks:
  if s.startswith('[node ') and 'parent="wall"' in s and 'shape = ' in s:
   id=re.search(r'SubResource\(\s*"([^"]+)"',s)[1];shape=resources[id];v=re.search(r'size = Vector2\(([^)]+)\)',shape);pos=re.search(r'position = Vector2\(([^)]+)\)',s)
   if v and pos:
    w,h=map(float,v[1].split(','));x,y=map(float,pos[1].split(','));ranges.append((x-w/2,x+w/2,y-h/2));continue
  if n==2 and s.startswith('[node ') and 'parent="wall"' in s and 'instance=ExtResource( "3" )' in s:
   x,y=map(float,re.search(r'position = Vector2\(([^)]+)\)',s)[1].split(','));ranges.append((x-102,x+102,y-11));continue
  kept.append(s)
 baseline=max(y for a,b,y in ranges if b-a>1500)
 xs=list(range(0,5401,8));ys=[min([baseline]+[y for a,b,y in ranges if a<=x<=b]) for x in xs]
 # 用原矩形顶面构成连续坡面，避免当前角色跳跃参数无法攀爬的垂直台阶。
 for i in range(len(ys)-2,-1,-1):ys[i]=min(ys[i],ys[i+1]+8*.9)
 for i in range(1,len(ys)):ys[i]=min(ys[i],ys[i-1]+8*.9)
 points=[(xs[0],ys[0])]
 for i in range(1,len(xs)-1):
  if abs((ys[i]-ys[i-1])-(ys[i+1]-ys[i]))>.01:points.append((xs[i],ys[i]))
 points+=[(xs[-1],ys[-1]),(5400,900),(0,900)]
 s=''.join(kept)+ '\n[node name="ContinuousTerrain" type="CollisionPolygon2D" parent="wall"]\npolygon = PackedVector2Array('+', '.join(f'{v:.2f}' for pt in points for v in pt)+')\n'
 f.write_text(s,encoding='utf-8');changes.append({'map':n,'adjustment':'旧矩形顶面连续坡化；水帘洞未迁水域伤害，水域以连通地面保证地图试玩可走通' if n==2 else '旧高台顶面保留，垂直接缝改为连续坡面','points':points})
(p/'文档/system/人间地形调整.json').write_text(json.dumps(changes,ensure_ascii=False,indent=2),encoding='utf-8')
