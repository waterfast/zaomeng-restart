# 造梦重启

《造梦八荒》旧工程的 Godot/C# 重构。使用 **Godot 4.7.2 .NET + .NET 10 SDK** 打开 `project.godot`，构建后按 F5 进入主菜单；F6 可运行当前选中的独立场景。

## 当前可用内容

主菜单 → 存档格 → 选择悟空/唐僧 → 主地图。花果山已迁入整关地图，4波小怪后挑战大猩猩；清场后出现旧通关光圈，靠近按W进入结算。死亡完整播放角色效果后进入失败页；结算支持重试和返回地图。水帘洞、桃花源保留现有试玩入口，尚未按花果山标准完整迁移。

悟空已登记9个主动和4个被动；唐僧提供冰龙波、玄冰阵、天降甘露、九环圣经及3个通用被动。技能学习、逐级升级、改键、当前数值提示和快捷槽接入同一资源目录。背包支持装备、宝石、使用、出售/分解、白装批量出售；法宝独立详情支持浏览，穿戴仍在背包操作；地煞葫芦、天煞古剑、紫金葫芦已接基础主动。主地图任务入口当前提供10级成长礼包。

当前没有完整法宝主动行为、五行成长、日常/击杀/收集任务、唐僧全技能、分身联动及筋斗云两段机制。详见[本次整合记录](文档/system/本次整合记录.md)。工程处于开发期，不要求兼容旧过渡接口或存档。

## 操作

| 操作 | 行为 |
| --- | --- |
| A/D、左右方向键 | 移动 |
| K | 跳跃 |
| 空格 | 满无双时开启无双 |
| J | 普通攻击 |
| Y/U/I/O/L | 默认五个已学主动技能槽，可在技能面板改键 |
| C、左下背包入口 | 背包与暂停 |
| B、左下法宝入口 | 独立法宝详情与暂停 |
| H | 使用当前法宝基础主动 |
| Esc、左下设置入口 | 局内设置，继续或返回地图 |
| 主地图“学习技能”/局内技能入口 | 学习、升级、装配与按键设置 |
| 主地图“任务” | 查看任务条件与领取奖励 |
| 通关光圈内新按W | 胜利结算；长按重复和范围外按键不触发 |

`Scenes/TestArena.tscn` 是开发测试大厅，R/F2等调试键只用于该场景。实际关卡死亡后使用结算重试。直接运行玩法场景会准备会话存档；自动测试使用独立临时数据。

## 开发入口

- [系统架构索引](文档/system/README.md)：各系统职责、状态归属、调用链和失败处理。
- [内容注册与调用指南](文档/system/内容注册与调用指南.md)：新增等级任务、武器触发技能、角色主动/被动技能及可复制调用示例。
- [本次整合记录](文档/system/本次整合记录.md)：当前功能、尚未实现范围和整合验证。

先设计对应系统，再写代码；内容优先在Inspector编辑原生`.tres`并显式加入目录。新行为使用独立组件，不在Player/UI里扩充具体角色、技能或武器Id分支。具体要求见[AGENTS.md](AGENTS.md)。

## 目录

| 目录 | 内容 |
| --- | --- |
| Scripts | C#运行逻辑、服务、注册资源类型和UI绑定 |
| Content/Skills | 角色技能目录、动作、成长和特殊行为引用 |
| Content/Items | 物品/装备目录与定义 |
| Content/GameData | 属性注册、装备授予技能 |
| Content/Quests | 任务目录、条件与奖励 |
| Scenes | 当前可运行角色、地图、UI与测试大厅 |
| Assets/Art、Assets/Font | 当前场景引用的迁移美术与字体 |
| Tests | 独立Godot功能场景、纯C#背包和存档测试 |
| Reference/OriginalScenes | 旧场景参考，.gdignore隔离，不能当运行场景 |
| Tools | 一次性恢复/迁移工具，不是游戏运行时注册器 |

旧资源来自同级 `造梦八荒-(4.1)`，缺失图片/字体核对原引用后从 `zmbh-rebuild/assets` 补全。运行资源已放在本仓库；不把旧GDScript接回当前场景。素材使用范围与归属沿用原项目和原素材权利方。

## 验证

Boss 平地演示：在 Godot 中打开 `Tests/BossAnimationShowcase.tscn`，按 **F6** 运行。自动依次展示当前三只 Boss 的普攻与技能（左右共 12 项），支持上一项、下一项、重播、暂停与慢放，不读写存档。

自动验证：`Godot控制台程序 --headless --path . res://Tests/BossAnimationShowcase.tscn -- --verify`。可见窗口运行时增加 `--capture-boss`，将起招、命中、后半段及收招画面保存到 `.godot/boss-checks`。

```text
dotnet build
dotnet run --project Tests/InventoryLogic/InventoryLogic.csproj
dotnet run --project Tests/SaveLogic/SaveLogic.csproj
Godot控制台程序 --headless --path . res://Tests/GameplayUpgradeSmokeTest.tscn
Godot控制台程序 --headless --path . res://Tests/ContentSystemsSmokeTest.tscn
```

技能注册、装备技能、死亡结算等独立场景与选择方法见[调用指南的检查与调试](文档/system/内容注册与调用指南.md)。非headless运行GameplayUpgradeSmokeTest并附 `-- --capture-gameplay` 可保存画面到`.godot/gameplay-checks`。

不要随意重跑 `Tools/build_slice.py` 或一次性迁移脚本覆盖手工调整的场景/策划数值；今后以现行资源与系统文档为编辑入口。`.godot`、bin/obj、调试截图和Python缓存不作为内容提交。

条件被动验证：运行 `Tests/PassiveEffectsSmokeTest.tscn`。Boss 平地巡检新增“普通 / 低血量”切换；命令行 `--verify --capture-boss --rage-boss` 可验证强化动作并保存带 `rage-` 前缀的关键帧。参数与扩展方式见 `文档/system/战斗被动效果系统.md` 和内容注册与调用指南。

无双已接入：空格开启、K跳跃，满值保留；技能/法宝接触获取按旧版区间，无双中伤害与移动1.5倍并具有霸体。验证入口为 `Tests/WushuangSmokeTest.tscn`；数值、时装条件Buff扩展和旧整数衰减说明见[无双系统](文档/system/无双系统.md)。
