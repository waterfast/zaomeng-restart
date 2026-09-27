namespace Zaomeng.Save.Storage;

public interface ISaveStorage
{
	void Write(int slot, byte[] data, bool preserveBackup = false);
	byte[] Read(int slot);
	byte[] ReadBackup(int slot);
}
