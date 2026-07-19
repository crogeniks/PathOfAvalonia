using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace PathOfAvalonia.TreeApp.Services;

internal static class StorageStartFolderResolver
{
    public static async Task<IStorageFolder?> TryGetAsync(IStorageProvider storageProvider, string startPath)
    {
        try
        {
            Directory.CreateDirectory(startPath);
            return await storageProvider.TryGetFolderFromPathAsync(startPath);
        }
        catch
        {
            return null;
        }
    }
}
