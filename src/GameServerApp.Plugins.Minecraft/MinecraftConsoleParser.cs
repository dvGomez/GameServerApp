using System.Text.RegularExpressions;
using GameServerApp.Core.Models;

namespace GameServerApp.Plugins.Minecraft;

public static partial class MinecraftConsoleParser
{
    [GeneratedRegex(@"^\[[\d:]+\]\s+\[.+/(INFO|WARN|ERROR)\]:\s+(.+)$")]
    private static partial Regex LogPattern();

    public static ConsoleOutputLine Parse(string rawLine)
    {
        var match = LogPattern().Match(rawLine);

        if (match.Success)
        {
            var level = match.Groups[1].Value switch
            {
                "WARN" => ConsoleOutputLevel.Warning,
                "ERROR" => ConsoleOutputLevel.Error,
                _ => ConsoleOutputLevel.Info
            };

            return new ConsoleOutputLine(rawLine, level, DateTime.Now);
        }

        return new ConsoleOutputLine(rawLine, ConsoleOutputLevel.Info, DateTime.Now);
    }
}
