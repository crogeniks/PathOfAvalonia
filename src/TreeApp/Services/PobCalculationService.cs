using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.Services;

public interface IPobCalculationService
{
    Task<ImportedBuildMetrics> CalculateAsync(
        GameId gameId,
        ImportedBuild build,
        CancellationToken cancellationToken);
}

public interface IPobBackendLocator
{
    PobBackendConfig Resolve(GameId gameId);
}

public sealed record PobBackendConfig(
    GameId GameId,
    string? RepositoryPath,
    string? LuaExecutablePath,
    bool IsEnabled);

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

public sealed class PobBackendLocator(IUserSettingsService settings) : IPobBackendLocator
{
    public PobBackendConfig Resolve(GameId gameId)
    {
        var repoPath = gameId == GameId.PathOfExile2 ? settings.Poe2PobPath : settings.Poe1PobPath;
        repoPath = FirstExistingDirectory(repoPath, DefaultRepoPath(gameId));
        return new PobBackendConfig(gameId, repoPath, ResolveLua(settings.LuaExecutablePath), settings.EnablePobBackend);
    }

    private static string DefaultRepoPath(GameId gameId)
    {
        var name = gameId == GameId.PathOfExile2 ? "PathOfBuilding-PoE2" : "PathOfBuilding";
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", name));
    }

    private static string? FirstExistingDirectory(params string?[] paths) =>
        paths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path));

    private static string? ResolveLua(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        foreach (var candidate in new[] { "luajit", "lua5.1", "lua" })
        {
            if (IsOnPath(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static bool IsOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir, executable);
            if (File.Exists(full))
            {
                return true;
            }
        }
        return false;
    }
}

