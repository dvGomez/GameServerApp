namespace GameServerApp.Core.Models;

public sealed class ConfigField
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required ConfigFieldType FieldType { get; init; }
    public object? DefaultValue { get; init; }
    public bool IsRequired { get; init; } = true;
    public object? MinValue { get; init; }
    public object? MaxValue { get; init; }
    public IReadOnlyList<string>? EnumOptions { get; init; }
    public string? Category { get; init; }
}
