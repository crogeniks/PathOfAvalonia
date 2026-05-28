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

public sealed class BuildPlannerExportService(IUserSettingsService settings) : IBuildPlannerExportService
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
        var startPath = ResolveBuildPlannerDirectory(settings);
        IStorageFolder? startFolder = null;
        try
        {
            Directory.CreateDirectory(startPath);
            startFolder = await storageProvider.TryGetFolderFromPathAsync(startPath);
        }
        catch
        {
            startFolder = null;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Path of Exile 2 build",
            SuggestedStartLocation = startFolder,
            SuggestedFileName = SanitizeFileName(BuildName(build)) + ".build",
            DefaultExtension = "build",
            FileTypeChoices = [BuildFileType],
            ShowOverwritePrompt = true,
        });
        if (file is null)
        {
            return null;
        }

        await using (var stream = await file.OpenWriteAsync())
        {
            stream.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes(export.Json);
            await stream.WriteAsync(bytes, cancellationToken);
        }

        if (file.Path.IsFile)
        {
            var directory = Path.GetDirectoryName(file.Path.LocalPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                settings.Poe2BuildPlannerDirectory = directory;
                settings.Save();
            }
        }

        return new BuildPlannerExportFileResult(file.Name, export.SkippedNodeIds.Count);
    }

    private static string ResolveBuildPlannerDirectory(IUserSettingsService settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Poe2BuildPlannerDirectory))
        {
            return settings.Poe2BuildPlannerDirectory;
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games",
                "Path of Exile 2",
                "BuildPlanner");
        }

        return "/home/deck/.local/share/Steam/steamapps/compatdata/2315204395/pfx/drive_c/users/steamuser/Documents/My Games/Path of Exile 2/BuildPlanner";
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
