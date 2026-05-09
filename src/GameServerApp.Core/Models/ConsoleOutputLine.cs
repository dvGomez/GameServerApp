namespace GameServerApp.Core.Models;

public sealed record ConsoleOutputLine(
    string Text,
    ConsoleOutputLevel Level,
    DateTime Timestamp
);
