using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaTracker.ViewModels;

public partial class SeasonGroup : ObservableObject
{
    public int SeasonNumber { get; set; }
    public string Header => $"Season {SeasonNumber}";
    public ObservableCollection<EpisodeViewModel> Episodes { get; set; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    public int WatchedCount => Episodes.Count(e => e.IsWatched);
    public int TotalCount => Episodes.Count;
    public string Progress => $"{WatchedCount}/{TotalCount}";
    public bool AllWatched => WatchedCount == TotalCount && TotalCount > 0;

    public void Refresh()
    {
        OnPropertyChanged(nameof(WatchedCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(AllWatched));
    }
}
