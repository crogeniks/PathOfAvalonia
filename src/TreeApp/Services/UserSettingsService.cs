using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IUserSettingsService
{
    GameId? LastGameId { get; set; }
    string? Poe2BuildPlannerDirectory { get; set; }
    void Save();
}

public sealed class UserSettingsService : IUserSettingsService
{
    private readonly string _path;

    public UserSettingsService()
        : this(new UserPathService())
    {
    }

    public UserSettingsService(IUserPathService paths)
    {
        _path = Path.Combine(paths.ConfigRoot, "PathOfAvalonia", "settings.json");
        Load();
    }

    public GameId? LastGameId { get; set; }
    public string? Poe2BuildPlannerDirectory { get; set; }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var dto = new SettingsDto
        {
            LastGameId = LastGameId?.ToString(),
            Poe2BuildPlannerDirectory = Poe2BuildPlannerDirectory,
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(dto, JsonOpts));
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }
            var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(_path), JsonOpts);
            if (Enum.TryParse<GameId>(dto?.LastGameId, out var gameId))
            {
                LastGameId = gameId;
            }
            Poe2BuildPlannerDirectory = dto?.Poe2BuildPlannerDirectory;
        }
        catch
        {
            LastGameId = null;
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class SettingsDto
    {
        [JsonPropertyName("lastGameId")] public string? LastGameId { get; set; }
        [JsonPropertyName("poe2BuildPlannerDirectory")] public string? Poe2BuildPlannerDirectory { get; set; }
    }
}
