# 造梦重启：C# 战斗起点

使用 **Godot 4.7.2 .NET + .NET 10 SDK** 打开 `project.godot`，点击构建，再按 F6 运行 `Scenes/TestArena.tscn`，或 F5 运行项目。

这是供后续自己写代码的最小战斗起点：悟空、小猴、原第一关。没有接入旧项目的全局单例、装备、存档或数值框架。

## 操作

| 按键 | 行为 |
| --- | --- |
| A / D、左右方向键 | 移动 |
| 空格 / K | 跳跃 |
| J | 悟空第一段普攻 |
| F2 | 小猴 AI / 木桩切换 |
| R | 重载关卡，恢复双方血量和位置 |

## 从哪里开始写

1. `Scripts/Actors/CharacterActor.cs`：共享角色，负责血量、动作状态、动画切换和受击。
2. `Scripts/Actors/Player.cs`：玩家输入。增加自己的操作优先从这里开始。
3. `Scripts/Actors/Monster.cs`：简单追踪与攻击 AI，可替换成自己的怪物逻辑。
4. `Scripts/Actors/CharacterMotor.cs`：输入移动、重力、外部击退；击退独立衰减。
5. `Scripts/Combat/HitBox.cs` / `HurtBox.cs`：区域命中，每次攻击对同一目标只结算一次。
6. `Scripts/Combat/CombatResolver.cs`：统一阵营检查、伤害与击退方向入口。
7. `Content/PlayerHit.tres` / `MonsterHit.tres`：在检查器修改伤害、击退、硬直。

类关系为 `CharacterBody2D → CharacterActor → Player / Monster`。目前只有 `Free / Attacking / Hurt / Dead` 四种动作状态；不提前拆 Buff、被动、技能控制器等尚未用到的系统。

## 编辑动作

打开 `Scenes/Actors/Player.tscn` 的 `AnimationPlayer`。身体、装备、特效、攻击框位置及 `Facing/HitBox:Active` 都由同一时间轴驱动。攻击开始只是播放 `hit1`，结束依靠动画完成信号；没有额外的攻击时长计时器，也不生成临时角色。

悟空 `hit1` 保留原轨道的帧序列和攻击框，已把原来的 3 倍速烘焙为实际秒数：

- 动作总长 0.35 秒。
- 0.033333 秒开启攻击，0.166667 秒关闭。
- 击退为 `(60, 0)`，对应原 `hurtBack=[2,0]` 的横向换算；这里是速度，不是后退 60 像素。
- 默认伤害与摩擦、移动速度是测试值，不宣称复刻旧版完整数值公式。

小猴攻击仍使用原 0.4 秒时间轴，在 0.3 秒开启命中。角色坐标以脚底为原点，原图默认朝左，`Facing` 同时翻转表现和攻击区域。每个角色始终只有一套身体。受击会关闭当前攻击，播放 hurt，并保留击退速度；死亡保留最后姿态，按 R 重置。

## 素材与地图

- `Assets/Art/`：从原项目压缩缓存恢复的 **2256 张 PNG**，包含 HeroPicture、Monster、Level、Game_2、Game_3、AddEffect、AllTX、Bullet、Skill、StrikeSpecialEffects、Trap、zhenfa、MagicWeapon。约 501 MiB，不依赖同级旧工程即可使用。
- `Scenes/Maps/Forest.tscn`：可运行的第一关，提取原背景、前景、地面与斜坡碰撞。移除了依赖旧刷怪流程的 stop 障碍，增加右边界；未迁移掉落、出口、波次管理。
- `Reference/OriginalScenes/`：**241 个原始场景参考文件**，保存原人物、怪物、技能、弹道、地图及基类的动画编排。该目录用 `.gdignore` 隔离，保留原引用，**不能直接当新项目场景运行**。其余地图尚未逐个解除旧逻辑依赖。
- `Tools/texture_manifest.json`：每张图的原始路径、压缩源与新路径对应表。
- `Tools/recovery_report.json`：纹理恢复结果。

技能贴图和原场景编排已保留，当前运行版本只接入第一段普攻；连击、技能施放、弹道、音效、Buff、装备等留待你逐步写。迁移素材的使用范围和归属沿用原项目及原素材权利方要求。

## 验证与迁移工具

`dotnet build` 编译运行代码。使用 Godot .NET 可执行文件运行：

```text
Godot --headless --path . -- --smoke-test
```

测试直接运行真实关卡与物理帧，检查地面、攻击时长、重复命中、再次攻击、左右朝向、击退、受击打断、阵营、怪物攻击、跳跃落地和死亡。成功输出 `COMBAT_SMOKE_TEST: PASS` 并退出。去掉 `--headless` 会额外保存 `Tests/combat-preview.png` 供视觉检查。

`Tools/` 中 Python/GDScript 仅是一次性素材恢复工具，游戏运行代码全为 C#。`build_slice.py` 会覆盖生成的角色和地图场景，手工编辑场景后不要随意重跑。新角色可以复制现有角色场景，更换贴图/动画及攻击资源，并沿用 C# 基类。
