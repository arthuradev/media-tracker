namespace MediaTracker.Models;

public class ProviderMapping
{
    public int Id { get; set; }
    public int MediaItemId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }

    public MediaItem MediaItem { get; set; } = null!;
}
