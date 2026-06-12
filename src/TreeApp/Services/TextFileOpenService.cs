using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace PathOfAvalonia.TreeApp.Services;

public sealed record TextFileOpenRequest(
    string Title,
    string SuggestedStartDirectory,
    IReadOnlyList<FilePickerFileType> FileTypeChoices);

public interface ITextFileOpenService
{
    Task<TextFileOpenResult?> OpenAsync(
        IStorageProvider storageProvider,
        TextFileOpenRequest request,
        CancellationToken cancellationToken);
}

public sealed record TextFileOpenResult(IStorageFile File, string Contents);

public sealed class TextFileOpenService : ITextFileOpenService
{
    public async Task<TextFileOpenResult?> OpenAsync(
        IStorageProvider storageProvider,
        TextFileOpenRequest request,
        CancellationToken cancellationToken)
    {
        var startFolder = await TryGetStartFolderAsync(storageProvider, request.SuggestedStartDirectory);
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = request.Title,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = request.FileTypeChoices.ToArray(),
            AllowMultiple = false,
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        var contents = await reader.ReadToEndAsync(cancellationToken);
        return new TextFileOpenResult(file, contents);
    }

    private static async Task<IStorageFolder?> TryGetStartFolderAsync(
        IStorageProvider storageProvider,
        string startPath)
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
