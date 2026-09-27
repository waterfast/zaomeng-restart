# 物品系统设计

本设计沿用重构规划中的划分：`ItemDefinition` 描述一种物品，`Inventory` 记录角色拥有什么，后续 `Equipment` 记录穿戴关系。定义使用 Godot C# 自定义 `Resource`，保存在 `Content/Items/*.tres`；图片只留在 `Assets/Art`。游戏规则不依赖背包 UI。

## 当前数据流

`Content/Items/ItemCatalog.tres` 显式引用本次迁入的六个物品定义。TestArena 和前三关在检查器中引用目录资源，进入场景时验证 ID 唯一及堆叠上限，再从当前存档恢复背包。背包槽位仅保存 `(ItemId, Count)`；`InventoryViewAdapter` 用目录解析名称、图标和描述，`LegacyBackpackView` 将数据填入旧版背包场景。独立的 InventoryDemo 仍使用 `InventorySample` 和 `BackpackView`。因此调整物品名称、图标或数值时，只改对应 `.tres`，无需改测试脚本或 UI。

目录引用采用显式列表，避免运行时扫描文件夹和根据 ID 拼路径。增加测试物品时，在 `Content/Items` 新建 `ItemDefinition` 资源并加入 `ItemCatalog.tres` 的 `Definitions`；两个测试场景会使用同一份内容。`ItemDefinition` 的 ID 是稳定标识，显示名称可以调整，但已有存档引用的 ID 不应随意更改。

## 职责边界

| 模块 | 职责 |
| --- | --- |
| `ItemDefinition` | 静态 ID、分类、堆叠上限、名称、图标、描述和当前演示所需属性 |
| `ItemCatalog` | 物品定义的引用清单、按 ID 查询、基础数据校验 |
| `InventoryService` | 槽位容量、增减、拆堆与合堆；操作失败不改变任何槽位 |
| `InventoryViewAdapter` | 把槽位中的 ID 和数量转成 UI 展示数据 |
| `LegacyBackpackView` / `BackpackView` | 分类、分页、选择和展示，不保存游戏状态 |

当前六件武器为不可堆叠装备，攻击与暴击数值只作静态定义。TestArena 和前三关已恢复存档背包并展示旧版角色面板，但没有实现穿戴、使用、出售、强化或实际战斗属性加成，不能把预览数值当作已生效装备。

## 后续扩展规则

当装备操作接入时，单独建立装备实例和穿戴槽。装备实例只保存定义 ID、唯一实例 ID，以及已确定的随机值、强化、五行、宝石等可变数据；图标、名称和基础配置继续由定义提供。随机属性在创建实例时掷一次并保存，读取资源或打开 UI 不重新掷值。穿戴槽引用实例 ID，装备效果交给属性、Buff 或技能模块处理，卸下时按同一来源撤销。存档保存实例与穿戴关系，并在恢复时校验定义和实例引用。

材料、消耗品仍可沿用 `ItemDefinition` 与堆叠规则；真正需要使用效果时，再引入对应行为数据和执行入口。不要把使用、穿戴、出售逻辑放进 UI，也不要把静态定义复制进每个背包槽位。
