namespace Typee.Core.Models;

public sealed class CustomCommand
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required string Instruction { get; init; }
    public string? Hotkey { get; init; }
    public int UseCount { get; init; }
}
