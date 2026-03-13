using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class AddEditMediaViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly Action _onSaved;
    private readonly Action _onCancelled;

    private int? _editingId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _originalTitle;

    [ObservableProperty]
    private MediaType _mediaType;

    [ObservableProperty]
    private int? _releaseYear;

    [ObservableProperty]
    private string? _synopsis;

    [ObservableProperty]
    private string? _genres;

    [ObservableProperty]
    private MediaStatus _status = MediaStatus.PlanToWatch;

    [ObservableProperty]
    private int? _userScore;

    [ObservableProperty]
    private string? _userReview;

    [ObservableProperty]
    private int? _totalEpisodes;

    [ObservableProperty]
    private int? _totalSeasons;

    [ObservableProperty]
    private int? _runtimeMinutes;

    [ObservableProperty]
    private string _windowTitle = "Add Media";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    public Array MediaTypes => Enum.GetValues<MediaType>();
    public Array MediaStatuses => Enum.GetValues<MediaStatus>();

    public AddEditMediaViewModel(MediaService mediaService, Action onSaved, Action onCancelled)
    {
        _mediaService = mediaService;
        _onSaved = onSaved;
        _onCancelled = onCancelled;
    }

    public void LoadForEdit(MediaItem item)
    {
        _editingId = item.Id;
        WindowTitle = "Edit Media";
        Title = item.Title;
        OriginalTitle = item.OriginalTitle;
        MediaType = item.MediaType;
        ReleaseYear = item.ReleaseYear;
        Synopsis = item.Synopsis;
        Genres = item.Genres;
        Status = item.Status;
        UserScore = item.UserScore;
        UserReview = item.UserReview;
        TotalEpisodes = item.TotalEpisodes;
        TotalSeasons = item.TotalSeasons;
        RuntimeMinutes = item.RuntimeMinutes;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = MediaInputValidator.ValidateMedia(
            Title,
            ReleaseYear,
            UserScore,
            TotalEpisodes,
            TotalSeasons,
            RuntimeMinutes);

        if (!string.IsNullOrEmpty(ErrorMessage))
            return;

        IsSaving = true;
        try
        {
            if (_editingId.HasValue)
            {
                var existing = await _mediaService.GetByIdAsync(_editingId.Value);
                if (existing is null)
                {
                    ErrorMessage = "This item could not be loaded for editing.";
                    return;
                }

                existing.Title = Title.Trim();
                existing.OriginalTitle = OriginalTitle?.Trim();
                existing.MediaType = MediaType;
                existing.ReleaseYear = ReleaseYear;
                existing.Synopsis = Synopsis?.Trim();
                existing.Genres = Genres?.Trim();
                existing.Status = Status;
                existing.UserScore = UserScore;
                existing.UserReview = UserReview?.Trim();
                existing.TotalEpisodes = TotalEpisodes;
                existing.TotalSeasons = TotalSeasons;
                existing.RuntimeMinutes = RuntimeMinutes;

                await _mediaService.UpdateAsync(existing);
            }
            else
            {
                var item = new MediaItem
                {
                    Title = Title.Trim(),
                    OriginalTitle = OriginalTitle?.Trim(),
                    MediaType = MediaType,
                    ReleaseYear = ReleaseYear,
                    Synopsis = Synopsis?.Trim(),
                    Genres = Genres?.Trim(),
                    Status = Status,
                    UserScore = UserScore,
                    UserReview = UserReview?.Trim(),
                    TotalEpisodes = TotalEpisodes,
                    TotalSeasons = TotalSeasons,
                    RuntimeMinutes = RuntimeMinutes
                };

                await _mediaService.CreateAsync(item);
            }

            _onSaved();
        }
        catch (Exception)
        {
            ErrorMessage = "Could not save this media item right now.";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _onCancelled();
}
