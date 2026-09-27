using Godot;

namespace Zaomeng.Items;

/// <summary>一种物品的静态定义。背包只保存 Id 和数量，不复制这里的展示与属性数据。</summary>
[GlobalClass]
[Tool]
public partial class ItemDefinition : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public ItemCategory Category { get; set; } = ItemCategory.Equipment;
	[Export(PropertyHint.Range, "1,9999,1")] public int MaxStack { get; set; } = 1;
	[Export] public Texture2D? Icon { get; set; }
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	[Export] public int Attack { get; set; }
	[Export] public int CriticalChance { get; set; }
}
