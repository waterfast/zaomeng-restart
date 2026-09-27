namespace Zaomeng.Save.Serialization;

public interface ISaveSerializer
{
	byte[] Serialize(GameSaveData data);
	GameSaveData Deserialize(byte[] bytes);
}
