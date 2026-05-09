using System.Diagnostics;
using GameServerApp.Core.Models;

namespace GameServerApp.Core.Interfaces;

public interface IGameServerPlugin
{
    string GameId { get; }
    string DisplayName { get; }
    string? Description { get; }
    string? IconResourceKey { get; }

    IReadOnlyList<ConfigField> GetConfigSchema();
    Dictionary<string, object> GetDefaultConfig();

    ProcessStartInfo BuildStartInfo(ServerConfig config);
    string? GetGracefulStopCommand();
    int GracefulStopTimeoutMs { get; }

    ConsoleOutputLine ParseConsoleLine(string rawLine);
    int? ParsePlayerDelta(string rawLine);

    Task<string> GetLatestVersionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAvailableVersionsAsync(CancellationToken ct = default);
    Task DownloadServerAsync(string version, string targetDirectory,
        IProgress<double>? progress = null, CancellationToken ct = default);
    Task<bool> ValidateInstallationAsync(string serverDirectory, CancellationToken ct = default);

    Task WriteGameConfigAsync(string serverDirectory, Dictionary<string, object> configValues,
        CancellationToken ct = default);
    Task<Dictionary<string, object>> ReadGameConfigAsync(string serverDirectory,
        CancellationToken ct = default);

    Task OnBeforeFirstStartAsync(string serverDirectory, CancellationToken ct = default);

    int GetDefaultPort();
}
