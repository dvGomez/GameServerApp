using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;

namespace GameServerApp.Plugins.Minecraft;

public sealed class MinecraftPlugin : IGameServerPlugin
{
    private static readonly HttpClient Http = new();

    public string GameId => "minecraft";
    public string DisplayName => "Minecraft Java Edition";
    public string? Description => "Host a Minecraft Java server for you and your friends.";
    public string? IconResourceKey => "minecraft";
    public int GracefulStopTimeoutMs => 30_000;

    public IReadOnlyList<ConfigField> GetConfigSchema() =>
    [
        new() { Key = "server-port", DisplayName = "Server Port", FieldType = ConfigFieldType.Int, DefaultValue = 25565, MinValue = 1, MaxValue = 65535, Category = "Network" },
        new() { Key = "max-players", DisplayName = "Max Players", FieldType = ConfigFieldType.Int, DefaultValue = 20, MinValue = 1, MaxValue = 1000, Category = "General" },
        new() { Key = "motd", DisplayName = "Message of the Day", FieldType = ConfigFieldType.String, DefaultValue = "A Minecraft Server", Category = "General" },
        new() { Key = "level-name", DisplayName = "World Name", FieldType = ConfigFieldType.String, DefaultValue = "world", Category = "World" },
        new() { Key = "gamemode", DisplayName = "Game Mode", FieldType = ConfigFieldType.Enum, DefaultValue = "survival", EnumOptions = ["survival", "creative", "adventure", "spectator"], Category = "Gameplay" },
        new() { Key = "difficulty", DisplayName = "Difficulty", FieldType = ConfigFieldType.Enum, DefaultValue = "easy", EnumOptions = ["peaceful", "easy", "normal", "hard"], Category = "Gameplay" },
        new() { Key = "pvp", DisplayName = "PvP Enabled", FieldType = ConfigFieldType.Bool, DefaultValue = true, Category = "Gameplay" },
        new() { Key = "online-mode", DisplayName = "Online Mode", FieldType = ConfigFieldType.Bool, DefaultValue = true, Description = "Require Microsoft authentication", Category = "Network" },
        new() { Key = "white-list", DisplayName = "Whitelist", FieldType = ConfigFieldType.Bool, DefaultValue = false, Category = "Network" },
        new() { Key = "spawn-protection", DisplayName = "Spawn Protection Radius", FieldType = ConfigFieldType.Int, DefaultValue = 16, MinValue = 0, MaxValue = 256, Category = "World" },
        new() { Key = "view-distance", DisplayName = "View Distance", FieldType = ConfigFieldType.Int, DefaultValue = 10, MinValue = 2, MaxValue = 32, Category = "Performance" },
        new() { Key = "enable-command-block", DisplayName = "Command Blocks", FieldType = ConfigFieldType.Bool, DefaultValue = false, Category = "Gameplay" },
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
        var jarPath = Path.Combine(config.ServerDirectory, "server.jar");
        var memoryMb = config.MemoryMb;

        var javaPath = ResolveJavaPath(config.ServerDirectory);

        return new ProcessStartInfo
        {
            FileName = javaPath,
            Arguments = $"-Xmx{memoryMb}M -Xms{memoryMb}M -jar \"{jarPath}\" nogui",
            WorkingDirectory = config.ServerDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
    }

    public string? GetGracefulStopCommand() => "stop";

    public ConsoleOutputLine ParseConsoleLine(string rawLine) =>
        MinecraftConsoleParser.Parse(rawLine);

    public int? ParsePlayerDelta(string rawLine)
    {
        if (rawLine.Contains("joined the game")) return 1;
        if (rawLine.Contains("left the game")) return -1;
        return null;
    }

    public int GetDefaultPort() => 25565;

    public async Task<string> GetLatestVersionAsync(CancellationToken ct = default)
    {
        var manifest = await Http.GetFromJsonAsync<JsonElement>(
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);

        return manifest.GetProperty("latest").GetProperty("release").GetString()
            ?? throw new InvalidOperationException("Could not determine latest Minecraft version");
    }

    public async Task<IReadOnlyList<string>> GetAvailableVersionsAsync(CancellationToken ct = default)
    {
        var manifest = await Http.GetFromJsonAsync<JsonElement>(
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);

        var versions = new List<string>();
        foreach (var v in manifest.GetProperty("versions").EnumerateArray())
        {
            if (v.GetProperty("type").GetString() == "release")
                versions.Add(v.GetProperty("id").GetString()!);
        }

        return versions.AsReadOnly();
    }

    public async Task DownloadServerAsync(string version, string targetDirectory,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(0.0);

        var manifest = await Http.GetFromJsonAsync<JsonElement>(
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);

        progress?.Report(0.05);

        var versions = manifest.GetProperty("versions");
        string? versionUrl = null;

        foreach (var v in versions.EnumerateArray())
        {
            if (v.GetProperty("id").GetString() == version)
            {
                versionUrl = v.GetProperty("url").GetString();
                break;
            }
        }

        if (versionUrl is null)
            throw new InvalidOperationException($"Minecraft version '{version}' not found");

        progress?.Report(0.1);

        var versionData = await Http.GetFromJsonAsync<JsonElement>(versionUrl, ct);

        var javaMajor = 21;
        if (versionData.TryGetProperty("javaVersion", out var javaVersionProp) &&
            javaVersionProp.TryGetProperty("majorVersion", out var majorProp))
        {
            javaMajor = majorProp.GetInt32();
        }

        progress?.Report(0.15);

        var runtimesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameServerApp", "runtimes");

        var javaProgress = new Progress<double>(p =>
            progress?.Report(0.15 + 0.35 * p));

        var javaHome = await JavaManager.EnsureJavaAsync(runtimesPath, javaMajor, javaProgress, ct);

        var javaInfoPath = Path.Combine(targetDirectory, ".java-home");
        await File.WriteAllTextAsync(javaInfoPath, javaHome, ct);

        progress?.Report(0.5);

        var serverDownload = versionData.GetProperty("downloads").GetProperty("server");
        var serverUrl = serverDownload.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("Server download URL not found");
        var expectedSha1 = serverDownload.GetProperty("sha1").GetString();

        var jarPath = Path.Combine(targetDirectory, "server.jar");
        using (var response = await Http.GetAsync(serverUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            long bytesRead = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = File.Create(jarPath);
            var buffer = new byte[81920];
            int read;

            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report(0.5 + 0.45 * ((double)bytesRead / totalBytes));
            }
        }

        if (expectedSha1 is not null)
        {
            var actualSha1 = await ComputeSha1Async(jarPath, ct);
            if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SHA1 verification failed for server.jar");
        }

        progress?.Report(1.0);
    }

    public async Task<bool> ValidateInstallationAsync(string serverDirectory, CancellationToken ct = default)
    {
        var jarPath = Path.Combine(serverDirectory, "server.jar");
        return await Task.FromResult(File.Exists(jarPath));
    }

    public Task WriteGameConfigAsync(string serverDirectory, Dictionary<string, object> configValues,
        CancellationToken ct = default)
    {
        var propsPath = Path.Combine(serverDirectory, "server.properties");
        MinecraftServerProperties.Write(propsPath, configValues);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object>> ReadGameConfigAsync(string serverDirectory,
        CancellationToken ct = default)
    {
        var propsPath = Path.Combine(serverDirectory, "server.properties");
        return Task.FromResult(MinecraftServerProperties.Read(propsPath));
    }

    public Task OnBeforeFirstStartAsync(string serverDirectory, CancellationToken ct = default)
    {
        var eulaPath = Path.Combine(serverDirectory, "eula.txt");
        File.WriteAllText(eulaPath, "eula=true\n");
        return Task.CompletedTask;
    }

    private static string ResolveJavaPath(string serverDirectory)
    {
        var javaInfoPath = Path.Combine(serverDirectory, ".java-home");
        if (File.Exists(javaInfoPath))
        {
            var javaHome = File.ReadAllText(javaInfoPath).Trim();
            var javaExe = JavaManager.GetJavaExecutablePath(javaHome);
            if (File.Exists(javaExe))
                return javaExe;
        }

        return "java";
    }

    private static async Task<string> ComputeSha1Async(string filePath, CancellationToken ct)
    {
        using var sha1 = SHA1.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha1.ComputeHashAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }
}
