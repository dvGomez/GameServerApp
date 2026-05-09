using System.Collections.ObjectModel;
using GameServerApp.Core.Events;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace GameServerApp.Core.Services;

public sealed class ServerManager : IServerManager
{
    private readonly IProcessManager _processManager;
    private readonly IConfigurationService _configService;
    private readonly Dictionary<string, IGameServerPlugin> _plugins;
    private readonly Dictionary<string, ServerConfig> _configs = new();
    private readonly Dictionary<string, List<ConsoleOutputLine>> _consoleBuffers = new();
    private readonly ILogger<ServerManager> _logger;

    private const int MaxBufferLines = 5000;
    private const int BufferTrimCount = 1000;

    public ObservableCollection<ServerInstance> Instances { get; } = new();

    public event EventHandler<ConsoleOutputEventArgs>? ConsoleOutput;
    public event EventHandler<ServerStateChangedEventArgs>? ServerStateChanged;
    public event EventHandler<ServerConfigChangedEventArgs>? ServerConfigChanged;

    public ServerManager(
        IProcessManager processManager,
        IConfigurationService configService,
        IEnumerable<IGameServerPlugin> plugins,
        ILogger<ServerManager> logger)
    {
        _processManager = processManager;
        _configService = configService;
        _logger = logger;
        _plugins = plugins.ToDictionary(p => p.GameId);

        _processManager.OutputReceived += OnProcessOutput;
        _processManager.StateChanged += OnProcessStateChanged;
    }

