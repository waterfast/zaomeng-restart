"""按旧场景真实引用迁入页面；已有背包资源保持当前实现。"""
from pathlib import Path
import re
import shutil
import json

ROOT = Path(__file__).resolve().parents[1]
OLD = ROOT.parent / '造梦八荒-(4.1)'
ASSETS = ROOT.parent / 'zmbh-rebuild/assets'
manifest = ROOT / 'Tools/shop_alchemy_textures.json'
recover = json.loads(manifest.read_text(encoding='utf-8')) if manifest.exists() else []

def migrate(relative):
    target = ROOT / relative.replace('Scene/', 'Scenes/UI/', 1)
    if target.exists():
        return
    source = OLD / relative
    text = source.read_text(encoding='utf-8-sig')
    text = re.sub(r'^\[ext_resource type="Script".*\n', '', text, flags=re.M)
    text = re.sub(r'^\[connection .*\n?', '', text, flags=re.M)
    text = re.sub(r'^script = .*\n', '', text, flags=re.M)
    text = re.sub(r' uid="[^"]+"| load_steps=\d+', '', text)
    for kind, path in re.findall(r'\[ext_resource type="([^"]+)"[^\n]*?path="res://([^"]+)"', text):
        if kind == 'PackedScene':
            migrate(path)
            new = path.replace('Scene/', 'Scenes/UI/', 1)
        else:
            new = 'Assets/' + path
            dest = ROOT / new
            if not dest.exists():
                candidates = [OLD / path, ASSETS / path, ASSETS / new]
                found = next((p for p in candidates if p.exists()), None)
                if found is None:
                    imported = (OLD / (path + '.import')).read_text(encoding='utf-8')
                    cache = re.search(r'path="res://([^"]+\.ctex)"', imported)[1]
                    recover.append({'source': (OLD / cache).as_posix(), 'destination': 'res://' + new})
                else:
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(found, dest)
        text = text.replace('res://' + path, 'res://' + new)
    # 脚本导出字段是旧逻辑状态，纯 UI 不保留。
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding='utf-8')

for scene in ['Shop/SHOP', 'Shop/Shop_item', 'LDL/ldl', 'LDL/strength',
              'LDL/Synthesis', 'LDL/Mosaic', 'LDL/Decompose', 'LDL/Forge',
              'OtherScene/Transmogrified']:
    if (OLD / f'Scene/{scene}.tscn').exists():
        migrate(f'Scene/{scene}.tscn')
manifest.write_text(json.dumps(recover, ensure_ascii=False, indent=2), encoding='utf-8')
