using System.Text.Json;
using System.Text.Json.Serialization;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;

namespace GameServerApp.Core.Services;

public sealed class ConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string AppDataPath { get; }

    private string ConfigDirectory => Path.Combine(AppDataPath, "config", "servers");
    private string ServersDirectory => Path.Combine(AppDataPath, "servers");

    public ConfigurationService()
    {
        AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameServerApp");

        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(ServersDirectory);
    }

    public async Task<ServerConfig?> LoadServerConfigAsync(string instanceId, CancellationToken ct = default)
    {
        var path = GetConfigPath(instanceId);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<ServerConfig>(json, JsonOptions);
    }

    public async Task SaveServerConfigAsync(ServerConfig config, CancellationToken ct = default)
    {
        Directory.CreateDirectory(ConfigDirectory);
        var path = GetConfigPath(config.InstanceId);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    public Task DeleteServerConfigAsync(string instanceId, CancellationToken ct = default)
    {
        var path = GetConfigPath(instanceId);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ServerConfig>> LoadAllServerConfigsAsync(CancellationToken ct = default)
    {
        var configs = new List<ServerConfig>();

        if (!Directory.Exists(ConfigDirectory))
            return configs;

        foreach (var file in Directory.GetFiles(ConfigDirectory, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var config = JsonSerializer.Deserialize<ServerConfig>(json, JsonOptions);
                if (config != null)
                    configs.Add(config);
            }
            catch
            {
                // skip corrupted config files
            }
        }

        return configs;
    }

    private string GetConfigPath(string instanceId) =>
        Path.Combine(ConfigDirectory, $"{instanceId}.json");
}
