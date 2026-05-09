namespace GameServerApp.Core.Models;

public sealed class GameDefinition
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? IconResourceKey { get; init; }
}
