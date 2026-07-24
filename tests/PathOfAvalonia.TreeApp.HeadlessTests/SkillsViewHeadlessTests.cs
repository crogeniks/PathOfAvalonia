using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PathOfAvalonia.TreeApp.ViewModels;
using PathOfAvalonia.TreeDomain.Import;

namespace PathOfAvalonia.TreeApp.HeadlessTests;

public sealed class SkillsViewHeadlessTests
{
    [AvaloniaFact]
    public void SkillsWorkspaceSwitchesSetsAndShowsSelectedGroupDetailsWithoutEditors()
    {
        var viewModel = new EquipmentViewModel();
        viewModel.LoadBuild(BuildWithSkills());
        var view = new SkillsView { DataContext = viewModel };
        var window = Show(view);
        try
        {
            var setSelector = Required<ComboBox>(view, "SkillSetSelector");
            var groupList = Required<ListBox>(view, "SkillGroupList");
            var detail = Required<ScrollViewer>(view, "SkillGroupDetail");

            Assert.Equal(2, setSelector.ItemCount);
            Assert.Equal(1, setSelector.SelectedIndex);
            Assert.Equal(2, groupList.ItemCount);
            Assert.True(detail.IsVisible);
            Assert.Equal("Searing Bond", viewModel.SelectedSkillGroup!.Header);
            Assert.True(viewModel.SelectedSkillGroup.IsMainSkillGroup);
            Assert.Equal(2, Required<ItemsControl>(view, "SkillGemList").ItemCount);

            setSelector.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Mapping", viewModel.SkillSetOptions[viewModel.SelectedSkillSetIndex].DisplayName);
            Assert.Equal("Spark", viewModel.SelectedSkillGroup!.Header);
            Assert.Equal(1, groupList.ItemCount);

            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => !textBox.GetVisualAncestors().OfType<ComboBox>().Any());
            Assert.Empty(view.GetVisualDescendants().OfType<NumericUpDown>());
            Assert.Empty(view.GetVisualDescendants().OfType<CheckBox>());
        }
        finally
        {
            window.Close();
        }
    }

    private static ImportedBuild BuildWithSkills() => new(
        ClassId: 0,
        AscendClassId: 0,
        SecondaryAscendClassId: 0,
        NodeHashes: [],
        ClusterNodeHashes: [],
        MasterySelections: new Dictionary<int, int>(),
        TreeVersion: null,
        Source: "test")
    {
        Skills = new ImportedSkills(
            [
                new ImportedSkillSet(
                    0,
                    1,
                    "Mapping",
                    [
                        new ImportedSkillGroup(
                            0,
                            "Spark",
                            "Body Armour",
                            null,
                            true,
                            true,
                            1,
                            0,
                            0,
                            [Gem("Spark")]),
                    ]),
                new ImportedSkillSet(
                    1,
                    2,
                    "Bossing",
                    [
                        new ImportedSkillGroup(
                            0,
                            "Searing Bond",
                            "Helmet",
                            null,
                            true,
                            false,
                            1,
                            0,
                            0,
                            [Gem("Searing Bond"), Gem("Burning Damage Support")]),
                        new ImportedSkillGroup(
                            1,
                            "Flame Dash",
                            "Boots",
                            null,
                            false,
                            false,
                            1,
                            0,
                            0,
                            [Gem("Flame Dash")]),
                    ]),
            ],
            ActiveSkillSetIndex: 1,
            MainSocketGroupIndex: 0),
    };

    private static ImportedGem Gem(string name) => new(
        name,
        GemId: null,
        SkillId: null,
        VariantId: null,
        Level: 20,
        Quality: 20,
        Enabled: true,
        EnableGlobal1: false,
        EnableGlobal2: false,
        Count: 1,
        SkillPart: null,
        SkillPartCalcs: null,
        SkillStageCount: null,
        SkillStageCountCalcs: null,
        SkillMineCount: null,
        SkillMineCountCalcs: null,
        SkillMinion: null,
        SkillMinionCalcs: null,
        SkillMinionItemSet: null,
        SkillMinionItemSetCalcs: null,
        SkillMinionSkill: null,
        SkillMinionSkillCalcs: null);

    private static Window Show(Control content)
    {
        var window = new Window
        {
            Width = 1280,
            Height = 800,
            Content = content,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Required<T>(Control root, string name)
        where T : Control =>
        root.FindControl<T>(name) ?? throw new InvalidOperationException($"Missing control '{name}'.");
}
