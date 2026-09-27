namespace Zaomeng.Save.Storage;

/// <summary>开发时使用，可直接查看 JSON。</summary>
public sealed class PlainFileSaveStorage(string directory) : FileSaveStorage(directory, "json");
