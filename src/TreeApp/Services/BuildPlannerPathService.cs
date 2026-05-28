using System.IO;

namespace PathOfAvalonia.TreeApp.Services;

public interface IBuildPlannerPathService
{
    string CurrentDirectory { get; }
    void RememberDirectory(string directory);
}

public sealed class BuildPlannerPathService(
    IUserSettingsService settings,
    IUserPathService paths) : IBuildPlannerPathService
{
    public string CurrentDirectory =>
        string.IsNullOrWhiteSpace(settings.Poe2BuildPlannerDirectory)
            ? paths.DefaultPoe2BuildPlannerDirectory
            : settings.Poe2BuildPlannerDirectory;

    public void RememberDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        settings.Poe2BuildPlannerDirectory = Path.GetFullPath(directory);
        settings.Save();
    }
}
