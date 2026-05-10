using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;

namespace GameServerApp.Plugins.FiveM;

public sealed class FiveMPlugin : IGameServerPlugin
{
    private static readonly HttpClient Http = new();
    private Dictionary<string, string>? _versionDownloadUrls;

    public string GameId => "fivem";
    public string DisplayName => "FiveM (GTA V)";
    public string? Description => "Host a FiveM server for GTA V multiplayer.";
    public string? IconResourceKey => "fivem";
    public int GracefulStopTimeoutMs => 15_000;

    public IReadOnlyList<ConfigField> GetConfigSchema() =>
    [
        new() { Key = "sv_hostname", DisplayName = "Server Name", FieldType = ConfigFieldType.String, DefaultValue = "My FiveM Server", Category = "General" },
        new() { Key = "sv_maxclients", DisplayName = "Max Players", FieldType = ConfigFieldType.Int, DefaultValue = 48, MinValue = 1, MaxValue = 2048, Category = "General" },
        new() { Key = "server-port", DisplayName = "Server Port", FieldType = ConfigFieldType.Int, DefaultValue = 30120, MinValue = 1, MaxValue = 65535, Category = "Network" },
        new() { Key = "sv_licenseKey", DisplayName = "License Key", FieldType = ConfigFieldType.String, DefaultValue = "changeme", Description = "Get your key at keymaster.fivem.net", Category = "Network" },
        new() { Key = "steam_webApiKey", DisplayName = "Steam Web API Key", FieldType = ConfigFieldType.String, DefaultValue = "", Description = "Optional, from steamcommunity.com/dev/apikey", Category = "Network" },
        new() { Key = "sv_scriptHookAllowed", DisplayName = "Allow ScriptHook", FieldType = ConfigFieldType.Bool, DefaultValue = false, Category = "Security" },
        new() { Key = "sv_enforceGameBuild", DisplayName = "Game Build", FieldType = ConfigFieldType.Enum, DefaultValue = "", EnumOptions = ["", "2802", "2944", "3095", "3258", "3323"], Description = "Force specific GTA build (empty = latest)", Category = "General" },
        new() { Key = "sets tags", DisplayName = "Tags", FieldType = ConfigFieldType.String, DefaultValue = "default", Category = "General" },
        new() { Key = "sets locale", DisplayName = "Locale", FieldType = ConfigFieldType.String, DefaultValue = "en-US", Category = "General" },
    ];

    public Dictionary<string, object> GetDefaultConfig()
    {
        var config = new Dictionary<string, object>();
        foreach (var field in GetConfigSchema())
        {
            if (field.DefaultValue is not null)
                config[field.Key] = field.DefaultValue;
        }
        return config;
    }

    public ProcessStartInfo BuildStartInfo(ServerConfig config)
    {
        var port = 30120;
        if (config.GameSettings.TryGetValue("server-port", out var portObj))
        {
            if (portObj is int p) port = p;
            else if (portObj is JsonElement je && je.TryGetInt32(out var jp)) port = jp;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exePath = Path.Combine(config.ServerDirectory, "server", "FXServer.exe");
            return new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"+exec server.cfg +endpoint_add_tcp \"0.0.0.0:{port}\" +endpoint_add_udp \"0.0.0.0:{port}\"",
                WorkingDirectory = config.ServerDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
        }
        else
        {
            var runScript = Path.Combine(config.ServerDirectory, "server", "run.sh");
            return new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{runScript}\" +exec server.cfg +endpoint_add_tcp \"0.0.0.0:{port}\" +endpoint_add_udp \"0.0.0.0:{port}\"",
                WorkingDirectory = config.ServerDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
        }
    }

    public string? GetGracefulStopCommand() => "quit";

    public ConsoleOutputLine ParseConsoleLine(string rawLine) =>
        FiveMConsoleParser.Parse(rawLine);

