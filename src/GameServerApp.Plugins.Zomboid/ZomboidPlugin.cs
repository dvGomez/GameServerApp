using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using GameServerApp.Core.Interfaces;
using GameServerApp.Core.Models;
using GameServerApp.Core.Services;

namespace GameServerApp.Plugins.Zomboid;

public sealed class ZomboidPlugin : IGameServerPlugin
{
    private const int SteamAppId = 380870;

    public string GameId => "zomboid";
    public string DisplayName => "Project Zomboid";
    public string? Description => "Host a Project Zomboid dedicated server (Steam or Non-Steam).";
    public string? IconResourceKey => "zomboid";
    public int GracefulStopTimeoutMs => 20_000;

    public IReadOnlyList<ConfigField> GetConfigSchema() =>
    [
        new() { Key = "PublicName", DisplayName = "Server Name", FieldType = ConfigFieldType.String, DefaultValue = "My PZ Server", Category = "General" },
        new() { Key = "PublicDescription", DisplayName = "Description", FieldType = ConfigFieldType.String, DefaultValue = "", Category = "General" },
        new() { Key = "MaxPlayers", DisplayName = "Max Players", FieldType = ConfigFieldType.Int, DefaultValue = 32, MinValue = 1, MaxValue = 100, Category = "General" },
        new() { Key = "DefaultPort", DisplayName = "Server Port", FieldType = ConfigFieldType.Int, DefaultValue = 16261, MinValue = 1, MaxValue = 65535, Category = "Network" },
        new() { Key = "Password", DisplayName = "Server Password", FieldType = ConfigFieldType.String, DefaultValue = "", Description = "Leave empty for no password", Category = "Network" },
        new() { Key = "NoSteam", DisplayName = "Non-Steam Mode", FieldType = ConfigFieldType.Bool, DefaultValue = false, Description = "Allow non-Steam clients to connect", Category = "Network" },
        new() { Key = "Open", DisplayName = "Public Server", FieldType = ConfigFieldType.Bool, DefaultValue = true, Description = "Show in public server list", Category = "Network" },
        new() { Key = "PVP", DisplayName = "PvP Enabled", FieldType = ConfigFieldType.Bool, DefaultValue = true, Category = "Gameplay" },
        new() { Key = "PauseEmpty", DisplayName = "Pause When Empty", FieldType = ConfigFieldType.Bool, DefaultValue = true, Category = "Gameplay" },
        new() { Key = "Map", DisplayName = "Map", FieldType = ConfigFieldType.String, DefaultValue = "Muldraugh, KY", Category = "World" },
        new() { Key = "SpawnPoint", DisplayName = "Spawn Point", FieldType = ConfigFieldType.String, DefaultValue = "0,0,0", Description = "X,Y,Z coordinates (0,0,0 = random)", Category = "World" },
        new() { Key = "SafeHouse", DisplayName = "Safe Houses", FieldType = ConfigFieldType.Bool, DefaultValue = true, Category = "Gameplay" },
        new() { Key = "SleepAllowed", DisplayName = "Sleep Allowed", FieldType = ConfigFieldType.Bool, DefaultValue = false, Category = "Gameplay" },
        new() { Key = "SleepNeeded", DisplayName = "Sleep Needed", FieldType = ConfigFieldType.Bool, DefaultValue = false, Category = "Gameplay" },
        new() { Key = "SteamPort1", DisplayName = "Steam Port 1", FieldType = ConfigFieldType.Int, DefaultValue = 8766, MinValue = 1, MaxValue = 65535, Category = "Network" },
        new() { Key = "SteamPort2", DisplayName = "Steam Port 2", FieldType = ConfigFieldType.Int, DefaultValue = 8767, MinValue = 1, MaxValue = 65535, Category = "Network" },
        new() { Key = "RCONPort", DisplayName = "RCON Port", FieldType = ConfigFieldType.Int, DefaultValue = 27015, MinValue = 1, MaxValue = 65535, Description = "Remote console port (0 = disabled)", Category = "Network" },
        new() { Key = "RCONPassword", DisplayName = "RCON Password", FieldType = ConfigFieldType.String, DefaultValue = "", Category = "Network" },
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
        var noSteam = GetBoolSetting(config.GameSettings, "NoSteam");
        var serverName = "servertest";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var javaExe = Path.Combine(config.ServerDirectory, "jre64", "bin", "java.exe");
            if (!File.Exists(javaExe))
                javaExe = "java";

            var classpath = BuildClasspath(config.ServerDirectory);
            var steamFlag = noSteam ? "0" : "1";

            var args = string.Join(" ",
                "-Djava.awt.headless=true",
                $"-Dzomboid.steam={steamFlag}",
                "-Dzomboid.znetlog=1",
                "-XX:+UseZGC",
                "-XX:-CreateCoredumpOnCrash",
                "-XX:-OmitStackTraceInFastThrow",
                $"-Xms{config.MemoryMb}M",
                $"-Xmx{config.MemoryMb}M",
                $"-Djava.library.path=natives/;natives/win64/;.",
                $"-cp \"{classpath}\"",
                "zombie.network.GameServer",
                $"-servername \"{serverName}\"");

            return new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = args,
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
            var shPath = Path.Combine(config.ServerDirectory, "start-server.sh");
            var gameArgs = $"-servername \"{serverName}\"";
            if (noSteam) gameArgs += " -nosteam";

            return new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{shPath}\" {gameArgs}",
                WorkingDirectory = config.ServerDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
        }
    }

    private static string BuildClasspath(string serverDir)
    {
        var javaDir = Path.Combine(serverDir, "java");
        if (!Directory.Exists(javaDir))
            return ".";

        var jars = Directory.GetFiles(javaDir, "*.jar")
            .Select(f => "java/" + Path.GetFileName(f))
            .Append("java/");

        return string.Join(";", jars);
    }

    public string? GetGracefulStopCommand() => "quit";

    public ConsoleOutputLine ParseConsoleLine(string rawLine) =>
        ZomboidConsoleParser.Parse(rawLine);

    public int? ParsePlayerDelta(string rawLine)
    {
        if (rawLine.Contains("fully connected", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (rawLine.Contains("disconnected", StringComparison.OrdinalIgnoreCase) &&
            rawLine.Contains("player", StringComparison.OrdinalIgnoreCase))
            return -1;
        return null;
    }

    public int GetDefaultPort() => 16261;

    public Task<string> GetLatestVersionAsync(CancellationToken ct = default)
    {
        return Task.FromResult("steam");
    }

    public Task<IReadOnlyList<string>> GetAvailableVersionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> versions = new List<string> { "steam" }.AsReadOnly();
        return Task.FromResult(versions);
    }

    public async Task DownloadServerAsync(string version, string targetDirectory,
        IProgress<double>? progress = null, CancellationToken ct = default,
        Action<string>? logOutput = null)
    {
        await SteamCmdManager.InstallOrUpdateAppAsync(SteamAppId, targetDirectory, progress, ct, logOutput);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var shPath = Path.Combine(targetDirectory, "start-server.sh");
            if (File.Exists(shPath))
            {
                var chmod = Process.Start("chmod", $"+x \"{shPath}\"");
                if (chmod != null) await chmod.WaitForExitAsync(ct);
            }
        }
    }

    public Task<bool> ValidateInstallationAsync(string serverDirectory, CancellationToken ct = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var batPath = Path.Combine(serverDirectory, "StartServer64.bat");
            return Task.FromResult(File.Exists(batPath));
        }
        else
        {
            var shPath = Path.Combine(serverDirectory, "start-server.sh");
            return Task.FromResult(File.Exists(shPath));
        }
    }

    public Task WriteGameConfigAsync(string serverDirectory, Dictionary<string, object> configValues,
        CancellationToken ct = default)
    {
        var serverName = "servertest";
        var zomboidDataDir = GetZomboidDataDirectory();
        var configDir = Path.Combine(zomboidDataDir, "Server");
        Directory.CreateDirectory(configDir);

        var iniPath = Path.Combine(configDir, $"{serverName}.ini");
        ZomboidIniConfig.Write(iniPath, configValues);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object>> ReadGameConfigAsync(string serverDirectory,
        CancellationToken ct = default)
    {
        var serverName = "servertest";
        var zomboidDataDir = GetZomboidDataDirectory();
        var iniPath = Path.Combine(zomboidDataDir, "Server", $"{serverName}.ini");
        return Task.FromResult(ZomboidIniConfig.Read(iniPath));
    }

    public Task OnBeforeFirstStartAsync(string serverDirectory, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    private static string GetZomboidDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Zomboid");
        }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Zomboid");
    }

    private static bool GetBoolSetting(Dictionary<string, object> settings, string key)
    {
        if (!settings.TryGetValue(key, out var val)) return false;
        if (val is bool b) return b;
        if (val is JsonElement je) return je.ValueKind == JsonValueKind.True;
        if (val is string s) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
        return false;
    }
}
