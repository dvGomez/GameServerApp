using GameServerApp.Core.Models;

namespace GameServerApp.Core.Events;

public sealed class ServerStateChangedEventArgs : EventArgs
{
    public required string InstanceId { get; init; }
    public required ServerState OldState { get; init; }
    public required ServerState NewState { get; init; }
}
