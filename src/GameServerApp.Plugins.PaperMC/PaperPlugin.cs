using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;
using GameServerApp.Plugins.Minecraft;

namespace GameServerApp.Plugins.PaperMC;

public sealed class PaperPlugin : IGameServerPlugin
{
    private static readonly HttpClient Http = new();
    private const string FillApiBase = "https://fill.papermc.io/v3/projects/paper";

    public string GameId => "paper";
    public string DisplayName => "Minecraft PaperMC";
    public string? Description => "High-performance Minecraft server with Bukkit/Spigot plugin support.";
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

    // --- Fill v3 API: Versions ---

    public async Task<string> GetLatestVersionAsync(CancellationToken ct = default)
    {
        var versions = await GetAvailableVersionsAsync(ct);
        return versions.Count > 0
            ? versions[0]
            : throw new InvalidOperationException("No PaperMC versions available");
    }

    public async Task<IReadOnlyList<string>> GetAvailableVersionsAsync(CancellationToken ct = default)
    {
        var data = await Http.GetFromJsonAsync<JsonElement>(
            $"{FillApiBase}/versions", ct);

        var versionsArray = data.GetProperty("versions");
        var versions = new List<string>();

        foreach (var entry in versionsArray.EnumerateArray())
        {
            var versionInfo = entry.GetProperty("version");
            var versionId = versionInfo.GetProperty("id").GetString();
            if (versionId is null) continue;

            // Filter out pre-releases and release candidates
            if (versionId.Contains("-pre") || versionId.Contains("-rc"))
                continue;

            versions.Add(versionId);
        }

        // API already returns newest first
        return versions.AsReadOnly();
    }

    // --- Fill v3 API: Download ---

    public async Task DownloadServerAsync(string version, string targetDirectory,
        IProgress<double>? progress = null, CancellationToken ct = default,
        Action<string>? logOutput = null)
    {
        progress?.Report(0.0);

        // 1. Get version metadata (java requirement + builds)
        var versionsData = await Http.GetFromJsonAsync<JsonElement>(
            $"{FillApiBase}/versions", ct);

        int javaMajor = 21;
        int latestBuild = -1;

        foreach (var entry in versionsData.GetProperty("versions").EnumerateArray())
        {
            var versionInfo = entry.GetProperty("version");
            if (versionInfo.GetProperty("id").GetString() != version)
                continue;

            // Get Java minimum from version metadata
            if (versionInfo.TryGetProperty("java", out var javaProp) &&
                javaProp.TryGetProperty("version", out var javaVer) &&
                javaVer.TryGetProperty("minimum", out var minJava))
            {
                javaMajor = minJava.GetInt32();
            }

            // Get the latest build number
            var builds = entry.GetProperty("builds");
            if (builds.GetArrayLength() > 0)
            {
                // Find the max build number
                foreach (var b in builds.EnumerateArray())
                {
                    var buildNum = b.GetInt32();
                    if (buildNum > latestBuild)
                        latestBuild = buildNum;
                }
            }

            break;
        }

        if (latestBuild < 0)
            throw new InvalidOperationException($"No builds available for PaperMC {version}");

        progress?.Report(0.05);

        // 2. Get build details (download URL + SHA256)
        var buildData = await Http.GetFromJsonAsync<JsonElement>(
            $"{FillApiBase}/versions/{version}/builds/{latestBuild}", ct);

        var downloads = buildData.GetProperty("downloads").GetProperty("server:default");
        var jarName = downloads.GetProperty("name").GetString()
            ?? throw new InvalidOperationException("JAR name not found in build data");
        var downloadUrl = downloads.GetProperty("url").GetString()
            ?? throw new InvalidOperationException("Download URL not found in build data");
        var expectedSha256 = downloads.GetProperty("checksums")
            .GetProperty("sha256").GetString();

        progress?.Report(0.10);

        // 3. Ensure Java is available
        var runtimesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameServerApp", "runtimes");

        var javaProgress = new Progress<double>(p =>
            progress?.Report(0.10 + 0.35 * p));

        var javaHome = await JavaManager.EnsureJavaAsync(runtimesPath, javaMajor, javaProgress, ct);

        var javaInfoPath = Path.Combine(targetDirectory, ".java-home");
        await File.WriteAllTextAsync(javaInfoPath, javaHome, ct);

        progress?.Report(0.45);

        // 4. Download the Paper jar using the direct URL from the API
        var jarPath = Path.Combine(targetDirectory, "server.jar");

        using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
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
                    progress?.Report(0.45 + 0.50 * ((double)bytesRead / totalBytes));
            }
        }

        progress?.Report(0.95);

        // 5. Verify SHA256
        if (expectedSha256 is not null)
        {
            var actualSha256 = await ComputeSha256Async(jarPath, ct);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SHA256 verification failed for Paper server jar");
        }

        progress?.Report(1.0);
    }

    public Task<bool> ValidateInstallationAsync(string serverDirectory, CancellationToken ct = default)
    {
        var jarPath = Path.Combine(serverDirectory, "server.jar");
        return Task.FromResult(File.Exists(jarPath));
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

    // --- Private helpers ---

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

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }
}
