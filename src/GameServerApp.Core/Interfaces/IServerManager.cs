using System.Collections.ObjectModel;
using GameServerApp.Core.Events;
using GameServerApp.Core.Models;

namespace GameServerApp.Core.Interfaces;

public interface IServerManager
{
    ObservableCollection<ServerInstance> Instances { get; }

    event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput;
    event EventHandler<ServerStateChangedEventArgs>? ServerStateChanged;
    event EventHandler<ServerConfigChangedEventArgs>? ServerConfigChanged;

    Task<ServerInstance> CreateServerAsync(string gameId, string name,
        string? version = null,
        Dictionary<string, object>? initialConfig = null,
        IProgress<double>? downloadProgress = null,
        CancellationToken ct = default);
    Task StartServerAsync(string instanceId, CancellationToken ct = default);
    Task StopServerAsync(string instanceId, CancellationToken ct = default);
    Task RestartServerAsync(string instanceId, CancellationToken ct = default);
    Task SendCommandAsync(string instanceId, string command);
    Task DeleteServerAsync(string instanceId, CancellationToken ct = default);
    Task SaveConfigAsync(string instanceId, Dictionary<string, object> config,
        CancellationToken ct = default);
    ServerConfig? GetServerConfig(string instanceId);
    IGameServerPlugin? GetPlugin(string gameId);
    IReadOnlyList<IGameServerPlugin> AvailablePlugins { get; }
    IReadOnlyList<ConsoleOutputLine> GetConsoleHistory(string instanceId);
    void AddConsoleEntry(string instanceId, ConsoleOutputLine line);
    Task LoadAllServersAsync(CancellationToken ct = default);
}
