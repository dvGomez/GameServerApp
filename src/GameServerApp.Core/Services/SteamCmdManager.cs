using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace GameServerApp.Core.Services;

public static class SteamCmdManager
{
    private static readonly HttpClient Http = new();
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public static string GetSteamCmdDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GameServerApp", "runtimes", "steamcmd");
    }

    public static string GetSteamCmdExecutable()
    {
        var dir = GetSteamCmdDirectory();
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(dir, "steamcmd.exe")
            : Path.Combine(dir, "steamcmd.sh");
    }

    public static async Task EnsureSteamCmdAsync(
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var exe = GetSteamCmdExecutable();
        if (File.Exists(exe)) return;

        await Lock.WaitAsync(ct);
        try
        {
            if (File.Exists(exe)) return;
            await DownloadSteamCmdAsync(progress, ct);
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task InstallOrUpdateAppAsync(
        int appId, string installDir,
        IProgress<double>? progress = null, CancellationToken ct = default,
        Action<string>? logOutput = null)
    {
        logOutput?.Invoke("Ensuring SteamCMD is installed...");
        await EnsureSteamCmdAsync(new Progress<double>(p => progress?.Report(p * 0.1)), ct);
        progress?.Report(0.1);

        var exe = GetSteamCmdExecutable();
        var absoluteInstallDir = Path.GetFullPath(installDir);
        Directory.CreateDirectory(absoluteInstallDir);

        var isUpdate = Directory.EnumerateFileSystemEntries(absoluteInstallDir).Any();
        var mode = isUpdate ? "update" : "fresh install";
        logOutput?.Invoke($"Starting {mode} for app {appId} to {absoluteInstallDir}");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (attempt > 0)
                logOutput?.Invoke("SteamCMD self-updated, retrying download...");

            var exitCode = await RunSteamCmdAsync(exe, appId, absoluteInstallDir, isUpdate,
                new Progress<double>(p =>
                {
                    var baseProgress = attempt == 0 ? 0.1 : 0.15;
                    var scale = attempt == 0 ? 0.05 : 0.85;
                    progress?.Report(baseProgress + scale * p);
                }), ct, logOutput);

            if (exitCode == 0)
            {
                logOutput?.Invoke($"SteamCMD finished successfully (app {appId})");
                break;
            }

            if (attempt == 1 && exitCode != 0 && exitCode != 7)
                throw new InvalidOperationException($"SteamCMD exited with code {exitCode}");
        }

        progress?.Report(1.0);
    }

    private static async Task<int> RunSteamCmdAsync(
        string exe, int appId, string installDir, bool validate,
        IProgress<double>? progress, CancellationToken ct,
        Action<string>? logOutput = null)
    {
        var validateFlag = validate ? " validate" : "";
        var args = $"+@ShutdownOnFailedCommand +@NoPromptForPassword +force_install_dir \"{installDir}\" +login anonymous +app_update {appId}{validateFlag} +quit";

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = GetSteamCmdDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = Task.Run(async () =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync(ct);
                if (line == null) continue;

                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    logOutput?.Invoke(trimmed);

                if (line.Contains("Update state") && line.Contains("progress:"))
                {
                    var pctIdx = line.IndexOf("progress:", StringComparison.Ordinal);
                    if (pctIdx >= 0)
                    {
                        var after = line[(pctIdx + 9)..].Trim();
                        var spaceIdx = after.IndexOf(' ');
                        var pctStr = spaceIdx > 0 ? after[..spaceIdx] : after;
                        if (double.TryParse(pctStr.TrimEnd('%'), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var pct))
                        {
                            progress?.Report(pct / 100.0);
                        }
                    }
                }
            }
        }, ct);

        await process.WaitForExitAsync(ct);
        await outputTask;

        return process.ExitCode;
    }

    private static async Task DownloadSteamCmdAsync(
        IProgress<double>? progress, CancellationToken ct)
    {
        var dir = GetSteamCmdDirectory();
        Directory.CreateDirectory(dir);

        string downloadUrl;
        string archiveName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            downloadUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
            archiveName = "steamcmd.zip";
        }
        else
        {
            downloadUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz";
            archiveName = "steamcmd_linux.tar.gz";
        }

        var archivePath = Path.Combine(dir, archiveName);

        progress?.Report(0.0);

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
                    progress?.Report(0.7 * ((double)bytesRead / totalBytes));
            }
        }

        progress?.Report(0.7);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, dir, overwriteFiles: true);
        }
        else
        {
            var psi = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"xzf \"{archivePath}\" -C \"{dir}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi)!;
            await process.WaitForExitAsync(ct);
        }

        File.Delete(archivePath);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var chmod = Process.Start("chmod", $"+x \"{GetSteamCmdExecutable()}\"");
            if (chmod != null) await chmod.WaitForExitAsync(ct);
        }

        progress?.Report(1.0);
    }
}
