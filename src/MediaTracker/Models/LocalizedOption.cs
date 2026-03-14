namespace MediaTracker.Models;

public sealed class LocalizedOption<T>
{
    public required T Value { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
}
