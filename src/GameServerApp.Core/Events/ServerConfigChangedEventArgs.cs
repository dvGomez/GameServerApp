namespace GameServerApp.Core.Events;

public sealed class ServerConfigChangedEventArgs : EventArgs
{
    public required string InstanceId { get; init; }
    public required string ServerName { get; init; }
    public required int Port { get; init; }
    public required int MaxPlayers { get; init; }
    public required string Version { get; init; }
}
