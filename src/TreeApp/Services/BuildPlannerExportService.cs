using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia;
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

public sealed record BuildPlannerExportFileResult(string Name, int SkippedNodeCount, int FileCount = 1);

public interface IBuildNamePrefixPromptService
{
    Task<string?> PromptAsync(CancellationToken cancellationToken);
}

public sealed class BuildPlannerExportService(
    IBuildPlannerPathService buildPlannerPaths,
    ITextFileSaveService files,
    IBuildNamePrefixPromptService prefixPrompt) : IBuildPlannerExportService
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
        var prefix = await prefixPrompt.PromptAsync(cancellationToken);
        if (prefix is null)
        {
            return null;
        }

        var exports = Poe2BuildPlannerExporter.ExportFiles(build, tree, classes, prefix);
        if (exports.Count > 1)
        {
            return await ExportManyAsync(storageProvider, exports, cancellationToken);
        }

        var export = exports[0].Export;
        var file = await files.SaveAsync(
            storageProvider,
            new TextFileSaveRequest(
                "Export Path of Exile 2 build",
                buildPlannerPaths.CurrentDirectory,
                SanitizeFileName(exports[0].Name) + ".build",
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

    private async Task<BuildPlannerExportFileResult?> ExportManyAsync(
        IStorageProvider storageProvider,
        IReadOnlyList<Poe2BuildPlannerExportFile> exports,
        CancellationToken cancellationToken)
    {
        var startFolder = await StorageStartFolderResolver.TryGetAsync(storageProvider, buildPlannerPaths.CurrentDirectory);
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Export Path of Exile 2 builds",
            SuggestedStartLocation = startFolder,
            AllowMultiple = false,
        });
        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return null;
        }

        var skipped = 0;
        foreach (var export in exports)
        {
            var file = await folder.CreateFileAsync(SanitizeFileName(export.Name) + ".build");
            if (file is null)
            {
                throw new IOException($"Could not create export file for '{export.Name}'.");
            }
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            var bytes = Encoding.UTF8.GetBytes(export.Export.Json);
            await stream.WriteAsync(bytes, cancellationToken);
            skipped += export.Export.SkippedNodeIds.Count;
        }

        if (folder.Path.IsFile)
        {
            buildPlannerPaths.RememberDirectory(folder.Path.LocalPath);
        }

        return new BuildPlannerExportFileResult(folder.Name, skipped, exports.Count);
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

public sealed class BuildNamePrefixPromptService : IBuildNamePrefixPromptService
{
    public async Task<string?> PromptAsync(CancellationToken cancellationToken)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return string.Empty;
        }

        var input = new TextBox
        {
            PlaceholderText = "Optional prefix",
            Width = 360,
        };
        var dialog = new Window
        {
            Title = "Build name prefix",
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Build name prefix",
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    },
                    input,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            new Button { Content = "Cancel" },
                            new Button { Content = "Export" },
                        },
                    },
                },
            },
        };

        var buttons = ((StackPanel)((StackPanel)dialog.Content!).Children[2]).Children;
        ((Button)buttons[0]).Click += (_, _) => dialog.Close(null);
        ((Button)buttons[1]).Click += (_, _) => dialog.Close(input.Text?.Trim() ?? string.Empty);

        await using var _ = cancellationToken.Register(() => dialog.Close(null));
        return await dialog.ShowDialog<string?>(desktop.MainWindow);
    }
}
