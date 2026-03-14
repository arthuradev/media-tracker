namespace MediaTracker.Services;

public static class MediaInputValidator
{
    public static string? ValidateMedia(
        LocalizationService localization,
        string title,
        int? releaseYear,
        int? userScore,
        int? totalEpisodes,
        int? totalSeasons,
        int? runtimeMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            return localization.Get("validation.titleRequired");

        if (releaseYear is not null)
        {
            int maxYear = DateTime.UtcNow.Year + 1;
            if (releaseYear < 1800 || releaseYear > maxYear)
                return localization.Format("validation.releaseYearRange", maxYear);
        }

        if (userScore is not null && (userScore < 1 || userScore > 10))
            return localization.Get("validation.ratingRange");

        if (totalEpisodes is not null && totalEpisodes < 0)
            return localization.Get("validation.episodesNegative");

        if (totalSeasons is not null && totalSeasons < 0)
            return localization.Get("validation.seasonsNegative");

        if (runtimeMinutes is not null && runtimeMinutes <= 0)
            return localization.Get("validation.runtimePositive");

        return null;
    }

    public static string? ValidateGameProgress(LocalizationService localization, double? hoursPlayed)
    {
        if (hoursPlayed is not null && hoursPlayed < 0)
            return localization.Get("validation.hoursNegative");

        return null;
    }
}
