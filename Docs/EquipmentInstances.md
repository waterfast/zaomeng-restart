# 独立装备与宝石

`EquipmentDefinition` 是共用的静态资源；`EquipmentInstance` 是玩家拥有的某一件装备。获得两把同名武器时，`InventoryService.AddItem` 为每把创建不同的 `InstanceId`，不会堆叠。实例目前保存定义 ID 与宝石孔；强化、词条等以后按实际玩法扩展。

## 所有权与快照

一件装备只存在于一个位置：背包格子的 `Equipment`，或角色 `EquipmentLoadout.Slots` 的一个槽位。穿戴/卸下转移实例，不重新生成 ID，也不重新创建宝石列表。完整存档检查所有角色与背包的实例 ID，拒绝重复所有权。

实例是不可变 C# record，宝石孔使用 `ImmutableArray<string>`，空字符串代表空孔。镶嵌生成同 ID 的新快照，旧背包快照和事件的 Previous 数据不会被修改。事件传递的是快照，业务修改必须通过服务。

`ItemStack.ItemId` 仍用于查询定义与统计数量；装备必须 Count=1 且携带实例。普通物品按定义堆叠且不携带实例。移动装备保持原实例，不能把两把同名装备合并。`RemoveItem` 只扣堆叠物品；销毁某件装备使用 `RemoveEquipment(instanceId)`，存档编辑器也可按选中格子清除。

## 宝石规则与操作

- 装备资源的 `GemSocketCount` 配置可用孔数，默认 0，上限 8；获得实例时生成对应空孔。
- `GemDefinition` 是可堆叠的材料类物品，配置属性类型与加成；加入 `ItemCatalog` 才能获取和使用。
- 空孔镶嵌消耗一颗指定背包格子的宝石，拆卸归还同种宝石；占用孔必须先拆，不会隐式销毁原宝石。
- 满背包且无法归还宝石时拆卸失败，装备、宝石、角色属性、存档和通知都保持原状。
- 背包和当前角色上的装备都能操作，但只有穿戴实例的宝石影响角色属性。
- 当前是可配置孔数的基础实现，不还原旧版强化等级解锁孔位、宝石合成、关卡解锁或同类型限制。旧工程 `Box_1.gd/get_MaxBs` 按强化等级开放 0～4 孔，这些规则应在对应成长系统实现时接入，不伪造强化等级。

右键背包装备或穿戴槽打开基础镶嵌弹窗；选择背包宝石点击“镶嵌”，已有宝石点击“拆卸”。暂停期间也可操作。弹窗按实例追踪目标，穿戴/卸下后仍指向原来那一件；关闭背包同步关闭弹窗。说明框显示所选实例的宝石孔与总加成。

```csharp
// 背包装备或已穿戴装备的 InstanceId 均可作为目标。
GemResult result = events.GemRequested.Send(new GemRequest(
    GemAction.Socket, instance.InstanceId, socketIndex,
    gemEntry.SlotIndex, gemEntry.Definition.Id), this);

GemResult removal = events.GemRequested.Send(new GemRequest(
    GemAction.Remove, instance.InstanceId, socketIndex,
    ExpectedGemId: instance.SocketedGemIds[socketIndex]), this);
```

请求由 `GemSocketService` 处理。`GameplayEquipmentBinding` 统一刷新属性、保存并发出 `EquipmentModified`；跨穿戴和宝石通道的嵌套操作返回 Busy。`EquipmentChanged` 表示穿戴槽换装，`EquipmentModified` 表示同一实例的数据改变，二者不能混用。

## 存档与验证

存档版本已改为 2，完整保存背包与穿戴实例及每个孔位。不迁移版本 1、不删除旧文件；开发测试使用新建的独立用户目录。

`Tests/EquipmentInstanceSmokeTest.tscn` 使用独立角色与内存存档，覆盖同款装备切换、宝石消耗/归还、失败原子性、不可变快照、读档后继续换装、存档编辑器保留数据、重复实例拒绝，以及真实右键弹窗按钮。测试孔数和宝石资源仅属于测试配置，不修改现有武器数值或向玩家发测试物品。
