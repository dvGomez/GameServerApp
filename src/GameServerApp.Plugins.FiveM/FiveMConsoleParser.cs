using System.Text.RegularExpressions;
using GameServerApp.Core.Models;

namespace GameServerApp.Plugins.FiveM;

public static partial class FiveMConsoleParser
{
    [GeneratedRegex(@"^\[\s*(Warning|Error|Info|Trace)\]", RegexOptions.IgnoreCase)]
    private static partial Regex LevelPattern();

    [GeneratedRegex(@"^(\w+\s+\w+\s+\d+\s+[\d:]+\s+\d+)\s+")]
    private static partial Regex TimestampPattern();

    public static ConsoleOutputLine Parse(string rawLine)
    {
        var match = LevelPattern().Match(rawLine);
        if (match.Success)
        {
            var level = match.Groups[1].Value.ToLowerInvariant() switch
            {
                "warning" => ConsoleOutputLevel.Warning,
                "error" => ConsoleOutputLevel.Error,
                "trace" => ConsoleOutputLevel.Info,
                _ => ConsoleOutputLevel.Info
            };
            return new ConsoleOutputLine(rawLine, level, DateTime.Now);
        }

        if (rawLine.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("exception", StringComparison.OrdinalIgnoreCase))
            return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Error, DateTime.Now);

        if (rawLine.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("warn", StringComparison.OrdinalIgnoreCase))
            return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Warning, DateTime.Now);

        return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Info, DateTime.Now);
    }
}
