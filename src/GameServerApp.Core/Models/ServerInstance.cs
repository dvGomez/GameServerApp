using System.Diagnostics;

namespace GameServerApp.Core.Models;

public sealed class ServerInstance
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string GameId { get; init; }
    public required string ServerDirectory { get; init; }

    public ServerState State { get; set; } = ServerState.Stopped;
    public int? ProcessId { get; set; }
    public DateTime? StartedAt { get; set; }
    public Process? Process { get; set; }

    public int Port { get; set; }
    public string Version { get; set; } = string.Empty;
    public int OnlinePlayers { get; set; }
    public int MaxPlayers { get; set; }
}
