using System.Text.Json.Serialization;

namespace GameServerApp.Core.Models;

public sealed class ServerConfig
{
    public required string InstanceId { get; init; }
    public required string GameId { get; init; }
    public required string Name { get; set; }
    public string? ServerVersion { get; set; }
    public string ServerDirectory { get; set; } = string.Empty;
    public Dictionary<string, object> GameSettings { get; set; } = new();
    public int MemoryMb { get; set; } = 1024;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
