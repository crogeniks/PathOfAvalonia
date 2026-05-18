using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PathOfAvalonia.TreeDomain;

namespace PathOfAvalonia.TreeApp.Services;

public interface IUserSettingsService
{
    GameId? LastGameId { get; set; }
    string? Poe1PobPath { get; set; }
    string? Poe2PobPath { get; set; }
    string? LuaExecutablePath { get; set; }
    bool EnablePobBackend { get; set; }
    void Save();
}

public sealed class UserSettingsService : IUserSettingsService
{
    private readonly string _path;

    public UserSettingsService()
    {
        _path = Path.Combine(ConfigRoot(), "PathOfAvalonia", "settings.json");
        Load();
    }

    public GameId? LastGameId { get; set; }
    public string? Poe1PobPath { get; set; }
    public string? Poe2PobPath { get; set; }
    public string? LuaExecutablePath { get; set; }
    public bool EnablePobBackend { get; set; } = true;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var dto = new SettingsDto
        {
            LastGameId = LastGameId?.ToString(),
            Poe1PobPath = Poe1PobPath,
            Poe2PobPath = Poe2PobPath,
            LuaExecutablePath = LuaExecutablePath,
            EnablePobBackend = EnablePobBackend,
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
            Poe1PobPath = dto?.Poe1PobPath;
            Poe2PobPath = dto?.Poe2PobPath;
            LuaExecutablePath = dto?.LuaExecutablePath;
            EnablePobBackend = dto?.EnablePobBackend ?? true;
        }
        catch
        {
            LastGameId = null;
            EnablePobBackend = true;
        }
    }

    private static string ConfigRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        return Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class SettingsDto
    {
        [JsonPropertyName("lastGameId")] public string? LastGameId { get; set; }
        [JsonPropertyName("poe1PobPath")] public string? Poe1PobPath { get; set; }
        [JsonPropertyName("poe2PobPath")] public string? Poe2PobPath { get; set; }
        [JsonPropertyName("luaExecutablePath")] public string? LuaExecutablePath { get; set; }
        [JsonPropertyName("enablePobBackend")] public bool? EnablePobBackend { get; set; }
    }
}
