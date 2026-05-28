using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Export;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.Services;

public interface IBuildPlannerExportService
{
    Task<BuildPlannerExportFileResult?> ExportAsync(
        IStorageProvider storageProvider,
        TreeModel tree,
        ClassCatalog classes,
        ImportedBuild build,
        CancellationToken cancellationToken);
}

public sealed record BuildPlannerExportFileResult(string Name, int SkippedNodeCount);

public sealed class BuildPlannerExportService(
    IBuildPlannerPathService buildPlannerPaths,
    ITextFileSaveService files) : IBuildPlannerExportService
{
    private static readonly FilePickerFileType BuildFileType = new("Path of Exile 2 Build")
    {
        Patterns = ["*.build"],
        MimeTypes = ["application/json"],
    };

    public async Task<BuildPlannerExportFileResult?> ExportAsync(
        IStorageProvider storageProvider,
        TreeModel tree,
        ClassCatalog classes,
        ImportedBuild build,
        CancellationToken cancellationToken)
    {
        var export = Poe2BuildPlannerExporter.Export(build, tree, classes);
        var file = await files.SaveAsync(
            storageProvider,
            new TextFileSaveRequest(
                "Export Path of Exile 2 build",
                buildPlannerPaths.CurrentDirectory,
                SanitizeFileName(BuildName(build)) + ".build",
                "build",
                [BuildFileType],
                export.Json),
            cancellationToken);
        if (file is null)
        {
            return null;
        }

        if (file.Path.IsFile)
        {
            var directory = Path.GetDirectoryName(file.Path.LocalPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                buildPlannerPaths.RememberDirectory(directory);
            }
        }

        return new BuildPlannerExportFileResult(file.Name, export.SkippedNodeIds.Count);
    }

    private static string BuildName(ImportedBuild build)
    {
        var passiveVariant = build.PassiveTreeVariants.FirstOrDefault(v => v.Index == build.ActivePassiveTreeVariantIndex);
        return string.IsNullOrWhiteSpace(passiveVariant?.DisplayName)
            ? "PathOfAvalonia Export"
            : passiveVariant.DisplayName;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var result = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "PathOfAvalonia Export" : result;
    }
}
