using Zaomeng.Save;

namespace Zaomeng.UI.MainMenu;

public static class WorldMapAccess
{
	public static bool CanEnter(WorldMapDefinition destination, GameSaveData data)
		=> destination.AllowDevelopmentAccess || destination.RequiredBossId.Length == 0 ||
			data.DefeatedBossIds.Contains(destination.RequiredBossId);
}
