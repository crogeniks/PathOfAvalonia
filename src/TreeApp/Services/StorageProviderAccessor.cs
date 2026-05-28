using Avalonia.Platform.Storage;

namespace PathOfAvalonia.TreeApp.Services;

public interface IStorageProviderAccessor
{
    IStorageProvider? StorageProvider { get; set; }
}

public sealed class StorageProviderAccessor : IStorageProviderAccessor
{
    public IStorageProvider? StorageProvider { get; set; }
}
