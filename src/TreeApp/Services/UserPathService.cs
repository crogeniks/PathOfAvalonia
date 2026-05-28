using System;
using System.IO;

namespace PathOfAvalonia.TreeApp.Services;

public interface IUserPathService
{
    string ConfigRoot { get; }
    string DefaultPoe2BuildPlannerDirectory { get; }
}

public sealed class UserPathService : IUserPathService
{
    public string ConfigRoot
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            return Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                   ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
    }

    public string DefaultPoe2BuildPlannerDirectory
    {
        get
        {
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
    }
}
