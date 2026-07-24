using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.Services;

public sealed record SavedBuild(
    Guid Id,
    string Name,
    GameId GameId,
    string TreeVersion,
    ImportedBuild CharacterBuild,
    string? AtlasTreeVersion,
    IReadOnlyList<int> AtlasNodeIds,
    DateTimeOffset UpdatedAt);

public sealed record SavedBuildSummary(
    Guid Id,
    string Name,
    GameId GameId,
    string TreeVersion,
    DateTimeOffset UpdatedAt);

public interface IBuildLibraryService
{
    Task<IReadOnlyList<SavedBuildSummary>> ListAsync(GameId? gameId = null, CancellationToken cancellationToken = default);
    Task<SavedBuild?> LoadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SavedBuild> SaveAsync(SavedBuild build, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Stores PathOfAvalonia-owned build snapshots in the user's config directory.</summary>
public sealed class BuildLibraryService : IBuildLibraryService
{
    private const int CurrentFormatVersion = 1;
    private readonly string _directory;

    public BuildLibraryService(IUserPathService paths)
    {
        _directory = Path.Combine(paths.ConfigRoot, "PathOfAvalonia", "builds");
    }

    public Task<IReadOnlyList<SavedBuildSummary>> ListAsync(
        GameId? gameId = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ListCoreAsync(gameId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<SavedBuildSummary>> ListCoreAsync(
        GameId? gameId,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var summaries = new List<SavedBuildSummary>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var envelope = await ReadSummaryEnvelopeAsync(path, cancellationToken);
                if (envelope is { FormatVersion: CurrentFormatVersion, Build: { } build }
                    && (gameId is null || build.GameId == gameId))
                {
                    summaries.Add(build);
                }
            }
            catch (IOException)
            {
                // A single unavailable build must not hide the rest of the library.
            }
            catch (JsonException)
            {
                // Corrupt or future-format files stay on disk and are ignored.
            }
        }

        return summaries
            .OrderByDescending(summary => summary.UpdatedAt)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<SavedBuild?> LoadAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.Run(() => LoadCoreAsync(id, cancellationToken), cancellationToken);

    private async Task<SavedBuild?> LoadCoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = BuildPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        var envelope = await ReadEnvelopeAsync(path, cancellationToken);
        if (envelope is null || envelope.FormatVersion != CurrentFormatVersion || envelope.Build.Id != id)
        {
            throw new InvalidDataException($"The saved build '{id}' has an unsupported or invalid format.");
        }
        return envelope.Build;
    }

    public async Task<SavedBuild> SaveAsync(SavedBuild build, CancellationToken cancellationToken = default)
    {
        var normalized = build with
        {
            Name = string.IsNullOrWhiteSpace(build.Name) ? "Unnamed build" : build.Name.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        Directory.CreateDirectory(_directory);
        var path = BuildPath(normalized.Id);
        var temporaryPath = Path.Combine(_directory, $".{normalized.Id:N}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new SavedBuildEnvelope(CurrentFormatVersion, normalized),
                    JsonOptions,
                    cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        return normalized;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = BuildPath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private string BuildPath(Guid id) => Path.Combine(_directory, $"{id:N}.json");

    private static async Task<SavedBuildEnvelope?> ReadEnvelopeAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SavedBuildEnvelope>(stream, JsonOptions, cancellationToken);
    }

    private static async Task<SavedBuildSummaryEnvelope?> ReadSummaryEnvelopeAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = OpenRead(path);
        return await JsonSerializer.DeserializeAsync<SavedBuildSummaryEnvelope>(
            stream,
            SummaryJsonOptions,
            cancellationToken);
    }

    private static FileStream OpenRead(string path) =>
        new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static readonly JsonSerializerOptions SummaryJsonOptions = CreateSummaryJsonOptions();

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateSummaryJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(ImportedItem))
            {
                return;
            }

            var parsedText = typeInfo.Properties.FirstOrDefault(property =>
                string.Equals(property.Name, nameof(ImportedItem.Text), StringComparison.OrdinalIgnoreCase));
            if (parsedText is not null)
            {
                typeInfo.Properties.Remove(parsedText);
            }
        });

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = resolver,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record SavedBuildEnvelope(int FormatVersion, SavedBuild Build);
    private sealed record SavedBuildSummaryEnvelope(int FormatVersion, SavedBuildSummary Build);
}
