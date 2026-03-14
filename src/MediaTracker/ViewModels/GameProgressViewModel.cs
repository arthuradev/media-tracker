using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaTracker.Models;
using MediaTracker.Services;

namespace MediaTracker.ViewModels;

public partial class GameProgressViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly LocalizationService _localization;
    private readonly int _mediaItemId;

    [ObservableProperty]
    private double? _hoursPlayed;

    [ObservableProperty]
    private string? _currentStage;

    [ObservableProperty]
    private string? _platform;

    [ObservableProperty]
    private CompletionState _completionState = CompletionState.NotStarted;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    public Array CompletionStates => Enum.GetValues<CompletionState>();

    public GameProgressViewModel(MediaService mediaService, LocalizationService localization, int mediaItemId, GameProgress? existing)
    {
        _mediaService = mediaService;
        _localization = localization;
        _mediaItemId = mediaItemId;

        if (existing is not null)
        {
            HoursPlayed = existing.HoursPlayed;
            CurrentStage = existing.CurrentStage;
            Platform = existing.Platform;
            CompletionState = existing.CompletionState;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        StatusMessage = null;
        ErrorMessage = MediaInputValidator.ValidateGameProgress(_localization, HoursPlayed);

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            IsSaving = false;
            return;
        }

        var progress = new GameProgress
        {
            MediaItemId = _mediaItemId,
            HoursPlayed = HoursPlayed,
            CurrentStage = CurrentStage?.Trim(),
            Platform = Platform?.Trim(),
            CompletionState = CompletionState
        };

        try
        {
            await _mediaService.UpdateGameProgressAsync(progress);
            StatusMessage = _localization.Get("progress.saved");
        }
        catch (Exception)
        {
            ErrorMessage = _localization.Get("gameProgress.saveError");
        }
        finally
        {
            IsSaving = false;
        }
    }
}
