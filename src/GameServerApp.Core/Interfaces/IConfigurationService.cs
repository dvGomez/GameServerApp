using GameServerApp.Core.Models;

namespace GameServerApp.Core.Interfaces;

public interface IConfigurationService
{
    string AppDataPath { get; }
    Task<ServerConfig?> LoadServerConfigAsync(string instanceId, CancellationToken ct = default);
    Task SaveServerConfigAsync(ServerConfig config, CancellationToken ct = default);
    Task DeleteServerConfigAsync(string instanceId, CancellationToken ct = default);
    Task<IReadOnlyList<ServerConfig>> LoadAllServerConfigsAsync(CancellationToken ct = default);
}
