using System.Text.RegularExpressions;
using GameServerApp.Core.Models;

namespace GameServerApp.Plugins.Zomboid;

public static partial class ZomboidConsoleParser
{
    [GeneratedRegex(@"^LOG\s*:\s*(General|Network|Firewall)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LogPrefixPattern();

    public static ConsoleOutputLine Parse(string rawLine)
    {
        if (rawLine.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
            return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Error, DateTime.Now);

        if (rawLine.Contains("WARNING", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("WARN", StringComparison.OrdinalIgnoreCase))
            return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Warning, DateTime.Now);

        if (rawLine.StartsWith('>') ||
            rawLine.Contains("server is listening", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("connected", StringComparison.OrdinalIgnoreCase) ||
            rawLine.Contains("disconnected", StringComparison.OrdinalIgnoreCase))
            return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.System, DateTime.Now);

        return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Info, DateTime.Now);
    }
}
