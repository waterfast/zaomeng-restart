# 动作位移实现说明

> 2026-09-26。对应实施前的 [技能系统评估](SkillSystemReview.md)。本文写清楚这次实际修改的位置、配置含义和暂留事项，方便继续调手感。

## 一次动作如何移动

```text
Player 读取方向键、普攻键或技能键
    ↓
CharacterActor 选中 AttackStep / SkillDefinition，记录起招速度、面向和动画
    ↓
ActionMotionPlayback 根据动画当前帧算出本帧动作水平速度
    ↓
CharacterMotor 合并受击击退，施加重力，调用 MoveAndSlide 处理碰撞
```

`ActionMotion` 是可在 Godot 检查器里编辑的配置资源；`ActionMotionPlayback` 只保存**本次**动作的起招状态。角色本体坐标始终由 `CharacterMotor` 移动，特效动画和命中判定各走原有流程。角色受击、动画结束或技能被外部动画打断时，动作位移会清除。

## 配置字段

| 字段 | 含义 |
| --- | --- |
| `Mode = Stop` | 水平速度为 0；动作未填写 `Motion` 时也是这个效果 |
| `Mode = CarryMomentum` | 使用起招前的水平速度；`MomentumDeceleration = 0` 时保持到动作结束 |
| `Mode = FacingDash` | 用起招时的面向乘以正的 `Speed`；攻击中按反方向不会突然掉头 |
| `StartFrame`、`EndFrame` | 生效的帧区间，含开始帧、不含结束帧；`EndFrame = -1` 持续到动画结束 |
| `FramesPerSecond` | 在 `AttackStep` 或 `SkillDefinition` 上，和动作动画的帧率对应；时间轴变速时仍读取动画当前位置 |
| `Motion`、`AirMotion` | 地面规则与可选空中覆盖；不填 `AirMotion` 就沿用 `Motion` |

起招时只缓存角色自身的水平速度，不把尚未衰减完的受击击退计入“惯性”。受击击退存在时先执行击退，不叠加动作冲刺。自由移动仍由输入速度决定；跳跃、重力和竖直速度保留原有实现。动作暂停或动画切走时，位移输出为 0。

## 目前悟空的动作配置

配置位置：[玩家场景](../Scenes/Actors/Player.tscn)。数值取旧版行为作为第一轮手感基准，均可在检查器里单独调整。

| 动作 | 地面位移 | 空中位移 |
| --- | --- | --- |
| `hit1` | 保留起招 X 速度 | 同地面 |
| `hit2`～`hit4` | 停止 | 停止 |
| `lys` 烈焰闪 | 起招方向 1000 像素/秒，持续到动画结束 | 同地面 |
| `jdy`、`qsez` | 起招方向 1000 像素/秒，持续到动画结束 | 同地面 |
| `lyfb` 烈焰风暴 | 停止 | 保留起招 X 速度 |
| `slz` | 保留起招 X 速度 | 同地面 |

`hytj` 在旧版是 570 像素/秒的冲刺，但当前五个装备槽中没有它的 `SkillDefinition`，所以本次没有制造一个不被使用的配置。将来装备这项技能时，给它添加 `FacingDash`、`Speed = 570` 即可。旧版 `jdy` 还会单独处理竖直速度；这次只实现了文档规划的**水平位移**，没有把竖直特例混入公共规则。

## 代码分工

- [`ActionMotion.cs`](../Scripts/Combat/ActionMotion.cs)：存储位移模式、帧区间、速度，并检查配置有效性。
- [`ActionMotionPlayback.cs`](../Scripts/Combat/ActionMotionPlayback.cs)：固定本次动作的起招面向和速度，按动画时间给出水平速度。
- [`AttackStep.cs`](../Scripts/Combat/AttackStep.cs)、[`SkillDefinition.cs`](../Scripts/Combat/SkillDefinition.cs)：只引用位移配置；仍分别负责普攻段和技能数据。
- [`CharacterActor.cs`](../Scripts/Actors/CharacterActor.cs)：选择地面/空中规则，管理动作开始、中断、结束；把本帧动作速度交给移动组件。
- [`Player.cs`](../Scripts/Actors/Player.cs)：每帧采样水平输入；只有可自由控制时才据此转向和跳跃。
- [`CharacterMotor.cs`](../Scripts/Actors/CharacterMotor.cs)：仍是角色速度与碰撞移动的唯一执行位置，本次无需改动。

## 验证和暂留事项

编译通过，Godot 无界面战斗测试通过。新增测试覆盖实际奔跑后起普攻、跳跃中起普攻、烈焰闪左右冲刺、暂停动画停止冲刺、受击取消冲刺、动画结束停止，以及空中烈焰风暴保留惯性。本体火焰的显示、逐帧推进和受击隐藏也一起验证。

目前技能命中起止帧仍未在玩家场景配置；`SkillDefinition` 会提示“只播放动画，不产生攻击命中”。烈焰风暴分身、多波重复命中和技能伤害数值仍需单独确定。旧版数值只是初始速度，实际位移距离受动画时长和地图碰撞影响，手感可继续在 Godot 中调整。
