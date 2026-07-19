using System.Threading;
using System.Threading.Tasks;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.Services;

/// <summary>File-picker boundary for Build Planner commands used by view models.</summary>
public interface IBuildPlannerFileService
{
    Task<BuildPlannerExportFileResult?> ExportAsync(BuildWorkspaceExportRequest request, CancellationToken cancellationToken);
    Task<BuildPlannerImportFileResult?> ImportAsync(BuildWorkspaceImportRequest request, CancellationToken cancellationToken);
}

public sealed record BuildWorkspaceExportRequest(TreeDomain.TreeModel Tree, TreeDomain.ClassCatalog Classes, ImportedBuild Build);
public sealed record BuildWorkspaceImportRequest(TreeDomain.TreeModel Tree);

public sealed class BuildPlannerFileService(
    IStorageProviderAccessor storage,
    IBuildPlannerExportService exporter,
    IBuildPlannerImportService importer) : IBuildPlannerFileService
{
    public Task<BuildPlannerExportFileResult?> ExportAsync(BuildWorkspaceExportRequest request, CancellationToken cancellationToken) =>
        storage.StorageProvider is { } provider
            ? exporter.ExportAsync(provider, request.Tree, request.Classes, request.Build, cancellationToken)
            : Task.FromResult<BuildPlannerExportFileResult?>(null);

    public Task<BuildPlannerImportFileResult?> ImportAsync(BuildWorkspaceImportRequest request, CancellationToken cancellationToken) =>
        storage.StorageProvider is { } provider
            ? importer.ImportAsync(provider, request.Tree, cancellationToken)
            : Task.FromResult<BuildPlannerImportFileResult?>(null);
}
