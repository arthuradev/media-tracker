using CommunityToolkit.Mvvm.ComponentModel;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class MediaCardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private MediaType _mediaType;

    [ObservableProperty]
    private MediaStatus _status;

    [ObservableProperty]
    private int? _releaseYear;

    [ObservableProperty]
    private string? _genres;

    [ObservableProperty]
    private int? _userScore;

    [ObservableProperty]
    private string? _coverImagePath;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public static MediaCardViewModel FromModel(MediaItem item)
    {
        var localization = LocalizationService.Current;
        var vm = new MediaCardViewModel
        {
            Id = item.Id,
            Title = item.Title,
            MediaType = item.MediaType,
            Status = item.Status,
            ReleaseYear = item.ReleaseYear,
            Genres = item.Genres,
            UserScore = item.UserScore,
            CoverImagePath = item.CoverImagePath
        };

        vm.ProgressText = item.MediaType switch
        {
            Models.MediaType.Movie => item.Status == MediaStatus.Completed ? localization?.Get("progress.movie.watched") ?? "Watched" : string.Empty,
            Models.MediaType.Game => FormatGameProgress(item, localization),
            _ => FormatEpisodeProgress(item)
        };

        return vm;
    }

    private static string FormatEpisodeProgress(MediaItem item)
    {
        if (item.TotalEpisodes is null or 0)
            return string.Empty;

        var watched = item.Episodes?.Count(e => e.IsWatched) ?? 0;
        return watched == item.TotalEpisodes
            ? LocalizationService.Current?.Get("status.completed") ?? "Completed"
            : (LocalizationService.Current?.Format("progress.episodesWatched", watched, item.TotalEpisodes) ?? $"{watched}/{item.TotalEpisodes} watched");
    }

    private static string FormatGameProgress(MediaItem item, LocalizationService? localization)
    {
        return item.GameProgress?.CompletionState switch
        {
            Models.CompletionState.NotStarted => localization?.GetCompletionStateLabel(Models.CompletionState.NotStarted) ?? "Not started",
            Models.CompletionState.InProgress => localization?.GetCompletionStateLabel(Models.CompletionState.InProgress) ?? "In progress",
            Models.CompletionState.Completed => localization?.GetCompletionStateLabel(Models.CompletionState.Completed) ?? "Completed",
            Models.CompletionState.HundredPercent => localization?.GetCompletionStateLabel(Models.CompletionState.HundredPercent) ?? "100% complete",
            Models.CompletionState.Abandoned => localization?.GetCompletionStateLabel(Models.CompletionState.Abandoned) ?? "Abandoned",
            _ => string.Empty
        };
    }
}
