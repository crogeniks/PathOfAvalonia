using System.Text.RegularExpressions;
using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class PobCalculationServiceTests : IDisposable
{
    private readonly string _repoPath = Path.Combine(Path.GetTempPath(), "PathOfAvaloniaTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BackendSuccessReturnsPobBackendMetrics()
    {
        CreateBackendRepo();
        var runner = new FakeProcessRunner((_, args, _, _, _, _) =>
        {
            File.WriteAllText(OutputPath(args), """
                {"success":true,"backendName":"PathOfBuilding-PoE2","backendVersion":"abc","playerStats":[{"stat":"FullDPS","value":"42","displayValue":"42"}],"skillDps":[{"name":"Spark","dps":"40","displayDps":"40","count":2}]}
                """);
            return Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty, false));
        });
        var service = new PobCalculationService(new FixedLocator(_repoPath, "lua"), runner);

        var metrics = await service.CalculateAsync(GameId.PathOfExile2, Build(), CancellationToken.None);

        Assert.Equal(ImportedMetricSource.PobBackend, metrics.Source);
        Assert.Equal("PathOfBuilding-PoE2", metrics.BackendName);
        Assert.Equal(42, Assert.Single(metrics.PlayerStats).NumericValue);
        Assert.Equal(40, Assert.Single(metrics.SkillDps).Dps);
    }

    [Fact]
    public async Task MissingBackendFallsBackToSavedSnapshot()
    {
        var service = new PobCalculationService(new FixedLocator(null, null), new FakeProcessRunner());

        var metrics = await service.CalculateAsync(GameId.PathOfExile2, Build(), CancellationToken.None);

        Assert.Equal(ImportedMetricSource.SavedXmlSnapshot, metrics.Source);
        Assert.Contains("not configured", metrics.ErrorMessage);
    }

    [Fact]
    public async Task InvalidJsonFallsBackToSavedSnapshot()
    {
        CreateBackendRepo();
        var runner = new FakeProcessRunner((_, args, _, _, _, _) =>
        {
            File.WriteAllText(OutputPath(args), "not json");
            return Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty, false));
        });
        var service = new PobCalculationService(new FixedLocator(_repoPath, "lua"), runner);

        var metrics = await service.CalculateAsync(GameId.PathOfExile2, Build(), CancellationToken.None);

        Assert.Equal(ImportedMetricSource.SavedXmlSnapshot, metrics.Source);
        Assert.Contains("invalid JSON", metrics.ErrorMessage);
    }

    [Fact]
    public async Task TimeoutFallsBackToSavedSnapshot()
    {
        CreateBackendRepo();
        TimeSpan? observedTimeout = null;
        var runner = new FakeProcessRunner((_, _, _, _, timeout, _) =>
        {
            observedTimeout = timeout;
            return Task.FromResult(new ProcessRunResult(-1, string.Empty, string.Empty, true));
        });
        var service = new PobCalculationService(new FixedLocator(_repoPath, "lua", TimeSpan.FromSeconds(90)), runner);

        var metrics = await service.CalculateAsync(GameId.PathOfExile2, Build(), CancellationToken.None);

        Assert.Equal(ImportedMetricSource.SavedXmlSnapshot, metrics.Source);
        Assert.Contains("timed out", metrics.ErrorMessage);
        Assert.Equal(TimeSpan.FromSeconds(90), observedTimeout);
    }

    [Fact]
    public async Task NoBackendAndNoSnapshotReturnsNone()
    {
        var service = new PobCalculationService(new FixedLocator(null, null), new FakeProcessRunner());

        var metrics = await service.CalculateAsync(GameId.PathOfExile2, Build(ImportedBuildMetrics.Empty), CancellationToken.None);

        Assert.Equal(ImportedMetricSource.None, metrics.Source);
        Assert.Contains("not configured", metrics.ErrorMessage);
    }

    [Fact]
    public async Task CacheKeyChangesWhenXmlChanges()
    {
        CreateBackendRepo();
        var calls = 0;
        var runner = new FakeProcessRunner((_, args, _, _, _, _) =>
        {
            calls++;
            File.WriteAllText(OutputPath(args), $$"""
                {"success":true,"playerStats":[{"stat":"FullDPS","value":"{{calls}}","displayValue":"{{calls}}"}]}
                """);
            return Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty, false));
        });
        var service = new PobCalculationService(new FixedLocator(_repoPath, "lua"), runner);

        await service.CalculateAsync(GameId.PathOfExile2, Build(rawXml: "<PathOfBuilding2><Tree><Spec nodes=\"10\" /></Tree></PathOfBuilding2>"), CancellationToken.None);
        await service.CalculateAsync(GameId.PathOfExile2, Build(rawXml: "<PathOfBuilding2><Tree><Spec nodes=\"20\" /></Tree></PathOfBuilding2>"), CancellationToken.None);

        Assert.Equal(2, calls);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoPath))
        {
            Directory.Delete(_repoPath, recursive: true);
        }
    }

    private void CreateBackendRepo()
    {
        Directory.CreateDirectory(Path.Combine(_repoPath, "src"));
        File.WriteAllText(Path.Combine(_repoPath, "src", "HeadlessWrapper.lua"), string.Empty);
    }

    private static ImportedBuild Build(ImportedBuildMetrics? metrics = null, string rawXml = "<PathOfBuilding2><Tree><Spec nodes=\"10\" /></Tree></PathOfBuilding2>") =>
        new(
            ClassId: 0,
            AscendClassId: 0,
            SecondaryAscendClassId: 0,
            NodeHashes: [10],
            ClusterNodeHashes: [],
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: null,
            Source: "test")
        {
            RawXml = rawXml,
            Metrics = metrics ?? ImportedBuildMetrics.Empty with
            {
                Source = ImportedMetricSource.SavedXmlSnapshot,
                PlayerStats = [new ImportedStatMetric("FullDPS", "Full DPS", 12, "12")],
            },
        };

    private static string OutputPath(string args)
    {
        var matches = Regex.Matches(args, "\"([^\"]+)\"");
        return matches[^1].Groups[1].Value;
    }

    private sealed class FixedLocator(string? repoPath, string? luaPath, TimeSpan? timeout = null) : IPobBackendLocator
    {
        public PobBackendConfig Resolve(GameId gameId) => new(gameId, repoPath, luaPath, true, timeout ?? TimeSpan.FromSeconds(120));
    }

    private sealed class FakeProcessRunner(
        Func<string, string, string, IReadOnlyDictionary<string, string>, TimeSpan, CancellationToken, Task<ProcessRunResult>>? run = null)
        : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string> environment,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            run?.Invoke(fileName, arguments, workingDirectory, environment, timeout, cancellationToken)
            ?? Task.FromResult(new ProcessRunResult(0, string.Empty, string.Empty, false));
    }
}
