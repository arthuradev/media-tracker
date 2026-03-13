using CommunityToolkit.Mvvm.ComponentModel;
using MediaTracker.Models;

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
            Models.MediaType.Movie => item.Status == MediaStatus.Completed ? "Watched" : "",
            Models.MediaType.Game => FormatGameProgress(item),
            _ => FormatEpisodeProgress(item)
        };

        return vm;
    }

    private static string FormatEpisodeProgress(MediaItem item)
    {
        if (item.TotalEpisodes is null or 0)
            return "";

        var watched = item.Episodes?.Count(e => e.IsWatched) ?? 0;
        return watched == item.TotalEpisodes
            ? "Completed"
            : $"{watched}/{item.TotalEpisodes} watched";
    }

    private static string FormatGameProgress(MediaItem item)
    {
        return item.GameProgress?.CompletionState switch
        {
            Models.CompletionState.NotStarted => "Not started",
            Models.CompletionState.InProgress => "In progress",
            Models.CompletionState.Completed => "Completed",
            Models.CompletionState.HundredPercent => "100% complete",
            Models.CompletionState.Abandoned => "Abandoned",
            _ => string.Empty
        };
    }
}
