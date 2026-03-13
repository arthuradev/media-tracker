namespace MediaTracker.Models;

public class GameProgress
{
    public int Id { get; set; }
    public int MediaItemId { get; set; }
    public double? HoursPlayed { get; set; }
    public string? CurrentStage { get; set; }
    public string? Platform { get; set; }
    public CompletionState CompletionState { get; set; } = CompletionState.NotStarted;

    public MediaItem MediaItem { get; set; } = null!;
}
