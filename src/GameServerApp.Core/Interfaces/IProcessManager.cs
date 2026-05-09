using System.Diagnostics;
using GameServerApp.Core.Events;
using GameServerApp.Core.Models;

namespace GameServerApp.Core.Interfaces;

public interface IProcessManager
{
    event EventHandler<ConsoleOutputEventArgs>? OutputReceived;
    event EventHandler<ServerStateChangedEventArgs>? StateChanged;

    Task StartAsync(ServerInstance instance, ProcessStartInfo startInfo,
        CancellationToken ct = default);
    Task StopGracefullyAsync(ServerInstance instance, string? stopCommand,
        int timeoutMs, CancellationToken ct = default);
    Task KillAsync(ServerInstance instance);
    Task SendCommandAsync(ServerInstance instance, string command);
    bool IsRunning(ServerInstance instance);
}
