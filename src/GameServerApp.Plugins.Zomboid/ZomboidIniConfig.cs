using System.Text.Json;

namespace GameServerApp.Plugins.Zomboid;

public static class ZomboidIniConfig
{
    public static void Write(string path, Dictionary<string, object> configValues)
    {
        var existingLines = new List<string>();
        var writtenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(path))
            existingLines.AddRange(File.ReadAllLines(path));

        using var writer = new StreamWriter(path);

        foreach (var line in existingLines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                writer.WriteLine(line);
                continue;
            }

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0)
            {
                writer.WriteLine(line);
                continue;
            }

            var key = trimmed[..eqIdx].Trim();
            if (configValues.TryGetValue(key, out var newVal))
            {
                writer.WriteLine($"{key}={FormatValue(newVal)}");
                writtenKeys.Add(key);
            }
            else
            {
                writer.WriteLine(line);
            }
        }

        foreach (var (key, value) in configValues)
        {
            if (!writtenKeys.Contains(key))
                writer.WriteLine($"{key}={FormatValue(value)}");
        }
    }

    public static Dictionary<string, object> Read(string path)
    {
        var result = new Dictionary<string, object>();
        if (!File.Exists(path)) return result;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = trimmed[..eqIdx].Trim();
            var value = trimmed[(eqIdx + 1)..].Trim();

            if (int.TryParse(value, out var intVal))
                result[key] = intVal;
            else if (bool.TryParse(value, out var boolVal))
                result[key] = boolVal;
            else
                result[key] = value;
        }

        return result;
    }

    private static string FormatValue(object value) => value switch
    {
        bool b => b.ToString().ToLowerInvariant(),
        JsonElement je => je.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => je.GetRawText(),
            _ => je.GetString() ?? ""
        },
        _ => value.ToString() ?? ""
    };
}