    private void OnProcessOutput(object? sender, ConsoleOutputEventArgs e)
    {
        try
        {
            BufferLine(e.InstanceId, e.Line);

            var instance = Instances.FirstOrDefault(i => i.Id == e.InstanceId);
            if (instance != null && _configs.TryGetValue(e.InstanceId, out var cfg) &&
                _plugins.TryGetValue(cfg.GameId, out var plugin))
            {
                var delta = plugin.ParsePlayerDelta(e.Line.Text);
                if (delta.HasValue)
                {
                    instance.OnlinePlayers = Math.Max(0, instance.OnlinePlayers + delta.Value);
                }
            }

            ConsoleOutput?.Invoke(this, e);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error forwarding console output"); }
    }

    private void OnProcessStateChanged(object? sender, ServerStateChangedEventArgs e)
    {
        try
        {
            if (e.NewState == ServerState.Stopped || e.NewState == ServerState.Error)
            {
                var instance = Instances.FirstOrDefault(i => i.Id == e.InstanceId);
                if (instance != null)
                    instance.OnlinePlayers = 0;
            }

            ServerStateChanged?.Invoke(this, e);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error forwarding state change"); }
    }

    public IGameServerPlugin? GetPlugin(string gameId) =>
        _plugins.GetValueOrDefault(gameId);

    public ServerConfig? GetServerConfig(string instanceId) =>
        _configs.GetValueOrDefault(instanceId);

    public IReadOnlyList<ConsoleOutputLine> GetConsoleHistory(string instanceId)
    {
        lock (_consoleBuffers)
        {
            if (_consoleBuffers.TryGetValue(instanceId, out var buffer))
                return buffer.ToList().AsReadOnly();
            return Array.Empty<ConsoleOutputLine>();
        }
    }

    public void AddConsoleEntry(string instanceId, ConsoleOutputLine line)
    {
        BufferLine(instanceId, line);
    }

    private void BufferLine(string instanceId, ConsoleOutputLine line)
    {
        lock (_consoleBuffers)
        {
            if (!_consoleBuffers.TryGetValue(instanceId, out var buffer))
            {
                buffer = new List<ConsoleOutputLine>();
                _consoleBuffers[instanceId] = buffer;
            }

            buffer.Add(line);

            if (buffer.Count > MaxBufferLines)
                buffer.RemoveRange(0, BufferTrimCount);
        }
    }

    public async Task<ServerInstance> CreateServerAsync(
        string gameId, string name,
        string? version = null,
        Dictionary<string, object>? initialConfig = null,
        IProgress<double>? downloadProgress = null,
        CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(gameId, out var plugin))
            throw new InvalidOperationException($"No plugin found for game '{gameId}'");

        var instanceId = Guid.NewGuid().ToString("N");
        var serverDir = Path.Combine(_configService.AppDataPath, "servers", gameId, instanceId);
        Directory.CreateDirectory(serverDir);

        var memoryMb = 1024;
        if (initialConfig?.TryGetValue("memory-mb", out var mem) == true && mem is int m)
            memoryMb = m;

        var config = new ServerConfig
        {
            InstanceId = instanceId,
            GameId = gameId,
            Name = name,
            ServerDirectory = serverDir,
            MemoryMb = memoryMb,
            GameSettings = plugin.GetDefaultConfig()
        };

        _logger.LogInformation("Downloading server for {GameId}...", gameId);
        version ??= await plugin.GetLatestVersionAsync(ct);
        config.ServerVersion = version;

        await plugin.DownloadServerAsync(version, serverDir, downloadProgress, ct);
        await plugin.OnBeforeFirstStartAsync(serverDir, ct);
        await plugin.WriteGameConfigAsync(serverDir, config.GameSettings, ct);
        await _configService.SaveServerConfigAsync(config, ct);

        _configs[instanceId] = config;

        var instance = new ServerInstance
        {
            Id = instanceId,
            Name = name,
            GameId = gameId,
            ServerDirectory = serverDir,
            Port = GetPortFromConfig(config, plugin),
            Version = version,
            MaxPlayers = GetMaxPlayersFromConfig(config)
        };

        Instances.Add(instance);
        _logger.LogInformation("Server '{Name}' created with id {InstanceId}", name, instanceId);

        return instance;
    }

    public async Task StartServerAsync(string instanceId, CancellationToken ct = default)
    {
        var instance = GetInstance(instanceId);
        var config = GetRequiredConfig(instanceId);
        var plugin = GetRequiredPlugin(config.GameId);

        try
        {
            var currentSettings = await plugin.ReadGameConfigAsync(config.ServerDirectory, ct);
            config.GameSettings = currentSettings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read game config, using last known config");
        }

        var startInfo = plugin.BuildStartInfo(config);
        await _processManager.StartAsync(instance, startInfo, ct);
    }

    public async Task StopServerAsync(string instanceId, CancellationToken ct = default)
    {
        var instance = GetInstance(instanceId);
        var config = GetRequiredConfig(instanceId);
        var plugin = GetRequiredPlugin(config.GameId);

        await _processManager.StopGracefullyAsync(
            instance, plugin.GetGracefulStopCommand(), plugin.GracefulStopTimeoutMs, ct);
    }

    public async Task RestartServerAsync(string instanceId, CancellationToken ct = default)
    {
        try
        {
            await StopServerAsync(instanceId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping server during restart, continuing with start");
        }

        await Task.Delay(1000, ct);
        await StartServerAsync(instanceId, ct);
    }

    public async Task SendCommandAsync(string instanceId, string command)
    {
        var instance = GetInstance(instanceId);
        await _processManager.SendCommandAsync(instance, command);
    }

    public async Task DeleteServerAsync(string instanceId, CancellationToken ct = default)
    {
        var instance = Instances.FirstOrDefault(i => i.Id == instanceId);
        if (instance != null)
        {
            if (_processManager.IsRunning(instance))
            {
                try { await StopServerAsync(instanceId, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error stopping server during delete"); }
            }

            Instances.Remove(instance);
        }

        try { await _configService.DeleteServerConfigAsync(instanceId, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error deleting config for {InstanceId}", instanceId); }

        _configs.Remove(instanceId);
    }

    public async Task SaveConfigAsync(string instanceId, Dictionary<string, object> config,
        CancellationToken ct = default)
    {
        var serverConfig = GetRequiredConfig(instanceId);
        var plugin = GetRequiredPlugin(serverConfig.GameId);

        serverConfig.GameSettings = config;
        await plugin.WriteGameConfigAsync(serverConfig.ServerDirectory, config, ct);
        await _configService.SaveServerConfigAsync(serverConfig, ct);

        // Sync the in-memory ServerInstance so every UI component sees the new values
        var instance = Instances.FirstOrDefault(i => i.Id == instanceId);
        if (instance != null)
        {
            instance.Name = serverConfig.Name;
            instance.Port = GetPortFromConfig(serverConfig, plugin);
            instance.MaxPlayers = GetMaxPlayersFromConfig(serverConfig);
            instance.Version = serverConfig.ServerVersion ?? instance.Version;
        }

        ServerConfigChanged?.Invoke(this, new ServerConfigChangedEventArgs
        {
            InstanceId = instanceId,
            ServerName = serverConfig.Name,
            Port = instance?.Port ?? 0,
            MaxPlayers = instance?.MaxPlayers ?? 0,
            Version = instance?.Version ?? string.Empty
        });
    }

    public async Task LoadAllServersAsync(CancellationToken ct = default)
    {
        try
        {
            var configs = await _configService.LoadAllServerConfigsAsync(ct);
            foreach (var config in configs)
            {
                _configs[config.InstanceId] = config;
                _plugins.TryGetValue(config.GameId, out var plugin);

                var instance = new ServerInstance
                {
                    Id = config.InstanceId,
                    Name = config.Name,
                    GameId = config.GameId,
                    ServerDirectory = config.ServerDirectory,
                    Port = GetPortFromConfig(config, plugin),
                    Version = config.ServerVersion ?? string.Empty,
                    MaxPlayers = GetMaxPlayersFromConfig(config)
                };

                Instances.Add(instance);
            }

            _logger.LogInformation("Loaded {Count} server(s) from disk", configs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading servers from disk");
        }
    }

    private static int GetPortFromConfig(ServerConfig config, IGameServerPlugin? plugin)
    {
        if (config.GameSettings.TryGetValue("server-port", out var portObj))
        {
            if (portObj is int port) return port;
            if (portObj is System.Text.Json.JsonElement je && je.TryGetInt32(out var p)) return p;
        }

        return plugin?.GetDefaultPort() ?? 0;
    }

    private static int GetMaxPlayersFromConfig(ServerConfig config)
    {
        if (config.GameSettings.TryGetValue("max-players", out var obj))
        {
            if (obj is int max) return max;
            if (obj is System.Text.Json.JsonElement je && je.TryGetInt32(out var m)) return m;
        }
        return 20;
    }

    private ServerInstance GetInstance(string instanceId) =>
        Instances.FirstOrDefault(i => i.Id == instanceId)
        ?? throw new InvalidOperationException($"Server instance '{instanceId}' not found");

    private ServerConfig GetRequiredConfig(string instanceId) =>
        _configs.GetValueOrDefault(instanceId)
        ?? throw new InvalidOperationException($"Config for instance '{instanceId}' not found");

    private IGameServerPlugin GetRequiredPlugin(string gameId) =>
        _plugins.GetValueOrDefault(gameId)
        ?? throw new InvalidOperationException($"Plugin for game '{gameId}' not found");
}
