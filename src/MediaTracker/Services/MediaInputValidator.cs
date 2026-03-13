namespace MediaTracker.Services;

public static class MediaInputValidator
{
    public static string? ValidateMedia(
        string title,
        int? releaseYear,
        int? userScore,
        int? totalEpisodes,
        int? totalSeasons,
        int? runtimeMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Title is required.";

        if (releaseYear is not null)
        {
            int maxYear = DateTime.UtcNow.Year + 1;
            if (releaseYear < 1800 || releaseYear > maxYear)
                return $"Release year must be between 1800 and {maxYear}.";
        }

        if (userScore is not null && (userScore < 1 || userScore > 10))
            return "Your rating must be between 1 and 10.";

        if (totalEpisodes is not null && totalEpisodes < 0)
            return "Episodes cannot be negative.";

        if (totalSeasons is not null && totalSeasons < 0)
            return "Seasons cannot be negative.";

        if (runtimeMinutes is not null && runtimeMinutes <= 0)
            return "Runtime must be greater than zero.";

        return null;
    }

    public static string? ValidateGameProgress(double? hoursPlayed)
    {
        if (hoursPlayed is not null && hoursPlayed < 0)
            return "Hours played cannot be negative.";

        return null;
    }
}
