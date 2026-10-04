# 玩家资源与战斗显示

- `Player` 管理当前 MP、技能消耗与冷却、经验奖励和升级；`CharacterProgression` 提供旧版成长与经验门槛，`CharacterStatCalculator` 汇总装备及永久加成。HUD 不计算游戏规则。
- 悟空基础 HP / MP 为 80 / 50，每级增加 50 / 15。其他角色沿用已有 `CharacterProgression` 的旧版公式。
- 技能蓝耗为 `基础消耗 + (等级 - 1) × 线性增长 + (等级 - 1)² × 二次增长`。只有动作成功起手才扣蓝并开始冷却；被打断不退蓝、不清冷却。游戏暂停时冷却和自然恢复一起暂停。
- 自然恢复每秒结算一次，基础回魔为 0，最终回魔读取装备和永久加成。刷新装备只截断超过新上限的资源，不补满。
- 1～19 级按原版表计算升级经验；20 级起为 `5000 + 5000 × (等级 - 19)`。沿用旧版 `CureLevel`：每次奖励至多升一级，升级清空本级经验，最高 55 级；升级不补满 HP / MP。
- 普通小猴、猴兵、大猩猩分别奖励 1 / 5 / 10 经验，与旧脚本一致。`CombatResolver` 在玩家致死命中后调用 `MonsterRewardSpawner.Award`，由玩家 `GainExperience` 更新经验和等级。尸体或离屏回收不奖励。关卡与测试场景监听玩家成长变化并通过现有存档系统保存。
- 灵魂沿用旧版 `set_gxp`：每只怪产生 3 个光球，每球的 `fall_coin` 分别为 1 / 2 / 2，所以全部吸收后共获得 3 / 6 / 6 灵魂。`SoulPickup` 使用旧版 `AuraPurple` 动画，先上浮显现、0.6 秒后加速追踪玩家；吸收时由玩家发出 `SoulsCollected`，关卡写入共享 `Wallet` 并保存，掉落时不立即入账。
- `PlayerHud` 绑定旧 HUD 的头像、HP / MP / EXP 及等级；背包魔法属性也显示当前 MP。
- `CombatTextSpawner` 显示物理、法术、真实伤害、暴击、闪避及升级提示。伤害与闪避直接使用恢复的原版 PNG 和迁入的 `DamageText.tscn` / `miss_effect.tscn` 动画轨道，C# 仅负责组合数字和起播。普通武力按当前要求采用旧版红色 `monster/physics` 数字，魔法采用蓝色 `magic`。保留旧版目标差异：怪物受暴击使用 `physicscrit` / `magiccrit` 独立大号数字及普通上浮动画，玩家受暴击使用普通数字及彩色抖动动画。
- 原版 PNG 缺失，但 `.png.import` 仍指向旧工程 `godot/imported` 的 `.ctex`。按每个导入记录的准确路径恢复 PNG，未按同名缓存猜测。原版数字美术已放入 `Assets/Art/AllNumber`。
- 玩家与小怪的头顶血条已从场景删除，HP 仅由 HUD 显示。小怪死亡动画会把身体透明度降到 0，`Monster.ResetForSpawn` 在对象池复用时恢复透明度，避免出现能战斗却看不见的小怪。

独立验证场景：`res://Tests/PlayerResourcesSmokeTest.tscn`，不使用真实存档，覆盖技能资源、经验成长、奖励去重、HUD 与飘字寿命。原有战斗测试为位移测试补充法力并等待实际冷却。

`res://Tests/CombatFeedbackSmokeTest.tscn` 验证数字分类和原版动画关键帧、三类小怪帧图有效性、死亡回收后的可见性、三光球奖励及吸收去重。
