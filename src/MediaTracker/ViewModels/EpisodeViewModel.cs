using CommunityToolkit.Mvvm.ComponentModel;
using MediaTracker.Models;

namespace MediaTracker.ViewModels;

public partial class EpisodeViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private int _seasonNumber;

    [ObservableProperty]
    private int _episodeNumber;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private bool _isWatched;

    [ObservableProperty]
    private int? _userScore;

    [ObservableProperty]
    private string? _airDate;

    public static EpisodeViewModel FromModel(Episode ep) => new()
    {
        Id = ep.Id,
        SeasonNumber = ep.SeasonNumber,
        EpisodeNumber = ep.EpisodeNumber,
        Title = ep.Title,
        IsWatched = ep.IsWatched,
        UserScore = ep.UserScore,
        AirDate = ep.AirDate?.ToString("dd/MM/yyyy")
    };
}
