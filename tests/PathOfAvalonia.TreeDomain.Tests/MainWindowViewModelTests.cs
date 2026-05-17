using PathOfAvalonia.TreeApp.Services;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain;
using PathOfAvalonia.TreeDomain.Import;
using Xunit;

namespace PathOfAvalonia.TreeDomain.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void ImportUpdatesAscendancySelectionOnFirstImport()
    {
        var build = new ImportedBuild(
            ClassId: 1,
            AscendClassId: 2,
            SecondaryAscendClassId: 0,
            NodeHashes: Array.Empty<int>(),
            ClusterNodeHashes: Array.Empty<int>(),
            MasterySelections: new Dictionary<int, int>(),
            TreeVersion: null,
            Source: "test");
        var vm = new MainWindowViewModel(LoadSpec(), new StubImportService(build), new EquipmentViewModel())
        {
            ImportInput = "test",
        };

        vm.ImportCommand.Execute(null);

        Assert.Equal(1, vm.SelectedClassIndex);
        Assert.Equal(new[] { "None", "Juggernaut", "Berserker", "Chieftain" }, vm.AscendancyNames);
        Assert.Equal(2, vm.SelectedAscendancyIndex);
        Assert.Equal("Berserker", vm.SelectedAscendancyName);
    }

    private static PassiveSpec LoadSpec() => new(LoadTree());

    private static TreeModel LoadTree()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "PoE1", "tree_3_28.json"));
        using var stream = File.OpenRead(path);
        return TreeLoader.LoadFromJson(stream, "3.28");
    }

    private sealed class StubImportService(ImportedBuild build) : IImportService
    {
        public ImportedBuild Import(string text) => build;
    }
}
