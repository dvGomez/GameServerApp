using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GameServerApp.Plugins.Minecraft;

public static class JavaManager
{
    private static readonly HttpClient Http = new();

    public static string GetJavaExecutablePath(string javaHome)
    {
        var bin = Path.Combine(javaHome, "bin");
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(bin, "java.exe")
            : Path.Combine(bin, "java");
    }

    public static async Task<string> EnsureJavaAsync(
        string runtimesBasePath,
        int majorVersion,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var javaDir = Path.Combine(runtimesBasePath, "java", $"jre-{majorVersion}");

        var javaExe = GetJavaExecutablePath(javaDir);
        if (File.Exists(javaExe))
            return javaDir;

        var nested = FindNestedJavaHome(javaDir);
        if (nested != null)
            return nested;

        await DownloadJavaAsync(majorVersion, javaDir, progress, ct);

        nested = FindNestedJavaHome(javaDir);
        return nested ?? javaDir;
    }

    private static string? FindNestedJavaHome(string baseDir)
    {
        if (!Directory.Exists(baseDir))
            return null;

        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";

        foreach (var dir in Directory.GetDirectories(baseDir))
        {
            var candidate = Path.Combine(dir, "bin", exeName);
            if (File.Exists(candidate))
                return dir;
        }

        return null;
    }

    private static async Task DownloadJavaAsync(
        int majorVersion, string targetDir,
        IProgress<double>? progress, CancellationToken ct)
    {
        var os = GetOsName();
        var arch = GetArchName();

        var apiUrl = $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot" +
                     $"?architecture={arch}&image_type=jre&os={os}&vendor=eclipse";

        progress?.Report(0.0);

        var assets = await Http.GetFromJsonAsync<JsonElement>(apiUrl, ct);

        if (assets.ValueKind != JsonValueKind.Array || assets.GetArrayLength() == 0)
            throw new InvalidOperationException(
                $"No Java {majorVersion} JRE found for {os}/{arch} on Adoptium");

        var firstAsset = assets[0];
        var binary = firstAsset.GetProperty("binary");
        var package_ = binary.GetProperty("package");
        var downloadLink = package_.GetProperty("link").GetString()
            ?? throw new InvalidOperationException("Download link not found");
        var archiveName = package_.GetProperty("name").GetString() ?? "java-archive";

        progress?.Report(0.1);

        Directory.CreateDirectory(targetDir);
        var archivePath = Path.Combine(targetDir, archiveName);

        using (var response = await Http.GetAsync(downloadLink, HttpCompletionOption.ResponseHeadersRead, ct))
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
                    progress?.Report(0.1 + 0.7 * ((double)bytesRead / totalBytes));
            }
        }

        progress?.Report(0.8);

        if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, targetDir, overwriteFiles: true);
        }
        else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                 archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractTarGzAsync(archivePath, targetDir, ct);
        }

        File.Delete(archivePath);

        progress?.Report(1.0);
    }

    private static async Task ExtractTarGzAsync(string archivePath, string targetDir, CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gzipStream, targetDir, overwriteFiles: true, cancellationToken: ct);
    }

    private static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "mac";
        throw new PlatformNotSupportedException("Unsupported OS");
    }

    private static string GetArchName()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "aarch64",
            _ => "x64"
        };
    }
}
