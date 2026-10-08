"""读取显式内容资源更新迁移记录，同时清理地图掉落中的其他职业装备。"""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[1]
removed_tables = []
for path in (root / 'Content').rglob('*.tres'):
    text = path.read_text(encoding='utf-8')
    if 'MonsterDropEntry.cs' not in text: continue
    discarded = []
    for entry in re.finditer(r'\[sub_resource[^\n]+\bid="([^"]+)"\](.*?)(?=\n\[|\Z)', text, re.S):
        item = re.search(r'ItemId = "([^"]+)"', entry[2])
        if item is None: continue
        item_path = root / f'Content/Items/{item[1]}.tres'
        if not item_path.exists(): continue
        definition = item_path.read_text(encoding='utf-8')
        owner = re.search(r'RequiredCharacterId = "([^"]+)"', definition)
        if owner and owner[1] not in ['role_1', 'role_2']:
            discarded.append((entry[1], item[1], entry[0]))
    for key, item_id, block in discarded:
        text = text.replace(block, '')
        text = re.sub(rf'SubResource\(\s*"{re.escape(key)}"\s*\),?\s*', '', text)
        text = text.replace(', ])', '])')
        removed_tables.append({'resource': str(path.relative_to(root)), 'item': item_id})
    if discarded: path.write_text(text, encoding='utf-8')

report_path = root / '文档/system/后六关迁移清单.json'
report = json.loads(report_path.read_text(encoding='utf-8'))
report['monsters'] = []
mechanics = {8: '石像召唤、护盾、红色弱点五秒失效', 11: '毒弹、三段震地、毒与眩晕Buff', 12: '地面/飞行喷火、俯冲、灼烧、七秒化卵复生', 17: '小怪/首领独立数值、旋风冲撞', 18: '弹体、五段地爪、减速与眩晕', 20: '七成血量形态变化、伴生鲨鱼、血流与眩晕'}
for number in range(7, 21):
    text = (root / f'Content/Monsters/Human/monster_{number}.tres').read_text(encoding='utf-8')
    old = root.parent / f'造梦八荒-(4.1)/Script/Monster/Monster_{number}.gd'
    script = old.read_text(encoding='utf-8')
    source_drops = re.search(r'self.fall_list\s*=\s*\[(.*?)\]\s', script, re.S)
    original = re.findall(r'"name":\s*"([^"]+)"', source_drops[1]) if source_drops else []
    drops = re.findall(r'ItemId = "([^"]+)"', text)
    if number == 17: drops = ['xqzy']
    report['monsters'].append({
        'number': number, 'name': re.search(r'DisplayName = "([^"]+)"', text)[1],
        'source_scene': f'Scene/Monster/Monster_{number}.tscn',
        'source_script': f'Script/Monster/Monster_{number}.gd',
        'drops': drops, 'removed_other_roles': [item for item in original if item not in drops],
        'implemented': mechanics.get(number, '旧帧动画、独立攻击配置；飞行小怪通过飞行演员实现' if number in [7, 13, 19] else '旧帧动画、独立攻击配置'),
    })
report['equipment_policy'] = '所有现有地图掉落只保留悟空、唐僧或通用；物品目录暂不删除其他职业资源。'
report['additional_filtered_tables'] = removed_tables
report['verification'] = ['六关四波次与最终首领配置', 'Buff刷新去重与装备穿脱', '食品无玩家不消耗', '烈焰风暴四段与打断', '地刺实际矩形区域与周期', '石像护盾与鹏魔王化卵池复用', '六关连续移动清场门禁出口与解锁', '六关原生渲染首领脚点']
report['limits'] = ['地形巡检通过伤害清场隔离路线验证，不代表低等级角色平衡通关。', '飞行冲撞重新实现为锁定位置俯冲，未复制旧版四次瞬移冲撞。', '石像弱点固定在首领前方，未复制旧版随机位置弱点。', '节日食品只接入通用接口，未配置具体节日数值。']
report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print('已更新六关报告；移除其他职业地图掉落', len(removed_tables))