    public int? ParsePlayerDelta(string rawLine)
    {
        if (rawLine.Contains("joined the server", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("Authenticating a new player", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (rawLine.Contains("dropped", StringComparison.OrdinalIgnoreCase) &&
            rawLine.Contains("Player", StringComparison.OrdinalIgnoreCase))
            return -1;
        return null;
    }

    public int GetDefaultPort() => 30120;

    public async Task<string> GetLatestVersionAsync(CancellationToken ct = default)
    {
        var versions = await GetAvailableVersionsAsync(ct);
        return versions.Count > 0
            ? versions[0]
            : throw new InvalidOperationException("Could not determine latest FiveM version");
    }

    public async Task<IReadOnlyList<string>> GetAvailableVersionsAsync(CancellationToken ct = default)
    {
        await FetchVersionInfoAsync(ct);
        return _versionDownloadUrls!.Keys.ToList().AsReadOnly();
    }

    private async Task FetchVersionInfoAsync(CancellationToken ct)
    {
        if (_versionDownloadUrls != null) return;

        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win32" : "linux";
        var url = $"https://changelogs-live.fivem.net/api/changelog/versions/{os}/server";

        var response = await Http.GetStringAsync(url, ct);
        var json = JsonDocument.Parse(response);
        var urls = new Dictionary<string, string>();

        TryAddVersion(json.RootElement, "recommended", "recommended_download", urls);
        TryAddVersion(json.RootElement, "latest", "latest_download", urls);
        TryAddVersion(json.RootElement, "critical", "critical_download", urls);
        TryAddVersion(json.RootElement, "optional", "optional_download", urls);

        if (urls.Count == 0)
            throw new InvalidOperationException("Could not determine available FiveM versions");

        _versionDownloadUrls = urls;
    }

    private static void TryAddVersion(JsonElement root, string versionKey, string urlKey, Dictionary<string, string> urls)
    {
        if (root.TryGetProperty(versionKey, out var versionProp) &&
            root.TryGetProperty(urlKey, out var urlProp))
        {
            var version = versionProp.GetString();
            var downloadUrl = urlProp.GetString();
            if (version != null && downloadUrl != null && !urls.ContainsKey(version))
                urls[version] = downloadUrl;
        }
    }

    public async Task DownloadServerAsync(string version, string targetDirectory,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(0.0);

        await FetchVersionInfoAsync(ct);

        if (!_versionDownloadUrls!.TryGetValue(version, out var downloadUrl))
            throw new InvalidOperationException($"No download URL found for FiveM build {version}");

        progress?.Report(0.05);

        var serverDir = Path.Combine(targetDirectory, "server");
        Directory.CreateDirectory(serverDir);

        var archiveName = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? "server.zip"
            : "fx.tar.xz";
        var archivePath = Path.Combine(targetDirectory, archiveName);

        using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            long bytesRead = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = File.Create(archivePath);
            var buffer = new byte[81920];
            int read;

            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report(0.05 + 0.75 * ((double)bytesRead / totalBytes));
            }
        }

        progress?.Report(0.8);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, serverDir, overwriteFiles: true);
        }
        else
        {
            await ExtractTarXzAsync(archivePath, serverDir, ct);
        }

        File.Delete(archivePath);
        progress?.Report(1.0);
    }

    public async Task<bool> ValidateInstallationAsync(string serverDirectory, CancellationToken ct = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exePath = Path.Combine(serverDirectory, "server", "FXServer.exe");
            return await Task.FromResult(File.Exists(exePath));
        }
        else
        {
            var runScript = Path.Combine(serverDirectory, "server", "run.sh");
            return await Task.FromResult(File.Exists(runScript));
        }
    }

    public Task WriteGameConfigAsync(string serverDirectory, Dictionary<string, object> configValues,
        CancellationToken ct = default)
    {
        var cfgPath = Path.Combine(serverDirectory, "server.cfg");
        FiveMServerConfig.Write(cfgPath, configValues);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object>> ReadGameConfigAsync(string serverDirectory,
        CancellationToken ct = default)
    {
        var cfgPath = Path.Combine(serverDirectory, "server.cfg");
        return Task.FromResult(FiveMServerConfig.Read(cfgPath));
    }

    public Task OnBeforeFirstStartAsync(string serverDirectory, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.Combine(serverDirectory, "resources"));
        return Task.CompletedTask;
    }

    private static async Task ExtractTarXzAsync(string archivePath, string targetDir, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"xf \"{archivePath}\" -C \"{targetDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);
    }
}
