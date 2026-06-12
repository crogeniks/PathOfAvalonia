using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.Services;

public interface IBuildPlannerImportService
{
    Task<BuildPlannerImportFileResult?> ImportAsync(
        IStorageProvider storageProvider,
        TreeModel tree,
        CancellationToken cancellationToken);
}

public sealed record BuildPlannerImportFileResult(string Name, ImportedBuild Build, int SkippedPassiveCount);

public sealed class BuildPlannerImportService(
    IBuildPlannerPathService buildPlannerPaths,
    ITextFileOpenService files) : IBuildPlannerImportService
{
    private static readonly FilePickerFileType BuildFileType = new("Path of Exile 2 Build")
    {
        Patterns = ["*.build"],
        MimeTypes = ["application/json"],
    };

    public async Task<BuildPlannerImportFileResult?> ImportAsync(
        IStorageProvider storageProvider,
        TreeModel tree,
        CancellationToken cancellationToken)
    {
        var file = await files.OpenAsync(
            storageProvider,
            new TextFileOpenRequest(
                "Import Path of Exile 2 build",
                buildPlannerPaths.CurrentDirectory,
                [BuildFileType]),
            cancellationToken);
        if (file is null)
        {
            return null;
        }

        if (file.File.Path.IsFile)
        {
            var directory = Path.GetDirectoryName(file.File.Path.LocalPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                buildPlannerPaths.RememberDirectory(directory);
            }
        }

        var result = Poe2BuildPlannerImporter.Import(file.Contents, tree);
        return new BuildPlannerImportFileResult(file.File.Name, result.Build, result.SkippedPassiveIds.Count);
    }
}
