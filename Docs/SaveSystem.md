# 存档系统

`Scripts/Save` 保存跨关卡持续存在的数据：角色档案与成长、共享货币、背包、独立装备与已解锁关卡。关卡不做中途存档；每次进入关卡都重新放置角色并初始化当前血量、朝向和敌人。`GameSaveData` 不含场景路径、玩家坐标、当前血量或关卡中途状态。物品名称、图标、角色动作等静态定义和临时状态也不进入存档。

## 使用方式

主菜单存档格和前三关入口已通过 `GameSession` 调用，关卡切换时保存持久数据；不保存关卡中途状态。底层也可独立调用：

```csharp
using Godot;
using Zaomeng.Save;
using Zaomeng.Save.Serialization;
using Zaomeng.Save.Storage;

string directory = ProjectSettings.GlobalizePath("user://saves");
var manager = new SaveManager(new JsonSaveSerializer(), new PlainFileSaveStorage(directory));
GameSaveData save = new();
// 初次创建时给 save.Characters 添加角色；双人角色共用 save.Wallet。
save = InventorySaveMapper.Capture(inventory, save);
manager.Save(1, save);

GameSaveData data = manager.Load(1);
InventorySaveMapper.Restore(data, inventory);
// 角色成长和货币由各自系统从 data.Characters / data.Wallet 恢复。
// 关卡角色的位置、当前血量、敌人和机关均由新进入的关卡初始化。
```

开发时 `PlainFileSaveStorage` 写 `save_01.json`。正式版可以传入 `EncryptedFileSaveStorage(directory, masterKey)`，写 `save_01.dat`；`masterKey` 必须是稳定的 32 字节密钥，由未来的应用入口提供。同一存档必须用同一密钥读取。密钥在单机客户端中无法完全保密，加密的目的是提高随手改档的门槛。不要用设备 ID 或版本号派生密钥，否则换设备或升级后可能无法读档。

## 文件安全与版本

写入先落到 `.tmp`，刷新并校验，再替换正式文件；替换前的有效文件保留为 `.bak`。读取主文件失败时尝试备份。密文采用 AES-256-CBC 和 HMAC-SHA256；每次写入生成新 IV，读取时先验签再解密。JSON 的 `version` 目前为 2，背包和穿戴槽保存独立装备实例及宝石孔。按开发期约定，不兼容或迁移版本 1；旧存档文件不会自动删除，其他版本明确报错。

恢复背包时检查槽位数量、物品定义、堆叠上限、装备实例及宝石孔配置，失败不会部分修改背包。整个存档检查实例 ID 唯一，拒绝同一装备同时出现在背包、多个穿戴槽或不同角色上。角色档案也检查唯一 ID、技能等级和快捷栏引用；货币余额不能为负。

逻辑验证：`dotnet run --project Tests/SaveLogic/SaveLogic.csproj`。
