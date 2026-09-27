namespace Zaomeng.Character;

/// <summary>只保存已装备物品的 ID；数值、图标和效果由物品定义提供。</summary>
public sealed class EquipmentLoadout
{
	public string WeaponId { get; set; } = "";
	public string ArmorId { get; set; } = "";
	public string AccessoryId { get; set; } = "";
	public string WingId { get; set; } = "";
	public string TitleId { get; set; } = "";
	public string CostumeId { get; set; } = "";
	public string MagicWeaponId { get; set; } = "";
}