public sealed class PobCalculationService(IPobBackendLocator locator, IProcessRunner processRunner) : IPobCalculationService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, ImportedBuildMetrics> _cache = new();

    public async Task<ImportedBuildMetrics> CalculateAsync(
        GameId gameId,
        ImportedBuild build,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(build.RawXml))
        {
            return Fallback(build, "PoB backend requires raw XML; showing saved DPS snapshot.");
        }

        var config = locator.Resolve(gameId);
        if (!config.IsEnabled || string.IsNullOrWhiteSpace(config.RepositoryPath) || string.IsNullOrWhiteSpace(config.LuaExecutablePath))
        {
            return Fallback(build, "PoB backend not configured; showing saved DPS snapshot.");
        }

        var srcDir = Path.Combine(config.RepositoryPath, "src");
        var wrapper = Path.Combine(srcDir, "HeadlessWrapper.lua");
        if (!Directory.Exists(srcDir) || !File.Exists(wrapper))
        {
            return Fallback(build, "PoB backend not configured; showing saved DPS snapshot.");
        }

        var commit = TryReadGitCommit(config.RepositoryPath);
        var key = CacheKey(gameId, build, config, commit);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "PathOfAvalonia", Guid.NewGuid().ToString("N"));
        var inputPath = Path.Combine(tempRoot, "build.xml");
        var outputPath = Path.Combine(tempRoot, "metrics.json");
        try
        {
            Directory.CreateDirectory(tempRoot);
            await File.WriteAllTextAsync(inputPath, build.RawXml, Encoding.UTF8, cancellationToken);

            var adapter = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "pob-headless", "pathofavalonia_export.lua"));
            if (!File.Exists(adapter))
            {
                return Fallback(build, "PoB backend adapter is missing; showing saved DPS snapshot.");
            }

            var environment = new Dictionary<string, string>
            {
                ["LUA_PATH"] = "../runtime/lua/?.lua;../runtime/lua/?/init.lua;?.lua",
            };
            var result = await processRunner.RunAsync(
                config.LuaExecutablePath,
                $"{Quote(adapter)} {Quote(inputPath)} {Quote(outputPath)}",
                srcDir,
                environment,
                Timeout,
                cancellationToken);

            if (result.TimedOut)
            {
                return Fallback(build, "PoB backend timed out; showing saved DPS snapshot.");
            }
            if (result.ExitCode != 0)
            {
                return Fallback(build, ConciseError("PoB backend error", result.StandardError, result.StandardOutput));
            }
            if (!File.Exists(outputPath))
            {
                return Fallback(build, "PoB backend returned no metrics; showing saved DPS snapshot.");
            }

            var dto = JsonSerializer.Deserialize<PobExportDto>(
                await File.ReadAllTextAsync(outputPath, cancellationToken),
                JsonOptions);
            if (dto is null || dto.Success == false)
            {
                return Fallback(build, dto?.Error ?? "PoB backend returned invalid metrics; showing saved DPS snapshot.");
            }

            var metrics = ToMetrics(dto, config, commit);
            _cache[key] = metrics;
            return metrics;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fallback(build, "PoB backend timed out; showing saved DPS snapshot.");
        }
        catch (JsonException ex)
        {
            return Fallback(build, $"PoB backend returned invalid JSON; showing saved DPS snapshot. {ex.Message}");
        }
        catch (Exception ex)
        {
            return Fallback(build, $"PoB backend error: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static ImportedBuildMetrics ToMetrics(PobExportDto dto, PobBackendConfig config, string? commit)
    {
        var stats = dto.PlayerStats?
            .Where(stat => !string.IsNullOrWhiteSpace(stat.Stat))
            .Select(stat => new ImportedStatMetric(
                stat.Stat!,
                stat.Label ?? StatLabel(stat.Stat!),
                ParseDouble(stat.Value),
                stat.DisplayValue ?? stat.Value ?? string.Empty))
            .ToArray() ?? [];
        var dps = dto.SkillDps?
            .Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new ImportedSkillDpsMetric(
                row.Name!,
                ParseDouble(row.Dps),
                row.DisplayDps ?? row.Dps ?? string.Empty,
                row.Count ?? 1,
                row.SkillPart,
                row.Source))
            .ToArray() ?? [];

        return new ImportedBuildMetrics(
            ImportedMetricSource.PobBackend,
            dto.BackendName ?? BackendName(config.GameId),
            dto.BackendVersion ?? commit,
            config.RepositoryPath,
            stats,
            dps,
            dto.Warnings ?? [],
            dto.Error);
    }

    private static ImportedBuildMetrics Fallback(ImportedBuild build, string error)
    {
        if (build.Metrics.Source != ImportedMetricSource.None)
        {
            return build.Metrics with { ErrorMessage = error };
        }

        return ImportedBuildMetrics.Empty with { ErrorMessage = error };
    }

    private static string CacheKey(GameId gameId, ImportedBuild build, PobBackendConfig config, string? commit)
    {
        var raw = string.Join('|',
            gameId,
            Sha256(build.RawXml ?? string.Empty),
            build.ActivePassiveTreeVariantIndex.ToString(CultureInfo.InvariantCulture),
            build.ActiveItemSetVariantIndex.ToString(CultureInfo.InvariantCulture),
            config.RepositoryPath,
            commit);
        return Sha256(raw);
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string? TryReadGitCommit(string repoPath)
    {
        try
        {
            var head = Path.Combine(repoPath, ".git", "HEAD");
            if (!File.Exists(head))
            {
                return null;
            }
            var value = File.ReadAllText(head).Trim();
            const string refPrefix = "ref: ";
            if (!value.StartsWith(refPrefix, StringComparison.Ordinal))
            {
                return value;
            }
            var refPath = Path.Combine(repoPath, ".git", value[refPrefix.Length..].Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(refPath) ? File.ReadAllText(refPath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string ConciseError(string prefix, string stderr, string stdout)
    {
        var detail = FirstLine(stderr) ?? FirstLine(stdout);
        return detail is null ? $"{prefix}; showing saved DPS snapshot." : $"{prefix}: {detail}";
    }

    private static string? FirstLine(string value) =>
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();

    private static string BackendName(GameId gameId) =>
        gameId == GameId.PathOfExile2 ? "PathOfBuilding-PoE2" : "PathOfBuilding";

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string StatLabel(string stat)
    {
        var spaced = System.Text.RegularExpressions.Regex.Replace(stat, "([a-z])([A-Z])", "$1 $2");
        return spaced.Replace('_', ' ');
    }

    private sealed class PobExportDto
    {
        [JsonPropertyName("success")] public bool? Success { get; set; }
        [JsonPropertyName("backendName")] public string? BackendName { get; set; }
        [JsonPropertyName("backendVersion")] public string? BackendVersion { get; set; }
        [JsonPropertyName("playerStats")] public List<StatDto>? PlayerStats { get; set; }
        [JsonPropertyName("skillDps")] public List<SkillDpsDto>? SkillDps { get; set; }
        [JsonPropertyName("warnings")] public List<string>? Warnings { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    private sealed class StatDto
    {
        [JsonPropertyName("stat")] public string? Stat { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }
        [JsonPropertyName("displayValue")] public string? DisplayValue { get; set; }
    }

    private sealed class SkillDpsDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("dps")] public string? Dps { get; set; }
        [JsonPropertyName("displayDps")] public string? DisplayDps { get; set; }
        [JsonPropertyName("count")] public int? Count { get; set; }
        [JsonPropertyName("skillPart")] public string? SkillPart { get; set; }
        [JsonPropertyName("source")] public string? Source { get; set; }
    }
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        foreach (var (key, value) in environment)
        {
            process.StartInfo.Environment[key] = value;
        }

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            return new ProcessRunResult(process.ExitCode, await stdoutTask, await stderrTask, false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
            return new ProcessRunResult(-1, string.Empty, string.Empty, true);
        }
    }
}
