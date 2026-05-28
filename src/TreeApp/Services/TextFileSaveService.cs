using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace PathOfAvalonia.TreeApp.Services;

public sealed record TextFileSaveRequest(
    string Title,
    string SuggestedStartDirectory,
    string SuggestedFileName,
    string DefaultExtension,
    IReadOnlyList<FilePickerFileType> FileTypeChoices,
    string Contents);

public interface ITextFileSaveService
{
    Task<IStorageFile?> SaveAsync(
        IStorageProvider storageProvider,
        TextFileSaveRequest request,
        CancellationToken cancellationToken);
}

public sealed class TextFileSaveService : ITextFileSaveService
{
    public async Task<IStorageFile?> SaveAsync(
        IStorageProvider storageProvider,
        TextFileSaveRequest request,
        CancellationToken cancellationToken)
    {
        var startFolder = await TryGetStartFolderAsync(storageProvider, request.SuggestedStartDirectory);
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = request.Title,
            SuggestedStartLocation = startFolder,
            SuggestedFileName = request.SuggestedFileName,
            DefaultExtension = request.DefaultExtension,
            FileTypeChoices = request.FileTypeChoices.ToArray(),
            ShowOverwritePrompt = true,
        });
        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        var bytes = Encoding.UTF8.GetBytes(request.Contents);
        await stream.WriteAsync(bytes, cancellationToken);

        return file;
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
