using GameServerApp.Core.Models;

namespace GameServerApp.Core.Events;

public sealed class ConsoleOutputEventArgs : EventArgs
{
    public required string InstanceId { get; init; }
    public required ConsoleOutputLine Line { get; init; }
}
